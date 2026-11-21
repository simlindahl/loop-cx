using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.History;

public partial class HistoryRouteView
{
    [Parameter, EditorRequired] public Guid RunId { get; set; }
    [Parameter] public MeasuredStatistics? Total { get; set; }
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private StoredRoute? stored;
    private HistoryRouteGeometry? geometry;
    private HistoryRouteDrawing? drawing;
    private bool loadingRoute;
    private bool showMain;
    private bool disposed;
    private HistoryMapStatus mapStatus;
    private ElementReference mapSlot;
    private Guid mapOwner;
    private Guid observerOwner;
    private MapObservation? observation;
    private bool nativeVisible;

    private sealed record MapObservation(IJSObjectReference Module, IJSObjectReference Observer,
        DotNetObjectReference<HistoryRouteView> Callback);

    private StoredRouteScope Scope => showMain ? stored!.Main : stored!.Whole;

    private string RecordedDistance => Total is null || (Total.KnownGpsActiveTime == TimeSpan.Zero && Total.MeasuredMetres == 0)
        ? "Unavailable" : (Total.MeasuredMetres / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " km";

    private string Completeness => stored is null ? "Unavailable"
        : !Scope.Supported ? "Unavailable · main-set points cannot be identified"
        : Scope.Points.Count == 0 ? "Unavailable · no stored coordinates"
        : Scope.UnknownQuality ? "Unknown · older GPS quality and gaps were not fully saved"
        : geometry?.HasSeparateSegments == true ? "Separate recorded segments"
        : "Completeness not established";

    private string RouteDescription => Scope.UnknownQuality ? "Stored route with isolated points of unknown quality"
        : geometry?.HasSeparateSegments == true ? "Stored route with separate segments and no line across gaps"
        : "Stored route geometry";

    private bool MapVisible => mapStatus == HistoryMapStatus.Ready && nativeVisible;

    private string MapStatusTitle => mapStatus switch
    {
        HistoryMapStatus.NotConfigured => "Map is not configured",
        HistoryMapStatus.Offline => "Map unavailable offline",
        HistoryMapStatus.Loading => "Loading map…",
        HistoryMapStatus.Failed => "Map could not load",
        _ => "Recorded track"
    };

    private string MapStatusText => mapStatus switch
    {
        HistoryMapStatus.NotConfigured => "Map configuration is missing. Your recording and saved coordinates are unchanged.",
        HistoryMapStatus.Offline => "The map background cannot load. The saved route stays available.",
        HistoryMapStatus.Loading => "Your recording stays available while the map loads.",
        HistoryMapStatus.Failed => "The map background could not be confirmed. You can use the stored route shape or retry.",
        _ => MapVisible ? "The selected stored route is shown on a Google map background."
            : "The map background is shown when the track fits on screen."
    };

    protected override Task OnInitializedAsync() => ReadRoute();

    private async Task ReadRoute()
    {
        if (loadingRoute)
        {
            return;
        }

        loadingRoute = true;
        var id = RunId;

        try
        {
            var route = await Task.Run(() => RunJournal.Shared.ReadRoute(id));

            if (!disposed && route.Id == id && RunId == id)
            {
                stored = route;
                await UpdateDrawing();
            }
        }
        catch (Exception error) when (error is IOException or JsonException or global::Android.Database.Sqlite.SQLiteException
            or KeyNotFoundException or InvalidOperationException or OverflowException or ArgumentException)
        {
            if (!disposed)
            {
                stored = null;
            }
        }
        finally
        {
            loadingRoute = false;
        }
    }

    private Task ShowWhole()
    {
        showMain = false;
        return UpdateDrawing();
    }

    private Task ShowMain()
    {
        showMain = true;
        return UpdateDrawing();
    }

    private async Task UpdateDrawing()
    {
        geometry = Scope.Supported ? HistoryRouteGeometry.From(Scope) : null;
        drawing = geometry is null ? null : HistoryRouteDrawing.Create(geometry, Scope.UnknownQuality);
        await ConfigureMap();
    }

    private async Task ConfigureMap()
    {
        var previous = mapOwner;
        var owner = mapOwner = Guid.NewGuid();
        mapStatus = HistoryMapStatus.NotConfigured;
        nativeVisible = false;
        await CloseMap(previous);

        if (!Current(owner) || drawing is null) return;

        var status = await MainThread.InvokeOnMainThreadAsync(() =>
            HistoryAndroidMap.Availability(global::Android.App.Application.Context));

        if (Current(owner))
        {
            mapStatus = status;
        }
    }

    private bool Current(Guid owner) => !disposed && mapOwner == owner;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed || mapStatus != HistoryMapStatus.WaitingForViewport || observerOwner == mapOwner) return;

        var owner = observerOwner = mapOwner;
        var callback = DotNetObjectReference.Create(this);
        IJSObjectReference? module = null;
        IJSObjectReference? observer = null;

        try
        {
            module = await Js.InvokeAsync<IJSObjectReference>("import", "./js/history-map-slot.js");

            if (!Current(owner)) return;

            observer = await module.InvokeAsync<IJSObjectReference>("observe", mapSlot, callback, owner);

            if (Current(owner))
            {
                observation = new(module, observer, callback);
                return;
            }
        }
        catch (JSException)
        {
            if (Current(owner))
            {
                mapStatus = HistoryMapStatus.Failed;
                StateHasChanged();
            }
        }
        finally
        {
            if (observation?.Callback != callback)
            {
                await DisposeObservation(module, observer, callback);
            }
        }
    }

    [JSInvokable]
    public async Task PositionMap(Guid owner, double left, double top, double width, double height, double viewportWidth)
    {
        if (!Current(owner) || geometry is not { } selected
            || mapStatus is not (HistoryMapStatus.WaitingForViewport or HistoryMapStatus.Loading or HistoryMapStatus.Ready))
        {
            return;
        }

        var visible = await MainThread.InvokeOnMainThreadAsync(() =>
            Current(owner) && Platform.CurrentActivity is MainActivity activity
                && activity.PositionHistoryMap(owner, new(left, top, width, height, viewportWidth), selected,
                    status => _ = ReportMapStatus(owner, status)));

        if (Current(owner) && nativeVisible != visible)
        {
            nativeVisible = visible;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task ReportMapStatus(Guid owner, HistoryMapStatus status) => InvokeAsync(() =>
    {
        if (!Current(owner)) return;

        mapStatus = status;
        if (status is HistoryMapStatus.Failed or HistoryMapStatus.Offline) nativeVisible = false;
        StateHasChanged();
    });

    private async Task CloseMap(Guid owner)
    {
        var previous = observation;
        observation = null;

        if (owner != Guid.Empty)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                (Platform.CurrentActivity as MainActivity)?.CloseHistoryMap(owner));
        }

        if (previous is not null)
        {
            await DisposeObservation(previous.Module, previous.Observer, previous.Callback);
        }
    }

    private static async ValueTask DisposeObservation(IJSObjectReference? module, IJSObjectReference? observer,
        DotNetObjectReference<HistoryRouteView> callback)
    {
        try
        {
            if (observer is not null)
            {
                await observer.InvokeVoidAsync("dispose");
                await observer.DisposeAsync();
            }

            if (module is not null) await module.DisposeAsync();
        }
        catch (JSException)
        {
            // The owning WebView may already be gone.
        }
        finally
        {
            callback.Dispose();
        }
    }

    public async Task Leave()
    {
        if (disposed) return;

        disposed = true;
        await CloseMap(mapOwner);
    }

    public async ValueTask DisposeAsync()
    {
        await Leave();
        GC.SuppressFinalize(this);
    }
}
