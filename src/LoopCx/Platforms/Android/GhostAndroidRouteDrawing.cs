using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
using Android.Widget;
using LoopCx.Features.GhostRun;
using LoopCx.Features.GhostRun.Ui;
using Paint = Android.Graphics.Paint;

namespace LoopCx;

internal readonly record struct GhostMapFrame(GhostRouteMarkers Markers, bool Overview);

internal sealed class GhostAndroidRouteDrawing
{
    private readonly GhostRoute route;
    private readonly GhostRouteDrawing drawing;
    private readonly Func<GhostMapFrame?> latest;
    private readonly float density;
    private Marker? runner;
    private Marker? opponent;
    private BitmapDescriptor? runnerIcon;
    private BitmapDescriptor? opponentIcon;
    private LatLngBounds? routeBounds;
    private int width;
    private int height;
    private bool? overview;
    private LatLng? followed;
    private float followZoom;
    private GhostMapFrame? lastFrame;

    public GhostAndroidRouteDrawing(GhostRoute route, GhostRouteDrawing drawing, float density, Func<GhostMapFrame?> latest)
    {
        this.route = route;
        this.drawing = drawing;
        this.density = density;
        this.latest = latest;
    }

    public bool OpponentOutside { get; private set; }

    public void Prepare(GoogleMap map, FrameLayout.LayoutParams viewport)
    {
        var vertices = route.Geometry.Select(static point => new LatLng(point.Latitude, point.Longitude)).ToArray();
        var builder = new LatLngBounds.Builder();

        foreach (var vertex in vertices)
        {
            _ = builder.Include(vertex);
        }

        routeBounds = builder.Build();
        _ = map.AddPolyline(new PolylineOptions().InvokeColor(unchecked((int)0xffc4da73)).InvokeWidth(5).Add(vertices));
        _ = map.AddMarker(Options(vertices[0], CreateIcon(Icon.Start), 0, .5f, .5f));
        _ = map.AddMarker(Options(vertices[^1], CreateIcon(Icon.Finish), 0, .5f, .75f));

        var direction = route.LengthMetres * .3;
        if (drawing.CoordinateAt(direction) is { } indicator)
        {
            _ = map.AddMarker(Options(Position(indicator), CreateIcon(Icon.Direction), 0, .5f, .5f)
                .SetRotation((float)drawing.HeadingAt(direction)));
        }

        runnerIcon = CreateIcon(Icon.Runner);
        opponentIcon = CreateIcon(Icon.Opponent);
        runner = null;
        opponent = null;
        width = 0;
        height = 0;
        overview = null;
        followed = null;
        lastFrame = null;
        OpponentOutside = false;
        _ = Update(map, viewport);
    }

    public bool Update(GoogleMap map, FrameLayout.LayoutParams viewport)
    {
        if (latest() is not { } state)
        {
            return false;
        }

        if (lastFrame == state && width == viewport.Width && height == viewport.Height)
        {
            return true;
        }

        var runnerPoint = drawing.CoordinateAt(state.Markers.Runner);
        var opponentPoint = drawing.CoordinateAt(state.Markers.Opponent);
        var heading = state.Markers.Runner is { } metres ? (float)drawing.HeadingAt(metres) : 0;
        UpdateMarker(map, ref runner, runnerPoint, runnerIcon, zIndex: 2, heading);
        UpdateMarker(map, ref opponent, opponentPoint, opponentIcon, zIndex: 1, heading: 0);
        UpdateCamera(map, viewport, state.Overview, runnerPoint);
        OpponentOutside = !state.Overview && runnerPoint is not null && opponentPoint is { } ghost
            && map.Projection?.VisibleRegion?.LatLngBounds is { } visible && !visible.Contains(Position(ghost));
        lastFrame = state;

        return true;
    }

    private void UpdateCamera(GoogleMap map, FrameLayout.LayoutParams viewport, bool showOverview, GhostRouteCoordinate? runnerPoint)
    {
        var resized = width != viewport.Width || height != viewport.Height;
        var fitRoute = showOverview || runnerPoint is null;

        if (resized || (fitRoute && overview != true))
        {
            var bounds = routeBounds!;
            var first = route.Geometry[0];
            var update = bounds.Southwest!.Equals(bounds.Northeast)
                ? CameraUpdateFactory.NewLatLngZoom(new(first.Latitude, first.Longitude), 16)
                : CameraUpdateFactory.NewLatLngBounds(bounds, viewport.Width, viewport.Height, 32);
            map.MoveCamera(update);
            followZoom = Math.Min(map.MaxZoomLevel, map.CameraPosition!.Zoom + 1f);
            width = viewport.Width;
            height = viewport.Height;
        }

        if (!fitRoute && runnerPoint is { } point)
        {
            var target = Position(point);

            if (resized || overview != false || !target.Equals(followed))
            {
                map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(target, followZoom));
                followed = target;
            }
        }

        overview = fitRoute;
    }

    private static void UpdateMarker(GoogleMap map, ref Marker? marker, GhostRouteCoordinate? point,
        BitmapDescriptor? icon, float zIndex, float heading)
    {
        if (point is not { } coordinate)
        {
            if (marker is not null) marker.Visible = false;
            return;
        }

        var position = Position(coordinate);
        if (marker is null)
        {
            var requiredIcon = icon ?? throw new Java.Lang.IllegalStateException("Route marker is unavailable.");
            marker = map.AddMarker(Options(position, requiredIcon, zIndex, .5f, .5f).SetRotation(heading));
            return;
        }

        marker.Position = position;
        marker.Rotation = heading;
        marker.Visible = true;
    }

    private static LatLng Position(GhostRouteCoordinate point)
    {
        return new(point.Latitude, point.Longitude);
    }

    private static MarkerOptions Options(LatLng position, BitmapDescriptor icon, float zIndex, float anchorX, float anchorY)
    {
        return new MarkerOptions().SetPosition(position).SetIcon(icon).Anchor(anchorX, anchorY).InvokeZIndex(zIndex);
    }

    private enum Icon { Start, Finish, Direction, Runner, Opponent }

    private BitmapDescriptor CreateIcon(Icon shape)
    {
        var pixels = (int)Math.Round(40 * density);
        using var bitmap = Bitmap.CreateBitmap(pixels, pixels, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap!);
        canvas.Scale(density, density);
        using var paint = new Paint(PaintFlags.AntiAlias);
        using var path = new global::Android.Graphics.Path();
        var dark = new global::Android.Graphics.Color(unchecked((int)0xff111111));
        var accent = new global::Android.Graphics.Color(unchecked((int)0xffc4da73));

        if (shape is Icon.Start or Icon.Opponent)
        {
            paint.Color = shape == Icon.Start ? dark : new global::Android.Graphics.Color(255, 255, 255, 204);
            var radius = shape == Icon.Start ? 8 : 14;
            canvas.DrawCircle(20, 20, radius, paint);
            paint.Color = shape == Icon.Start ? global::Android.Graphics.Color.White : dark;
            paint.StrokeWidth = 3;
            paint.SetStyle(Paint.Style.Stroke);
            canvas.DrawCircle(20, 20, radius, paint);
        }
        else
        {
            if (shape == Icon.Finish)
            {
                path.MoveTo(20, 31);
                path.LineTo(20, 9);
                path.LineTo(35, 9);
                path.LineTo(35, 21);
                path.LineTo(20, 21);
            }
            else
            {
                var size = shape == Icon.Runner ? 12 : 7;
                path.MoveTo(20, 20 - size);
                path.LineTo(20 - size, 20 + size);
                path.LineTo(20 + size, 20 + size);
                path.Close();
            }

            paint.Color = shape == Icon.Finish ? global::Android.Graphics.Color.White : accent;
            canvas.DrawPath(path, paint);
            paint.Color = dark;
            paint.StrokeWidth = 2;
            paint.SetStyle(Paint.Style.Stroke);
            canvas.DrawPath(path, paint);
        }

        return BitmapDescriptorFactory.FromBitmap(bitmap!) ?? throw new Java.Lang.IllegalStateException("Route marker is unavailable.");
    }
}
