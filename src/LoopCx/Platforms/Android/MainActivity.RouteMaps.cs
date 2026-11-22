using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.OS;
using Android.Widget;
using LoopCx.Features.History;

namespace LoopCx;

public sealed partial class MainActivity
{
    private AndroidRouteMap? routeMap;
    private Guid? routeMapOwner;
    private bool routeMapForeground;
    private bool routeMapLive;
    private bool resumeNeedsPlacement;

    internal global::Android.Webkit.WebView? RouteMapWebView { get; set; }

    internal bool PositionHistoryMap(Guid owner, RouteMapViewport viewport, HistoryRouteGeometry geometry,
        Action<RouteMapStatus> report)
    {
        return PositionRouteMap(owner, viewport, live: false,
            (map, bounds) => DrawHistoryRoute(map, bounds, geometry), update: null, report);
    }

    internal bool PositionLiveRouteMap(Guid owner, RouteMapViewport viewport,
        Action<GoogleMap, FrameLayout.LayoutParams> prepare,
        Func<GoogleMap, FrameLayout.LayoutParams, bool> update, Action<RouteMapStatus> report)
    {
        return PositionRouteMap(owner, viewport, live: true, prepare, update, report);
    }

    private bool PositionRouteMap(Guid owner, RouteMapViewport viewport, bool live,
        Action<GoogleMap, FrameLayout.LayoutParams> prepare, Func<GoogleMap, FrameLayout.LayoutParams, bool>? update,
        Action<RouteMapStatus> report)
    {
        var parent = FindViewById<FrameLayout>(global::Android.Resource.Id.Content);
        var position = routeMapForeground && parent is not null ? AndroidRouteMap.Position(this, parent, viewport) : null;

        if (position is null)
        {
            if (routeMapOwner == owner)
            {
                if (routeMapLive) CloseRouteMap();
                else _ = routeMap?.Reposition(null);
            }

            return false;
        }

        if (routeMapOwner == owner)
        {
            return routeMap?.Reposition(position) == true;
        }

        CloseRouteMap();

        var availability = AndroidRouteMap.Availability(this);
        if (availability != RouteMapStatus.WaitingForViewport)
        {
            report(availability);
            return false;
        }

        try
        {
            routeMapOwner = owner;
            routeMapLive = live;
            report(RouteMapStatus.Loading);
            var created = new AndroidRouteMap(this, parent!, position, lite: !live, prepare, update, status =>
            {
                if (routeMapOwner != owner) return;

                if (status == RouteMapStatus.Failed)
                {
                    routeMap = null;
                    routeMapOwner = null;
                }

                report(status);
            });

            if (routeMapOwner == owner)
            {
                routeMap = created;
                return true;
            }

            created.Close();
            return false;
        }
        catch (Java.Lang.RuntimeException)
        {
            CloseRouteMap();
            report(RouteMapStatus.Failed);
            return false;
        }
    }

    internal bool RefreshRouteMap(Guid owner)
    {
        return routeMapOwner == owner && routeMap?.Refresh() == true;
    }

    internal void CloseRouteMap(Guid owner)
    {
        if (routeMapOwner == owner) CloseRouteMap();
    }

    private void CloseRouteMap()
    {
        routeMap?.Close();
        routeMap = null;
        routeMapOwner = null;
        routeMapLive = false;
    }

    private void ResumeRouteMap()
    {
        routeMapForeground = true;
        var needsPlacement = resumeNeedsPlacement || routeMap is not null;
        resumeNeedsPlacement = false;

        if (!needsPlacement)
        {
            return;
        }

        routeMap?.Resume();

        try
        {
            RouteMapWebView?.EvaluateJavascript("window.dispatchEvent(new Event('loop-history-map-resume'));", null);
        }
        catch (Java.Lang.RuntimeException)
        {
            // Keep the local route visible when the WebView cannot revalidate placement.
        }
    }

    protected override void OnStart()
    {
        base.OnStart();
        routeMap?.Start();
    }

    protected override void OnPause()
    {
        routeMapForeground = false;

        if (routeMapLive)
        {
            resumeNeedsPlacement = true;
            CloseRouteMap();
        }
        else
        {
            routeMap?.Pause();
        }

        base.OnPause();
    }

    protected override void OnStop()
    {
        routeMap?.Stop();
        base.OnStop();
    }

    public override void OnLowMemory()
    {
        routeMap?.Trim();
        base.OnLowMemory();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        // Give the SDK its lifecycle callback without retaining an entire Google view or viewport across reopening.
        using var transient = new Bundle();
        routeMap?.Save(transient);
        base.OnSaveInstanceState(outState);
    }

    private static void DrawHistoryRoute(GoogleMap map, FrameLayout.LayoutParams bounds, HistoryRouteGeometry geometry)
    {
        var camera = new LatLngBounds.Builder();
        LatLng? first = null;
        var unique = false;

        foreach (var segment in geometry.Segments)
        {
            var coordinates = segment.Select(static point => new LatLng(point.LatitudeDegrees, point.LongitudeDegrees)).ToArray();

            foreach (var point in coordinates)
            {
                first ??= point;
                unique |= !first.Equals(point);
                _ = camera.Include(point);
            }

            if (coordinates.Length == 1)
            {
                _ = map.AddMarker(new MarkerOptions().SetPosition(coordinates[0]));
            }
            else
            {
                _ = map.AddPolyline(new PolylineOptions().InvokeColor(unchecked((int)0xffc4da73)).InvokeWidth(5).Add(coordinates));
            }
        }

        if (first is not null)
        {
            map.MoveCamera(unique
                ? CameraUpdateFactory.NewLatLngBounds(camera.Build(), bounds.Width, bounds.Height, 32)
                : CameraUpdateFactory.NewLatLngZoom(first, 16));
        }
    }
}
