using System.Text.Json;

namespace LoopCx.Features.ExportRun;

// Storage facts, detached inside one journal read transaction. Projection never consults live state.
public sealed record ExportLocation(long SnapshotOrder, long UtcMilliseconds, long BootMonotonicNanoseconds,
    double LatitudeDegrees, double LongitudeDegrees, double? AccuracyMetres);
public sealed record ExportHeartRate(long Sequence, string ReceiptUtc, long BootReceiptMonotonicTicks, int? Bpm, string Quality);
public sealed record ExportEvent(long Sequence, string Command, string ObservedUtc, long ObservedElapsedMilliseconds, JsonElement Checkpoint);
public sealed record ExportJournalSnapshot(Guid Id, string StartedUtc, string LastObservedUtc,
    long ObservedElapsedMilliseconds, string Status, JsonElement OriginalPlan, JsonElement? Template,
    JsonElement? HeartRateSetup, JsonElement Checkpoint, string? ExportStatus,
    IReadOnlyList<ExportEvent> Events, IReadOnlyList<ExportLocation> Locations, IReadOnlyList<ExportHeartRate> HeartRate);

public sealed record ExportRunV1(int SchemaVersion, IReadOnlyDictionary<string, string> Units, ExportJournalSnapshot Run)
{
    public string SnapshotConsistency { get; } = "transactional-storage; raw GPS may precede the last derived checkpoint";

    public static readonly IReadOnlyDictionary<string, string> Version1Units = new Dictionary<string, string>
    {
        ["distance"] = "metres",
        ["coordinates"] = "decimal-degrees",
        ["heartRate"] = "beats-per-minute",
        ["utc"] = "ISO-8601",
        ["gpsUtc"] = "unix-milliseconds",
        ["observedElapsed"] = "milliseconds",
        ["gpsBootMonotonic"] = "nanoseconds",
        ["hrBootReceiptMonotonic"] = "100-nanosecond-ticks",
        ["checkpointDuration"] = "TimeSpan-constant-format",
        ["pace"] = "seconds-per-kilometre"
    };
    private static readonly string[] CheckpointFacts = ["Snapshot", "Plan", "Completed", "PhaseActiveTime", "TotalActiveTime", "MainActiveTime"];
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static string Project(ExportJournalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Checkpoint.TryGetProperty(nameof(SchemaVersion), out var checkpointVersion))
        {
            if (checkpointVersion.ValueKind != JsonValueKind.Number || !checkpointVersion.TryGetInt32(out var version) || version != 1
                || CheckpointFacts.Any(key => !snapshot.Checkpoint.TryGetProperty(key, out _)))
            {
                throw new InvalidDataException("The stored checkpoint version or required facts are unsupported.");
            }
        }
        if (snapshot.Id == Guid.Empty || snapshot.ObservedElapsedMilliseconds < 0)
            throw new InvalidDataException("A journal export requires an existing run and nonnegative observed elapsed time.");
        return snapshot.Events.Zip(snapshot.Events.Skip(1)).Any(pair => pair.First.Sequence >= pair.Second.Sequence)
            || snapshot.HeartRate.Zip(snapshot.HeartRate.Skip(1)).Any(pair => pair.First.Sequence >= pair.Second.Sequence)
            || snapshot.Locations.Zip(snapshot.Locations.Skip(1)).Any(pair => pair.First.SnapshotOrder >= pair.Second.SnapshotOrder)
            ? throw new InvalidDataException("Journal rows must retain their unique snapshot order.")
            : JsonSerializer.Serialize(new ExportRunV1(1, Version1Units, snapshot), JsonOptions);
    }
}
