using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.History;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.GhostRun.Ui;

public partial class GhostRoutesPage
{
    [Parameter] public Guid? SourceRunId { get; set; }
    [Parameter] public RouteKey? InitialRoute { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public EventCallback OnStarted { get; set; }
    private CancellationTokenSource? acquiring;
    private GhostMode mode;
    private bool ready;
    private string? focusTarget;
    private static bool HasCurrentOwner => RunService.Snapshot is { } owner && (owner.Phase != "Finished" || owner.Storage?.State != "saved");
    private string? referenceFocus;
    private readonly NameForm form = new();
    private readonly List<ArchivedGhostRoute> routes = [];
    private StoredGhostCapture? capture, frozenSave;
    private ArchivedGhostRoute? selected;
    private GhostRecords? records;
    private string? error, sourceReason;
    private bool loading, saving, disposed, catalogue, canRetry = true, focusPending;
    private int year;
    private long request;
    private IJSObjectReference? focusModule;

    private void OwnerChanged() => _ = InvokeAsync(() => { if (!disposed) StateHasChanged(); });

    protected override async Task OnInitializedAsync()
    {
        RunService.Changed += OwnerChanged;
        if (SourceRunId is null)
        {
            await LoadCatalogue();
            if (InitialRoute is { } route && !disposed) await OpenRoute(route);
            return;
        }
        loading = true;
        var generation = ++request;
        RouteKey? existingKey = null;
        try
        {
            var existing = await Task.Run(() => new AndroidGhostArchive(RunJournal.Shared).ReadRoutes().FirstOrDefault(item => item.Route.SourceRunId == SourceRunId));
            if (disposed || generation != request) return;
            if (existing is not null) existingKey = existing.Route.Key;
            var prepared = await Task.Run(() => RunJournal.Shared.PrepareGhostRoute(SourceRunId.Value, "Route preview"));
            if (!disposed && generation == request) capture = prepared;
        }
        catch (Exception issue) when (Expected(issue))
        {
            if (!disposed && generation == request) error = issue is InvalidDataException ? issue.Message : "The stored route evidence could not be read.";
        }
        finally { if (generation == request) { loading = false; focusPending = true; } }
        if (existingKey is { } key && !disposed && generation == request) await OpenRoute(key);
    }

    private string ReadyTitle => error?.StartsWith("Move", StringComparison.Ordinal) == true ? "Move to the start" : error?.StartsWith("GPS", StringComparison.Ordinal) == true ? "GPS uncertain" : records?.ReferenceFor(mode) is null ? "Set first time" : mode == GhostMode.YearBest ? "Race year best" : "Race personal best";
    private void ChooseReference(GhostMode choice)
    {
        mode = choice;
        ready = true;
        referenceFocus = records?.SameReference == true ? "ghost-same" : records?.ReferenceFor(choice) is null ? "ghost-first" : choice == GhostMode.YearBest ? "ghost-year" : "ghost-personal";
        error = null;
        focusPending = true;
    }
    private async Task LoadCatalogue()
    {
        if (saving) return;
        loading = true;
        catalogue = true;
        var generation = ++request;
        try
        {
            var list = await Task.Run(() => new AndroidGhostArchive(RunJournal.Shared).ReadRoutes());
            if (disposed || generation != request) return;
            routes.Clear(); routes.AddRange(list);
            selected = null; ready = false; catalogue = true; error = null; focusPending = true;
        }
        catch (Exception issue) when (Expected(issue)) { if (!disposed && generation == request) error = "Your routes could not be read. Try reopening Ghost Run."; }
        finally { if (generation == request) loading = false; }
    }

    private async Task StartRace()
    {
        if (selected is null || acquiring is not null || saving) return;
        error = null;
        using var operation = new CancellationTokenSource();
        acquiring = operation;
        try
        {
            await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>();
            if (disposed || operation.IsCancellationRequested) return;
            var position = await AndroidGhostPosition.Acquire(operation.Token);
            if (disposed || operation.IsCancellationRequested) return;
            var failure = await RunService.StartGhostAsync(new(selected.Route.Key, mode, position), operation.Token);
            if (disposed) return;
            if (failure is not null) error = failure;
            else await OnStarted.InvokeAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception issue) when (Expected(issue) || issue is Java.Lang.RuntimeException)
        { if (!disposed) error = issue is InvalidDataException ? issue.Message : "The race has not started. GPS or local storage is unavailable."; }
        finally { if (ReferenceEquals(acquiring, operation)) acquiring = null; }
    }

    private async Task SaveRoute()
    {
        if (saving || capture is null || !canRetry) return;
        saving = true;
        error = null;
        frozenSave ??= GhostStoredSource.Name(capture, form.Name);
        var candidate = frozenSave;
        var committed = false;
        try
        {
            _ = await Task.Run(() => RunJournal.Shared.SaveGhostRoute(candidate));
            committed = true;
        }
        catch (Exception issue) when (Expected(issue))
        {
            if (!disposed)
            {
                canRetry = issue is IOException and not RunSaveReadException;
                error = canRetry ? "Route save is not confirmed. Retry saving the same route." : "The route could not be saved. No archived data was replaced.";
            }
        }
        finally { saving = false; }
        if (committed && !disposed) await OpenRoute(candidate.Attempt.Route);
    }

    private async Task OpenRoute(RouteKey key)
    {
        if (loading || saving) return;
        loading = true;
        error = null;
        ready = false;
        referenceFocus = null;
        year = DateTimeOffset.Now.Year;
        var generation = ++request;
        try
        {
            var result = await Task.Run(() =>
            {
                var archive = new AndroidGhostArchive(RunJournal.Shared);
                var route = archive.ReadRoute(key) ?? throw new KeyNotFoundException("The archived route no longer exists.");
                return (Route: route, Records: archive.ReadRecords(key, year), Attempts: archive.ReadAttempts(key));
            });
            if (disposed || generation != request) return;
            selected = result.Route;
            records = result.Records;
            sourceReason = result.Attempts.FirstOrDefault(attempt => attempt.Attempt.SourceRunId == selected.Route.SourceRunId)?.QualityReason;
            focusPending = true;
        }
        catch (Exception issue) when (Expected(issue)) { if (!disposed && generation == request) error = "The saved route details could not be read. No data was changed."; }
        finally { if (generation == request) loading = false; }
    }

    public async Task GoBack()
    {
        if (saving) return;
        acquiring?.Cancel();
        if (ready)
        {
            ready = false;
            error = null;
            focusTarget = referenceFocus;
            focusPending = true;
            return;
        }
        if (selected is not null) { var key = selected.Route.Key; await LoadCatalogue(); focusTarget = RouteButton(key); }
        else { request++; await OnBack.InvokeAsync(); }
    }

    private static string RouteButton(RouteKey key) => $"ghost-route-{key.Id:N}-{key.Version:N}";
    private static bool Expected(Exception issue) => issue is IOException or InvalidDataException or JsonException
        or ArgumentException or KeyNotFoundException or InvalidOperationException or TimeZoneNotFoundException or InvalidTimeZoneException
        or global::Android.Database.Sqlite.SQLiteException;
    private static string Distance(double metres) => (metres / 1000).ToString("0.00", CultureInfo.InvariantCulture);
    private static string Record(GhostAttempt? attempt) => attempt is null ? "No verified result" : $"{(long)attempt.Elapsed.TotalMinutes}:{attempt.Elapsed.Seconds:00} · {attempt.StartedLocal.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusPending || disposed) return;
        focusPending = false;
        focusModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/core-dialog.js");
        if (!disposed) await focusModule.InvokeVoidAsync("focus", focusTarget ?? "ghost-heading");
        focusTarget = null;
    }
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        RunService.Changed -= OwnerChanged;
        acquiring?.Cancel();
        request++;
        if (focusModule is not null) await focusModule.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    private sealed class NameForm
    {
        [Required] public string Name { get; set; } = "";
    }
}
