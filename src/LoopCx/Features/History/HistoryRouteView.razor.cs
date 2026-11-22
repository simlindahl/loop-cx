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
    private RouteMapStatus mapStatus;
    private ElementReference mapSlot;
    private Guid mapOwner;
    private Guid observerOwner;
    private RouteMapSlot<HistoryRouteView>? observation;
    private bool nativeVisible;

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

    private bool MapVisible => mapStatus == RouteMapStatus.Ready && nativeVisible;

    private string MapStatusTitle => mapStatus switch
    {
        RouteMapStatus.NotConfigured => "Map is not configured",
        RouteMapStatus.Offline => "Map unavailable offline",
        RouteMapStatus.Loading => "Loading map…",
        RouteMapStatus.Failed => "Map could not load",
        _ => "Recorded track"
    };

    private string MapStatusText => mapStatus switch
    {
        RouteMapStatus.NotConfigured => "Map configuration is missing. Your recording and saved coordinates are unchanged.",
        RouteMapStatus.Offline => "The map background cannot load. The saved route stays available.",
        RouteMapStatus.Loading => "Your recording stays available while the map loads.",
        RouteMapStatus.Failed => "The map background could not be confirmed. You can use the stored route shape or retry.",
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
        mapStatus = RouteMapStatus.NotConfigured;
        nativeVisible = false;
        await CloseMap(previous);

        if (!Current(owner) || drawing is null) return;

        var status = await MainThread.InvokeOnMainThreadAsync(() =>
            AndroidRouteMap.Availability(global::Android.App.Application.Context));

        if (Current(owner))
        {
            mapStatus = status;
        }
    }

    private bool Current(Guid owner) => !disposed && mapOwner == owner;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed || mapStatus != RouteMapStatus.WaitingForViewport || observerOwner == mapOwner) return;

        var owner = observerOwner = mapOwner;

        try
        {
            var slot = await RouteMapSlot<HistoryRouteView>.Observe(Js, mapSlot, this, owner);

            if (Current(owner))
            {
                observation = slot;
            }
            else
            {
                await slot.DisposeAsync();
            }
        }
        catch (Exception error) when (error is JSException or OperationCanceledException)
        {
            if (Current(owner))
            {
                mapStatus = RouteMapStatus.Failed;
                StateHasChanged();
            }
        }
    }

    [JSInvokable]
    public async Task PositionMap(Guid owner, double left, double top, double width, double height, double viewportWidth)
    {
        if (!Current(owner) || geometry is not { } selected
            || mapStatus is not (RouteMapStatus.WaitingForViewport or RouteMapStatus.Loading or RouteMapStatus.Ready))
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

    private Task ReportMapStatus(Guid owner, RouteMapStatus status) => InvokeAsync(() =>
    {
        if (!Current(owner)) return;

        mapStatus = status;
        if (status is RouteMapStatus.Failed or RouteMapStatus.Offline) nativeVisible = false;
        StateHasChanged();
    });

    private async Task CloseMap(Guid owner)
    {
        var previous = observation;
        observation = null;

        if (owner != Guid.Empty)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                (Platform.CurrentActivity as MainActivity)?.CloseRouteMap(owner));
        }

        if (previous is not null)
        {
            await previous.DisposeAsync();
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
