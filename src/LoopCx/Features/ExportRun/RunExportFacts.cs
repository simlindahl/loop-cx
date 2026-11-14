using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.ExportRun;

public sealed record RunStartContext(string TimeZoneId, int UtcOffsetSeconds, long BootMonotonicStartMilliseconds);

public sealed record PhaseBoundaryFact(int PreviousPhaseIndex, int? NextPhaseIndex, RunPhase PreviousPhase,
    RunPhase? NextPhase, long EffectiveElapsedMilliseconds, long ObservedElapsedMilliseconds,
    DateTimeOffset ObservedUtc, DateTimeOffset StartAnchorDerivedEffectiveUtc, bool Synthetic);

// Time fields remain in the linked terminal boundary; this adds only the missing zone provenance.
public sealed record RunTerminalContext(int TerminalPreviousPhaseIndex, string ObservedTimeZoneId,
    int ObservedUtcOffsetSeconds, int EffectiveUtcOffsetSeconds, string Provenance)
{
    public const string ObservedZoneRulesV1 = "terminal-observed-zone-rules-v1";

    public static RunTerminalContext Capture(PhaseBoundaryFact boundary, TimeZoneInfo observedZone)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(observedZone);
        return boundary.NextPhaseIndex is not null || boundary.NextPhase is not null
            ? throw new ArgumentException("Terminal context requires a terminal boundary.", nameof(boundary))
            : new(boundary.PreviousPhaseIndex, observedZone.Id,
            (int)observedZone.GetUtcOffset(boundary.ObservedUtc).TotalSeconds,
            (int)observedZone.GetUtcOffset(boundary.StartAnchorDerivedEffectiveUtc).TotalSeconds, ObservedZoneRulesV1);
    }
}
