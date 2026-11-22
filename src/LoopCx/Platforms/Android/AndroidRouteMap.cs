using Android.Content;
using Android.Content.PM;
using Android.Gms.Common;
using Android.Gms.Maps;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace LoopCx;

internal enum RouteMapStatus { NotConfigured, Offline, WaitingForViewport, Loading, Failed, Ready }

internal readonly record struct RouteMapViewport(double Left, double Top, double Width, double Height, double ViewportWidth)
{
    public bool IsVisible => Width > 0 && Height > 0 && ViewportWidth > 0;
}

internal sealed class AndroidRouteMap : Java.Lang.Object, IOnMapReadyCallback, GoogleMap.IOnMapLoadedCallback
{
    private const string GoogleKeyMetadata = "com.google.android.geo.API_KEY";
    private readonly AndroidMapFrame frame;
    private readonly MapView mapView;
    private readonly Handler handler = new(Looper.MainLooper!);
    private readonly Action<GoogleMap, FrameLayout.LayoutParams> prepare;
    private readonly Func<GoogleMap, FrameLayout.LayoutParams, bool>? update;
    private readonly Action<RouteMapStatus> report;
    private GoogleMap? map;
    private bool started;
    private bool resumed;
    private bool closed;
    private bool positioned = true;
    private bool loaded;
    private bool readyReported;

    public AndroidRouteMap(MainActivity activity, FrameLayout parent, FrameLayout.LayoutParams bounds, bool lite,
        Action<GoogleMap, FrameLayout.LayoutParams> prepare, Func<GoogleMap, FrameLayout.LayoutParams, bool>? update,
        Action<RouteMapStatus> report)
    {
        this.prepare = prepare;
        this.update = update;
        this.report = report;
        frame = new(activity) { Alpha = 0 };
        var options = new GoogleMapOptions().InvokeLiteMode(lite).InvokeMapToolbarEnabled(false)
            .InvokeZoomControlsEnabled(false).InvokeCompassEnabled(false);
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

    public static RouteMapStatus Availability(Context context)
    {
        if (!HasConfiguredKey(context))
        {
            return RouteMapStatus.NotConfigured;
        }

        try
        {
            if (Connectivity.Current.NetworkAccess is NetworkAccess.None or NetworkAccess.Local)
            {
                return RouteMapStatus.Offline;
            }
        }
        catch (Exception error) when (error is PermissionException or Java.Lang.SecurityException)
        {
            // Loading is still bounded by the SDK completion timeout when connectivity cannot be queried.
        }

        try
        {
            return GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(context) == 0
                ? RouteMapStatus.WaitingForViewport : RouteMapStatus.Failed;
        }
        catch (Java.Lang.RuntimeException)
        {
            return RouteMapStatus.Failed;
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

    public static FrameLayout.LayoutParams? Position(MainActivity activity, FrameLayout parent, RouteMapViewport viewport)
    {
        if (!viewport.IsVisible || activity.RouteMapWebView is not { Width: > 0 } webView)
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

    public bool Reposition(FrameLayout.LayoutParams? position)
    {
        if (closed) return false;

        if (position is not null)
        {
            frame.LayoutParameters = position;
        }

        positioned = position is not null;
        return Refresh();
    }

    public void OnMapReady(GoogleMap googleMap)
    {
        if (closed) return;

        map = googleMap;

        try
        {
            if (frame.LayoutParameters is not FrameLayout.LayoutParams { Width: > 0, Height: > 0 } bounds)
            {
                Fail();
                return;
            }

            googleMap.UiSettings?.SetAllGesturesEnabled(false);
            prepare(googleMap, bounds);
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
        loaded = true;
        Refresh();
    }

    public bool Refresh()
    {
        if (closed) return false;

        var show = positioned && resumed;

        try
        {
            if (show && loaded && map is { } rendered && update is not null
                && frame.LayoutParameters is FrameLayout.LayoutParams bounds)
            {
                show = update(rendered, bounds);
            }

            frame.Visibility = show ? ViewStates.Visible : ViewStates.Invisible;
            frame.Alpha = loaded ? 1 : 0;

            if (show && loaded && !readyReported)
            {
                readyReported = true;
                report(RouteMapStatus.Ready);
            }

            return show;
        }
        catch (Java.Lang.RuntimeException)
        {
            Fail();
            return false;
        }
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

        positioned = false;
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
        report(RouteMapStatus.Failed);
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
