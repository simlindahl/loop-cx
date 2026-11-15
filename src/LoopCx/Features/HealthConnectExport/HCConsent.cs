using System.Text.Json;

namespace LoopCx.Features.HealthConnectExport;

// Opt-in is intent, never a remembered Android grant. Each start freezes only its own revision.
public sealed record HCConsent(int Version, bool Enabled, Guid Revision)
{
    public static HCConsent Off { get; } = new(1, false, Guid.Empty);
    public HCConsent Enable()
    {
        return this with { Version = 1, Enabled = true, Revision = Guid.NewGuid() };
    }

    public HCConsent Disable()
    {
        return this with { Enabled = false };
    }

    public Guid? ForNewRun()
    {
        return Version == 1 && Enabled && Revision != Guid.Empty ? Revision : null;
    }

    public static bool Eligible(JsonElement checkpoint)
    {
        return checkpoint.ValueKind == JsonValueKind.Object
        && checkpoint.TryGetProperty("HealthConnectConsentRevision", out var revision)
        && revision.ValueKind == JsonValueKind.String && revision.TryGetGuid(out var id) && id != Guid.Empty;
    }
}
