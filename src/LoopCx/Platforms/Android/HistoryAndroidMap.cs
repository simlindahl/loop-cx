using Android.Content;
using Android.Content.PM;
using Android.Gms.Common;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.OS;
using Android.Views;
using Android.Widget;
using LoopCx.Features.History;

namespace LoopCx;

internal enum HistoryMapStatus { NotConfigured, Offline, WaitingForViewport, Loading, Failed, Ready }

internal readonly record struct HistoryMapViewport(double Left, double Top, double Width, double Height, double ViewportWidth)
{
    public bool IsVisible => Width > 0 && Height > 0 && ViewportWidth > 0;
}

internal sealed class HistoryAndroidMap : Java.Lang.Object, IOnMapReadyCallback, GoogleMap.IOnMapLoadedCallback
{
    private const string GoogleKeyMetadata = "com.google.android.geo.API_KEY";
    private readonly AndroidMapFrame frame;
    private readonly MapView mapView;
    private readonly Handler handler = new(Looper.MainLooper!);
    private readonly HistoryRouteGeometry geometry;
    private readonly Action<HistoryMapStatus> report;
    private GoogleMap? map;
    private bool started;
    private bool resumed;
    private bool closed;
    private bool positioned = true;

    public HistoryAndroidMap(MainActivity activity, FrameLayout parent, FrameLayout.LayoutParams bounds,
        HistoryRouteGeometry geometry, Action<HistoryMapStatus> report)
    {
        this.geometry = geometry;
        this.report = report;
        frame = new(activity) { Alpha = 0 };
        var options = new GoogleMapOptions().InvokeLiteMode(true).InvokeMapToolbarEnabled(false);
        mapView = new(activity, options);
        frame.AddView(mapView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        parent.AddView(frame, bounds);

        try
        {
            mapView.OnCreate(null);
            Start();
            Resume();
            _ = handler.PostDelayed(LoadTimedOut, 8000);
            mapView.GetMapAsync(this);
        }
        catch (Java.Lang.RuntimeException)
        {
            Close();
            throw;
        }
    }

    public static HistoryMapStatus Availability(Context context)
    {
        if (!HasConfiguredKey(context))
        {
            return HistoryMapStatus.NotConfigured;
        }

        try
        {
            if (Connectivity.Current.NetworkAccess is NetworkAccess.None or NetworkAccess.Local)
            {
                return HistoryMapStatus.Offline;
            }
        }
        catch (Exception error) when (error is PermissionException or Java.Lang.SecurityException)
        {
            // Loading is still bounded by the SDK completion timeout when connectivity cannot be queried.
        }

        try
        {
            return GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(context) == 0
                ? HistoryMapStatus.WaitingForViewport : HistoryMapStatus.Failed;
        }
        catch (Java.Lang.RuntimeException)
        {
            return HistoryMapStatus.Failed;
        }
    }

    private static bool HasConfiguredKey(Context context)
    {
        var manager = context.PackageManager;
        var package = context.PackageName;

        if (manager is null || string.IsNullOrEmpty(package))
        {
            return false;
        }

        try
        {
            var info = OperatingSystem.IsAndroidVersionAtLeast(33)
                ? manager.GetApplicationInfo(package, PackageManager.ApplicationInfoFlags.Of((long)PackageInfoFlags.MetaData))
                : manager.GetApplicationInfo(package, PackageInfoFlags.MetaData);
            var value = info?.MetaData?.GetString(GoogleKeyMetadata);

            return !string.IsNullOrWhiteSpace(value) && !value.Contains("${", StringComparison.Ordinal);
        }
        catch (Exception error) when (error is PackageManager.NameNotFoundException or Java.Lang.ClassCastException)
        {
            return false;
        }
    }

    public static FrameLayout.LayoutParams? Position(MainActivity activity, FrameLayout parent, HistoryMapViewport viewport)
    {
        if (!viewport.IsVisible || activity.HistoryMapWebView is not { Width: > 0 } webView)
        {
            return null;
        }

        using var visible = new global::Android.Graphics.Rect();
        using var parentVisible = new global::Android.Graphics.Rect();
        using var webOrigin = new global::Android.Graphics.Point();
        using var parentOrigin = new global::Android.Graphics.Point();

        if (!webView.GetGlobalVisibleRect(visible, webOrigin) || visible.IsEmpty
            || !parent.GetGlobalVisibleRect(parentVisible, parentOrigin))
        {
            return null;
        }

        var pixelsPerCssUnit = webView.Width / viewport.ViewportWidth;
        var left = webOrigin.X + (int)Math.Round(viewport.Left * pixelsPerCssUnit);
        var top = webOrigin.Y + (int)Math.Round(viewport.Top * pixelsPerCssUnit);
        var width = (int)Math.Round(viewport.Width * pixelsPerCssUnit);
        var height = (int)Math.Round(viewport.Height * pixelsPerCssUnit);

        if (width < 80 || height < 80 || !visible.Contains(left, top, left + width, top + height))
        {
            return null;
        }

        return new(width, height)
        {
            Gravity = GravityFlags.Top | GravityFlags.Left,
            LeftMargin = left - parentOrigin.X,
            TopMargin = top - parentOrigin.Y
        };
    }

    public void Reposition(FrameLayout.LayoutParams? position)
    {
        if (closed) return;

        if (position is not null)
        {
            frame.LayoutParameters = position;
        }

        positioned = position is not null;
        frame.Visibility = positioned && resumed ? ViewStates.Visible : ViewStates.Invisible;
    }

    public void OnMapReady(GoogleMap googleMap)
    {
        if (closed) return;

        map = googleMap;

        try
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
                    _ = googleMap.AddMarker(new MarkerOptions().SetPosition(coordinates[0]));
                }
                else
                {
                    _ = googleMap.AddPolyline(new PolylineOptions().InvokeColor(unchecked((int)0xffc4da73)).InvokeWidth(5).Add(coordinates));
                }
            }

            if (first is null || frame.LayoutParameters is not { Width: > 0, Height: > 0 } bounds)
            {
                Fail();
                return;
            }

            googleMap.MoveCamera(unique
                ? CameraUpdateFactory.NewLatLngBounds(camera.Build(), bounds.Width, bounds.Height, 32)
                : CameraUpdateFactory.NewLatLngZoom(first, 16));
            googleMap.SetOnMapLoadedCallback(this);
        }
        catch (Java.Lang.RuntimeException)
        {
            Fail();
        }
    }

    public void OnMapLoaded()
    {
        if (closed) return;

        handler.RemoveCallbacks(LoadTimedOut);
        frame.Alpha = 1;
        frame.Visibility = positioned && resumed ? ViewStates.Visible : ViewStates.Invisible;
        report(HistoryMapStatus.Ready);
    }

    public void Start()
    {
        if (closed || started) return;

        mapView.OnStart();
        started = true;
    }

    public void Resume()
    {
        if (closed || resumed) return;

        mapView.OnResume();
        resumed = true;
    }

    public void Pause()
    {
        if (!resumed) return;

        frame.Visibility = ViewStates.Invisible;
        mapView.OnPause();
        resumed = false;
    }

    public void Stop()
    {
        if (!started) return;

        mapView.OnStop();
        started = false;
    }

    public void Save(Bundle state)
    {
        if (!closed) mapView.OnSaveInstanceState(state);
    }

    public void Trim()
    {
        if (!closed) mapView.OnLowMemory();
    }

    public void Close()
    {
        if (closed) return;

        closed = true;
        handler.RemoveCallbacks(LoadTimedOut);

        try
        {
            map?.SetOnMapLoadedCallback(null);
            Pause();
            Stop();
            mapView.OnDestroy();
        }
        catch (Java.Lang.RuntimeException)
        {
            // A failed SDK view must not keep an overlay or obstruct the local History destination.
        }
        finally
        {
            (frame.Parent as FrameLayout)?.RemoveView(frame);
        }
    }

    private void LoadTimedOut()
    {
        if (!closed) Fail();
    }

    private void Fail()
    {
        Close();
        report(HistoryMapStatus.Failed);
    }

    private sealed class AndroidMapFrame : FrameLayout
    {
        public AndroidMapFrame(Context context) : base(context)
        {
            ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        }

        public override bool DispatchTouchEvent(MotionEvent? e) => false;
    }
}
