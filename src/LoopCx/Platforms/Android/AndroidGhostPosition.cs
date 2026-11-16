using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using LoopCx.Features.GhostRun;

namespace LoopCx;

// One cancellable actual GPS acquisition for the explicit Start action; no UI timer or workout owner.
internal sealed class AndroidGhostPosition : Java.Lang.Object, ILocationListener
{
    private readonly LocationManager manager;
    private readonly long requested = SystemClock.ElapsedRealtime();
    private readonly TaskCompletionSource<GhostStartPosition> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AndroidGhostPosition(LocationManager manager) => this.manager = manager;

    internal static async Task<GhostStartPosition> Acquire(CancellationToken cancellation)
    {
        var context = global::Android.App.Application.Context;
        if (context.CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) != Permission.Granted
            || context.GetSystemService(Context.LocationService) is not LocationManager manager
            || !manager.IsProviderEnabled(LocationManager.GpsProvider))
            throw new InvalidDataException("GPS uncertain. Allow precise location and move to the route start.");
        using var owner = new AndroidGhostPosition(manager);
        using var registration = cancellation.Register(() => owner.completion.TrySetCanceled(cancellation));
        try
        {
            manager.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 0, owner, Looper.MainLooper!);
            return await owner.completion.Task;
        }
        finally { manager.RemoveUpdates(owner); }
    }
    public void OnLocationChanged(global::Android.Locations.Location location)
    {
        if (location.ElapsedRealtimeNanos / 1000000 < requested) return;
        completion.TrySetResult(new(requested, location.ElapsedRealtimeNanos / 1000000, location.Time,
            location.Latitude, location.Longitude, location.HasAccuracy ? location.Accuracy : null));
    }
    public void OnProviderDisabled(string provider) => completion.TrySetException(new InvalidDataException("GPS unavailable. The race has not started."));
    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
}
