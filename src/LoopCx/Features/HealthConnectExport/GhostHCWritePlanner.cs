using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;

namespace LoopCx.Features.HealthConnectExport;

// Pure mapping-2 prerequisite. This does not enable outbox replay or provider delivery.
public static class GhostHCWritePlanner
{
    public static HCPlanResult Create(ExportJournalSnapshot source, bool debugBuild)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (debugBuild || source.ExportStatus == "disabled_synthetic")
            return new("disabled_synthetic", "Debug or synthetic source is not eligible.", null);

        try
        {
            var value = GhostRaceCheckpoint.Read(source.Checkpoint.GetRawText());
            var clock = value.Snapshot.Clock;

            if (source.Id != value.Id || Utc(source.StartedUtc) != value.StartedUtc
                || source.ObservedElapsedMilliseconds != value.ObservedBootMilliseconds - value.StartedBootMilliseconds)
            {
                throw new InvalidDataException("Selected source and frozen Ghost clocks disagree.");
            }

            if (value.Origin is null)
                return new("local_only", "The recording's creation origin is unknown; no production origin was inferred.", null);

            if (value.Origin.Kind != GhostOriginKind.NormalService)
                return new("disabled_synthetic", "Debug or synthetic creation origin is not eligible.", null);

            if (!HCConsent.Eligible(source.Checkpoint))
                return new("local_only", "This recording started without Health Connect opt-in.", null);

            var matchedFinish = source.Status == "finished" && clock.State == GhostClockState.Finished
                && value.TerminalKind == "matched-finish";
            var explicitEnd = source.Status == "incomplete" && clock.State == GhostClockState.Incomplete
                && value.TerminalKind == "explicit-end";

            if ((!matchedFinish && !explicitEnd) || clock.RaceElapsed <= TimeSpan.Zero)
            {
                throw new InvalidDataException("Mapping requires a positive matched finish or an explicitly ended Incomplete race with a known end.");
            }

            var start = value.StartedUtc;
            var end = start + clock.RaceElapsed;
            var bootStart = TimeSpan.FromMilliseconds(value.StartedBootMilliseconds);
            var bootObserved = TimeSpan.FromMilliseconds(value.ObservedBootMilliseconds);
            var bootEnd = bootStart + clock.RaceElapsed;

            if (bootEnd > bootObserved || (Utc(source.LastObservedUtc) - start - (bootObserved - bootStart)).Duration() > TimeSpan.FromSeconds(1))
                throw new InvalidDataException("Observed Ghost UTC and boot clocks contradict finality.");

            var pauses = new List<HCPause>();
            var previousEnd = bootStart;
            var excluded = TimeSpan.Zero;

            foreach (var pause in value.Pauses)
            {
                if (pause.StartedAt < previousEnd || pause.StartedAt < bootStart || pause.StartedAt > bootObserved
                    || (pause.EndedAt is { } resumed && (resumed < pause.StartedAt || resumed > bootObserved)))
                {
                    throw new InvalidDataException("Ghost pauses must be ordered nonoverlapping boot intervals.");
                }

                var until = pause.EndedAt ?? bootEnd;
                previousEnd = until;
                var clipped = until < bootEnd ? until : bootEnd;

                if (clipped <= pause.StartedAt) continue;

                excluded += clipped - pause.StartedAt;
                pauses.Add(new(start + (pause.StartedAt - bootStart), start + (clipped - bootStart)));
            }

            if (clock.TrainingActive != clock.RaceElapsed - excluded
                || clock.TrainingPaused != value.Pauses.Any(pause => pause.StartedAt <= bootEnd && (pause.EndedAt is null || pause.EndedAt > bootEnd)))
            {
                throw new InvalidDataException("Ghost training time does not conserve the recorded pause union.");
            }

            var warnings = new List<string>();
            var context = value.TerminalContext;

            if (context is not null && (context.ObservedUtc != Utc(source.LastObservedUtc)
                || (context.ObservedUtc - start - (bootObserved - bootStart)).Duration() > TimeSpan.FromSeconds(1)))
            {
                throw new InvalidDataException("Present terminal observation contradicts selected-row clocks.");
            }

            if (context is null) warnings.Add("End local offset was not recorded; no current system zone is substituted.");

            var offset = checked((int)value.StartedLocal.Offset.TotalSeconds);
            var endOffset = context?.EffectiveUtcOffsetSeconds;
            var terminal = "Ghost mapping 2; finalized positive race window. Frozen terminal evidence: " + JsonSerializer.Serialize(new
            {
                value.TerminalKind,
                value.Origin,
                Outcome = clock.State.ToString(),
                value.StartedUtc,
                value.StartedLocal,
                value.TimeZoneId,
                value.StartedBootMilliseconds,
                TerminalContext = context
            });
            var records = new List<HCPlannedRecord>
            {
                new(Id(value.Id, "session"), 2, HCRecordKind.RunningSession, start, end, offset, endOffset,
                    Array.AsReadOnly(pauses.ToArray()), null, [], terminal + " Training pauses retained; race elapsed includes stops.")
            };

            if (value.MeasuredTrainingDistance is { } distance)
            {
                if (distance.TotalMeasuredMetres > 0 && clock.TrainingActive == TimeSpan.Zero)
                    throw new InvalidDataException("Positive accepted training distance contradicts zero active training time.");

                var distanceProvenance = JsonSerializer.Serialize(new
                {
                    distance.Policy,
                    distance.MainHasGaps,
                    SplitGaps = distance.Splits.Select(split => new { split.Number, split.HasGaps }).ToArray()
                });
                records.Add(new(Id(value.Id, "distance"), 2, HCRecordKind.Distance, start, end, offset, endOffset, [],
                    distance.TotalMeasuredMetres, [], terminal + " Frozen distance evidence: " + distanceProvenance + (distance.MainHasGaps || distance.Splits.Any(split => split.HasGaps)
                        ? " Accepted partial training distance; coverage gaps remain unknown; no gap-fill."
                        : " Accepted training distance; paused movement excluded; no extrapolation.")));
            }
            else
            {
                warnings.Add("Stored accepted training distance is unknown; distance record omitted.");
            }

            var samples = new List<HCRawSample>();
            var milliseconds = new Dictionary<long, HCRawSample>();

            foreach (var row in source.HeartRate.OrderBy(row => row.Sequence))
            {
                if (row.Quality != "valid" || row.Bpm is null) continue;

                var receipt = Utc(row.ReceiptUtc);
                var boot = TimeSpan.FromTicks(row.BootReceiptMonotonicTicks);
                var utcInside = receipt >= start && receipt <= end;
                var bootInside = boot >= bootStart && boot <= bootEnd;

                if (utcInside != bootInside || (utcInside && (receipt - start - (boot - bootStart)).Duration() > TimeSpan.FromSeconds(1)))
                    throw new InvalidDataException("HR receipt UTC and boot window membership or clocks disagree.");

                if (!utcInside) continue;

                if (row.Bpm is < 1 or > 300)
                    throw new InvalidDataException("Valid HR is outside the API representation range of 1–300 BPM.");

                var epoch = receipt.ToUnixTimeMilliseconds();

                if (milliseconds.TryGetValue(epoch, out var existing))
                {
                    if (existing.Bpm != row.Bpm) throw new InvalidDataException("Conflicting HR values in one platform millisecond.");

                    continue;
                }

                var sample = new HCRawSample(row.Sequence, receipt, row.Bpm.Value);
                milliseconds.Add(epoch, sample);
                samples.Add(sample);
            }

            if (samples.Count > 0)
            {
                records.Add(new(Id(value.Id, "heart-rate"), 2, HCRecordKind.HeartRate, start, end, offset, endOffset, [], null,
                    Array.AsReadOnly(samples.OrderBy(sample => sample.ReceiptUtc).ThenBy(sample => sample.SourceSequence).ToArray()),
                    terminal + " Original valid GATT receipts; 1000 ms anchored clock guard; no acquisition-time or aggregate claim."));
            }
            else
            {
                warnings.Add("No valid raw HR receipts in the race window; HR record omitted.");
            }

            var frozen = Array.AsReadOnly(records.ToArray());
            var frozenWarnings = Array.AsReadOnly(warnings.ToArray());
            // In mapping 2 this means finalized window, after typed matched-finish or explicit-end validation above.
            var gate = new HCSourceGate(true, false, false);

            return new("ready", null, new(2, value.Id, gate, frozen, frozenWarnings,
                HCWritePlanV1.Hash(2, value.Id, gate, frozen, frozenWarnings)));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException
            or ArgumentException or FormatException or OverflowException or KeyNotFoundException)
        {
            return new("action_required", error.Message, null);
        }
    }

    private static string Id(Guid id, string kind)
    {
        return $"loop-cx:{id:D}:hc-v2:{kind}";
    }

    private static DateTimeOffset Utc(string text)
    {
        return !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) || value.Offset != TimeSpan.Zero
            ? throw new InvalidDataException("Recorded UTC timestamp is unavailable or malformed.")
            : value;
    }
}
