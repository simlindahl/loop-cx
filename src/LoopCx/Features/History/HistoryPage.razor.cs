using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.Storage;

namespace LoopCx.Features.History;

public partial class HistoryPage
{
    [Parameter] public EventCallback OnTraining { get; set; }
    [Parameter] public EventCallback<LoopCx.Features.GhostRun.RouteKey> OnGhostRoute { get; set; }
    [Parameter] public EventCallback<Guid> OnHealthSettings { get; set; }
    [Parameter] public Guid? InitialRun { get; set; }
    private bool HasMotionDestination => !ghostOpen && sharingRun is null && detailDestination is null && partial is null && ghostResult?.Status != "interrupted";
    private string MotionDestination => ghostOpen ? "route" : sharingRun is { } shared ? $"share:{shared}" : detailDestination is { } destination ? $"detail:{destination}"
        : partial is { } interruptedRun ? $"interrupted:{interruptedRun.Summary.Id}" : (ghostResult?.Id ?? detail?.Summary.Id) is { } selected ? $"run:{selected}" : "list";
    private string? lastMotionDestination;
    private readonly List<LoopCx.Features.GhostRun.GhostStoredRun> ghostRuns = [];
    private LoopCx.Features.GhostRun.GhostStoredRun? ghostResult;
    private IEnumerable<object> HistoryEntries => runs.Cast<object>().Concat(ghostRuns).OrderByDescending(item => item switch
    {
        StoredRunSummary core => core.StartedUtc ?? DateTimeOffset.MinValue,
        LoopCx.Features.GhostRun.GhostStoredRun race => race.Checkpoint.StartedUtc,
        _ => DateTimeOffset.MinValue
    }).ThenBy(item => item is StoredRunSummary core ? core.Id : ((LoopCx.Features.GhostRun.GhostStoredRun)item).Id);
    private bool ghostOpen;
    private LoopCx.Features.GhostRun.Ui.GhostRoutesPage? ghostPage;
    private void OpenGhostRoutes()
    {
        if (busy || detail?.IsContinuousGhostSource != true) return;
        ghostOpen = true;
        focusPending = null;
    }
    private Task CloseGhostRoutes()
    {
        ghostOpen = false;
        focusPending = "history-ghost";
        return Task.CompletedTask;
    }

    private readonly List<StoredRunSummary> runs = [];
    private StoredRunDetail? detail;
    private string? detailDestination;
    private HistoryRouteView? routeView;
    private string DetailTitle => detailDestination switch
    {
        "splits" => "Splits & phases",
        "route" => "Recorded route",
        _ => "Time in zones"
    };

    private void ShowDetailDestination(string destination)
    {
        if (busy || (destination == "route"
            ? detail is null && partial?.SavedLocally is null
            : detail is null || destination is not ("splits" or "zones")))
        {
            return;
        }

        detailDestination = destination;
        focusPending = "history-heading";
    }
    private static string SplitLabel(StoredSplit split)
    {
        return split.Number + (split.FinalPartial ? " · final partial" : "");
    }

    private static string PhaseLabel(StoredPhase phase)
    {
        return phase.Kind switch
        {
            RunPhase.Warmup => "Warm-up",
            RunPhase.Cooldown => "Cool-down",
            RunPhase.Work => $"Work · phase {phase.Number}",
            RunPhase.Recovery => $"Recovery · phase {phase.Number}",
            _ => "Unavailable"
        };
    }

    private static string Metres(double metres)
    {
        return (metres / 1000).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string PaceValue(double? value)
    {
        return value is > 0 ? Time(TimeSpan.FromSeconds(value.Value)) : "Unavailable";
    }

    private static string SplitCoverage(StoredSplit split)
    {
        return $"GPS {Time(split.GpsKnown)} known / {Time(split.GpsUnknown)} unknown. HR {Time(split.HrKnown)} known / {Time(split.HrUnknown)} unknown.";
    }

    private readonly List<InterruptedRun> partials = [];
    private InterruptedRun? partial;
    private bool recoveryFailed, recoveryUnavailable;
    private bool busy, disposed, savingRecordedPart;
    private int interrupted, unreadable;
    private string? error;
    private string? focusPending = "history-heading";
    private IJSObjectReference? module;
    private IJSObjectReference? layoutModule;
    private ElementReference historyContent;
    private readonly string layoutOwner = Guid.NewGuid().ToString("N");
    private Guid? sharingRun;
    private bool preparingShare;
    private bool sharingGhost;
    private bool GhostShareEligible => ghostResult is { } run
        && !(RunService.Snapshot is { } owner && owner.Id == run.Id && owner.Storage?.State != "saved")
        && (run.Status == "finished" || run.Status == "incomplete" && run.Checkpoint.Snapshot.Clock.RaceElapsed > TimeSpan.Zero
            || run.Status == "interrupted" && run.SavedLocally is not null);
    private long shareRequest;
    private string? shareMessage;

    protected override async Task OnInitializedAsync()
    {
        HealthConnectSettings.Changed += RefreshHealthConnect;
        await ShowList();
        if (InitialRun is { } id && !disposed) await Open(id);
    }
    private void RefreshHealthConnect() => _ = InvokeAsync(async () =>
    {
        if (disposed || busy || ghostOpen || sharingRun is not null || partial is not null) return;
        if (ghostResult is { } race)
        {
            try
            {
                var loaded = await Task.Run(() => RunJournal.Shared.ReadGhostRun(race.Id));
                if (disposed || ghostResult?.Id != race.Id) return;
                ghostResult = loaded;
            }
            catch (Exception problem) when (ReadError(problem))
            { if (!disposed && ghostResult?.Id == race.Id) error = "Health Connect status could not be refreshed. Your recording remains local."; }
        }
        else if (detail is { } selected) await Open(selected.Summary.Id);
        else await ShowList();
        StateHasChanged();
    });

    public async Task ShowList()
    {
        if (busy) return;
        busy = true;
        try
        {
            if (detail is not null || ghostResult is not null) await LeaveMotionDestination();
            error = null;
            detail = null;
            detailDestination = null;
            ghostOpen = false;
            partial = null;
            sharingRun = null;
            shareRequest++;
            preparingShare = false;
            shareMessage = null;
            recoveryFailed = recoveryUnavailable = false;
            runs.Clear();
            partials.Clear();
            ghostRuns.Clear();
            ghostResult = null;
            var archive = await Task.Run(() => RunJournal.Shared.ReadFinishedRuns());
            if (disposed) return;
            ghostRuns.AddRange(archive.Ghost);
            interrupted = archive.InterruptedCount;
            unreadable = archive.UnreadableCount;
            partials.AddRange(archive.Interrupted ?? []);
            foreach (var row in archive.Finished)
            {
                try { runs.Add(StoredRunProjection.Summary(row)); }
                catch (Exception issue) when (ReadError(issue)) { unreadable++; }
            }
        }
        catch (Exception issue) when (ReadError(issue)) { error = "Stored recordings could not be read. No data was changed."; }
        finally { busy = false; }
        focusPending = "history-heading";
        await InvokeAsync(StateHasChanged);
    }
    private void OpenInterrupted(InterruptedRun selected)
    {
        if (busy) return;
        partial = selected;
        error = null;
        recoveryFailed = recoveryUnavailable = false;
        focusPending = "history-heading";
    }

    private async Task SaveRecordedPart()
    {
        if (busy || partial is not { SavedLocally: null } selected || recoveryUnavailable) return;
        busy = savingRecordedPart = true;
        error = null;
        try
        {
            var saved = await Task.Run(() => RunJournal.Shared.SaveInterruptedPart(selected.Summary.Id));
            if (disposed || partial?.Summary.Id != selected.Summary.Id) return;
            partial = saved;
            recoveryFailed = false;
            focusPending = "history-heading";
        }
        catch (Exception issue) when (ReadError(issue))
        {
            recoveryFailed = true;
            recoveryUnavailable = issue is not IOException || issue is RunSaveReadException;
            error = recoveryUnavailable ? "This recording is unavailable. No data was repaired or overwritten."
                : "Could not save the recorded part. Save is not confirmed. Try again.";
            focusPending = recoveryUnavailable ? "history-heading" : "recovery-save";
        }
        finally { busy = savingRecordedPart = false; }
    }
    private async Task SaveGhostRecordedPart()
    {
        if (busy || ghostResult is not { Status: "interrupted", SavedLocally: null } selected || recoveryUnavailable) return;
        busy = savingRecordedPart = true;
        error = null;
        try
        {
            var saved = await Task.Run(() => RunJournal.Shared.SaveGhostRecordedPart(selected.Id));
            if (disposed || ghostResult?.Id != selected.Id) return;
            ghostResult = saved with { Records = selected.Records };
            recoveryFailed = false;
            focusPending = "history-heading";
        }
        catch (Exception issue) when (ReadError(issue))
        {
            recoveryFailed = true;
            recoveryUnavailable = issue is not IOException || issue is RunSaveReadException;
            error = recoveryUnavailable ? "This recording is unavailable. No data was repaired or overwritten."
                : "Could not save the recorded part. Save is not confirmed. Try again.";
            focusPending = recoveryUnavailable ? "history-heading" : "recovery-save";
        }
        finally { busy = savingRecordedPart = false; }
    }
    private async Task Open(Guid id)
    {
        if (busy) return;
        busy = true;
        error = null;
        try
        {
            if ((ghostResult?.Id ?? detail?.Summary.Id) != id) await LeaveMotionDestination();
            var ghost = await Task.Run(() => RunJournal.Shared.ReadGhostRun(id));
            if (disposed) return;
            if (ghost is not null)
            {
                ghostResult = ghost;
                detail = null;
                focusPending = "history-heading";
                return;
            }
            var loaded = await Task.Run(() => RunJournal.Shared.ReadFinishedRun(id));
            if (disposed) return;
            detail = loaded;
            focusPending = "history-heading";
        }
        catch (Exception issue) when (ReadError(issue)) { error = "This stored recording could not be read. No data was changed."; }
        finally { busy = false; }
    }
    private async Task RetryHealthConnect()
    {
        if (busy || (detail is null && ghostResult is null)) return;
        var id = ghostResult?.Id ?? detail!.Summary.Id;
        var ghostRetry = ghostResult is not null;
        busy = true;
        error = null;
        try
        {
            var result = await HealthConnectScheduling.RetryAsync(id);
            if (disposed || (ghostRetry ? ghostResult?.Id : detail?.Summary.Id) != id) return;
            if (ghostRetry)
            {
                var loaded = await Task.Run(() => RunJournal.Shared.ReadGhostRun(id));
                if (disposed || ghostResult?.Id != id) return;
                ghostResult = loaded;
            }
            else
            {
                var loaded = await Task.Run(() => RunJournal.Shared.ReadFinishedRun(id));
                if (disposed || detail?.Summary.Id != id) return;
                detail = loaded;
            }
            if (result is "disabled" or "unavailable") error = "Enable Health Connect and review write access in Settings. Your run remains local.";
            else if (result == "busy") error = "An export attempt is already running. Retry after it completes.";
            focusPending = "history-heading";
        }
        catch (Exception problem) when (ReadError(problem) || problem is Java.Lang.RuntimeException)
        {
            if (!disposed && (ghostRetry ? ghostResult?.Id : detail?.Summary.Id) == id)
                error = "Export is not confirmed. Your run is stored locally. Try again.";
        }
        finally { busy = false; }
    }

    private void ShowShare()
    {
        if (busy || preparingShare) return;
        var selected = GhostShareEligible ? ghostResult!.Id : partial is { SavedLocally: not null } recovered ? recovered.Summary.Id : detail?.Summary.Id;
        if (selected is null) return;

        sharingGhost = ghostResult is not null;
        sharingRun = selected;
        shareMessage = null;
        error = null;
        focusPending = "history-heading";
    }

    private bool CurrentShare(Guid id, long request) => !disposed && sharingRun == id && shareRequest == request;

    private async Task OpenShareSheet()
    {
        if (preparingShare || sharingRun is not { } id) return;
        var request = ++shareRequest;
        preparingShare = true;
        shareMessage = null;
        error = null;
        var opening = false;
        try
        {
            var json = await Task.Run(() => RunJournal.Shared.ReadShareJson(id));
            if (!CurrentShare(id, request)) return;
            var cache = FileSystem.CacheDirectory;
            var path = await Task.Run(() => RunJsonSharing.PrepareFile(cache, json));
            if (!CurrentShare(id, request)) return;

            opening = true;
            await RunJsonShareSheet.OpenAsync(path, () => CurrentShare(id, request));
            if (!CurrentShare(id, request)) return;
            shareMessage = "Android controls sharing. No recipient delivery is confirmed.";
        }
        catch (Exception issue) when (ReadError(issue) || issue is UnauthorizedAccessException or NotSupportedException or Java.Lang.RuntimeException)
        {
            if (!CurrentShare(id, request)) return;
            error = opening ? "Could not confirm the share sheet opened. No delivery is confirmed. Try again."
                : "The JSON file could not be prepared. Your stored recording was not changed. Try again.";
        }
        finally
        {
            if (CurrentShare(id, request))
            {
                preparingShare = false;
                focusPending = "share-open";
            }
        }
    }

    public async Task GoBack()
    {
        if (ghostOpen)
        {
            if (ghostPage is not null) await ghostPage.GoBack();
            else await CloseGhostRoutes();
            return;
        }

        if (detailDestination is { } destination)
        {
            if (destination == "route" && routeView is { } route)
            {
                await route.Leave();
                if (disposed || detailDestination != destination) return;

                routeView = null;
            }

            detailDestination = null;
            focusPending = "history-" + destination;
            await InvokeAsync(StateHasChanged);
            return;
        }
        if (sharingRun is not null)
        {
            sharingRun = null;
            shareRequest++; // A late file read/preparation must never open Android after Back.
            preparingShare = false;
            shareMessage = null;
            error = null;
            focusPending = "history-share";
            await InvokeAsync(StateHasChanged);

            return;
        }
        if (ghostResult is not null) { var ghostId = ghostResult.Id; await ShowList(); focusPending = $"history-{ghostId}"; return; }
        var selectedId = partial?.Summary.Id ?? detail?.Summary.Id;
        if (selectedId is { } id)
        {
            await ShowList();
            focusPending = runs.Any(run => run.Id == id) || partials.Any(run => run.Summary.Id == id) ? "history-" + id : "history-heading";
        }
        else
        {
            var exit = LeaveMotionDestination();
            await OnTraining.InvokeAsync();
            await exit; // The parent already owns the new destination before cosmetic completion.
        }
    }
    private static bool ReadError(Exception error) => error is IOException or InvalidDataException or JsonException
        or global::Android.Database.Sqlite.SQLiteException or KeyNotFoundException or InvalidOperationException or OverflowException or ArgumentException;
    private static string Time(TimeSpan? value) => value is null ? "Unavailable" : $"{(long)value.Value.TotalMinutes}:{value.Value.Seconds:00}";
    private static string Number(double? value) => value?.ToString("0", CultureInfo.InvariantCulture) ?? "Unavailable";
    private static string Distance(MeasuredStatistics? value) => value is null || value.KnownGpsActiveTime == TimeSpan.Zero && value.MeasuredMetres == 0
        ? "Unavailable" : (value.MeasuredMetres / 1000).ToString("0.00", CultureInfo.InvariantCulture);
    private static string Pace(double? value) => value is > 0 && double.IsFinite(value.Value) ? Time(TimeSpan.FromSeconds(value.Value)) + "/km" : "Unavailable";
    private static string Started(StoredRunSummary run) => run.StartedUtc is not { } utc ? "Start time unavailable"
        : run.StartOffset is { } offset ? utc.ToOffset(TimeSpan.FromSeconds(offset)).ToString("d MMM yyyy · HH:mm zzz", CultureInfo.InvariantCulture)
        : utc.ToUniversalTime().ToString("d MMM yyyy · HH:mm 'UTC'", CultureInfo.InvariantCulture);
    private static string Dose(IReadOnlyList<TimedPhase>? plan) => plan is null ? "Unavailable"
        : $"{plan.Count(phase => phase.Kind == RunPhase.Work)} work phases ({Time(TimeSpan.FromTicks(plan.Where(phase => phase.Kind == RunPhase.Work).Sum(phase => phase.Duration.Ticks)))}) · {plan.Count(phase => phase.Kind == RunPhase.Recovery)} recoveries ({Time(TimeSpan.FromTicks(plan.Where(phase => phase.Kind == RunPhase.Recovery).Sum(phase => phase.Duration.Ticks)))})";
    private static string Coverage(MeasuredStatistics? scope) => scope is null ? "Measurement coverage unavailable."
        : $"Main GPS known {Time(scope.KnownGpsActiveTime)} / unknown {Time(scope.UnknownGpsActiveTime)}. HR known {Time(scope.KnownHeartRateActiveTime)} / unknown {Time(scope.UnknownHeartRateActiveTime)}.";
    private static string ZoneSummary(IReadOnlyList<HeartRateZone>? zones) => zones is null ? "Frozen heart-rate zones unavailable."
        : zones.Count == 0 ? "No personal heart-rate zones were configured for this run."
        : "Frozen zones: " + string.Join(" · ", zones.Select(zone => $"Zone {zone.Number}: {zone.MinimumBpm}–{zone.MaximumBpm} bpm"));
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var renderedDestination = MotionDestination;
        var motionChanged = !busy && lastMotionDestination != renderedDestination;
        layoutModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/history-layout.js");
        if (disposed)
        {
            await layoutModule.DisposeAsync();
            return;
        }
        await layoutModule.InvokeVoidAsync("observe", historyContent, layoutOwner);
        if (focusPending is null && !motionChanged) return;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
        if (disposed)
        {
            await module.DisposeAsync();
            return;
        }
        if (focusPending is { } focus)
        {
            focusPending = null;
            await module.InvokeVoidAsync("focus", focus);
        }
        if (motionChanged && !disposed && !busy && MotionDestination == renderedDestination)
        {
            lastMotionDestination = renderedDestination;
            await module.InvokeVoidAsync("syncMotion", false);
        }
    }
    private async Task LeaveMotionDestination()
    {
        if (module is null || lastMotionDestination is null) return;
        try
        {
            await module.InvokeVoidAsync("leaveSurface", historyContent);
        }
        catch (JSException) { } // Cosmetic feedback must not block the actual destination change.
    }
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        HealthConnectSettings.Changed -= RefreshHealthConnect;
        shareRequest++;
        if (layoutModule is not null)
        {
            await layoutModule.InvokeVoidAsync("dispose", layoutOwner);
            await layoutModule.DisposeAsync();
        }
        if (module is not null)
        {
            await module.InvokeVoidAsync("disposeSurface", layoutOwner);
            await module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
