using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.StartRun;

// Presentation only: never infer a sensor result or intended target from archived effort values.
public static class CorePresentation
{
    public static string Dose(CoreStartRequest request)
    {
        if (request.ProtocolId == "C2") return $"{request.ContinuousMinutes ?? 45} min continuous";
        var protocol = CoreProtocols.Find(request.ProtocolId);
        var count = request.Repetitions ?? protocol.DefaultCount;
        return protocol.WorkSeconds % 60 == 0 && protocol.RecoverySeconds % 60 == 0
            ? $"{count} × {protocol.WorkSeconds / 60}:{protocol.RecoverySeconds / 60} min"
            : $"{count} × {protocol.WorkSeconds} sec:{protocol.RecoverySeconds / 60} min";
    }

    public static string DoseExplanation(CoreStartRequest request)
    {
        if (request.ProtocolId == "C2") return Dose(request);
        var protocol = CoreProtocols.Find(request.ProtocolId);
        var count = request.Repetitions ?? protocol.DefaultCount;
        static string Time(int seconds) => seconds % 60 == 0 ? $"{seconds / 60} minutes" : $"{seconds} seconds";
        return $"{count} work efforts, {Time(protocol.WorkSeconds)} work and {Time(protocol.RecoverySeconds)} recovery between efforts.";
    }

    public static string MeasuredZone(HeartRateState? heartRate)
    {
        if (heartRate is not { Quality: "valid", CurrentBpm: { } bpm }) return "No current reading";

        var zones = heartRate.Configuration.Zones;

        if (zones.Count == 0) return "Personal zones not configured";

        var zone = zones.FirstOrDefault(item => bpm >= item.MinimumBpm && bpm <= item.MaximumBpm);

        return zone is not null ? $"measured Zone {zone.Number}" : bpm < zones[0].MinimumBpm ? "below Zone 1" : "above Zone 5";
    }
}
