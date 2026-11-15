using System.Text.Json;
using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.ExportRun;

public sealed record ExportLocationV2(long SnapshotOrder, long UtcMilliseconds, long BootMonotonicNanoseconds,
    double LatitudeDegrees, double LongitudeDegrees, double? AccuracyMetres, RunPointProvenance? Provenance);
public sealed record ExportJournalSnapshotV2(Guid Id, string StartedUtc, string LastObservedUtc,
    long ObservedElapsedMilliseconds, string Status, JsonElement OriginalPlan, JsonElement? Template,
    JsonElement? HeartRateSetup, JsonElement Checkpoint, string? ExportStatus,
    IReadOnlyList<ExportEvent> Events, IReadOnlyList<ExportLocationV2> Locations, IReadOnlyList<ExportHeartRate> HeartRate);
public sealed record ExportRunV2(int SchemaVersion, IReadOnlyDictionary<string, string> Units, ExportJournalSnapshotV2 Run)
{
    public string SnapshotConsistency { get; } = "transactional-storage; raw GPS may precede the last derived checkpoint";

    public static string Project(ExportJournalSnapshot snapshot)
    {
        ExportRunV1.Validate(snapshot);
        ValidatePoints(snapshot.Id, snapshot.Locations);
        var run = new ExportJournalSnapshotV2(snapshot.Id, snapshot.StartedUtc, snapshot.LastObservedUtc,
            snapshot.ObservedElapsedMilliseconds, snapshot.Status, snapshot.OriginalPlan, snapshot.Template,
            snapshot.HeartRateSetup, snapshot.Checkpoint, snapshot.ExportStatus, snapshot.Events,
            snapshot.Locations.Select(static point => new ExportLocationV2(point.SnapshotOrder, point.UtcMilliseconds,
                point.BootMonotonicNanoseconds, point.LatitudeDegrees, point.LongitudeDegrees, point.AccuracyMetres, point.Provenance)).ToArray(), snapshot.HeartRate);
        return JsonSerializer.Serialize(new ExportRunV2(2, ExportRunV1.Version1Units, run), ExportRunV1.JsonOptions);
    }

    public static void ValidatePoints(Guid runId, IReadOnlyList<ExportLocation> points)
    {
        var seen = new Dictionary<long, ExportLocation>();
        long latest = 0;
        long? chain = null;
        foreach (var point in points)
        {
            var fact = point.Provenance;
            if (fact is null) { chain = null; continue; }
            if (fact.Version != 1 || fact.RunId != runId || fact.PointId <= latest
                || fact.Outcome is not ("valid" or "rejected" or "ignored" or "not-assessed")
                || (fact.DrawablePredecessor is null && string.IsNullOrWhiteSpace(fact.BreakReason)))
                throw new InvalidDataException("Unsupported or inconsistent stored point provenance.");

            if (fact.DrawablePredecessor is { } predecessor)
            {
                if (fact.Outcome != "valid" || predecessor != chain || fact.BreakReason is not null
                    || !seen.TryGetValue(predecessor, out var previous) || previous.Provenance?.Outcome != "valid"
                    || previous.BootMonotonicNanoseconds >= point.BootMonotonicNanoseconds)
                    throw new InvalidDataException("A drawable predecessor must be an earlier supported point in this run's current chain.");
            }
            else if (fact.Outcome == "valid" && point.BootMonotonicNanoseconds < 0)
                throw new InvalidDataException("A supported point requires an actual monotonic timestamp.");

            seen.Add(fact.PointId, point);
            latest = fact.PointId;
            if (fact.Outcome == "valid") chain = fact.PointId;
            else if (fact.Outcome != "ignored") chain = null;
        }
    }
}
