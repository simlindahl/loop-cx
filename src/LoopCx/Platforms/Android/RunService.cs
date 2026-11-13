using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using LoopCx.Features.ActiveRun;

namespace LoopCx;

public sealed record RunSnapshot(Guid Id, string Phase, bool Paused, long ActiveSeconds,
    long RemainingSeconds, int Locations, string GpsStatus);

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class RunService : Service, ILocationListener
{
    public static RunSnapshot? Snapshot { get; private set; }
    public static event Action? Changed;
    private TimedSession? session;
    private Guid id;
    private DateTimeOffset started;
    private long startedMonotonic;
    private string initialPlan = "";
    private RunJournal journal = null!;

    public override void OnCreate()
    {
        base.OnCreate();
        journal = RunJournal.Shared;
    }
    private Handler? handler;
    private Action? tick;
    private long lastTick;
    private int locations;
    private string gpsStatus = "Waiting for GPS";
    private LocationManager? locationManager;
    private const int NotificationId = 1;
    private const string ChannelId = "running";

    public override IBinder? OnBind(Intent? intent) => null;

    public static void Command(string command)
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(RunService)).SetAction(command);
        if (command == "start") context.StartForegroundService(intent);
        else context.StartService(intent);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var command = intent?.Action;
        if (session is null && command != "start")
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        if (session is null)
        {
            id = Guid.NewGuid();
            // Core C1 template revision 1; the stored favorite will supply this plan.
            session = TimedSession.Intervals(5, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(2),
                7, 2, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));
            started = DateTimeOffset.UtcNow;
            startedMonotonic = SystemClock.ElapsedRealtime();
            initialPlan = System.Text.Json.JsonSerializer.Serialize(session.Plan);
            lastTick = startedMonotonic;
            var manager = (NotificationManager)GetSystemService(NotificationService)!;
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Running session", NotificationImportance.Low));
            Publish();
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeLocation);
            else StartForeground(NotificationId, BuildNotification());
            locationManager = (LocationManager)GetSystemService(LocationService)!;
            try
            {
                locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 0, this, Looper.MainLooper!);
            }
            catch (Java.Lang.SecurityException) { gpsStatus = "Location permission required"; }
            catch (Java.Lang.IllegalArgumentException) { gpsStatus = "GPS unavailable"; }
            handler = new Handler(Looper.MainLooper!);
            tick = Tick;
            handler.PostDelayed(tick, 1000);
        }
        AdvanceClock();
        switch (command)
        {
            case "pause": session.Pause(); break;
            case "resume": session.Resume(); break;
            case "extend": session.Extend(); break;
            case "skip": session.SkipPreparation(); break;
#if DEBUG
            case "complete": session.Resume(); session.Advance(TimeSpan.FromHours(1)); break;
#endif
        }
        Publish();
        return StartCommandResult.NotSticky;
    }

    private void AdvanceClock()
    {
        var now = SystemClock.ElapsedRealtime();
        session!.Advance(TimeSpan.FromMilliseconds(now - lastTick));
        lastTick = now;
    }

    private void Tick()
    {
        AdvanceClock();
        Publish();
        if (session!.IsFinished) StopSelf();
        else handler!.PostDelayed(tick!, 1000);
    }

    private void Publish()
    {
        Snapshot = new RunSnapshot(id, session!.Current?.Kind.ToString() ?? "Finished",
            session.IsPaused, (long)session.TotalActiveTime.TotalSeconds,
            (long)Math.Ceiling(session.Remaining.TotalSeconds), locations, gpsStatus);
        ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, BuildNotification());
        journal.Checkpoint(id, started, SystemClock.ElapsedRealtime() - startedMonotonic,
            session, initialPlan, Snapshot);
        Changed?.Invoke();
#if DEBUG
        global::Android.Util.Log.Info("LoopRun", System.Text.Json.JsonSerializer.Serialize(Snapshot));
#endif
    }

    private Notification BuildNotification()
    {
        var open = new Intent(this, typeof(MainActivity));
        var pendingOpen = PendingIntent.GetActivity(this, 0, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var state = Snapshot!;
        var builder = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle(state.Paused ? "Paused" : state.Phase)
            .SetContentText($"{state.RemainingSeconds}s remaining · {state.GpsStatus}")
            .SetContentIntent(pendingOpen)
            .SetVisibility(NotificationVisibility.Public)
            .SetOngoing(true);
        var command = state.Paused ? "resume" : "pause";
        builder.AddAction(new Notification.Action.Builder(null, state.Paused ? "Resume" : "Pause",
            PendingIntent.GetService(this, 1, new Intent(this, typeof(RunService)).SetAction(command),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        if (session!.Current?.Kind is RunPhase.Warmup or RunPhase.Cooldown)
        {
            builder.AddAction(new Notification.Action.Builder(null, "+1 min",
                PendingIntent.GetService(this, 2, new Intent(this, typeof(RunService)).SetAction("extend"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
            builder.AddAction(new Notification.Action.Builder(null,
                session.Current.Kind == RunPhase.Warmup ? "Start main run" : "Finish",
                PendingIntent.GetService(this, 3, new Intent(this, typeof(RunService)).SetAction("skip"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        }
        return builder.Build();
    }

    public void OnLocationChanged(global::Android.Locations.Location location)
    {
        if (session is null || session.IsFinished || session.IsPaused) return;
        journal.Location(id, location);
        locations++;
        gpsStatus = location.HasAccuracy ? $"GPS ±{location.Accuracy:0} m" : "GPS accuracy unknown";
        Publish();
    }

    public void OnProviderDisabled(string provider) { gpsStatus = "GPS disabled"; Publish(); }
    public void OnProviderEnabled(string provider) { gpsStatus = "Waiting for GPS"; Publish(); }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    public override void OnDestroy()
    {
        if (tick is not null) handler?.RemoveCallbacks(tick);
        locationManager?.RemoveUpdates(this);
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }
}
