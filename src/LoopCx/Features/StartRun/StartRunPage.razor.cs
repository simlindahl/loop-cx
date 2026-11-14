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
    private bool managing;
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

    private CoreStartRequest SelectedC1 => statePreferences.SelectedC1;
    private CoreProtocol SelectedProtocol => CoreProtocols.Find(statePreferences.SelectedProtocol);
    private CoreProtocol DraftProtocol => CoreProtocols.Find(draftRequest.ProtocolId);
    private int CountValue => int.TryParse(draftCount, out var count) ? count : -1;
    private bool ValidCount => CountValue >= DraftProtocol.MinimumCount && CountValue <= DraftProtocol.MaximumCount;
    private string DialogTitle => dialogMode switch
    {
        "variant" => "Choose C1",
        "target" => $"Personalise · {draftRequest.ProtocolId}",
        "duration" => "Choose C2 duration",
        "settings" => "Warm-up and cool-down",
        "shortcut" => "Save a shortcut",
        "edit" => "Edit shortcut",
        "delete" => "Delete shortcut?",
        _ => "What does RPE mean?"
    };
    private string RpeCue => draftProtocol switch
    {
        "C1.1" => "C1.1: Work 10: maximal, all-out sprinting. Recovery 2: easy.",
        "C1.2" => "C1.2: Work 7: hard but repeatable. Recovery 3: easy.",
        "C1.3" => "C1.3: Work 6: controlled, sustained effort. Recovery 3: easy.",
        _ => "C2: Target 4: steady, conversational running."
    };
    private string RpeGuidance => draftProtocol == "C1.1"
        ? "Work 10 means maximal, all-out sprinting. Recovery 2 is easy. This is a maximal-effort option, not an automatic next step. Stop rather than force a sprint when control is lost. Perceived effort is not a measured heart-rate value."
        : draftProtocol == "C2" ? "Keep the effort comfortable rather than trying to run faster. The target is a guide, not a score to beat. Perceived effort describes how you feel today, not a measured heart-rate value."
        : "Aim for the planned effort. Stay in control so you can repeat the next effort; this is not an all-out sprint. Perceived effort describes how you feel, not a measured heart-rate value.";

    protected override void OnInitialized()
    {
        try { statePreferences = CorePreferences.Load(); }
        catch (InvalidDataException problem) { error = problem.Message; loadFailed = true; }

        RunService.Changed += Refresh;
    }

    private void Refresh() => _ = InvokeAsync(StateHasChanged);
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
    private static string RpeFor(CoreStartRequest request) => request.ProtocolId == "C2" ? "4"
        : $"{CoreProtocols.Find(request.ProtocolId).WorkRpe} / {CoreProtocols.Find(request.ProtocolId).RecoveryRpe}";

    private static string Dose(CoreStartRequest request)
    {
        if (request.ProtocolId == "C2") return $"{request.ContinuousMinutes ?? 45} min continuous";

        var protocol = CoreProtocols.Find(request.ProtocolId);
        var count = request.Repetitions ?? protocol.DefaultCount;

        return $"{count} × {Duration(protocol.WorkSeconds)} work · {count - 1} × {Duration(protocol.RecoverySeconds)} recovery";
    }

    private static string TargetLabel(CoreTargets? target, bool continuous = false) => target is null || target.Mode == CoreTargetMode.None ? "RPE only"
        : $"{(continuous ? "Continuous target" : "Work target")}: {(target.Mode == CoreTargetMode.Pace ? Clock(target.Work!.Value) + " min/km" : "Zone " + target.Work)}"
            + (target.Recovery is { } recovery ? $" · Recovery: {(target.Mode == CoreTargetMode.Pace ? Clock(recovery) + " min/km" : "Zone " + recovery)}" : "");
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
        if (target is null || target.Mode == CoreTargetMode.None || value is null) return "RPE only";

        return target.Mode == CoreTargetMode.Pace ? $"Target pace {Clock(value.Value)} min/km · intended effort"
            : $"Target Zone {value} · intended effort, not a measurement";
    }

    private static void TogglePause() => RunService.Command(RunService.Snapshot?.Paused == true ? "resume" : "pause");

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

    private void OpenRpe(string id, string trigger)
    {
        draftProtocol = id;
        Open("rpe", trigger);
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

    private void OpenSettings()
    {
        var settings = statePreferences.WarmupCooldown;
        warmupEnabled = settings.WarmupMinutes > 0;
        cooldownEnabled = settings.CooldownMinutes > 0;
        draftWarmup = (warmupEnabled ? settings.WarmupMinutes : 4).ToString(CultureInfo.InvariantCulture);
        draftCooldown = (cooldownEnabled ? settings.CooldownMinutes : 3).ToString(CultureInfo.InvariantCulture);
        Open("settings", "core-settings");
    }

    private static string TargetValue(int? value, CoreTargetMode mode) => value is null ? ""
        : mode == CoreTargetMode.Pace ? Clock(value.Value) : value.Value.ToString(CultureInfo.InvariantCulture);
    private void ChangeCount(int delta) => draftCount = (CountValue + delta).ToString(CultureInfo.InvariantCulture);
    private void ChangeTargetMode(ChangeEventArgs change)
    {
        targetMode = Enum.Parse<CoreTargetMode>(change.Value!.ToString()!);
        draftWork = draftRecovery = "";
    }

    private bool Persist(CorePreferencesState state)
    {
        try
        {
            CorePreferences.Save(state);
            statePreferences = state;

            return true;
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or IOException)
        {
            var message = problem is ArgumentException ? problem.Message : "Your changes could not be saved. Try again.";
            if (dialogMode is null) error = message; else dialogError = message;
            return false;
        }
    }

    private async Task SaveVariant()
    {
        var protocol = CoreProtocols.Find(draftProtocol);
        var choices = statePreferences.C1Selections.Select(request => request.ProtocolId == protocol.Id
            ? request with { Repetitions = protocol.DefaultCount } : request).ToArray();
        if (Persist(statePreferences with { SelectedProtocol = protocol.Id, C1Selections = choices })) await CloseDialog();
    }

    private async Task SaveDuration()
    {
        if (!int.TryParse(draftDuration, out var duration)) { Fail("Choose a whole duration from 20 to 120 minutes.", "c2-duration"); return; }
        var request = statePreferences.C2Selection with { ContinuousMinutes = duration };
        if (Persist(statePreferences with { C2Selection = request })) await CloseDialog();
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
            var state = editingShortcutId is { } editingId
                ? statePreferences with { Shortcuts = statePreferences.Shortcuts.Select(item => item.Id == editingId ? item with { Name = draftName.Trim(), Request = request } : item).ToArray() }
                : request.ProtocolId == "C2" ? statePreferences with { C2Selection = request }
                : statePreferences with { C1Selections = statePreferences.C1Selections.Select(value => value.ProtocolId == request.ProtocolId ? request : value).ToArray() };
            if (Persist(state)) await CloseDialog();
        }
        catch (ArgumentException problem) { Fail(problem.Message, "target-work"); }
    }

    private async Task SaveSettings()
    {
        if (warmupEnabled && (!int.TryParse(draftWarmup, out var warmup) || warmup is < 1 or > 15)) { Fail("Warm-up must be Off or 1–15 whole minutes.", "c1-warmup"); return; }
        if (cooldownEnabled && (!int.TryParse(draftCooldown, out var cooldown) || cooldown is < 1 or > 15)) { Fail("Cool-down must be Off or 1–15 whole minutes.", "c1-cooldown"); return; }
        var settings = new C1WarmupCooldownSettings(warmupEnabled ? int.Parse(draftWarmup, CultureInfo.InvariantCulture) : 0,
            cooldownEnabled ? int.Parse(draftCooldown, CultureInfo.InvariantCulture) : 0);
        if (Persist(statePreferences with { WarmupCooldown = settings })) await CloseDialog();
    }

    private async Task SaveShortcut()
    {
        if (string.IsNullOrWhiteSpace(draftName)) { Fail("Enter a shortcut name.", "shortcut-name"); return; }
        var shortcut = new CoreShortcut(Guid.NewGuid(), draftName.Trim(), draftRequest);
        if (Persist(statePreferences with { Shortcuts = statePreferences.Shortcuts.Append(shortcut).ToArray() })) await CloseDialog();
    }

    private void OpenManage()
    {
        managing = true;
        managePending = true;
        focusPending = "manage-heading";
        CoreDialogNavigation.Close = () => _ = InvokeAsync(CloseManage);
    }

    private async Task CloseManage()
    {
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
        if (!Persist(statePreferences with { Shortcuts = shortcuts })) return;

        await CloseDialog();
        focusPending = shortcuts.Count > 0 ? $"edit-{shortcuts[Math.Min(index, shortcuts.Count - 1)].Id}" : "manage-heading";
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
        if (!Persist(statePreferences with { Shortcuts = shortcuts })) return;

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
        if (!openPending && focusPending is null && !managePending) return;
        try
        {
            module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
            reference ??= DotNetObjectReference.Create(this);
            if (managePending) { managePending = false; await module.InvokeVoidAsync("manage", reference); }
            if (openPending) { openPending = false; await module.InvokeVoidAsync("open", opener, reference); }
            if (focusPending is { } focus) { focusPending = null; await module.InvokeVoidAsync("focus", focus); }
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
        CoreDialogNavigation.Close = managing ? () => _ = InvokeAsync(CloseManage) : null;
        if (module is not null) await module.InvokeVoidAsync("close");
        dialogMode = null;
        dialogError = null;
        focusPending = null;
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        RunService.Changed -= Refresh;
        CoreDialogNavigation.Close = null;
        reference?.Dispose();
        if (module is not null)
        {
            try { await module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }

        GC.SuppressFinalize(this);
    }
}

internal static class CoreDialogNavigation
{
    internal static Action? Close { get; set; }
}
