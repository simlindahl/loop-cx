using Android.Content;
using Android.Content.PM;
using LoopCx.Features.HealthConnectExport;
using System.Text.Json;

namespace LoopCx;

internal sealed record HealthConnectAccess(bool Available, bool Enabled, IReadOnlyList<string> Missing)
{
    internal string Label => !Available ? "Unavailable" : !Enabled ? "Off"
        : Missing.Count == 0 ? "Write access available" : "Write access needed";
}

internal static class HealthConnectSettings
{
    private static readonly object Gate = new();
#if DEBUG
    internal static bool NormalExportDisabled => true;
#else
    internal static bool NormalExportDisabled => false;
#endif
    internal static readonly string[] Permissions = ["android.permission.health.WRITE_EXERCISE",
        "android.permission.health.WRITE_DISTANCE", "android.permission.health.WRITE_HEART_RATE"];
    internal static event Action? Changed;
    internal static event Action? OpenRequested;
    private static bool openPending;
    internal static void RequestOpen() { openPending = true; OpenRequested?.Invoke(); }
    internal static bool ConsumeOpen() { var pending = openPending; openPending = false; return pending; }

    private static HCConsent? persisted;
    private static ISharedPreferences Store => global::Android.App.Application.Context.GetSharedPreferences("health-connect-consent", FileCreationMode.Private)!;
    internal static HCConsent Load()
    {
        lock (Gate)
        {
            if (persisted is not null) return persisted;
            try
            {
                var value = Store.GetString("state", null);
                var state = value is null ? HCConsent.Off : JsonSerializer.Deserialize<HCConsent>(value);
                return persisted = state is { Version: 1 } && (!state.Enabled || state.Revision != Guid.Empty) ? state : HCConsent.Off;
            }
            catch (Exception problem) when (problem is JsonException or Java.Lang.RuntimeException) { return persisted = HCConsent.Off; }
        }
    }

    internal static (DateTimeOffset Started, Guid? Revision) FreezeStart()
    {
        lock (Gate) return (DateTimeOffset.UtcNow, Load().ForNewRun());
    }

    internal static void SetEnabled(bool enabled)
    {
        lock (Gate)
        {
            var state = enabled ? Load().Enable() : Load().Disable();
            using var edit = Store.Edit()!;
            if (!edit.PutString("state", JsonSerializer.Serialize(state))!.Commit())
                throw new IOException("Health Connect choice could not be saved.");
            persisted = state;
        }
        Changed?.Invoke();
    }

    // Off and submission serialize only this short boundary; never hold it across a provider callback.
    internal static bool SubmitIfEnabled(Action submit)
    {
        lock (Gate)
        {
            if (!Load().Enabled) return false;
            submit();
            return true;
        }
    }

    internal static HealthConnectAccess Access()
    {
        var context = global::Android.App.Application.Context;
        bool available;
        try { available = OperatingSystem.IsAndroidVersionAtLeast(34) && context.GetSystemService(Context.HealthconnectService) is not null; }
        catch (Java.Lang.RuntimeException) { available = false; }
        var missing = available ? Permissions.Where(permission => context.CheckSelfPermission(permission) != Permission.Granted)
            .Select(static permission => permission switch
            {
                "android.permission.health.WRITE_EXERCISE" => "Running sessions",
                "android.permission.health.WRITE_DISTANCE" => "Distance",
                _ => "Heart rate"
            }).ToArray() : [];
        return new(available, Load().Enabled, missing);
    }

    internal static void Refresh() => Changed?.Invoke();
}
