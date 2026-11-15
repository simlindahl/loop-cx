using LoopCx.Features.ActiveRun;
using LoopCx.Features.History;
using LoopCx.Features.StartRun;

internal static class CorePresentationChecks
{
    internal static void Run()
    {
        // Arrange: intended targets are deliberately unrelated to these frozen sensor boundaries.
        var profile = HeartRateReserveProfile.Create(50, 190);
        var configuration = HeartRateConfiguration.Empty with { ReserveProfile = profile, Zones = profile.ResolveZones() };
        var fresh = new HeartRateState(configuration, 148, "valid", TimeSpan.Zero);

        // Act/Assert: a fresh personal-zone result must not be invented for stale/missing/unconfigured data.
        if (CorePresentation.MeasuredZone(fresh) != "measured Zone 3"
            || CorePresentation.MeasuredZone(fresh with { CurrentBpm = 100 }) != "below Zone 1"
            || CorePresentation.MeasuredZone(fresh with { CurrentBpm = 200 }) != "above Zone 5"
            || CorePresentation.MeasuredZone(fresh with { CurrentBpm = null }) != "No current reading"
            || CorePresentation.MeasuredZone(fresh with { Quality = "stale" }) != "No current reading"
            || CorePresentation.MeasuredZone(fresh with { Configuration = HeartRateConfiguration.Empty }) != "Personal zones not configured"
            || StoredRunProjection.TargetSummary(new(CoreTargetMode.None), false) != "Target not set")
        {
            throw new InvalidOperationException("Compact presentation must distinguish intended, actual, outside-zone and unknown values without RPE-derived defaults.");
        }

        Console.WriteLine("PASS: compact presentation keeps fresh frozen-zone classification distinct from targets, stale/missing/profile unknowns and known outside-zone readings.");
    }
}
