using System.Globalization;
using LoopCx.Features.ActiveRun;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.Settings;

public partial class SettingsPage
{
    [Parameter] public EventCallback OnTraining { get; set; }
    [Parameter] public EventCallback OnWarmupCooldown { get; set; }
    private HeartRateConfiguration configuration = HeartRateConfiguration.Empty;
    private string page = "settings";
    private string resting = "";
    private string maximum = "";
    private string? error;
    private string? message;
    private string? invalidField;
    private string? focusPending = "settings-heading";
    private bool selecting;
    private bool disposed;
    private IJSObjectReference? module;
    private string Title => page switch { "connection" => "Heart-rate connection", "zones" => "Your heart-rate zones", _ => "Settings" };
    private HeartRateState? LiveSource => RunService.Snapshot is { Phase: not "Finished", HeartRate: { } hr }
        && configuration.DeviceAddress is not null && string.Equals(configuration.DeviceAddress, hr.Configuration.DeviceAddress, StringComparison.OrdinalIgnoreCase) ? hr : null;
    private bool DifferentActiveSource => RunService.Snapshot is { Phase: not "Finished", HeartRate: { } hr }
        && !string.Equals(configuration.DeviceAddress, hr.Configuration.DeviceAddress, StringComparison.OrdinalIgnoreCase);
    private string ConnectionStatus => LiveSource is { } live ? QualityLabel(live.Quality)
        : configuration.DeviceAddress is null ? "Not connected" : "Selected · next run";
    private string ProfileStatus => configuration.ReserveProfile is { } profile ? $"HRR · {profile.RestingBpm}/{profile.MaximumBpm} bpm"
        : configuration.Zones.Count > 0 ? "Existing personal boundaries" : "Not configured";
    private IReadOnlyList<HeartRateZone>? Preview
    {
        get
        {
            if (!int.TryParse(resting, NumberStyles.None, CultureInfo.InvariantCulture, out var rest)
                || !int.TryParse(maximum, NumberStyles.None, CultureInfo.InvariantCulture, out var max)) return null;
            try { return HeartRateReserveProfile.Create(rest, max).ResolveZones(); }
            catch (ArgumentException) { return null; }
        }
    }

    protected override void OnInitialized()
    {
        var saved = HeartRatePreferences.Load();
        configuration = saved.Configuration;
        error = saved.ConfigurationError;
        RunService.Changed += Refresh;
    }

    private void Refresh() => _ = InvokeAsync(StateHasChanged);
    private static string QualityLabel(string quality) => quality switch
    {
        "valid" => "Connected · fresh reading",
        "connected-awaiting-reading" => "Connected · waiting for heart rate",
        "connecting" => "Connecting to selected sensor",
        "stale" or "contact-or-payload-invalid" => "Signal missing · heart rate unavailable",
        "permission-denied" => "Bluetooth access needed",
        _ => "Heart rate unavailable"
    };
    private void Open(string destination)
    {
        page = destination;
        message = null;
        invalidField = null;
        focusPending = "settings-heading";
    }
    private void OpenZones()
    {
        resting = configuration.ReserveProfile?.RestingBpm.ToString(CultureInfo.InvariantCulture) ?? "";
        maximum = configuration.ReserveProfile?.MaximumBpm.ToString(CultureInfo.InvariantCulture) ?? "";
        Open("zones");
    }
    public async Task GoBack()
    {
        if (page == "settings") await OnTraining.InvokeAsync();
        else CancelZones();
    }
    private void CancelZones()
    {
        error = HeartRatePreferences.Load().ConfigurationError;
        Open("settings");
    }
    private void ClearZoneError() { error = null; invalidField = null; }

    private void SaveZones()
    {
        invalidField = null;
        if (!int.TryParse(resting, NumberStyles.None, CultureInfo.InvariantCulture, out var rest) || rest <= 0)
        { error = "Use a positive whole resting heart rate."; invalidField = "resting-heart-rate"; focusPending = invalidField; return; }
        if (!int.TryParse(maximum, NumberStyles.None, CultureInfo.InvariantCulture, out var max))
        { error = "Use a positive whole maximum heart rate greater than resting."; invalidField = "maximum-heart-rate"; focusPending = invalidField; return; }
        try
        {
            var profile = HeartRateReserveProfile.Create(rest, max);
            var updated = HeartRatePreferences.Load().Configuration with { ReserveProfile = profile, Zones = profile.ResolveZones() };
            if (Save(updated)) { Open("settings"); message = "Zones saved for new runs."; }
        }
        catch (ArgumentException problem)
        { error = problem.Message; invalidField = "maximum-heart-rate"; focusPending = invalidField; }
    }
    private bool Save(HeartRateConfiguration updated)
    {
        try
        {
            HeartRatePreferences.Save(updated);
            configuration = updated;
            error = null;
            return true;
        }
        catch (Exception problem) when (problem is ArgumentException or IOException or InvalidOperationException or Java.Lang.RuntimeException)
        { error = "Settings could not be saved. Your previous values are unchanged. " + problem.Message; return false; }
    }
    private async Task SelectSensor()
    {
        if (selecting) return;
        selecting = true;
        try
        {
            var selected = await MainThread.InvokeOnMainThreadAsync(() =>
                Platform.CurrentActivity is MainActivity activity ? activity.SelectHeartRateAsync()
                    : Task.FromResult(new HeartRateSelection(null, null, "Android selection is unavailable. Try reopening the app.")));
            if (disposed) return;
            if (selected.Address is null) { error = selected.Status; return; }
            var updated = HeartRatePreferences.Load().Configuration with { DeviceAddress = selected.Address, DeviceName = selected.Name };
            if (Save(updated)) message = selected.Status;
        }
        finally { selecting = false; }
    }
    private void RemoveSelection()
    {
        var updated = HeartRatePreferences.Load().Configuration with { DeviceAddress = null, DeviceName = null };
        if (Save(updated)) message = "Selection removed for new runs. Any current run keeps its original sensor.";
    }
    private static Task ReviewAccess() => MainThread.InvokeOnMainThreadAsync(() => AppInfo.Current.ShowSettingsUI());
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (focusPending is not { } target) return;
        focusPending = null;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
        await module.InvokeVoidAsync("focus", target);
    }
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        RunService.Changed -= Refresh;
        if (module is not null) await module.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
