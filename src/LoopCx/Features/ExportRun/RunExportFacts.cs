using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.ExportRun;

public sealed record RunStartContext(string TimeZoneId, int UtcOffsetSeconds, long BootMonotonicStartMilliseconds);

public sealed record PhaseBoundaryFact(int PreviousPhaseIndex, int? NextPhaseIndex, RunPhase PreviousPhase,
    RunPhase? NextPhase, long EffectiveElapsedMilliseconds, long ObservedElapsedMilliseconds,
    DateTimeOffset ObservedUtc, DateTimeOffset StartAnchorDerivedEffectiveUtc, bool Synthetic);
