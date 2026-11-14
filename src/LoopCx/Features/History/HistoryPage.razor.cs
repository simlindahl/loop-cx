using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.History;

public partial class HistoryPage
{
    [Parameter] public EventCallback OnTraining { get; set; }
    [Parameter] public Guid? InitialRun { get; set; }
    private readonly List<StoredRunSummary> runs = [];
    private StoredRunDetail? detail;
    private bool busy, disposed;
    private int interrupted, unreadable;
    private string? error;
    private string? focusPending = "history-heading";
    private IJSObjectReference? module;

    protected override async Task OnInitializedAsync()
    {
        await ShowList();
        if (InitialRun is { } id && !disposed) await Open(id);
    }
    public async Task ShowList()
    {
        if (busy) return;
        busy = true;
        error = null;
        detail = null;
        runs.Clear();
        try
        {
            var archive = await Task.Run(() => RunJournal.Shared.ReadFinishedRuns());
            if (disposed) return;
            interrupted = archive.InterruptedCount;
            unreadable = archive.UnreadableCount;
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
    public async Task GoBack()
    {
        if (detail is { } selected)
        {
            await ShowList();
            focusPending = runs.Any(run => run.Id == selected.Summary.Id) ? "history-" + selected.Summary.Id : "history-heading";
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
    private static string Effort(IReadOnlyList<TimedPhase>? plan) => plan is null ? "Unavailable"
        : "Work " + string.Join("/", plan.Where(phase => phase.Kind == RunPhase.Work).Select(phase => phase.TargetRpe).Distinct())
        + (plan.Any(phase => phase.Kind == RunPhase.Recovery) ? " · recovery " + string.Join("/", plan.Where(phase => phase.Kind == RunPhase.Recovery).Select(phase => phase.TargetRpe).Distinct()) : " · no recovery phase");
    private static string Dose(IReadOnlyList<TimedPhase>? plan) => plan is null ? "Unavailable"
        : $"{plan.Count(phase => phase.Kind == RunPhase.Work)} work phases ({Time(TimeSpan.FromTicks(plan.Where(phase => phase.Kind == RunPhase.Work).Sum(phase => phase.Duration.Ticks)))}) · {plan.Count(phase => phase.Kind == RunPhase.Recovery)} recoveries ({Time(TimeSpan.FromTicks(plan.Where(phase => phase.Kind == RunPhase.Recovery).Sum(phase => phase.Duration.Ticks)))})";
    private static string Target(CoreTargets? target) => target is null ? "Unavailable" : target.Mode switch
    {
        CoreTargetMode.None => "RPE only",
        CoreTargetMode.Pace => $"Work {Time(target.Work is { } w ? TimeSpan.FromSeconds(w) : null)} / recovery {Time(target.Recovery is { } r ? TimeSpan.FromSeconds(r) : null)} min/km · intended",
        _ => $"Work Zone {target.Work} / recovery Zone {target.Recovery} · intended, not measured"
    };
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
        if (module is not null) await module.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
