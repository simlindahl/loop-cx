using System.Globalization;
using LoopCx.Features.ActiveRun;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.Settings;

public partial class SettingsPage
{
    [Parameter] public bool InitialHealth { get; set; }
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
    private bool healthRequest;
    private HealthConnectAccess health = new(false, false, []);
    private bool disposed;
    private IJSObjectReference? module;
    private ElementReference settingsSurface;
    private readonly string motionOwner = Guid.NewGuid().ToString("N");
    private string settingsVisit = Guid.NewGuid().ToString("N");
    private bool splitsEntrance;
    private AudioConfiguration audioConfiguration = new();
    private SplitPolicy? savedSplits;
    private SplitBasis splitBasis;
    private string splitInterval = "";
    private string? splitError;

    private string Title => page switch { "audio" => "Audio coaching", "splits" => "Splits", "connection" => "Heart-rate connection", "zones" => "Your heart-rate zones", "health" => "Health Connect", _ => "Settings" };
    private bool TimeSplits => splitBasis == SplitBasis.Time;
    private SplitPolicy? DraftSplits
    {
        get
        {
            if (!decimal.TryParse(splitInterval.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var interval))
            {
                return null;
            }

            if (TimeSplits)
            {
                return interval is >= 1 and <= 30 && interval % 1 == 0
                    ? new(1, SplitBasis.Time, (int)interval) : null;
            }

            return interval is >= 0.5m and <= 5 && interval % 0.5m == 0
                ? new(1, SplitBasis.Distance, (int)(interval * 1000)) : null;
        }
    }

    private void OpenAudio()
    {
        try { audioConfiguration = CorePreferences.Load().EffectiveAudio; Open("audio"); }
        catch (InvalidDataException problem) { error = problem.Message; }
    }
    private void CancelAudio() { Open("settings"); focusPending = "audio-settings"; }
    private void SaveAudio(AudioConfiguration configuration)
    {
        try { CorePreferences.Save(CorePreferences.Load() with { Audio = configuration }); Open("settings"); message = "Audio settings saved for new Core runs."; focusPending = "audio-settings"; }
        catch (Exception problem) when (problem is InvalidDataException or ArgumentException or IOException) { error = problem.Message; }
    }

    private void OpenSplits()
    {
        try
        {
            savedSplits = CorePreferences.Load().EffectiveSplits;
            SetSplitDraft(savedSplits);
            Open("splits");
        }
        catch (InvalidDataException problem)
        {
            error = problem.Message;
        }
    }

    private void ChangeSplitBasis(ChangeEventArgs change)
    {
        if (Enum.TryParse<SplitBasis>(change.Value as string, out var basis) && basis != splitBasis)
        {
            SetSplitDraft(basis == SplitBasis.Time ? SplitPolicy.DefaultTime : SplitPolicy.DefaultDistance);
        }
    }

    private void SetSplitDraft(SplitPolicy policy)
    {
        splitBasis = policy.Basis;
        splitInterval = (TimeSplits ? policy.Interval : policy.Interval / 1000m).ToString("0.#", CultureInfo.InvariantCulture);
        splitError = null;
    }

    private void SaveSplits()
    {
        if (DraftSplits is not { } policy)
        {
            return;
        }

        try
        {
            savedSplits = CorePreferences.Update(current => current with { Splits = policy }).EffectiveSplits;
            CancelSplits();
            message = "Split settings saved for new Core runs.";
        }
        catch (Exception problem) when (problem is InvalidDataException or ArgumentException or IOException or Java.Lang.RuntimeException)
        {
            splitError = problem.Message;
        }
    }

    private void CancelSplits()
    {
        Open("settings");
        focusPending = "splits-settings";
    }

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
        if (InitialHealth) page = "health";
        var saved = HeartRatePreferences.Load();
        configuration = saved.Configuration;
        error = saved.ConfigurationError;
        RunService.Changed += Refresh;
        HealthConnectSettings.Changed += Refresh;
        health = HealthConnectSettings.Access();
        try
        {
            savedSplits = CorePreferences.Load().EffectiveSplits;
        }
        catch (InvalidDataException problem)
        {
            error ??= problem.Message;
        }
    }

    private void Refresh() => _ = InvokeAsync(() => { health = HealthConnectSettings.Access(); StateHasChanged(); });
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
        if (page != destination)
        {
            splitsEntrance = (page, destination) is ("settings", "splits") or ("splits", "settings");
            if (module is { } current) _ = LeaveSplitsSurface(current, settingsVisit, splitsEntrance);
            settingsVisit = Guid.NewGuid().ToString("N");
        }
        page = destination;
        if (destination == "health") health = HealthConnectSettings.Access();
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
        if (page == "settings")
        {
            await OnTraining.InvokeAsync();
            return;
        }

        if (page == "audio") CancelAudio();
        else if (page == "splits") CancelSplits();
        else CancelZones();

        await InvokeAsync(StateHasChanged);
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
    public Task ShowHealth()
    {
        Open("health");
        return InvokeAsync(StateHasChanged);
    }

    private async Task EnableHealthConnect()
    {
        if (healthRequest || !HealthConnectSettings.Access().Available) return;
        healthRequest = true;
        error = null;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (disposed || page != "health") return;
                if (!HealthConnectSettings.Load().Enabled) HealthConnectSettings.SetEnabled(true);
                if (Platform.CurrentActivity is MainActivity activity) await activity.RequestHealthWriteAsync();
                else throw new InvalidOperationException("Android permission UI is unavailable. Try reopening the app.");
            });
            if (disposed) return;
            health = HealthConnectSettings.Access();
            message = health.Missing.Count == 0 ? "Write access is available. A run is exported only after acknowledgement."
                : "Write access needed. Your local recordings remain available.";
            HealthConnectScheduling.Resume();
        }
        catch (Exception problem) when (problem is IOException or InvalidOperationException or Java.Lang.RuntimeException)
        { if (!disposed) error = "Health Connect access could not be confirmed. Your local recordings are unchanged. Try again."; }
        finally { healthRequest = false; focusPending = "health-enable"; }
    }

    private void TurnOffHealthConnect()
    {
        try
        {
            HealthConnectSettings.SetEnabled(false);
            health = HealthConnectSettings.Access();
            message = "Automatic export is off. Existing local and Health Connect records are unchanged.";
            error = null;
            focusPending = "health-enable";
        }
        catch (IOException) { error = "The change could not be saved. Check the displayed status and try again."; }
    }

    private static Task ShowHealthPrivacy() => MainThread.InvokeOnMainThreadAsync(() =>
        (Platform.CurrentActivity ?? global::Android.App.Application.Context).StartActivity(new global::Android.Content.Intent(
            global::Android.App.Application.Context, typeof(HealthPrivacyActivity)).AddFlags(
                Platform.CurrentActivity is null ? global::Android.Content.ActivityFlags.NewTask : 0)));

    private static Task ReviewAccess() => MainThread.InvokeOnMainThreadAsync(() => AppInfo.Current.ShowSettingsUI());
    private async Task LeaveSplitsSurface(IJSObjectReference motion, string visit, bool animate)
    {
        try
        {
            await motion.InvokeVoidAsync("leaveSplitsSurface", settingsSurface, visit, animate);
        }
        catch (JSException) { } // Navigation does not depend on the outgoing effect.
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed || focusPending is null && !splitsEntrance) return;

        var visit = settingsVisit;
        var target = focusPending;
        var enter = splitsEntrance;
        focusPending = null;
        splitsEntrance = false;

        if (module is null)
        {
            var imported = await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
            if (disposed || module is not null) await imported.DisposeAsync();
            else module = imported;
        }
        if (disposed || settingsVisit != visit) return;

        var motion = module!;
        if (target is not null) await motion.InvokeVoidAsync("focus", target);
        if (!enter || disposed || settingsVisit != visit) return;
        try
        {
            await motion.InvokeVoidAsync("enterSplitsSurface", settingsSurface, visit);
        }
        catch (JSException) { } // Focus and navigation have already completed.
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        RunService.Changed -= Refresh;
        HealthConnectSettings.Changed -= Refresh;
        if (module is { } motion)
        {
            module = null;
            await motion.InvokeVoidAsync("disposeSplitsSurface", settingsSurface, motionOwner);
            await motion.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
