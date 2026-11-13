using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using LoopCx.Features.ActiveRun;

namespace LoopCx;

public sealed record RunSnapshot(Guid Id, string Phase, bool Paused, long ActiveSeconds,
    long RemainingSeconds, int Locations, string GpsStatus, string TemplateId, DistanceState? Distance = null, MotionState? Motion = null);

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeConnectedDevice)]
public sealed class RunService : Service, ILocationListener
{
#if DEBUG
    private NativeFeasibility? nativeProbe;

    internal static void Probe(string probe, Guid? expectedRunId, string? address = null, int associationId = -1)
    {
        var context = global::Android.App.Application.Context;
        try
        {
            context.StartForegroundService(new Intent(context, typeof(RunService)).SetAction("native-probe")
                .PutExtra("probe", probe).PutExtra("run-id", expectedRunId?.ToString()).PutExtra("address", address).PutExtra("association-id", associationId));
        }
        catch (Java.Lang.RuntimeException)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33)) NativeFeasibility.ReleaseAssociation(context, associationId);
            if (OperatingSystem.IsAndroidVersionAtLeast(33)) NativeFeasibility.Report("probe:service-start-rejected");
        }
    }
#endif
    public static RunSnapshot? Snapshot { get; private set; }
    public static event Action? Changed;
    private TimedSession? session;
    private RunDistance? distance;
    private RunMotion? motion;
    private RunDistance.CandidateDistance? candidateDistance;
    private Guid id;
    private DateTimeOffset started;
    private long startedMonotonic;
    private string initialPlan = "";
    private CoreTemplate template = CoreTemplate.C1;
    private string templateMetadata = "";
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
    private bool finishLogged;
    private string gpsStatus = "Waiting for GPS";
    private LocationManager? locationManager;
    private const int NotificationId = 1;
    private const string ChannelId = "running";

    public override IBinder? OnBind(Intent? intent) => null;

    public static void Command(string command)
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(RunService)).SetAction(command);
        if (command is "start" or "start-c2") context.StartForegroundService(intent);
        else context.StartService(intent);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var command = intent?.Action;
#if DEBUG
        if (command == "native-probe" && OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var pendingAssociation = intent?.GetIntExtra("association-id", -1) ?? -1;
            // Probes require an existing session and visible-activity entry; never create a hidden workout.
            if (session is null || session.IsFinished || intent?.GetStringExtra("run-id") != id.ToString())
            {
                NativeFeasibility.ReleaseAssociation(this, pendingAssociation);
                NativeFeasibility.Report("probe:requires-active-session");
                if (session is null) StopSelf();
                return StartCommandResult.NotSticky;
            }

            try
            {
                nativeProbe ??= new NativeFeasibility(this);
                if (intent?.GetStringExtra("probe") == "ble" && intent.GetStringExtra("address") is { } address)
                {
                    if (CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) == Permission.Granted)
                    {
                        StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeLocation | ForegroundService.TypeConnectedDevice);
                        nativeProbe.Connect(address, pendingAssociation);
                        pendingAssociation = -1; // The probe now owns cleanup, including failed connection paths.
                    }
                }
                else if (intent?.GetStringExtra("probe") == "speech") nativeProbe.SpeakOnce();
            }
            catch (Java.Lang.RuntimeException)
            {
                NativeFeasibility.Report("probe:permission-or-platform-unavailable");
                nativeProbe?.Stop();
                nativeProbe = null;
            }
            finally
            {
                NativeFeasibility.ReleaseAssociation(this, pendingAssociation);
            }
            return StartCommandResult.NotSticky;
        }
#endif
        if (session is null && command is not ("start" or "start-c2"))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        if (session is null)
        {
            id = Guid.NewGuid();
            template = command == "start-c2" ? CoreTemplate.C2 : CoreTemplate.C1;
            templateMetadata = System.Text.Json.JsonSerializer.Serialize(template);
            session = template.TemplateId == "C2"
                ? TimedSession.Continuous(TimeSpan.FromMinutes(45), 3)
                : CoreTemplate.CreateC1Session();
            started = DateTimeOffset.UtcNow;
            startedMonotonic = SystemClock.ElapsedRealtime();
            distance = new RunDistance(DistancePolicy.Pilot, TimeSpan.FromMilliseconds(startedMonotonic), session.Current?.Kind);
            motion = new RunMotion(MovementPolicy.Pilot);
            motion.Boundary(TimeSpan.FromMilliseconds(startedMonotonic));
            initialPlan = System.Text.Json.JsonSerializer.Serialize(session.Plan);
            lastTick = startedMonotonic;
            var manager = (NotificationManager)GetSystemService(NotificationService)!;
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Running session", NotificationImportance.Low));
            Publish("start");
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
        var phaseBeforeCommand = session.Current?.Kind;
        var effective = command switch
        {
            "pause" => session.Pause(),
            "resume" => session.Resume(),
            "extend" => session.Extend(),
            "skip" => session.SkipPreparation(),
            _ => false
        };
#if DEBUG
        if (command == "complete" && !session.IsFinished)
        {
            session.Resume();
            session.Advance(TimeSpan.FromHours(1), (next, consumed) =>
            {
                var simulatedAt = TimeSpan.FromMilliseconds(lastTick) + consumed;
                distance!.UpdateFreshness(simulatedAt);
                distance.Boundary(next, simulatedAt, session.MainActiveTime);
            });
            effective = true;
        }
#endif
        if (session.Current?.Kind != phaseBeforeCommand)
        {
            distance!.Boundary(session.Current?.Kind, TimeSpan.FromMilliseconds(lastTick), session.MainActiveTime);
            motion!.Boundary(TimeSpan.FromMilliseconds(lastTick));
            candidateDistance = null;
        }
        else if (effective && command is "pause" or "resume")
        {
            distance!.Interrupt(TimeSpan.FromMilliseconds(lastTick), "Manual pause boundary");
            motion!.Boundary(TimeSpan.FromMilliseconds(lastTick));
            candidateDistance = null;
        }
        Publish(effective ? command : null);
        return StartCommandResult.NotSticky;
    }

    private void AdvanceClock()
    {
        var now = SystemClock.ElapsedRealtime();
        if (!session!.IsPaused) distance!.UpdateFreshness(TimeSpan.FromMilliseconds(now));
        motion!.UpdateFreshness(TimeSpan.FromMilliseconds(now));

        if (motion.Candidate is null) candidateDistance = null;

        session!.Advance(TimeSpan.FromMilliseconds(now - lastTick), (next, consumed) =>
        {
            var boundaryAt = TimeSpan.FromMilliseconds(lastTick) + consumed;
            distance!.Boundary(next, boundaryAt, session.MainActiveTime);
            motion.Boundary(boundaryAt);
            candidateDistance = null;
        });
        lastTick = now;
    }

    private void Tick()
    {
        AdvanceClock();
        Publish();
        if (session!.IsFinished) StopSelf();
        else handler!.PostDelayed(tick!, 1000);
    }

    private void Publish(string? command = null, AutomaticMotionEvent? automaticEvent = null)
    {
        if (session!.IsFinished && !finishLogged) command ??= "finish";
        Snapshot = new RunSnapshot(id, session!.Current?.Kind.ToString() ?? "Finished",
            session.IsPaused, (long)session.TotalActiveTime.TotalSeconds,
            (long)Math.Ceiling(session.Remaining.TotalSeconds), locations, gpsStatus, template.TemplateId, distance?.Snapshot, motion?.Snapshot(session.PauseReason));
        ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, BuildNotification());
        journal.Checkpoint(id, started, lastTick - startedMonotonic,
            session, initialPlan, templateMetadata, Snapshot, command, automaticEvent);
#if DEBUG
        if (session.IsFinished && OperatingSystem.IsAndroidVersionAtLeast(33)) nativeProbe?.Stop();
#endif
        finishLogged |= session.IsFinished;
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
        if (session is null || session.IsFinished || session.PauseReason == PauseReason.Manual) return;

        journal.Location(id, location);
        AdvanceClock();
        var receivedAt = TimeSpan.FromMilliseconds(lastTick);
        var fix = new DistanceFix(TimeSpan.FromMilliseconds(location.ElapsedRealtimeNanos / 1_000_000),
            location.Latitude, location.Longitude, location.HasAccuracy ? location.Accuracy : null);
        var moved = !session.IsPaused && distance!.Observe(fix, receivedAt, session.MainActiveTime);
        var decision = motion!.Observe(fix, receivedAt, session.PhaseIndex, session.Current?.Kind, session.PauseReason, moved);
        AutomaticMotionEvent? automaticEvent = null;

        if (decision == MotionDecision.CandidateStarted)
        {
            // Capture after this onset fix, preserving the genuine edge leading into it.
            candidateDistance = distance!.CaptureCandidate();
        }
        else if (decision == MotionDecision.Pause && candidateDistance is { } candidate &&
            session.PauseAutomatically(TimeSpan.FromSeconds(10), motion.Event!.PhaseIndex))
        {
            automaticEvent = motion.Event;
            distance!.ConfirmStandstill(candidate, receivedAt,
                receivedAt > automaticEvent!.ExcludedThroughMonotonicTime);
            candidateDistance = null;
        }
        else if (decision == MotionDecision.Resume && session.ResumeAutomatically())
        {
            automaticEvent = motion.Event;
            distance!.Reanchor(receivedAt, "Automatic resume boundary");
            motion.Boundary(receivedAt);
            candidateDistance = null;
        }
        else if (motion.Candidate is null)
        {
            candidateDistance = null;
        }

        locations++;
        gpsStatus = location.HasAccuracy ? $"GPS ±{location.Accuracy:0} m" : "GPS accuracy unknown";
        Publish(automaticEvent?.Kind, automaticEvent);
    }

    public void OnProviderDisabled(string provider) { gpsStatus = "GPS disabled"; distance?.Unavailable("GPS disabled"); motion?.Unavailable(); candidateDistance = null; Publish(); }
    public void OnProviderEnabled(string provider) { gpsStatus = "Waiting for GPS"; Publish(); }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    public override void OnDestroy()
    {
#if DEBUG
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) nativeProbe?.Stop();
#endif
        if (tick is not null) handler?.RemoveCallbacks(tick);
        locationManager?.RemoveUpdates(this);
        StopForeground(StopForegroundFlags.Remove);

        Snapshot = null;
        Changed?.Invoke();

        base.OnDestroy();
    }
}
