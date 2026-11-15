using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using Microsoft.Maui.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.History;

public partial class HistoryPage
{
    [Parameter] public EventCallback OnTraining { get; set; }
    [Parameter] public EventCallback<Guid> OnHealthSettings { get; set; }
    [Parameter] public Guid? InitialRun { get; set; }
    private readonly List<StoredRunSummary> runs = [];
    private StoredRunDetail? detail;
    private string? detailDestination;
    private string DetailTitle => detailDestination == "splits" ? "Splits & phases" : "Time in zones";
    private void ShowDetailDestination(string destination)
    {
        if (busy || detail is null || destination is not ("splits" or "zones")) return;
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
    private bool busy, disposed;
    private int interrupted, unreadable;
    private string? error;
    private string? focusPending = "history-heading";
    private IJSObjectReference? module;
    private Guid? sharingRun;
    private bool preparingShare;
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
        if (disposed || busy || sharingRun is not null || partial is not null) return;
        if (detail is { } selected) await Open(selected.Summary.Id);
        else await ShowList();
        StateHasChanged();
    });

    public async Task ShowList()
    {
        if (busy) return;
        busy = true;
        error = null;
        detail = null;
        detailDestination = null;
        partial = null;
        sharingRun = null;
        shareRequest++;
        preparingShare = false;
        shareMessage = null;
        recoveryFailed = recoveryUnavailable = false;
        runs.Clear();
        partials.Clear();
        try
        {
            var archive = await Task.Run(() => RunJournal.Shared.ReadFinishedRuns());
            if (disposed) return;
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
        busy = true;
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
        finally { busy = false; }
    }
    private async Task Open(Guid id)
    {
        if (busy) return;
        busy = true;
        error = null;
        try
        {
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
        if (busy || detail is not { } run) return;
        var id = run.Summary.Id;
        busy = true;
        error = null;
        try
        {
            var result = await HealthConnectScheduling.RetryAsync(id);
            if (disposed || detail?.Summary.Id != id) return;
            detail = await Task.Run(() => RunJournal.Shared.ReadFinishedRun(id));
            if (disposed) return;
            if (result is "disabled" or "unavailable") error = "Enable Health Connect and review write access in Settings. Your run remains local.";
            else if (result == "busy") error = "An export attempt is already running. Retry after it completes.";
            focusPending = "history-heading";
        }
        catch (Exception problem) when (ReadError(problem) || problem is Java.Lang.RuntimeException)
        { if (!disposed) error = "Export is not confirmed. Your run is stored locally. Try again."; }
        finally { busy = false; }
    }

    private void ShowShare()
    {
        if (busy || preparingShare) return;
        var selected = partial is { SavedLocally: not null } recovered ? recovered.Summary.Id : detail?.Summary.Id;
        if (selected is null) return;

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
        if (detailDestination is { } destination)
        {
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
        var selectedId = partial?.Summary.Id ?? detail?.Summary.Id;
        if (selectedId is { } id)
        {
            await ShowList();
            focusPending = runs.Any(run => run.Id == id) || partials.Any(run => run.Summary.Id == id) ? "history-" + id : "history-heading";
        }
        else await OnTraining.InvokeAsync();
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
        if (focusPending is not { } focus) return;
        focusPending = null;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
        await module.InvokeVoidAsync("focus", focus);
    }
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        HealthConnectSettings.Changed -= RefreshHealthConnect;
        shareRequest++;
        if (module is not null) await module.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
