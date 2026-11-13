namespace LoopCx.Features.ActiveRun;

public sealed record CoreTemplate(string TemplateId, string Source, string Category,
    int TemplateRevision, int? IntendedHeartRateZone)
{
    public static CoreTemplate C1 { get; } = new("C1", "LOOP Core", "intervals", 2, null);
    public static CoreTemplate C2 { get; } = new("C2", "LOOP Core", "continuous", 1, 2);

    public static TimedSession CreateC1Session() => TimedSession.Intervals(5,
        TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(2), 7, 2,
        TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(4));
}
