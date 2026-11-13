namespace LoopCx.Features.ActiveRun;

public sealed record CoreTemplate(string TemplateId, string Source, string Category,
    int TemplateRevision, int? IntendedHeartRateZone)
{
    public static CoreTemplate C1 { get; } = new("C1", "LOOP Core", "intervals", 1, null);
    public static CoreTemplate C2 { get; } = new("C2", "LOOP Core", "continuous", 1, 2);
}
