using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.StartRun;

// Presentation only: never infer a sensor result or intended target from archived effort values.
public static class CorePresentation
{
    public static string MeasuredZone(HeartRateState? heartRate)
    {
        if (heartRate is not { Quality: "valid", CurrentBpm: { } bpm }) return "No current reading";

        var zones = heartRate.Configuration.Zones;

        if (zones.Count == 0) return "Personal zones not configured";

        var zone = zones.FirstOrDefault(item => bpm >= item.MinimumBpm && bpm <= item.MaximumBpm);

        return zone is not null ? $"measured Zone {zone.Number}" : bpm < zones[0].MinimumBpm ? "below Zone 1" : "above Zone 5";
    }
}
