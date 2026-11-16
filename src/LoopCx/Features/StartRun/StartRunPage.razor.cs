using System.Globalization;
using LoopCx.Features.ActiveRun;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;

namespace LoopCx.Features.StartRun;

public partial class StartRunPage
{
    private CorePreferencesState statePreferences = CorePreferencesState.Default;
    private bool loadFailed;
    private bool motionPending = true;
    private bool systemMotionPending = true;
    private bool resultPending;
    private string? lastMotionKey;
    private bool managing;
    private bool showSettings;
    private bool showHistory;
    private bool showGhostCatalogue;
    private bool pageVisible = true, visibilityBound, disposed;
    private Guid ghostCatalogueKey, ghostEndOwner;
    private Guid? ghostManualFocusOwner;
    private LoopCx.Features.GhostRun.RouteKey? ghostInitialRoute;
    private LoopCx.Features.GhostRun.Ui.GhostRoutesPage? ghostCatalogue;
    private Guid? historyInitialRun;
    private LoopCx.Features.History.HistoryPage? historyPage;
    private LoopCx.Features.Settings.SettingsPage? settingsPage;
    private bool initialHealthSettings;
    private Guid? healthSettingsReturnRun;
    private bool managePending;
    private Guid? editingShortcutId;
    private string orderFeedback = "";
    private string? error;
    private string? dialogMode;
    private string? dialogError;
    private string opener = "core-settings";
    private bool openPending;
    private string? focusPending;
    private IJSObjectReference? module;
    private DotNetObjectReference<StartRunPage>? reference;
    private CoreStartRequest draftRequest = new("C1.2");
    private string draftProtocol = "C1.2";
    private string draftCount = "5";
    private string draftDuration = "45";
    private CoreTargetMode targetMode;
    private string draftWork = "";
    private string draftRecovery = "";
    private string draftName = "";
    private string draftWarmup = "4";
    private string draftCooldown = "3";
    private bool warmupEnabled;
    private bool cooldownEnabled;
    private AudioConfiguration draftAudio = new();
    private bool draftAudioInherited;

    private void OpenShortcutAudio(CoreShortcut shortcut)
    {
        try
        {
            editingShortcutId = shortcut.Id;
            draftName = shortcut.Name;
            draftAudioInherited = shortcut.Request.Audio is null;
            draftAudio = shortcut.Request.Audio ?? CorePreferences.Load().EffectiveAudio;
            Open("audio", "audio-" + shortcut.Id);
        }
        catch (InvalidDataException problem) { error = problem.Message; }
    }
    private async Task SaveShortcutAudio(AudioConfiguration configuration)
    {
        if (Persist(current =>
        {
            if (!current.Shortcuts.Any(item => item.Id == editingShortcutId)) throw new ArgumentException("This shortcut no longer exists.");

            return current with
            {
                Shortcuts = [.. current.Shortcuts.Select(item => item.Id == editingShortcutId
                ? item with { Request = item.Request with { Audio = configuration } } : item)]
            };
        }))
        {
            await CloseDialog();
        }
        else focusPending = "audio-interval";
    }

    private string MotionSurface => showGhostCatalogue ? "ghost-catalogue" : showHistory ? "history" : showSettings ? "settings" : managing ? "manage" : RunService.Snapshot is null ? "training" : "run";
    private string MotionKey => $"{MotionSurface}:{RunService.Snapshot?.Id}:{RunService.Snapshot?.Phase}:{RunService.Snapshot?.Ghost?.PauseReason ?? RunService.Snapshot?.Motion?.PauseReason}";

    private CoreStartRequest SelectedC1 => statePreferences.SelectedC1;
    private CoreProtocol SelectedProtocol => CoreProtocols.Find(statePreferences.SelectedProtocol);
    private CoreProtocol DraftProtocol => CoreProtocols.Find(draftRequest.ProtocolId);
    private int CountValue => int.TryParse(draftCount, out var count) ? count : -1;
    private bool ValidCount => CountValue >= DraftProtocol.MinimumCount && CountValue <= DraftProtocol.MaximumCount;
    private string DialogTitle => dialogMode switch
    {
        "end-ghost" => "End this attempt?",
        "audio" => "Audio coaching",
        "variant" => "Choose C1",
        "target" => $"Personalise · {draftRequest.ProtocolId}",
        "duration" => "Choose C2 duration",
        "settings" => "Warm-up and cool-down",
        "shortcut" => "Save a shortcut",
        "edit" => "Edit shortcut",
        "delete" => "Delete shortcut?",
        _ => "Workout options"
    };
    protected override void OnInitialized()
    {
        try { statePreferences = CorePreferences.Load(); }
        catch (InvalidDataException problem) { error = problem.Message; loadFailed = true; }

        RunService.Changed += Refresh;
        AndroidMotionPreference.Changed += RefreshSystemMotion;
        RunNotificationEntry.Changed += OpenNotificationRun;
        HealthConnectSettings.OpenRequested += OpenHealthSettings;
        OpenHealthSettings();
        OpenNotificationRun();
    }

    private void OpenHealthSettings() => _ = InvokeAsync(async () =>
    {
        if (!HealthConnectSettings.ConsumeOpen()) return;
        if (settingsPage is not null) { await settingsPage.ShowHealth(); return; }
        if (dialogMode is not null) await CloseDialog();
        showGhostCatalogue = false;
        showHistory = false;
        historyPage = null;
        showSettings = false;
        settingsPage = null;
        OpenSettingsDestinationCore(true);
        StateHasChanged();
    });

    private void OpenNotificationRun() => _ = InvokeAsync(async () =>
    {
        if (!RunNotificationEntry.Consume(RunService.Snapshot?.Id)) return;
        if (dialogMode is not null) await CloseDialog();
        showGhostCatalogue = false;
        showHistory = false;
        historyPage = null;
        showSettings = false;
        settingsPage = null;
        managing = false;
        CoreDialogNavigation.Close = null;
        ReloadPreferences();
        focusPending = "phase-heading";
        StateHasChanged();
    });

    private void Refresh() => _ = InvokeAsync(async () =>
    {
        if (dialogMode == "end-ghost" && (RunService.Snapshot?.Id != ghostEndOwner || RunService.Snapshot.Phase == "Finished"))
        {
            await CloseDialog();
            focusPending = "phase-heading";
        }
        motionPending = true;
        StateHasChanged();
    });
    private void RefreshSystemMotion() => _ = InvokeAsync(() =>
    {
        systemMotionPending = true;
        StateHasChanged();
    });
    private static bool IsAutomatic(RunSnapshot state) => state.Motion?.PauseReason == PauseReason.Automatic;
    private static string Clock(long seconds) => $"{seconds / 60}:{seconds % 60:00}";
    private static string PhaseLabel(string phase) => phase switch { "Warmup" => "Warm-up", "Cooldown" => "Cool-down", _ => phase };
    private static string Duration(int seconds) => seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds} sec";
    private static string TimeLabel(TimeSpan time) => time.Seconds == 0 ? $"{(int)time.TotalMinutes} min" : $"{(int)time.TotalMinutes}:{time.Seconds:00} min";
    private string Times(CoreStartRequest request)
    {
        var resolved = CoreTemplate.Resolve(request, statePreferences.WarmupCooldown);
        var main = resolved.Session.Plan.Where(static phase => phase.Kind is RunPhase.Work or RunPhase.Recovery).Aggregate(TimeSpan.Zero, static (sum, phase) => sum + phase.Duration);
        var total = resolved.Session.Plan.Aggregate(TimeSpan.Zero, static (sum, phase) => sum + phase.Duration);

        return $"{TimeLabel(main)} main · {TimeLabel(total)} total";
    }

    private string WarmupCooldown(CoreStartRequest request) => request.ProtocolId == "C2" ? "No warm-up or cool-down."
        : $"Warm-up: {(statePreferences.WarmupCooldown.WarmupMinutes == 0 ? "Off" : statePreferences.WarmupCooldown.WarmupMinutes + " min")} · Cool-down: {(statePreferences.WarmupCooldown.CooldownMinutes == 0 ? "Off" : statePreferences.WarmupCooldown.CooldownMinutes + " min")}";

    private static string Dose(CoreStartRequest request) => CorePresentation.Dose(request);

    private static string TargetLabel(CoreTargets? target, bool continuous = false)
    {
        return target is null || target.Mode == CoreTargetMode.None ? (continuous ? "Personal zone target not set" : "Target zone not set")
        : $"{(continuous ? "Continuous target" : "Work target")}: {(target.Mode == CoreTargetMode.Pace ? Clock(target.Work!.Value) + " min/km" : "Zone " + target.Work)}"
            + (target.Recovery is { } recovery ? $" · Recovery: {(target.Mode == CoreTargetMode.Pace ? Clock(recovery) + " min/km" : "Zone " + recovery)}" : "");
    }

    private static string StatisticTime(MeasuredStatistics? value) => value is null ? "Unavailable" : Clock((long)value.ActiveTime.TotalSeconds);
    private static string StatisticDistance(MeasuredStatistics? value) => value is null ? "Unavailable" : value.MeasuredMetres is > 0 || value.KnownGpsActiveTime > TimeSpan.Zero
        ? (value.MeasuredMetres / 1000).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "Unavailable";
    private static string Pace(double? seconds) => seconds is > 0 && double.IsFinite(seconds.Value) ? Clock((long)Math.Round(seconds.Value)) : "Unavailable";
    private static string HeartRate(double? bpm) => bpm is > 0 && double.IsFinite(bpm.Value) ? Math.Round(bpm.Value).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "Unavailable";
    private static string CoverageLabel(MeasuredStatistics? value) => value is null ? "Unavailable"
        : $"GPS {Clock((long)value.KnownGpsActiveTime.TotalSeconds)} known, {Clock((long)value.UnknownGpsActiveTime.TotalSeconds)} unknown; HR {Clock((long)value.KnownHeartRateActiveTime.TotalSeconds)} known, {Clock((long)value.UnknownHeartRateActiveTime.TotalSeconds)} unknown (min:sec).";

    private static string PhaseTargetLabel(RunSnapshot state)
    {
        if (state.Phase is not ("Work" or "Recovery")) return "";
        var target = state.FrozenTemplate?.Targets;
        var value = state.Phase == "Recovery" ? target?.Recovery : target?.Work;
        return target is null || target.Mode == CoreTargetMode.None || value is null
            ? "Target not set · intended effort is not a measurement"
            : target.Mode == CoreTargetMode.Pace ? $"Target pace {Clock(value.Value)} min/km · intended effort"
            : $"Target Zone {value} · intended effort, not a measurement";
    }

    private bool VisibleGhostOwner(Guid owner) => !disposed && pageVisible && !showSettings && !showHistory && !showGhostCatalogue
        && dialogMode is null && RunService.Snapshot is { Ghost: not null, Phase: not "Finished" } current && current.Id == owner;

    private void PauseGhostManually(Guid owner)
    {
        if (!VisibleGhostOwner(owner) || RunService.Snapshot?.Ghost?.PauseReason != PauseReason.Automatic) return;

        ghostManualFocusOwner = owner;
        focusPending = $"ghost-training-toggle-{owner}";
        RunService.CommandGhost("pause", owner);
    }

    private static void TogglePause() => RunService.Command(RunService.Snapshot?.Paused == true ? "resume" : "pause");

    private void BackToTraining()
    {
        focusPending = "start-heading";
        RunService.DismissSaved();
    }

    private async Task OpenHistory(Guid? runId)
    {
        if (showHistory && runId is null)
        {
            if (historyPage is not null) await historyPage.ShowList();
            return;
        }
        showGhostCatalogue = false;
        showSettings = false;
        settingsPage = null;
        historyInitialRun = runId;
        showHistory = true;
        CoreDialogNavigation.Close = () => _ = InvokeAsync(() => historyPage?.GoBack() ?? Task.CompletedTask);
    }

    private void OpenGhostCatalogue(LoopCx.Features.GhostRun.RouteKey? route)
    {
        showHistory = false; historyPage = null;
        showSettings = false; settingsPage = null;
        showGhostCatalogue = true; ghostInitialRoute = route; ghostCatalogueKey = Guid.NewGuid();
        CoreDialogNavigation.Close = () => _ = InvokeAsync(() => ghostCatalogue?.GoBack() ?? Task.CompletedTask);
    }
    private void CloseGhostCatalogue()
    {
        showGhostCatalogue = false; ghostCatalogue = null; ghostInitialRoute = null;
        ReloadPreferences();
        CoreDialogNavigation.Close = null;
        focusPending = RunService.Snapshot is null ? "ghost-open" : "phase-heading";
    }
    private void OpenGhostEnd(Guid expected)
    {
        if (RunService.Snapshot is not { Ghost: not null, Phase: not "Finished" } owner || owner.Id != expected) return;
        ghostEndOwner = owner.Id;
        Open("end-ghost", "ghost-end-attempt");
    }
    private async Task ConfirmGhostEnd()
    {
        if (RunService.Snapshot is { Ghost: not null, Phase: not "Finished" } owner && owner.Id == ghostEndOwner)
            RunService.CommandGhost("end-ghost", ghostEndOwner);
        await CloseDialog();
    }
    [JSInvokable]
    public Task PageVisibilityChanged(bool visible)
    {
        if (!disposed) { pageVisible = visible; StateHasChanged(); }
        return Task.CompletedTask;
    }

    private void ShowTraining()
    {
        ReloadPreferences();
        showGhostCatalogue = false;
        showHistory = false;
        historyPage = null;
        showSettings = false;
        settingsPage = null;
        managing = false;
        CoreDialogNavigation.Close = null;
        RunService.DismissSaved(); // Guarded: never dismisses an active or failed-save owner.
        focusPending = RunService.Snapshot is null ? "start-heading" : "phase-heading";
    }

    private async Task Start(CoreStartRequest request)
    {
        try
        {
            CoreTemplate.Resolve(request, CorePreferences.Load().WarmupCooldown);
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (OperatingSystem.IsAndroidVersionAtLeast(33)) await Permissions.RequestAsync<NotificationPermission>();

            error = null;
            RunService.Start(request);
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidDataException)
        {
            error = problem.Message;
        }
    }

    private void Open(string mode, string trigger)
    {
        dialogMode = mode;
        dialogError = null;
        opener = trigger;
        openPending = true;
        CoreDialogNavigation.Close = () => _ = InvokeAsync(CloseDialog);
    }

    private void OpenVariant()
    {
        draftProtocol = statePreferences.SelectedProtocol;
        Open("variant", "change-c1");
    }

    private void OpenDuration()
    {
        draftDuration = (statePreferences.C2Selection.ContinuousMinutes ?? 45).ToString(CultureInfo.InvariantCulture);
        Open("duration", "change-c2");
    }

    private void OpenTarget(CoreStartRequest request, string trigger)
    {
        editingShortcutId = null;
        draftRequest = request;
        draftCount = (request.ProtocolId == "C2" ? 0 : request.Repetitions ?? CoreProtocols.Find(request.ProtocolId).DefaultCount).ToString(CultureInfo.InvariantCulture);
        var target = request.Targets ?? new CoreTargets();
        targetMode = target.Mode;
        draftWork = TargetValue(target.Work, target.Mode);
        draftRecovery = TargetValue(target.Recovery, target.Mode);
        Open("target", trigger);
    }

    private void OpenShortcut(CoreStartRequest request, string trigger)
    {
        draftRequest = request;
        draftName = "";
        Open("shortcut", trigger);
    }

    private void OpenSettingsDestination() => OpenSettingsDestinationCore(false);

    private void OpenSettingsDestinationCore(bool health)
    {
        initialHealthSettings = health;
        showGhostCatalogue = false;
        showHistory = false;
        historyPage = null;
        showSettings = true;
        CoreDialogNavigation.Close = () => _ = InvokeAsync(() => settingsPage?.GoBack() ?? Task.CompletedTask);
    }
    private void OpenHistoryHealthSettings(Guid runId)
    {
        healthSettingsReturnRun = runId;
        OpenSettingsDestinationCore(true);
    }

    private async Task CloseSettingsDestination()
    {
        initialHealthSettings = false;
        ReloadPreferences();
        if (healthSettingsReturnRun is { } runId)
        {
            healthSettingsReturnRun = null;
            await OpenHistory(runId);
            return;
        }
        showSettings = false;
        settingsPage = null;
        CoreDialogNavigation.Close = null;
        focusPending = RunService.Snapshot is null ? "core-settings" : "active-settings";
    }

    private void ReloadPreferences()
    {
        try { statePreferences = CorePreferences.Load(); loadFailed = false; }
        catch (InvalidDataException problem) { error = problem.Message; loadFailed = true; }
    }

    private void OpenSettings()
    {
        ReloadPreferences();
        var settings = statePreferences.WarmupCooldown;
        warmupEnabled = settings.WarmupMinutes > 0;
        cooldownEnabled = settings.CooldownMinutes > 0;
        draftWarmup = (warmupEnabled ? settings.WarmupMinutes : 4).ToString(CultureInfo.InvariantCulture);
        draftCooldown = (cooldownEnabled ? settings.CooldownMinutes : 3).ToString(CultureInfo.InvariantCulture);
        Open("settings", showSettings ? "warmup-cooldown-settings" : "core-settings");
    }

    private static string TargetValue(int? value, CoreTargetMode mode) => value is null ? ""
        : mode == CoreTargetMode.Pace ? Clock(value.Value) : value.Value.ToString(CultureInfo.InvariantCulture);
    private void ChangeCount(int delta) => draftCount = (CountValue + delta).ToString(CultureInfo.InvariantCulture);
    private async Task ChangeTargetMode(ChangeEventArgs change)
    {
        if (module is not null) await module.InvokeVoidAsync("leaveDisclosure");
        motionPending = true;
        targetMode = Enum.Parse<CoreTargetMode>(change.Value!.ToString()!);
        draftWork = draftRecovery = "";
    }

    private bool Persist(Func<CorePreferencesState, CorePreferencesState> change)
    {
        try
        {
            statePreferences = CorePreferences.Update(change);
            resultPending = true;

            return true;
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidDataException or InvalidOperationException or IOException)
        {
            if (problem is InvalidDataException) loadFailed = true;
            var message = problem is ArgumentException or InvalidDataException ? problem.Message : "Your changes could not be saved. Try again.";
            if (dialogMode is null) error = message; else dialogError = message;
            return false;
        }
    }

    private async Task SaveVariant()
    {
        var protocol = CoreProtocols.Find(draftProtocol);
        if (Persist(current => current with
        {
            SelectedProtocol = protocol.Id,
            C1Selections = [.. current.C1Selections.Select(request => request.ProtocolId == protocol.Id
            ? request with { Repetitions = protocol.DefaultCount } : request)]
        }))
        {
            await CloseDialog();
        }
    }

    private async Task SaveDuration()
    {
        if (!int.TryParse(draftDuration, out var duration)) { Fail("Choose a whole duration from 20 to 120 minutes.", "c2-duration"); return; }
        if (Persist(current => current with { C2Selection = current.C2Selection with { ContinuousMinutes = duration } })) await CloseDialog();
        else focusPending = "c2-duration";
    }

    private static int? ParseTarget(string value, CoreTargetMode mode, bool optional)
    {
        if (string.IsNullOrWhiteSpace(value) && optional) return null;
        if (mode == CoreTargetMode.HeartRateZone && int.TryParse(value, out var zone) && zone is >= 1 and <= 5) return zone;
        var parts = value.Trim().Split(':');
        if (mode == CoreTargetMode.Pace && parts.Length == 2 && int.TryParse(parts[0], out var minutes)
            && minutes >= 0 && parts[1].Length == 2 && int.TryParse(parts[1], out var seconds) && seconds is >= 0 and <= 59
            && minutes <= (int.MaxValue - seconds) / 60 && minutes * 60 + seconds > 0) return minutes * 60 + seconds;

        throw new ArgumentException("Set a work target. Use a positive pace as min:ss or choose a zone from 1 to 5.");
    }

    private async Task SaveTarget()
    {
        try
        {
            if (editingShortcutId is not null && string.IsNullOrWhiteSpace(draftName)) { Fail("Enter a shortcut name.", "edit-name"); return; }
            if (draftRequest.ProtocolId != "C2" && !ValidCount) { Fail("Use a whole repetition count within the displayed limits.", "rep-count"); return; }
            var targets = targetMode == CoreTargetMode.None ? new CoreTargets() : new(targetMode,
                ParseTarget(draftWork, targetMode, false), draftRequest.ProtocolId == "C2" ? null : ParseTarget(draftRecovery, targetMode, true));
            var request = draftRequest with { Repetitions = draftRequest.ProtocolId == "C2" ? null : CountValue, Targets = targets };
            if (editingShortcutId is not null && request.ProtocolId == "C2")
            {
                if (!int.TryParse(draftDuration, out var duration)) { Fail("Choose a whole duration from 20 to 120 minutes.", "edit-c2-duration"); return; }
                request = request with { ContinuousMinutes = duration };
            }
            CoreStartRequest ApplyTarget(CoreStartRequest saved)
            {
                return saved with
                {
                    Repetitions = request.Repetitions,
                    Targets = request.Targets,
                    ContinuousMinutes = editingShortcutId is not null && request.ProtocolId == "C2" ? request.ContinuousMinutes : saved.ContinuousMinutes
                };
            }

            if (Persist(current =>
            {
                if (editingShortcutId is { } editingId)
                {
                    if (!current.Shortcuts.Any(item => item.Id == editingId)) throw new ArgumentException("This shortcut no longer exists.");

                    return current with
                    {
                        Shortcuts = [.. current.Shortcuts.Select(item => item.Id == editingId
                        ? item with { Name = draftName.Trim(), Request = ApplyTarget(item.Request) } : item)]
                    };
                }

                return request.ProtocolId == "C2" ? current with { C2Selection = ApplyTarget(current.C2Selection) }
                    : current with { C1Selections = [.. current.C1Selections.Select(value => value.ProtocolId == request.ProtocolId ? ApplyTarget(value) : value)] };
            }))
            {
                await CloseDialog();
            }
        }
        catch (ArgumentException problem) { Fail(problem.Message, "target-work"); }
    }

    private async Task SaveSettings()
    {
        if (warmupEnabled && (!int.TryParse(draftWarmup, out var warmup) || warmup is < 1 or > 15)) { Fail("Warm-up must be Off or 1–15 whole minutes.", "c1-warmup"); return; }
        if (cooldownEnabled && (!int.TryParse(draftCooldown, out var cooldown) || cooldown is < 1 or > 15)) { Fail("Cool-down must be Off or 1–15 whole minutes.", "c1-cooldown"); return; }
        var settings = new C1WarmupCooldownSettings(warmupEnabled ? int.Parse(draftWarmup, CultureInfo.InvariantCulture) : 0,
            cooldownEnabled ? int.Parse(draftCooldown, CultureInfo.InvariantCulture) : 0);
        if (Persist(current => current with { WarmupCooldown = settings })) await CloseDialog();
    }

    private async Task SaveShortcut()
    {
        if (string.IsNullOrWhiteSpace(draftName)) { Fail("Enter a shortcut name.", "shortcut-name"); return; }
        var shortcutId = Guid.NewGuid();
        if (Persist(current => current with
        {
            Shortcuts =
            [
                .. current.Shortcuts,
                new CoreShortcut(shortcutId, draftName.Trim(),
                    draftRequest with { Audio = current.EffectiveAudio }),
            ]
        }))
        {
            await CloseDialog();
        }
    }

    private async Task OpenManage()
    {
        if (module is not null) await module.InvokeVoidAsync("leaveSurface");
        managing = true;
        managePending = true;
        focusPending = "manage-heading";
        CoreDialogNavigation.Close = () => _ = InvokeAsync(CloseManage);
    }

    private async Task CloseManage()
    {
        if (module is not null) await module.InvokeVoidAsync("leaveSurface");
        managing = false;
        CoreDialogNavigation.Close = null;
        focusPending = statePreferences.Shortcuts.Count > 0 ? "manage-shortcuts" : "start-heading";
        await InvokeAsync(StateHasChanged);
    }

    private void OpenEdit(CoreShortcut shortcut)
    {
        OpenTarget(shortcut.Request, $"edit-{shortcut.Id}");
        editingShortcutId = shortcut.Id;
        draftName = shortcut.Name;
        draftDuration = (shortcut.Request.ContinuousMinutes ?? 45).ToString(CultureInfo.InvariantCulture);
        dialogMode = "edit";
    }

    private void OpenDelete(CoreShortcut shortcut)
    {
        editingShortcutId = shortcut.Id;
        draftName = shortcut.Name;
        Open("delete", $"delete-{shortcut.Id}");
    }

    private async Task DeleteShortcut()
    {
        var shortcuts = statePreferences.Shortcuts.ToList();
        var index = shortcuts.FindIndex(item => item.Id == editingShortcutId);
        if (index < 0) return;

        shortcuts.RemoveAt(index);
        if (!Persist(current => current with { Shortcuts = [.. current.Shortcuts.Where(item => item.Id != editingShortcutId)] })) return;

        await CloseDialog();
        var remaining = statePreferences.Shortcuts;
        focusPending = remaining.Count > 0 ? $"edit-{remaining[Math.Min(index, remaining.Count - 1)].Id}" : "manage-heading";
    }

    [JSInvokable]
    public async Task MoveShortcut(Guid id, int offset)
    {
        var shortcuts = statePreferences.Shortcuts.ToList();
        var from = shortcuts.FindIndex(item => item.Id == id);
        var to = from + offset;
        if (from < 0 || offset is not (-1 or 1) || to < 0 || to >= shortcuts.Count) return;

        var item = shortcuts[from];
        shortcuts.RemoveAt(from);
        shortcuts.Insert(to, item);
        await CommitOrder(shortcuts, item, to);
    }

    [JSInvokable]
    public async Task DropShortcut(Guid id, Guid targetId, bool after)
    {
        if (id == targetId) return;

        var shortcuts = statePreferences.Shortcuts.ToList();
        var from = shortcuts.FindIndex(item => item.Id == id);
        if (from < 0 || !shortcuts.Any(item => item.Id == targetId)) return;

        var item = shortcuts[from];
        shortcuts.RemoveAt(from);
        var to = shortcuts.FindIndex(value => value.Id == targetId) + (after ? 1 : 0);
        shortcuts.Insert(to, item);
        await CommitOrder(shortcuts, item, to);
    }

    private async Task CommitOrder(List<CoreShortcut> shortcuts, CoreShortcut item, int index)
    {
        if (statePreferences.Shortcuts.Select(static value => value.Id).SequenceEqual(shortcuts.Select(static value => value.Id))) return;
        if (!Persist(current =>
        {
            var remaining = current.Shortcuts.ToList();
            var moving = remaining.SingleOrDefault(value => value.Id == item.Id)
                ?? throw new ArgumentException("This shortcut no longer exists.");
            _ = remaining.Remove(moving);
            var nextId = index + 1 < shortcuts.Count ? shortcuts[index + 1].Id : (Guid?)null;
            var destination = nextId is { } next ? remaining.FindIndex(value => value.Id == next) : remaining.Count;
            if (destination < 0) throw new ArgumentException("The shortcut order changed. Reopen Manage shortcuts and try again.");
            remaining.Insert(destination, moving);

            return current with { Shortcuts = remaining };
        }))
        {
            return;
        }

        orderFeedback = $"{item.Name} moved to position {index + 1} of {shortcuts.Count}.";
        focusPending = $"handle-{item.Id}";
        await InvokeAsync(StateHasChanged);
    }

    private void Fail(string message, string focus)
    {
        dialogError = message;
        focusPending = focus;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        motionPending |= lastMotionKey != MotionKey;
        if (!openPending && focusPending is null && !managePending && !motionPending && !resultPending && !systemMotionPending) return;
        try
        {
            module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
            reference ??= DotNetObjectReference.Create(this);
            if (!visibilityBound) { visibilityBound = true; pageVisible = await module.InvokeAsync<bool>("bindVisibility", reference); }
            if (systemMotionPending)
            {
                systemMotionPending = false;
                await module.InvokeVoidAsync("setSystemMotion", AndroidMotionPreference.Current);
            }
            if (managePending) { managePending = false; await module.InvokeVoidAsync("manage", reference); }
            if (openPending) { openPending = false; await module.InvokeVoidAsync("open", opener, reference); }
            if (focusPending is { } focus)
            {
                focusPending = null;
                var requestedOwner = ghostManualFocusOwner;
                ghostManualFocusOwner = null;
                if (!focus.StartsWith("ghost-training-toggle-", StringComparison.Ordinal)
                    || requestedOwner is { } owner && focus == $"ghost-training-toggle-{owner}" && VisibleGhostOwner(owner))
                    await module.InvokeVoidAsync("focus", focus);
            }
            if (motionPending || resultPending)
            {
                motionPending = false;
                lastMotionKey = MotionKey;
                var saved = resultPending;
                resultPending = false;
                await module.InvokeVoidAsync("syncMotion", saved);
            }
        }
        catch (JSException)
        {
            dialogMode = null; CoreDialogNavigation.Close = null; error = "This control could not open. Try reopening the view.";
            await InvokeAsync(StateHasChanged);
        }
    }
    [JSInvokable]
    public async Task CloseDialog()
    {
        CoreDialogNavigation.Close = managing ? () => _ = InvokeAsync(CloseManage)
            : showGhostCatalogue ? () => _ = InvokeAsync(() => ghostCatalogue?.GoBack() ?? Task.CompletedTask)
            : showHistory ? () => _ = InvokeAsync(() => historyPage?.GoBack() ?? Task.CompletedTask)
            : showSettings ? () => _ = InvokeAsync(() => settingsPage?.GoBack() ?? Task.CompletedTask) : null;
        if (module is not null) await module.InvokeVoidAsync("close");
        dialogMode = null;
        dialogError = null;
        focusPending = null;
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        RunService.Changed -= Refresh;
        RunNotificationEntry.Changed -= OpenNotificationRun;
        HealthConnectSettings.OpenRequested -= OpenHealthSettings;
        AndroidMotionPreference.Changed -= RefreshSystemMotion;
        CoreDialogNavigation.Close = null;
        reference?.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose");
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        GC.SuppressFinalize(this);
    }
}

internal static class CoreDialogNavigation
{
    internal static Action? Close { get; set; }
}
