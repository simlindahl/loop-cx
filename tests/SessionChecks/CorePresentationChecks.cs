using LoopCx.Features.ActiveRun;
using LoopCx.Features.History;
using LoopCx.Features.StartRun;

internal static class CorePresentationChecks
{
    internal static void Run()
    {
        // Arrange
        var threshold = new CoreStartRequest("C1.3", 4);
        var frozenBefore = System.Text.Json.JsonSerializer.Serialize(CoreTemplate.Resolve(threshold, new()).Session.Plan);
        // Act
        var label = CorePresentation.Dose(threshold);
        var frozenAfter = CoreTemplate.Resolve(threshold, new()).Session.Plan;
        // Assert
        if (CorePresentation.DoseExplanation(threshold) != "4 work efforts, 6 minutes work and 3 minutes recovery between efforts."
            || label != "4 × 6:3 min" || CorePresentation.Dose(new("C1.2")) != "5 × 3:2 min"
            || CorePresentation.Dose(new("C1.1")) != "7 × 30 sec:3 min"
            || CorePresentation.Dose(new("C2", ContinuousMinutes: 60)) != "60 min continuous"
            || frozenAfter.Count(phase => phase.Kind == RunPhase.Recovery) != 3
            || System.Text.Json.JsonSerializer.Serialize(frozenAfter) != frozenBefore)
            throw new InvalidOperationException("Compact dose wording must preserve the exact frozen plan and N−1 recovery.");

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
