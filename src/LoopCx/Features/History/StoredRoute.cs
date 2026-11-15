using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;

namespace LoopCx.Features.History;

public sealed record StoredRouteLink(long FromOrder, long ToOrder);
public sealed record StoredRouteScope(bool Supported, IReadOnlyList<ExportLocation> Points,
    IReadOnlyList<StoredRouteLink> Links, bool UnknownQuality);
public sealed record StoredRoute(Guid Id, StoredRouteScope Whole, StoredRouteScope Main, bool Synthetic);

// Geometry evidence only. Never an estimator, accepted-distance replay or recorded-data mutation.
public static class StoredRouteProjection
{
    public static StoredRoute Read(Guid id, JsonElement checkpoint, long observedElapsedMilliseconds, IReadOnlyList<ExportLocation> points)
    {
        if (id == Guid.Empty || observedElapsedMilliseconds < 0) throw new InvalidDataException("Invalid stored route identity or observation.");
        if (points.Zip(points.Skip(1)).Any(pair => pair.First.SnapshotOrder >= pair.Second.SnapshotOrder))
            throw new InvalidDataException("Stored route order is inconsistent.");
        ExportRunV2.ValidatePoints(id, points);
        var visible = points.Where(point => double.IsFinite(point.LatitudeDegrees) && point.LatitudeDegrees is >= -90 and <= 90
            && double.IsFinite(point.LongitudeDegrees) && point.LongitudeDegrees is >= -180 and <= 180
            && (point.Provenance is null || point.Provenance.Outcome == "valid")).ToArray();
        var whole = Scope(visible);
        var main = new StoredRouteScope(false, [], [], false);
        var boundaries = Field(checkpoint, "PhaseBoundaries");
        var synthetic = Field(checkpoint, "Snapshot", "Statistics", "HasSyntheticTime").ValueKind == JsonValueKind.True
            || boundaries.ValueKind == JsonValueKind.Array && boundaries.EnumerateArray().Any(
                static fact => Field(fact, "Synthetic").ValueKind == JsonValueKind.True);
        if (TryIntervals(checkpoint, observedElapsedMilliseconds, out var start, out var intervals))
        {
            var selected = visible.Where(point => intervals.Any(interval =>
            {
                var elapsed = (decimal)(point.BootMonotonicNanoseconds / 1_000_000) - start;
                return elapsed >= interval.From && (elapsed < interval.To || interval.Inclusive && elapsed == interval.To);
            })).ToArray();
            main = Scope(selected);
        }
        return new(id, whole, main, synthetic);
    }

    private static StoredRouteScope Scope(ExportLocation[] points)
    {
        var byId = points.Where(point => point.Provenance is not null).ToDictionary(point => point.Provenance!.PointId);
        var links = new List<StoredRouteLink>();
        foreach (var point in points)
        {
            if (point.Provenance?.DrawablePredecessor is { } previous && byId.TryGetValue(previous, out var from))
                links.Add(new(from.SnapshotOrder, point.SnapshotOrder));
        }
        return new(true, points, links, points.Any(point => point.Provenance is null));
    }

    private static bool TryIntervals(JsonElement checkpoint, long observed, out long start,
        out List<(long From, long To, bool Inclusive)> intervals)
    {
        start = 0;
        intervals = [];
        var anchor = Integer(Field(checkpoint, "StartContext", "BootMonotonicStartMilliseconds"));
        var plan = Field(checkpoint, "Plan");
        var boundaries = Field(checkpoint, "PhaseBoundaries");
        if (Integer(Field(checkpoint, "SchemaVersion")) != 1 || anchor is null or < 0
            || plan.ValueKind != JsonValueKind.Array || plan.GetArrayLength() == 0 || boundaries.ValueKind != JsonValueKind.Array)
            return false;
        start = anchor.Value;
        var motionStart = Field(checkpoint, "Snapshot", "Motion", "StartedMonotonicTime");
        if (motionStart.ValueKind != JsonValueKind.Undefined && motionStart.ValueKind != JsonValueKind.Null
            && (motionStart.ValueKind != JsonValueKind.String || !TimeSpan.TryParseExact(motionStart.GetString(), "c", CultureInfo.InvariantCulture, out var motion)
                || (decimal)motion.Ticks / TimeSpan.TicksPerMillisecond != start)) return false;
        var phases = plan.EnumerateArray().Select(phase => Integer(Field(phase, "Kind"))).ToArray();
        if (phases.Any(kind => kind is null or < 0 or > 3)) return false;
        var completed = Field(checkpoint, "Completed");
        if (completed.ValueKind != JsonValueKind.Array || completed.GetArrayLength() > phases.Length) return false;
        for (var index = 0; index < completed.GetArrayLength(); index++)
        {
            if (Field(completed[index], "Plan").ValueKind != JsonValueKind.Object || !JsonElement.DeepEquals(Field(completed[index], "Plan"), plan[index])
                || !Duration(Field(plan[index], "Duration"), out var duration) || duration <= TimeSpan.Zero
                || !Duration(Field(completed[index], "ActiveTime"), out var active) || active < TimeSpan.Zero || active > duration) return false;
        }
        var ends = new Dictionary<int, long>();
        long previousEffective = 0;
        long previousReceipt = 0;
        foreach (var fact in boundaries.EnumerateArray())
        {
            var index = Integer(Field(fact, "PreviousPhaseIndex"));
            var effective = Integer(Field(fact, "EffectiveElapsedMilliseconds"));
            var receipt = Integer(Field(fact, "ObservedElapsedMilliseconds"));
            var flag = Field(fact, "Synthetic");
            if (index != ends.Count || index >= completed.GetArrayLength() || index >= phases.Length || effective is null || receipt is null
                || effective < previousEffective || receipt < previousReceipt || receipt < effective || receipt > observed
                || Integer(Field(fact, "PreviousPhase")) != phases[index.Value]
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
            var last = index == phases.Length - 1;
            if (last ? Field(fact, "NextPhaseIndex").ValueKind != JsonValueKind.Null || Field(fact, "NextPhase").ValueKind != JsonValueKind.Null
                : Integer(Field(fact, "NextPhaseIndex")) != index + 1 || Integer(Field(fact, "NextPhase")) != phases[index.Value + 1]) return false;
            if (!ends.TryAdd((int)index.Value, effective.Value)) return false;
            previousEffective = effective.Value;
            previousReceipt = receipt.Value;
        }
        // Only the committed current phase can have an open end, bounded by observation, never guessed stop.
        var current = completed.ValueKind == JsonValueKind.Array ? completed.GetArrayLength() : -1;
        var phaseName = Field(checkpoint, "Snapshot", "Phase");
        if (phaseName.ValueKind != JsonValueKind.String || phaseName.GetString() != (current == phases.Length ? "Finished" : ((RunPhase)phases[current]!.Value).ToString())) return false;
        for (var index = 0; index < phases.Length; index++)
        {
            if (phases[index] != (int)RunPhase.Work && phases[index] != (int)RunPhase.Recovery) continue;
            var from = index == 0 ? 0 : ends.GetValueOrDefault(index - 1, -1);
            var to = ends.GetValueOrDefault(index, -1);
            var inclusive = to < 0 && current == index;
            if (to < 0 && current == index && Field(checkpoint, "Snapshot", "Phase").ValueKind == JsonValueKind.String
                && Field(checkpoint, "Snapshot", "Phase").GetString() == ((RunPhase)phases[index]!.Value).ToString()) to = observed;
            // Future planned main phases contain no committed point membership.
            if (from < 0 && current >= 0 && index > current) continue;
            if (from < 0 || to < from) return false;
            intervals.Add((from, to, inclusive));
        }
        return intervals.Count > 0;
    }

    private static bool Duration(JsonElement value, out TimeSpan duration)
    {
        duration = default;
        return value.ValueKind == JsonValueKind.String && TimeSpan.TryParseExact(value.GetString(), "c", CultureInfo.InvariantCulture, out duration);
    }

    private static long? Integer(JsonElement field) => field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out var value) ? value : null;
    private static JsonElement Field(JsonElement value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out value)) return default;
        }
        return value;
    }
}
