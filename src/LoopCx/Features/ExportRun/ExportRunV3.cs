using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.GhostRun;

namespace LoopCx.Features.ExportRun;

public sealed record ExportGhostSnapshotV3(Guid Id, string StartedUtc, string LastObservedUtc,
    long ObservedElapsedMilliseconds, string Status, JsonElement OriginalPlan, JsonElement? Template,
    JsonElement? HeartRateSetup, JsonElement Checkpoint, string? ExportStatus,
    IReadOnlyList<ExportEvent> Events, IReadOnlyList<ExportLocationV2> Locations,
    IReadOnlyList<ExportHeartRate> HeartRate, string? LocalSavedPartUtc);

public sealed record ExportRunV3(int SchemaVersion, string ActivityKind, IReadOnlyDictionary<string, string> Units, ExportGhostSnapshotV3 Run)
{
    public string SnapshotConsistency { get; } = "transactional-storage; raw GPS may precede the last derived checkpoint";
    public static readonly IReadOnlyDictionary<string, string> Version3Units = new Dictionary<string, string>(ExportRunV1.Version1Units)
    {
        ["ghostBootMonotonic"] = "milliseconds",
        ["pauseBootMonotonic"] = "TimeSpan-constant-format",
        ["raceProgressElapsed"] = "TimeSpan-constant-format"
    };

    private static readonly string[] FrozenEventFields = ["Route", "Reference", "Profile", "StartedLocal", "TimeZoneId", "Mode", "HealthConnectConsentRevision"];

    public static string Project(ExportJournalSnapshot source, string? savedPartUtc)
    {
        var stored = GhostStoredRun.Read(source.Id, source.Status, source.Checkpoint.GetRawText(), savedPartUtc);
        var state = stored.Checkpoint;
        if (source.Status == "interrupted" && stored.SavedLocally is null
            || source.Status == "incomplete" && state.Snapshot.Clock.RaceElapsed <= TimeSpan.Zero)
            throw new InvalidDataException("Sharing requires a confirmed Ghost result or explicitly saved recorded part.");
        ValidateClock(state);
        if (!DateTimeOffset.TryParse(source.StartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var started)
            || started.Offset != TimeSpan.Zero || started != state.StartedUtc || source.ObservedElapsedMilliseconds != state.ObservedBootMilliseconds - state.StartedBootMilliseconds
            || !DateTimeOffset.TryParse(source.LastObservedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var observed)
            || observed.Offset != TimeSpan.Zero || state.TerminalContext is { } terminal && terminal.ObservedUtc != observed
            || source.OriginalPlan.ValueKind != JsonValueKind.Array || source.OriginalPlan.GetArrayLength() != 0)
            throw new InvalidDataException("Ghost storage identity/clock and frozen checkpoint disagree.");
        if (source.Events.Zip(source.Events.Skip(1)).Any(pair => pair.First.Sequence >= pair.Second.Sequence)
            || source.HeartRate.Zip(source.HeartRate.Skip(1)).Any(pair => pair.First.Sequence >= pair.Second.Sequence)
            || source.Locations.Zip(source.Locations.Skip(1)).Any(pair => pair.First.SnapshotOrder >= pair.Second.SnapshotOrder))
            throw new InvalidDataException("Ghost raw rows must retain their original unique storage order.");
        ExportRunV2.ValidatePoints(source.Id, source.Locations);
        foreach (var item in source.Events)
        {
            var historical = GhostRaceCheckpoint.Read(item.Checkpoint.GetRawText());
            ValidateClock(historical);
            if (!DateTimeOffset.TryParse(item.ObservedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventUtc)
                || eventUtc.Offset != TimeSpan.Zero || historical.TerminalContext is { } eventTerminal && eventTerminal.ObservedUtc != eventUtc
                || historical.Id != state.Id || historical.StartedUtc != state.StartedUtc
                || historical.StartedBootMilliseconds != state.StartedBootMilliseconds
                || historical.ObservedBootMilliseconds - historical.StartedBootMilliseconds != item.ObservedElapsedMilliseconds
                || historical.SaveRevision > state.SaveRevision
                || !FrozenEventFields
                    .All(name => JsonElement.DeepEquals(item.Checkpoint.GetProperty(name), source.Checkpoint.GetProperty(name)))
                || !SameOptionalOrigin(item.Checkpoint, source.Checkpoint))
                throw new InvalidDataException("An event must retain the same frozen Ghost identity/reference/policy at its own observation.");
        }
        var run = new ExportGhostSnapshotV3(source.Id, source.StartedUtc, source.LastObservedUtc, source.ObservedElapsedMilliseconds,
            source.Status, source.OriginalPlan, source.Template, source.HeartRateSetup, source.Checkpoint, source.ExportStatus,
            source.Events, source.Locations.Select(point => new ExportLocationV2(point.SnapshotOrder, point.UtcMilliseconds,
                point.BootMonotonicNanoseconds, point.LatitudeDegrees, point.LongitudeDegrees, point.AccuracyMetres, point.Provenance)).ToArray(),
            source.HeartRate, source.Status == "interrupted" ? savedPartUtc : null);
        return JsonSerializer.Serialize(new ExportRunV3(3, "ghost", Version3Units, run), ExportRunV1.JsonOptions);
    }

    private static bool SameOptionalOrigin(JsonElement historical, JsonElement latest)
    {
        var before = historical.TryGetProperty("Origin", out var prior);
        var after = latest.TryGetProperty("Origin", out var current);

        return before == after && (!before || JsonElement.DeepEquals(prior, current));
    }

    private static void ValidateClock(GhostRaceCheckpoint value)
    {
        var start = TimeSpan.FromMilliseconds(value.StartedBootMilliseconds);
        var observed = TimeSpan.FromMilliseconds(value.ObservedBootMilliseconds);
        var end = start + value.Snapshot.Clock.RaceElapsed;
        var through = start;
        var paused = TimeSpan.Zero;
        foreach (var pause in value.Pauses.OrderBy(item => item.StartedAt))
        {
            if (pause.StartedAt < start || pause.StartedAt > observed || pause.EndedAt is { } until && (until < pause.StartedAt || until > observed))
                throw new InvalidDataException("Ghost pause timestamps require their actual boot clock.");
            var from = pause.StartedAt > through ? pause.StartedAt : through;
            var to = pause.EndedAt is { } resumed && resumed < end ? resumed : end;
            if (to <= from) continue;
            paused += to - from;
            through = to;
        }
        if (value.Snapshot.Clock.TrainingActive != value.Snapshot.Clock.RaceElapsed - paused
            || value.Snapshot.Clock.TrainingPaused != value.Pauses.Any(item => item.StartedAt <= end && (item.EndedAt is null || item.EndedAt > end)))
            throw new InvalidDataException("Ghost training time must match the recorded pause union, never a recalculated route time.");
    }
}
