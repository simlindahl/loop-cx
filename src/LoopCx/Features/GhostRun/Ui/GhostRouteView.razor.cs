using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LoopCx.Features.GhostRun.Ui;

public partial class GhostRouteView
{
    [Parameter, EditorRequired] public GhostRoute Route { get; set; } = null!;
    [Parameter] public GhostRaceSnapshot? Race { get; set; }
    [Parameter] public bool Active { get; set; } = true;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private GhostRouteDrawing drawing = null!;
    private GhostRoute? previousRoute;
    private GhostRouteMarkers markers;
    private RouteDrawingPoint? runnerPoint;
    private RouteDrawingPoint? ghostPoint;
    private RouteDrawingPoint direction = null!;
    private bool overview;
    private bool rendered;
    private bool disposed;

    private ElementReference mapSlot;
    private Guid mapOwner;
    private Guid observerOwner;
    private RouteMapSlot<GhostRouteView>? observation;
    private RouteMapStatus mapStatus;
    private GhostAndroidRouteDrawing? nativeDrawing;
    private bool nativeVisible;
    private bool slotVisible;
    private bool nativeOpponentOutside;

    private bool Live => Race is not null;
    private bool MapVisible => mapStatus == RouteMapStatus.Ready && nativeVisible;
    private bool GhostOutside => MapVisible ? nativeOpponentOutside : !overview && ghostPoint is { } far && runnerPoint is { } near
        && (Math.Abs(far.X - near.X) > 80 || Math.Abs(far.Y - near.Y) > 60);
    private string ViewBox => !Live || overview || runnerPoint is null ? "0 0 320 240"
        : string.Create(CultureInfo.InvariantCulture, $"{runnerPoint.X - 80} {runnerPoint.Y - 60} 160 120");

    private string BackgroundStatus => MapVisible ? "Google map background · local route and positions."
        : mapStatus == RouteMapStatus.Loading && nativeVisible ? "Local route view · background map loading."
        : "Local route view · background map unavailable.";

    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(previousRoute, Route))
        {
            await RetireMap();
            previousRoute = Route;
            drawing = new(Route);
            direction = drawing.PointAt(Route.LengthMetres * .3)!;
        }

        if (!Active && rendered)
        {
            await RetireMap();
            return;
        }

        markers = GhostRouteMarkers.From(Route, Race);
        runnerPoint = drawing.PointAt(markers.Runner);
        ghostPoint = drawing.PointAt(markers.Opponent);

        if (!Active || Race is not { Clock.State: GhostClockState.Running } race || race.Route != Route.Key)
        {
            await RetireMap();
            return;
        }

        if (mapOwner == Guid.Empty)
        {
            var owner = mapOwner = Guid.NewGuid();
            var status = await MainThread.InvokeOnMainThreadAsync(() =>
                AndroidRouteMap.Availability(global::Android.App.Application.Context));

            if (Current(owner))
            {
                mapStatus = status;
            }
        }
    }

    protected override bool ShouldRender()
    {
        return !rendered || Active;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        rendered = true;
        var owner = mapOwner;

        if (!Current(owner) || owner == Guid.Empty)
        {
            return;
        }

        if (mapStatus == RouteMapStatus.WaitingForViewport && observerOwner != owner)
        {
            observerOwner = owner;

            try
            {
                var slot = await RouteMapSlot<GhostRouteView>.Observe(Js, mapSlot, this, owner);

                if (Current(owner)) observation = slot;
                else await slot.DisposeAsync();
            }
            catch (Exception error) when (error is JSException or OperationCanceledException)
            {
                await ReportMapStatus(owner, RouteMapStatus.Failed);
            }
        }
        else if (slotVisible && mapStatus == RouteMapStatus.Ready)
        {
            var visible = await MainThread.InvokeOnMainThreadAsync(() =>
                Current(owner) && Platform.CurrentActivity is MainActivity activity && activity.RefreshRouteMap(owner));
            await UpdateVisibility(owner, visible, nativeDrawing?.OpponentOutside == true);
        }
    }

    [JSInvokable]
    public async Task PositionMap(Guid owner, double left, double top, double width, double height, double viewportWidth)
    {
        if (!Current(owner) || Race is not { } race
            || mapStatus is not (RouteMapStatus.WaitingForViewport or RouteMapStatus.Loading or RouteMapStatus.Ready))
        {
            return;
        }

        var run = race.Id;
        var key = Route.Key;
        var viewport = new RouteMapViewport(left, top, width, height, viewportWidth);
        slotVisible = viewport.IsVisible;

        var visible = await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (LatestFrame(owner, run, key) is null || Platform.CurrentActivity is not MainActivity activity)
            {
                return false;
            }

            var renderer = nativeDrawing ??= new(Route, drawing, activity.Resources?.DisplayMetrics?.Density ?? 1,
                () => LatestFrame(owner, run, key));
            return activity.PositionLiveRouteMap(owner, viewport, renderer.Prepare, renderer.Update,
                status => _ = ReportMapStatus(owner, status));
        });

        await UpdateVisibility(owner, visible, nativeDrawing?.OpponentOutside == true);
    }

    private GhostMapFrame? LatestFrame(Guid owner, Guid run, RouteKey route)
    {
        if (!Current(owner) || Race?.Id != run || Route.Key != route
            || RunService.Snapshot is not { Ghost: { Clock.State: GhostClockState.Running } current, GhostRoute: { } currentRoute }
            || current.Id != run || current.Route != route || currentRoute.Key != route)
        {
            return null;
        }

        return new(GhostRouteMarkers.From(Route, current), overview);
    }

    private bool Current(Guid owner)
    {
        return !disposed && Active && mapOwner == owner;
    }

    private Task ReportMapStatus(Guid owner, RouteMapStatus status) => InvokeAsync(() =>
    {
        if (!Current(owner)) return;

        mapStatus = status;
        if (status is RouteMapStatus.Offline or RouteMapStatus.Failed) nativeVisible = false;
        StateHasChanged();
    });

    private Task UpdateVisibility(Guid owner, bool visible, bool outside) => InvokeAsync(() =>
    {
        if (!Current(owner) || nativeVisible == visible && nativeOpponentOutside == outside) return;

        nativeVisible = visible;
        nativeOpponentOutside = outside;
        StateHasChanged();
    });

    private async Task RetireMap()
    {
        var owner = mapOwner;
        var previous = observation;
        mapOwner = Guid.Empty;
        observation = null;
        nativeDrawing = null;
        nativeVisible = false;
        slotVisible = false;
        nativeOpponentOutside = false;
        mapStatus = RouteMapStatus.NotConfigured;

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

    private void ToggleOverview()
    {
        overview = !overview;
    }

    private static string Transform(RouteDrawingPoint point, double degrees)
    {
        return string.Create(CultureInfo.InvariantCulture, $"translate({point.Position}) rotate({degrees})");
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await RetireMap();
        GC.SuppressFinalize(this);
    }
}
