using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.History;

public sealed record StoredSplit(int Number, bool FinalPartial, TimeSpan Active, double Metres,
    double? Pace, double? HeartRate, TimeSpan? GpsKnown, TimeSpan? GpsUnknown, TimeSpan? HrKnown, TimeSpan? HrUnknown)
{
    public bool HasMeasuredDistance => Metres > 0 || GpsKnown > TimeSpan.Zero;
}
public sealed record StoredPhase(int Number, RunPhase Kind, TimeSpan? Active, TimeSpan? Elapsed);
public sealed record StoredZone(int Number, int MinimumBpm, int MaximumBpm, TimeSpan? Active);
public sealed record StoredZoneScope(TimeSpan? Active, TimeSpan? Known, TimeSpan? Unknown, TimeSpan? Outside,
    IReadOnlyList<StoredZone> Zones);
public sealed record StoredMeasurementDetails(IReadOnlyList<StoredSplit>? Splits, IReadOnlyList<StoredPhase>? Phases,
    StoredZoneScope Main, StoredZoneScope Whole)
{
    public static StoredMeasurementDetails Unknown { get; } = new(null, null, new(null, null, null, null, []), new(null, null, null, null, []));
}

// Presence is checked before interpreting a DTO default as a recorded zero. Never replay raw observations.
public static class StoredMeasurementProjection
{
    public static StoredMeasurementDetails Read(JsonElement checkpoint, HeartRateConfiguration? configuration)
    {
        var statistics = Field(checkpoint, "Snapshot", "Statistics");
        var supported = Integer(Field(statistics, "Version")) == 1;
        return new(supported ? Splits(checkpoint, statistics) : null, Phases(checkpoint),
            Zones(supported ? Field(statistics, "Main") : default, configuration),
            Zones(supported ? Field(statistics, "Total") : default, configuration));
    }

    private static StoredSplit[]? Splits(JsonElement checkpoint, JsonElement statistics)
    {
        var distances = Field(checkpoint, "Snapshot", "Distance", "Splits");
        var scopes = Field(statistics, "CompletedSplits");
        if (distances.ValueKind != JsonValueKind.Array || scopes.ValueKind != JsonValueKind.Array
            || distances.GetArrayLength() != scopes.GetArrayLength())
        {
            return null;
        }

        var result = new List<StoredSplit>();
        for (var index = 0; index < distances.GetArrayLength(); index++)
        {
            var distance = distances[index];
            var scope = scopes[index];
            var active = Duration(Field(distance, "ActiveTime"));
            var metres = Number(Field(distance, "MeasuredMetres"));
            if (Integer(Field(distance, "Number")) != index + 1 || active is null || metres is null
                || active != Duration(Field(scope, "ActiveTime")) || metres != Number(Field(scope, "MeasuredMetres"))
                || metres > 1000 || (index < distances.GetArrayLength() - 1 && metres != 1000))
            {
                return null;
            }

            var gpsKnown = Duration(Field(scope, "KnownGpsActiveTime"));
            var gpsUnknown = Duration(Field(scope, "UnknownGpsActiveTime"));
            if (gpsKnown is null || gpsUnknown is null || (decimal)gpsKnown.Value.Ticks + gpsUnknown.Value.Ticks != active.Value.Ticks) gpsKnown = gpsUnknown = null;
            var hrKnown = Duration(Field(scope, "KnownHeartRateActiveTime"));
            if (hrKnown > active) hrKnown = null;
            var hrUnknown = Duration(Field(scope, "UnknownHeartRateActiveTime"));
            if (hrKnown is null || hrUnknown is null || (decimal)hrKnown.Value.Ticks + hrUnknown.Value.Ticks != active.Value.Ticks) hrUnknown = null;
            // These are the frozen strict pace and weighted HR values, not a new distance/time calculation.
            var pace = gpsKnown is not null && gpsUnknown == TimeSpan.Zero ? Number(Field(scope, "AveragePaceSecondsPerKilometre")) : null;
            var hr = hrKnown > TimeSpan.Zero ? Number(Field(scope, "AverageHeartRate")) : null;
            result.Add(new(index + 1, metres < 1000, active.Value, metres.Value, pace is > 0 && pace <= TimeSpan.MaxValue.TotalSeconds ? pace : null,
                hr is > 0 ? hr : null, gpsKnown, gpsUnknown, hrKnown, hrUnknown));
        }

        var main = Field(statistics, "Main");
        if ((Duration(Field(main, "ActiveTime")) is { } mainTime && result.Sum(static split => (decimal)split.Active.Ticks) != mainTime.Ticks)
            || (Number(Field(main, "MeasuredMetres")) is { } mainMetres && Math.Abs(result.Sum(static split => split.Metres) - mainMetres) > 0.000001))
        {
            return null;
        }

        // Finished distance allocation already includes the final partial; CurrentSplit is not a second row.
        return [.. result];
    }

    private static StoredPhase[]? Phases(JsonElement checkpoint)
    {
        var plan = Field(checkpoint, "Plan");
        var completed = Field(checkpoint, "Completed");
        var boundaries = Field(checkpoint, "PhaseBoundaries");
        if (plan.ValueKind != JsonValueKind.Array) return null;

        var result = new List<StoredPhase>();
        for (var index = 0; index < plan.GetArrayLength(); index++)
        {
            var phase = plan[index];
            var kind = Integer(Field(phase, "Kind"));
            if (kind is null || !Enum.IsDefined((RunPhase)kind)) return null;

            TimeSpan? active = null;
            if (completed.ValueKind == JsonValueKind.Array && completed.GetArrayLength() == plan.GetArrayLength())
            {
                var recorded = completed[index];
                var applied = Field(recorded, "Plan");
                if (Integer(Field(applied, "Kind")) == kind && Integer(Field(applied, "TargetRpe")) == Integer(Field(phase, "TargetRpe")) && Duration(Field(applied, "Duration")) == Duration(Field(phase, "Duration"))
                    && Duration(Field(phase, "Duration")) is { } duration && Duration(Field(recorded, "ActiveTime")) is { } time && time <= duration)
                {
                    active = time;
                }
            }

            var from = index == 0 ? 0L : Boundary(boundaries, plan, index - 1);
            var to = Boundary(boundaries, plan, index);
            var elapsed = from is not null && to >= from ? TimeSpan.FromTicks((to.Value - from.Value) * TimeSpan.TicksPerMillisecond) : (TimeSpan?)null;
            if (active > elapsed) elapsed = null;
            result.Add(new(index + 1, (RunPhase)kind.Value, active, elapsed));
        }

        return [.. result];
    }

    private static long? Boundary(JsonElement boundaries, JsonElement plan, int index)
    {
        if (boundaries.ValueKind != JsonValueKind.Array) return null;
        var matches = boundaries.EnumerateArray().Where(fact => Integer(Field(fact, "PreviousPhaseIndex")) == index).ToArray();
        if (matches.Length != 1) return null;
        var fact = matches[0];
        var last = index == plan.GetArrayLength() - 1;
        if (Integer(Field(fact, "PreviousPhase")) != Integer(Field(plan[index], "Kind"))
            || (last ? Field(fact, "NextPhaseIndex").ValueKind != JsonValueKind.Null || Field(fact, "NextPhase").ValueKind != JsonValueKind.Null
                : Integer(Field(fact, "NextPhaseIndex")) != index + 1 || Integer(Field(fact, "NextPhase")) != Integer(Field(plan[index + 1], "Kind"))))
        {
            return null;
        }

        var value = Field(fact, "EffectiveElapsedMilliseconds");
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var milliseconds)
            && milliseconds >= 0 && milliseconds <= TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond ? milliseconds : null;
    }

    private static StoredZoneScope Zones(JsonElement scope, HeartRateConfiguration? configuration)
    {
        var active = Duration(Field(scope, "ActiveTime"));
        var known = Duration(Field(scope, "KnownHeartRateActiveTime"));
        var outside = Duration(Field(scope, "UnclassifiedHeartRateActiveTime"));
        if (known > active) known = null;
        var unknown = Duration(Field(scope, "UnknownHeartRateActiveTime"));
        if (known is null || unknown is null || active is null || (decimal)known.Value.Ticks + unknown.Value.Ticks != active.Value.Ticks) unknown = null;
        var durations = Field(scope, "HeartRateZones");
        var values = new Dictionary<int, TimeSpan>();
        var configurationValid = configuration is { Version: 1, Zones.Count: > 0 };
        if (configurationValid)
        {
            try { configuration!.Validate(); }
            catch (ArgumentException) { configurationValid = false; }
        }
        var valid = configurationValid && active is not null && known is not null && outside is not null
            && durations.ValueKind == JsonValueKind.Array;
        if (valid)
        {
            foreach (var item in durations.EnumerateArray())
            {
                var number = Integer(Field(item, "Zone"));
                var time = Duration(Field(item, "ActiveTime"));
                if (number is null || time is null || !configuration!.Zones.Any(zone => zone.Number == number)
                    || !values.TryAdd(number.Value, time.Value))
                {
                    valid = false;
                }
            }
            // Decimal accumulation avoids overflow from contradictory stored durations.
            valid &= values.Values.Sum(time => (decimal)time.Ticks) + outside!.Value.Ticks == known!.Value.Ticks;
        }

        return new(active, known, unknown, valid ? outside : null,
            [.. (configurationValid ? configuration!.Zones : []).Select(zone => new StoredZone(zone.Number, zone.MinimumBpm, zone.MaximumBpm,
                valid ? values.GetValueOrDefault(zone.Number) : null))]);
    }

    private static JsonElement Field(JsonElement value, params string[] path)
    {
        foreach (var name in path)
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value)) return default;

        return value;
    }

    private static TimeSpan? Duration(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String
        && TimeSpan.TryParseExact(value.GetString(), "c", CultureInfo.InvariantCulture, out var duration) && duration >= TimeSpan.Zero ? duration : null;
    }

    private static int? Integer(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    }

    private static double? Number(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            && double.IsFinite(number) && number >= 0 ? number : null;
    }
}
