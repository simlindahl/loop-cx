namespace LoopCx.Features.ActiveRun;

public enum CoreTargetMode { None, Pace, HeartRateZone }
public sealed record CoreTargets(CoreTargetMode Mode = CoreTargetMode.None, int? Work = null, int? Recovery = null)
{
    public void Validate(bool continuous)
    {
        if (!Enum.IsDefined(Mode) || (Mode == CoreTargetMode.None && (Work is not null || Recovery is not null)) ||
            (Mode != CoreTargetMode.None && Work is null) || (continuous && Recovery is not null) ||
            (Mode == CoreTargetMode.Pace && (Work <= 0 || Recovery <= 0)) ||
            (Mode == CoreTargetMode.HeartRateZone && (Work is < 1 or > 5 || Recovery is < 1 or > 5)))
        {
            throw new ArgumentException("Choose one valid pace or zone target; recovery is optional for C1 only.");
        }
    }
}

public sealed record C1WarmupCooldownSettings(int WarmupMinutes = 4, int CooldownMinutes = 3)
{
    public void Validate()
    {
        if (WarmupMinutes is < 0 or > 15 || CooldownMinutes is < 0 or > 15)
            throw new ArgumentException("Warm-up and cool-down must each be Off or 1–15 whole minutes.");
    }
}
public sealed record CoreStartRequest(string ProtocolId, int? Repetitions = null, int? ContinuousMinutes = null,
    CoreTargets? Targets = null, Guid? ShortcutId = null, string? ShortcutName = null);
public sealed record ResolvedCoreStart(TimedSession Session, CoreTemplate Template);
public sealed record CoreProtocol(string Id, string Title, string Purpose, int DefaultCount, int MinimumCount,
    int MaximumCount, int WorkSeconds, int RecoverySeconds, int WorkRpe, int RecoveryRpe)
{
    public TimeSpan MainTime(int count) => TimeSpan.FromSeconds(count * WorkSeconds + (count - 1) * RecoverySeconds);
}

public static class CoreProtocols
{
    public static IReadOnlyList<CoreProtocol> All { get; } =
    [
        new("C1.1", "Sprint intervals (SIT)", "Repeated maximal sprint efforts with substantial easy recovery.", 7, 4, 18, 30, 180, 10, 2),
        new("C1.2", "Aerobic intervals (HIIT)", "Sustained, controlled hard intervals. Keep the effort repeatable.", 5, 3, 12, 180, 120, 7, 3),
        new("C1.3", "Threshold intervals", "Controlled sustained effort near your individual threshold.", 3, 2, 7, 360, 180, 6, 3)
    ];

    public static CoreProtocol Find(string id) => All.FirstOrDefault(protocol => protocol.Id == id)
        ?? throw new ArgumentException("Choose an official C1 protocol.", nameof(id));
}
