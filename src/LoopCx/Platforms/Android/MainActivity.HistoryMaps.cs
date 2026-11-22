using Android.OS;
using Android.Widget;
using LoopCx.Features.History;

namespace LoopCx;

public sealed partial class MainActivity
{
    private HistoryAndroidMap? historyMap;
    private Guid? historyMapOwner;
    private bool historyMapForeground;

    internal global::Android.Webkit.WebView? HistoryMapWebView { get; set; }

    internal bool PositionHistoryMap(Guid owner, HistoryMapViewport viewport, HistoryRouteGeometry geometry,
        Action<HistoryMapStatus> report)
    {
        var parent = FindViewById<FrameLayout>(global::Android.Resource.Id.Content);
        var position = historyMapForeground && parent is not null ? HistoryAndroidMap.Position(this, parent, viewport) : null;

        if (position is null)
        {
            if (historyMapOwner == owner) historyMap?.Reposition(null);

            return false;
        }

        if (historyMapOwner == owner)
        {
            historyMap?.Reposition(position);
            return historyMap is not null;
        }

        CloseHistoryMap();

        var availability = HistoryAndroidMap.Availability(this);
        if (availability != HistoryMapStatus.WaitingForViewport)
        {
            report(availability);
            return false;
        }

        try
        {
            historyMapOwner = owner;
            report(HistoryMapStatus.Loading);
            var created = new HistoryAndroidMap(this, parent!, position, geometry, status =>
            {
                if (historyMapOwner != owner) return;

                if (status == HistoryMapStatus.Failed)
                {
                    historyMap = null;
                    historyMapOwner = null;
                }

                report(status);
            });

            if (historyMapOwner == owner)
            {
                historyMap = created;
                return true;
            }

            created.Close();
            return false;
        }
        catch (Java.Lang.RuntimeException)
        {
            CloseHistoryMap();
            report(HistoryMapStatus.Failed);
            return false;
        }
    }

    internal void CloseHistoryMap(Guid owner)
    {
        if (historyMapOwner == owner) CloseHistoryMap();
    }

    private void CloseHistoryMap()
    {
        historyMap?.Close();
        historyMap = null;
        historyMapOwner = null;
    }

    private void ResumeHistoryMap()
    {
        historyMapForeground = true;

        if (historyMap is null)
        {
            return;
        }

        historyMap.Resume();

        try
        {
            HistoryMapWebView?.EvaluateJavascript("window.dispatchEvent(new Event('loop-history-map-resume'));", null);
        }
        catch (Java.Lang.RuntimeException)
        {
            // Keep the local route visible when the WebView cannot revalidate placement.
        }
    }

    protected override void OnStart()
    {
        base.OnStart();
        historyMap?.Start();
    }

    protected override void OnPause()
    {
        historyMapForeground = false;
        historyMap?.Pause();
        base.OnPause();
    }

    protected override void OnStop()
    {
        historyMap?.Stop();
        base.OnStop();
    }

    public override void OnLowMemory()
    {
        historyMap?.Trim();
        base.OnLowMemory();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        // Give the SDK its lifecycle callback without retaining an entire Google view or viewport across reopening.
        using var transient = new Bundle();
        historyMap?.Save(transient);
        base.OnSaveInstanceState(outState);
    }
}
