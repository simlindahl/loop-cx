using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;

namespace LoopCx.Features.History;

public sealed record GhostSourceEvidenceV2(int Version, Guid SourceRunId, string StartedUtc, string LastObservedUtc,
    long ObservedElapsedMilliseconds, JsonElement OriginalPlan, JsonElement? Template, JsonElement Checkpoint,
    IReadOnlyList<ExportEvent> Events, IReadOnlyList<ExportLocationV2> Locations,
    long MainStartMilliseconds, long MainEndMilliseconds, DateTimeOffset MainStartedLocal,
    string MainTimeZoneId, string LocalStartProvenance);

public sealed class StoredGhostCapture
{
    internal StoredGhostCapture(CapturedMainRoute geometry, GhostAttempt attempt, string? reason, string rawJson)
    {
        Geometry = geometry; Attempt = attempt; QualityReason = reason; RawJson = rawJson;
    }
    public CapturedMainRoute Geometry { get; }
    public GhostAttempt Attempt { get; }
    public string? QualityReason { get; }
    public string RawJson { get; }
}

public static class GhostSourceEvidenceCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Write(GhostSourceEvidenceV2 source) => JsonSerializer.Serialize(source, Options);
    public static GhostSourceEvidenceV2 Read(string json)
    {
        var source = JsonSerializer.Deserialize<GhostSourceEvidenceV2>(json, Options)
            ?? throw new InvalidDataException("Missing stored Ghost source evidence.");
        if (source.Version != 2 || source.SourceRunId == Guid.Empty || source.MainStartMilliseconds < 0
            || source.MainEndMilliseconds <= source.MainStartMilliseconds || source.MainEndMilliseconds > source.ObservedElapsedMilliseconds
            || source.MainEndMilliseconds - source.MainStartMilliseconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond
            || source.Locations is null || source.Events is null || source.Locations.Any(point => point is null)
            || source.Events.Any(item => item is null) || string.IsNullOrWhiteSpace(source.LocalStartProvenance))
            throw new InvalidDataException("Unsupported or malformed stored Ghost source evidence.");
        var clock = GhostStoredSource.ReadFrozenClock(source.Checkpoint, source.Template, source.ObservedElapsedMilliseconds, source.StartedUtc);
        if (source.MainStartMilliseconds != clock.From || source.MainEndMilliseconds != clock.To
            || source.MainStartedLocal.UtcDateTime != clock.SourceUtc.AddMilliseconds(clock.From).UtcDateTime
            || source.MainTimeZoneId != clock.Start.TimeZoneId
            || source.MainStartedLocal.Offset.TotalSeconds is < -50400 or > 50400
            || source.MainStartedLocal.Offset.TotalSeconds % 60 != 0
            || (clock.From == 0 && source.MainStartedLocal.Offset.TotalSeconds != clock.Start.UtcOffsetSeconds)
            || source.LocalStartProvenance != (clock.From == 0 ? GhostStoredSource.RecordedCivil : GhostStoredSource.DerivedCivil))
            throw new InvalidDataException("Stored Main clock and frozen civil provenance disagree.");
        ExportRunV2.ValidatePoints(source.SourceRunId, source.Locations.Select(point => new ExportLocation(point.SnapshotOrder,
            point.UtcMilliseconds, point.BootMonotonicNanoseconds, point.LatitudeDegrees, point.LongitudeDegrees, point.AccuracyMetres)
        { Provenance = point.Provenance }).ToArray());
        return source;
    }
    public static void ValidateAttempt(GhostSourceEvidenceV2 source, GhostAttempt attempt)
    {
        if (source.SourceRunId != attempt.SourceRunId
            || !source.MainStartedLocal.EqualsExact(attempt.StartedLocal)
            || source.MainTimeZoneId != attempt.TimeZoneId
            || (decimal)(source.MainEndMilliseconds - source.MainStartMilliseconds) * TimeSpan.TicksPerMillisecond != attempt.Elapsed.Ticks)
            throw new InvalidDataException("Archived attempt disagrees with its frozen Main source clock or civil provenance.");
    }

}

// Integration bridge only: original callback evidence stays separate from the legacy geometry derivation.
public static class GhostStoredSource
{
    public static RouteCapturePolicy Pilot { get; } = new("stored-main-source-v2-pilot", DistancePolicy.Pilot.MaximumGap,
        DistancePolicy.Pilot.MaximumAccuracyMetres, DistancePolicy.Pilot.MaximumSpeedMetresPerSecond);

    public static bool IsContinuous(CoreTemplate? template, IReadOnlyList<TimedPhase>? plan) =>
        template is { Category: "continuous" } && (template.ProtocolId ?? template.TemplateId) == "C2"
        && plan is { Count: > 0 } && plan.Count(phase => phase?.Kind == RunPhase.Work) == 1
        && plan.All(phase => phase is not null && phase.Kind is RunPhase.Warmup or RunPhase.Work or RunPhase.Cooldown)
        && plan.Select(phase => phase.Kind).Distinct().Count() == plan.Count
        && plan.Select(phase => phase.Kind).SequenceEqual(plan.Select(phase => phase.Kind).OrderBy(kind => kind == RunPhase.Warmup ? 0 : kind == RunPhase.Work ? 1 : 2));

    public static StoredGhostCapture Capture(ExportJournalSnapshot source, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (source.Status != "finished") throw new InvalidDataException("Finish and save the continuous run first.");
        ExportRunV1.Validate(source);
        ExportRunV2.ValidatePoints(source.Id, source.Locations);
        var clock = ReadFrozenClock(source.Checkpoint, source.Template, source.ObservedElapsedMilliseconds, source.StartedUtc);
        var (start, from, to, sourceUtc) = clock;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(start.TimeZoneId);
        if (zone.GetUtcOffset(sourceUtc) != TimeSpan.FromSeconds(start.UtcOffsetSeconds))
            throw new InvalidDataException("Recorded source offset conflicts with saved zone rules.");
        var mainUtc = sourceUtc.AddMilliseconds(from);
        var offset = from == 0 ? TimeSpan.FromSeconds(start.UtcOffsetSeconds) : zone.GetUtcOffset(mainUtc);
        var localProvenance = from == 0 ? RecordedCivil : DerivedCivil;
        var main = source.Locations.Where(point => (decimal)(point.BootMonotonicNanoseconds / 1000000) - start.BootMonotonicStartMilliseconds >= from
            && (decimal)(point.BootMonotonicNanoseconds / 1000000) - start.BootMonotonicStartMilliseconds <= to).ToArray();
        var recording = new List<RecordedRouteFix>();
        long? previous = null;
        var finalizationTail = false;
        foreach (var point in main)
        {
            var evidence = point.Provenance ?? throw new InvalidDataException("This older recording has no saved route-continuity evidence.");
            if (evidence.Outcome == "ignored") continue; // Original raw row remains in the v2 envelope.
            if (evidence.Outcome == "not-assessed" && evidence.BreakReason == "Finalization")
            {
                finalizationTail = true;
                continue; // Not assessed at this callback: supported prefix remains, endpoint does not become known.
            }
            if (finalizationTail) throw new InvalidDataException("Finalization evidence is not a trailing source suffix.");
            if (evidence.Outcome != "valid") throw new InvalidDataException("The Main track contains unassessed or rejected GPS evidence.");
            var elapsed = TimeSpan.FromMilliseconds((long)((decimal)(point.BootMonotonicNanoseconds / 1000000) - start.BootMonotonicStartMilliseconds - from));
            recording.Add(new(elapsed, DateTimeOffset.FromUnixTimeMilliseconds(point.UtcMilliseconds), point.LatitudeDegrees,
                point.LongitudeDegrees, point.AccuracyMetres, RecordedRunSection.Main,
                previous is not null && evidence.DrawablePredecessor != previous));
            previous = evidence.PointId;
        }
        var key = new RouteKey(source.Id, StableId(source.Id, Pilot.RuleVersion));
        var result = MainRouteCapture.Build(key, name.Trim(), source.Id, RecordedWorkoutKind.Continuous, true, recording, Pilot);
        if (result.Captured is not { } captured) throw new InvalidDataException(result.Detail ?? "The recorded Main track cannot supply a route.");
        var fullElapsed = TimeSpan.FromMilliseconds(to - from);
        var first = recording[0].Elapsed;
        var progress = captured.Progress.Select(point => point with { Elapsed = point.Elapsed + first }).ToList();
        var unknownPrefix = first > TimeSpan.Zero;
        var unknownSuffix = progress[^1].Elapsed < fullElapsed;
        if (unknownPrefix) progress.Insert(0, new(TimeSpan.Zero, 0, false));
        if (unknownSuffix) progress.Add(new(fullElapsed, captured.Route.LengthMetres, false));
        var reason = unknownPrefix || unknownSuffix ? "The recorded Main start or end lacks GPS progress evidence." : null;
        var attempt = new GhostAttempt(StableId(source.Id, "stored-source-attempt-v2"), source.Id, key, mainUtc.ToOffset(offset),
            start.TimeZoneId, fullElapsed, reason is null ? AttemptQuality.Verified : AttemptQuality.InsufficientEvidence,
            Pilot.RuleVersion, progress);
        var envelope = new GhostSourceEvidenceV2(2, source.Id, source.StartedUtc, source.LastObservedUtc, source.ObservedElapsedMilliseconds,
            source.OriginalPlan.Clone(), source.Template?.Clone(), source.Checkpoint.Clone(), source.Events.ToArray(),
            source.Locations.Select(point => new ExportLocationV2(point.SnapshotOrder, point.UtcMilliseconds, point.BootMonotonicNanoseconds,
                point.LatitudeDegrees, point.LongitudeDegrees, point.AccuracyMetres, point.Provenance)).ToArray(), from, to, attempt.StartedLocal, attempt.TimeZoneId, localProvenance);
        return new(captured, attempt, reason, GhostSourceEvidenceCodec.Write(envelope));
    }

    internal const string RecordedCivil = "recorded-source-start-civil-v1";
    internal const string DerivedCivil = "main-civil-derived-from-recorded-zone-at-capture-v1";

    // Validates persisted anchors only. Archive reads must never consult today's zone rules.
    internal static (RunStartContext Start, long From, long To, DateTimeOffset SourceUtc) ReadFrozenClock(
        JsonElement checkpoint, JsonElement? templateJson, long observedMilliseconds, string startedUtc)
    {
        if (!checkpoint.TryGetProperty("SchemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema != 1
            || checkpoint.GetProperty("Snapshot").GetProperty("Phase").GetString() != "Finished")
            throw new InvalidDataException("The saved source does not have supported finished phase facts.");
        var template = templateJson?.ValueKind == JsonValueKind.Null ? null : templateJson?.Deserialize<CoreTemplate>();
        var plan = checkpoint.GetProperty("Plan").Deserialize<TimedPhase[]>()
            ?? throw new InvalidDataException("The saved phase plan is unavailable.");
        if (!IsContinuous(template, plan)) throw new InvalidDataException("Only a recorded continuous C2 source can supply a Ghost route.");
        var completed = checkpoint.GetProperty("Completed").Deserialize<CompletedPhase[]>();
        if (completed is null || completed.Length != plan!.Length || completed.Where((phase, index) => phase is null
            || phase.Plan != plan[index] || phase.ActiveTime < TimeSpan.Zero || phase.ActiveTime > phase.Plan.Duration).Any())
            throw new InvalidDataException("The saved continuous phase instances are unavailable.");
        var start = checkpoint.GetProperty("StartContext").Deserialize<RunStartContext>()
            ?? throw new InvalidDataException("The source start clock and local zone were not recorded.");
        if (start.BootMonotonicStartMilliseconds < 0 || string.IsNullOrWhiteSpace(start.TimeZoneId)
            || start.UtcOffsetSeconds is < -50400 or > 50400 || start.UtcOffsetSeconds % 60 != 0)
            throw new InvalidDataException("The saved source start clock or local zone is unsupported.");
        var facts = checkpoint.GetProperty("PhaseBoundaries").Deserialize<PhaseBoundaryFact[]>()
            ?? throw new InvalidDataException("Main start and end times were not recorded.");
        if (facts.Any(fact => fact is null)) throw new InvalidDataException("Saved phase boundaries are malformed.");
        var mainIndex = Array.FindIndex(plan, phase => phase.Kind == RunPhase.Work);
        PhaseBoundaryFact Boundary(int index)
        {
            var matching = facts.Where(fact => fact.PreviousPhaseIndex == index).ToArray();
            if (matching.Length != 1 || matching[0].PreviousPhase != plan[index].Kind
                || matching[0].NextPhaseIndex != (index == plan.Length - 1 ? null : index + 1)
                || matching[0].NextPhase != (index == plan.Length - 1 ? null : plan[index + 1].Kind)
                || matching[0].EffectiveElapsedMilliseconds < 0
                || matching[0].EffectiveElapsedMilliseconds > matching[0].ObservedElapsedMilliseconds
                || matching[0].ObservedElapsedMilliseconds > observedMilliseconds
                || matching[0].StartAnchorDerivedEffectiveUtc.Offset != TimeSpan.Zero
                || matching[0].StartAnchorDerivedEffectiveUtc != DateTimeOffset.Parse(startedUtc, CultureInfo.InvariantCulture).AddMilliseconds(matching[0].EffectiveElapsedMilliseconds))
                throw new InvalidDataException("The saved Main boundary clock is inconsistent.");
            return matching[0];
        }
        var from = mainIndex == 0 ? 0 : Boundary(mainIndex - 1).EffectiveElapsedMilliseconds;
        var to = Boundary(mainIndex).EffectiveElapsedMilliseconds;
        if (to <= from || to - from > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond
            || completed[mainIndex].ActiveTime > TimeSpan.FromMilliseconds(to - from))
            throw new InvalidDataException("The full saved Main elapsed time is unavailable.");
        var sourceUtc = DateTimeOffset.Parse(startedUtc, CultureInfo.InvariantCulture);
        if (sourceUtc.Offset != TimeSpan.Zero) throw new InvalidDataException("The source UTC anchor is unavailable.");
        return (start, from, to, sourceUtc);
    }

    public static StoredGhostCapture Name(StoredGhostCapture captured, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var old = captured.Geometry;
        var route = new GhostRoute(old.Route.Key, name.Trim(), old.Route.SourceRunId, old.Route.RuleVersion, old.Route.Geometry);
        var geometry = new CapturedMainRoute(route, old.Policy, old.Raw.ToArray(), old.Progress.ToArray());
        return new(geometry, captured.Attempt, captured.QualityReason, captured.RawJson);
    }

    private static Guid StableId(Guid source, string purpose) => new(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString("N") + ":" + purpose)).AsSpan(0, 16));
}
