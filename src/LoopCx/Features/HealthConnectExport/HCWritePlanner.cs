using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ExportRun;

namespace LoopCx.Features.HealthConnectExport;

public static class HCWritePlanner
{
    // Mapping-v1 clock capture tolerance; not a physiological or sensor-accuracy filter.
    private static readonly TimeSpan ReceiptClockTolerance = TimeSpan.FromSeconds(1);
    public static HCPlanResult Create(ExportJournalSnapshot source, bool debugBuild)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (debugBuild || source.ExportStatus == "disabled_synthetic") return new("disabled_synthetic", "Debug or synthetic source is not eligible.", null);
        try
        {
            var checkpoint = source.Checkpoint;
            if (checkpoint.GetProperty("SchemaVersion").GetInt32() != 1)
                throw new InvalidDataException("The checkpoint version is not supported by mapping v1.");
            var snapshot = checkpoint.GetProperty("Snapshot");
            var statistics = snapshot.TryGetProperty("Statistics", out var measured) && measured.ValueKind == JsonValueKind.Object ? measured : (JsonElement?)null;
            if (statistics is { } versionedStatistics && versionedStatistics.GetProperty("Version").GetInt32() != 1)
                throw new InvalidDataException("The stored statistics version is not supported by mapping v1.");
            var boundaries = checkpoint.GetProperty("PhaseBoundaries").Deserialize<PhaseBoundaryFact[]>()
                ?? throw new InvalidDataException("Recorded effective phase boundaries are unavailable.");
            if (boundaries.Any(static fact => fact is null))
                throw new InvalidDataException("Recorded effective phase boundaries contain a null entry.");
            if ((statistics is { } stats && stats.GetProperty("HasSyntheticTime").GetBoolean()) || boundaries.Any(static fact => fact.Synthetic)
                || source.Events.Any(static item => item.Command is "complete" or "hr-source-failure")
                || (source.Template is { ValueKind: JsonValueKind.Object } template && template.TryGetProperty("Source", out var origin) && origin.GetString()?.StartsWith("Synthetic", StringComparison.OrdinalIgnoreCase) == true))
            {
                return new("disabled_synthetic", "Synthetic timing or source evidence is not eligible.", null);
            }

            if (source.Id == Guid.Empty || source.Status != "finished" || snapshot.GetProperty("Phase").GetString() != "Finished")
                throw new InvalidDataException("Only an actually finalized run can have a write plan.");
            var started = Utc(source.StartedUtc);
            var terminal = boundaries.Where(static fact => fact.NextPhaseIndex is null && fact.NextPhase is null).ToArray();
            var planCount = checkpoint.GetProperty("Plan").GetArrayLength();
            if (terminal.Length != 1 || terminal[0].PreviousPhaseIndex != planCount - 1
                || checkpoint.GetProperty("Completed").GetArrayLength() != planCount)
            {
                throw new InvalidDataException("One complete terminal phase boundary is required.");
            }

            var finish = terminal[0];
            if (finish.EffectiveElapsedMilliseconds <= 0 || finish.EffectiveElapsedMilliseconds > source.ObservedElapsedMilliseconds
                || finish.ObservedElapsedMilliseconds < finish.EffectiveElapsedMilliseconds)
            {
                throw new InvalidDataException("Effective and observed finish timing is contradictory.");
            }

            var ended = started.AddMilliseconds(finish.EffectiveElapsedMilliseconds);
            if (finish.StartAnchorDerivedEffectiveUtc != ended || ended <= started)
                throw new InvalidDataException("Stored effective finish disagrees with the frozen UTC start anchor.");
            var totalActive = Duration(checkpoint.GetProperty("TotalActiveTime"));
            var context = checkpoint.TryGetProperty("StartContext", out var originContext) && originContext.ValueKind == JsonValueKind.Object
                ? originContext.Deserialize<RunStartContext>() : null;
            var motionStart = snapshot.TryGetProperty("Motion", out var motion) && motion.ValueKind == JsonValueKind.Object
                ? Duration(motion.GetProperty("StartedMonotonicTime")) : (TimeSpan?)null;
            var bootStart = context is not null ? TimeSpan.FromMilliseconds(context.BootMonotonicStartMilliseconds) : motionStart;
            if (bootStart < TimeSpan.Zero || (context is not null && motionStart is { } recordedMotionStart && recordedMotionStart != bootStart))
                throw new InvalidDataException("Frozen start monotonic anchors disagree.");
            var pauses = Pauses(source.Events, started, ended, bootStart);
            if (ended - started - pauses.Aggregate(TimeSpan.Zero, static (sum, pause) => sum + (pause.EndUtc - pause.StartUtc)) != totalActive)
                throw new InvalidDataException("Recorded pauses and total active time do not conserve the elapsed window.");
            var terminalContext = checkpoint.TryGetProperty("TerminalContext", out var recordedTerminal) && recordedTerminal.ValueKind == JsonValueKind.Object
                ? recordedTerminal.Deserialize<RunTerminalContext>() : null;
            if (terminalContext is not null && (terminalContext.TerminalPreviousPhaseIndex != finish.PreviousPhaseIndex
                || string.IsNullOrWhiteSpace(terminalContext.ObservedTimeZoneId)
                || terminalContext.Provenance != RunTerminalContext.ObservedZoneRulesV1
                || terminalContext.ObservedUtcOffsetSeconds is < -64800 or > 64800
                || terminalContext.EffectiveUtcOffsetSeconds is < -64800 or > 64800))
            {
                throw new InvalidDataException("Stored terminal zone provenance is contradictory or unsupported.");
            }

            var endOffset = terminalContext?.EffectiveUtcOffsetSeconds;
            var offset = context?.UtcOffsetSeconds;
            if (offset is < -64800 or > 64800) throw new InvalidDataException("Recorded start offset is outside the platform range.");
            var warnings = new List<string>();
            if (context is null) warnings.Add("Local start zone/offset is unknown; UTC remains authoritative.");
            if (terminalContext is null) warnings.Add("End local offset was not recorded; no current system zone is substituted.");
            else warnings.Add("Terminal zone was observed at the linked boundary callback; effective offset uses that frozen zone's rules at the start-anchor-derived end instant.");
            var records = new List<HCPlannedRecord>
            {
                new(Id(source.Id, "session"), 1, HCRecordKind.RunningSession, started, ended, offset, endOffset,
                    pauses, null, [], "Whole elapsed run including warm-up/cool-down; recorded pause exclusions retained.")
            };
            if (statistics is { } totals)
            {
                var total = totals.GetProperty("Total");
                var metres = total.GetProperty("MeasuredMetres").GetDouble();
                var known = Duration(total.GetProperty("KnownGpsActiveTime"));
                var unknown = Duration(total.GetProperty("UnknownGpsActiveTime"));
                if (!double.IsFinite(metres) || metres < 0 || metres > 1_000_000 || known < TimeSpan.Zero || unknown < TimeSpan.Zero || known + unknown != totalActive)
                    throw new InvalidDataException("Stored distance or coverage is contradictory.");
                if (metres > 0 && known == TimeSpan.Zero)
                    throw new InvalidDataException("Positive accepted distance has no supported GPS interval.");
                if (metres > 0 && known > TimeSpan.Zero)
                {
                    records.Add(new(Id(source.Id, "distance"), 1, HCRecordKind.Distance, started, ended, offset, endOffset, [], metres, [],
                        unknown > TimeSpan.Zero ? "Accepted partial measured distance; no gap-fill or continuous coverage guarantee." : "Accepted measured distance; no extrapolation."));
                }
                else
                {
                    warnings.Add("No supported positive distance; distance record omitted.");
                }
            }
            else
            {
                warnings.Add("Stored distance statistics are unknown; distance record omitted.");
            }

            var samples = new List<HCRawSample>();
            var byReceipt = new Dictionary<long, HCRawSample>();
            foreach (var row in source.HeartRate.OrderBy(static row => row.Sequence))
            {
                if (row.Quality != "valid" || row.Bpm is null) continue;
                var received = Utc(row.ReceiptUtc);
                var outsideUtc = received < started || received > ended;
                if (bootStart is { } anchor)
                {
                    var relativeReceipt = TimeSpan.FromTicks(row.BootReceiptMonotonicTicks) - anchor;
                    var outsideBoot = relativeReceipt < TimeSpan.Zero || relativeReceipt > ended - started;
                    if (outsideUtc != outsideBoot || (!outsideUtc && (received - started - relativeReceipt).Duration() > ReceiptClockTolerance))
                        throw new InvalidDataException("Raw HR UTC and boot receipt clocks contradict the frozen window.");
                }
                if (outsideUtc) continue;
                if (row.Bpm is < 1 or > 300)
                    throw new InvalidDataException("Valid HR is outside the API 34 representable range of 1–300 BPM.");
                var epochMilliseconds = received.ToUnixTimeMilliseconds();
                if (byReceipt.TryGetValue(epochMilliseconds, out var same))
                {
                    if (same.Bpm != row.Bpm) throw new InvalidDataException("Conflicting BPM at one platform millisecond receipt instant.");
                    continue;
                }
                var sample = new HCRawSample(row.Sequence, received, row.Bpm.Value);
                byReceipt.Add(epochMilliseconds, sample);
                samples.Add(sample);
            }
            if (samples.Count > 0)
            {
                records.Add(new(Id(source.Id, "heart-rate"), 1, HCRecordKind.HeartRate, started, ended, offset, endOffset, [], null,
                    Array.AsReadOnly(samples.OrderBy(static sample => sample.ReceiptUtc).ThenBy(static sample => sample.SourceSequence).ToArray()),
                    "Actual local GATT receipt instants retained; platform projection uses epoch milliseconds. Identical BPM within one millisecond retains the first source sequence. API 34 range is 1–300 BPM; clock agreement guard is 1000 ms. No acquisition-time claim or extrapolation."));
            }
            else
            {
                warnings.Add("No valid raw HR receipts in the window; HR record omitted.");
            }

            var frozenRecords = Array.AsReadOnly(records.ToArray());
            var frozenWarnings = Array.AsReadOnly(warnings.ToArray());
            var gate = new HCSourceGate(true, false, false);
            var hash = HCWritePlanV1.Hash(1, source.Id, gate, frozenRecords, frozenWarnings);

            return new("ready", null, new(1, source.Id, gate, frozenRecords, frozenWarnings, hash));
        }
        catch (Exception problem) when (problem is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException or ArgumentException or OverflowException)
        {
            return new("action_required", problem.Message, null);
        }
    }

    private static string Id(Guid run, string kind)
    {
        return $"loop-cx:{run:D}:hc-v1:{kind}";
    }

    private static DateTimeOffset Utc(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || !value.Contains('T') || !(value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal))
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant) || instant.Offset != TimeSpan.Zero
            ? throw new InvalidDataException("An explicit valid UTC instant is required.")
            : instant;
    }
    private static TimeSpan Duration(JsonElement value)
    {
        return !TimeSpan.TryParseExact(value.GetString(), "c", CultureInfo.InvariantCulture, out var duration)
            ? throw new InvalidDataException("A recorded duration is missing or malformed.")
            : duration;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<HCPause> Pauses(IReadOnlyList<ExportEvent> events, DateTimeOffset start, DateTimeOffset end, TimeSpan? bootStart)
    {
        var intervals = new List<HCPause>();
        DateTimeOffset? pausedAt = null;
        var manual = false;
        long previousSequence = -1;
        long previousReceipt = -1;
        foreach (var item in events)
        {
            if (item.Sequence <= previousSequence) throw new InvalidDataException("Command sequence is not strictly ordered.");
            previousSequence = item.Sequence;
            if (item.Command is not ("pause" or "resume" or "auto-pause" or "auto-resume")) continue;
            if (item.ObservedElapsedMilliseconds < previousReceipt)
                throw new InvalidDataException("Pause command receipt times are not monotonic.");
            previousReceipt = item.ObservedElapsedMilliseconds;
            var at = start.AddMilliseconds(item.ObservedElapsedMilliseconds);
            if (at < start || at > end) throw new InvalidDataException("A pause command is outside the finished run.");
            if (item.Command == "auto-pause")
            {
                if (manual || bootStart is null) throw new InvalidDataException("Automatic pause provenance is unavailable or contradictory.");
                var automatic = item.Checkpoint.GetProperty("AutomaticEvent");
                var detected = start + (Duration(automatic.GetProperty("DetectedMonotonicTime")) - bootStart.Value);
                if ((detected - at).Duration() > TimeSpan.FromMilliseconds(1))
                    throw new InvalidDataException("Automatic pause detection and event receipt clocks disagree.");
                var from = start + (Duration(automatic.GetProperty("OnsetMonotonicTime")) - bootStart.Value);
                var to = start + (Duration(automatic.GetProperty("ExcludedThroughMonotonicTime")) - bootStart.Value);
                if (from < start || to <= from || to > at) throw new InvalidDataException("Automatic exclusion is outside its recorded window.");
                if (to - from != TimeSpan.FromSeconds(10)) throw new InvalidDataException("Mapping v1 requires the recorded ten-second automatic exclusion.");
                intervals.Add(new(from, to));
                pausedAt ??= at;
            }
            else if (item.Command == "pause") { pausedAt ??= at; manual = true; }
            else
            {
                if (pausedAt is null || (item.Command == "auto-resume" && manual))
                    throw new InvalidDataException("Resume contradicts recorded pause precedence.");
                if (at > pausedAt) intervals.Add(new(pausedAt.Value, at));
                pausedAt = null;
                manual = false;
            }
        }
        if (pausedAt is { } remaining && end > remaining) intervals.Add(new(remaining, end));
        var union = new List<HCPause>();
        foreach (var interval in intervals.OrderBy(static item => item.StartUtc))
        {
            if (union.Count > 0 && interval.StartUtc <= union[^1].EndUtc)
                union[^1] = union[^1] with { EndUtc = interval.EndUtc > union[^1].EndUtc ? interval.EndUtc : union[^1].EndUtc };
            else union.Add(interval);
        }

        return union.AsReadOnly();
    }
}
