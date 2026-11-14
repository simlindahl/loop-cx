using System.Text.Json.Serialization;

namespace LoopCx.Features.ActiveRun;

public sealed record CoreTemplate(string TemplateId, string Source, string Category,
    int TemplateRevision, int? IntendedHeartRateZone, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProtocolId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ProtocolRevision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CoreTargets? Targets = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] C1WarmupCooldownSettings? ResolvedWarmupCooldown = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ShortcutId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ShortcutName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsPersonalised = null)
{
    public static CoreTemplate C1 { get; } = new("C1", "LOOP Core", "intervals", 2, null);
    public static CoreTemplate C2 { get; } = new("C2", "LOOP Core", "continuous", 1, 2);

    public static ResolvedCoreStart Resolve(CoreStartRequest request, C1WarmupCooldownSettings warmupCooldown)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(warmupCooldown);
        if (request.ShortcutId == Guid.Empty || (request.ShortcutId is not null && string.IsNullOrWhiteSpace(request.ShortcutName)) ||
            (request.ShortcutId is null && request.ShortcutName is not null))
            throw new ArgumentException("The saved shortcut identity is invalid.");

        var continuous = request.ProtocolId == "C2";
        var targets = request.Targets ?? new CoreTargets();
        targets.Validate(continuous);

        if (continuous)
        {
            var minutes = request.ContinuousMinutes ?? 45;

            if (request.Repetitions is not null || minutes is < 20 or > 120 || minutes % 5 != 0)
                throw new ArgumentException("Choose a C2 duration from 20 to 120 minutes in five-minute steps.");

            return new(TimedSession.Continuous(TimeSpan.FromMinutes(minutes), 4),
                new("C2", "LOOP Core", "continuous", 2, 2, "C2", 2, targets, null, request.ShortcutId, request.ShortcutName,
                    minutes != 45 || targets.Mode != CoreTargetMode.None));
        }

        var protocol = CoreProtocols.Find(request.ProtocolId);
        var count = request.Repetitions ?? protocol.DefaultCount;

        if (request.ContinuousMinutes is not null || count < protocol.MinimumCount || count > protocol.MaximumCount ||
            protocol.MainTime(count) < TimeSpan.FromMinutes(10) || protocol.MainTime(count) > TimeSpan.FromMinutes(60))
            throw new ArgumentException("Choose a whole repetition count within the official protocol limits.");

        warmupCooldown.Validate();

        return new(TimedSession.Intervals(count, TimeSpan.FromSeconds(protocol.WorkSeconds),
            TimeSpan.FromSeconds(protocol.RecoverySeconds), protocol.WorkRpe, protocol.RecoveryRpe,
            TimeSpan.FromMinutes(warmupCooldown.WarmupMinutes), TimeSpan.FromMinutes(warmupCooldown.CooldownMinutes)),
            new("C1", "LOOP Core", "intervals", 3, null, protocol.Id, 1, targets, warmupCooldown, request.ShortcutId, request.ShortcutName,
                count != protocol.DefaultCount || targets.Mode != CoreTargetMode.None || warmupCooldown != new C1WarmupCooldownSettings()));
    }

    public static TimedSession CreateC1Session() => TimedSession.Intervals(5,
        TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(2), 7, 2,
        TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(4));
}
