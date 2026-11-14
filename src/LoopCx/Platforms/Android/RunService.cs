using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using LoopCx.Features.ActiveRun;

namespace LoopCx;

public sealed record RunSnapshot(Guid Id, string Phase, bool Paused, long ActiveSeconds,
    long RemainingSeconds, int Locations, string GpsStatus, string TemplateId, DistanceState? Distance = null, MotionState? Motion = null, RunStatisticsState? Statistics = null, CoreTemplate? FrozenTemplate = null,
    int? TargetRpe = null, int? WorkRepetition = null, int? WorkRepetitions = null, HeartRateState? HeartRate = null);

[Property("android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE", Value = "User-started ongoing timed workout phases and visible controls while screen is locked, when GPS is unavailable or declined.")]
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeConnectedDevice | ForegroundService.TypeSpecialUse)]
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
    public static string? StartError { get; private set; }

    public static void Start(CoreStartRequest request)
    {
        var context = global::Android.App.Application.Context;
        context.StartForegroundService(new Intent(context, typeof(RunService)).SetAction("start-core")
            .PutExtra("core-start", System.Text.Json.JsonSerializer.Serialize(request)));
    }

    public static RunSnapshot? Snapshot { get; private set; }
    public static event Action? Changed;
    private TimedSession? session;
    private RunDistance? distance;
    private RunMotion? motion;
    private RunHeartRate? heartRate;
    private AndroidHeartRateSource? heartRateSource;
    private HeartRateConfiguration heartRateConfiguration = HeartRateConfiguration.Empty;
    private string? heartRateMetadata;
    private readonly RunStatistics statistics = new();
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
    private bool locationSubscribed;
    private bool locationAvailable;
    private bool connectedDeviceRequested;
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
                        StartEligibleForeground(connectedDevice: true);
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
        if (session is null && command is not ("start" or "start-c2" or "start-core"))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        if (session is null)
        {
            try
            {
                var preferences = CorePreferences.Load();
                var request = command == "start-core"
                    ? System.Text.Json.JsonSerializer.Deserialize<CoreStartRequest>(intent?.GetStringExtra("core-start") ?? "")
                        ?? throw new ArgumentException("Choose an official Core workout.")
                    : command == "start-c2" ? preferences.C2Selection : preferences.SelectedC1;
                if (request.ShortcutId is { } shortcutId)
                {
                    var shortcut = preferences.Shortcuts.SingleOrDefault(item => item.Id == shortcutId)
                        ?? throw new ArgumentException("The saved shortcut is no longer available.");
                    request = shortcut.Request with { ShortcutId = shortcut.Id, ShortcutName = shortcut.Name };
                }

                var resolved = CoreTemplate.Resolve(request, preferences.WarmupCooldown);
                var savedHeartRate = HeartRatePreferences.Load();
                heartRateConfiguration = savedHeartRate.Configuration with { Zones = Array.AsReadOnly(savedHeartRate.Configuration.Zones.ToArray()) };
                heartRate = new RunHeartRate(heartRateConfiguration, savedHeartRate.ConfigurationError);
                heartRateMetadata = System.Text.Json.JsonSerializer.Serialize(savedHeartRate with { Configuration = heartRateConfiguration });
                session = resolved.Session;
                template = resolved.Template;
                templateMetadata = System.Text.Json.JsonSerializer.Serialize(template);
                StartError = null;
            }
            catch (Exception error) when (error is ArgumentException or System.Text.Json.JsonException or InvalidDataException)
            {
                StartError = error is System.Text.Json.JsonException
                    ? "The selected Core workout could not be read. Choose an official protocol and try again." : error.Message;
                Changed?.Invoke();
                StopSelf();

                return StartCommandResult.NotSticky;
            }

            id = Guid.NewGuid();
            started = DateTimeOffset.UtcNow;
            startedMonotonic = SystemClock.ElapsedRealtime();
            distance = new RunDistance(DistancePolicy.Pilot, TimeSpan.FromMilliseconds(startedMonotonic), session.Current?.Kind);
            motion = new RunMotion(MovementPolicy.Pilot with
            {
                MaximumSpeedMetresPerSecond = DistancePolicy.Pilot.MaximumSpeedMetresPerSecond
            }, TimeSpan.FromMilliseconds(startedMonotonic));
            motion.Boundary(TimeSpan.FromMilliseconds(startedMonotonic));
            initialPlan = System.Text.Json.JsonSerializer.Serialize(session.Plan);
            lastTick = startedMonotonic;
            var manager = (NotificationManager)GetSystemService(NotificationService)!;
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Running session", NotificationImportance.Low));
            Snapshot = CreateSnapshot();

            try { StartEligibleForeground(); }
            catch (Java.Lang.RuntimeException)
            {
                StartError = "The workout service could not start. Your existing sessions and Settings have been preserved.";
                session = null;
                Snapshot = null;
                Changed?.Invoke();
                StopSelf();

                return StartCommandResult.NotSticky;
            }

            Publish("start");
            RefreshLocationSubscription();
            handler = new Handler(Looper.MainLooper!);
            tick = Tick;
            handler.PostDelayed(tick, 1000);
            StartHeartRateSource();
        }
        AdvanceClock();
#if DEBUG
        if (command == "hr-source-failure" && Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64" && !session.IsFinished)
        {
            // Isolated lifecycle fault setup: exercise the same owner cleanup used after native/FGS failures.
            StopHeartRateSource();
            ReceiveHeartRate(id, new(TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100), DateTimeOffset.UtcNow, null, "synthetic-source-failure"));

            return StartCommandResult.NotSticky;
        }
#endif
        if (command == "refresh-sensors" && !session.IsFinished)
        {
            RefreshLocationSubscription();
            if (heartRateConfiguration.DeviceAddress is { } sourceAddress)
            {
                try
                {
                    if (heartRateSource is null)
                    {
                        StartHeartRateSource();
                    }
                    else
                    {
                        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) == Permission.Granted)
                            StartEligibleForeground(connectedDevice: true);
                        heartRateSource.Refresh(sourceAddress);
                    }
                }
                catch (Java.Lang.RuntimeException)
                {
                    StopHeartRateSource();
                    ReceiveHeartRate(id, new(TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100), DateTimeOffset.UtcNow, null, "foreground-or-permission-unavailable"));
                }
            }
        }

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
            var simulatedCursor = TimeSpan.FromMilliseconds(lastTick);
            var simulatedPhase = session.Current?.Kind;
            // Use the actual remaining finite plan, including runtime extensions, rather than a fixed cap.
            // Reaching every remaining boundary (including finish) marks all artificial pieces synthetic.
            var remainingPlan = session.Plan.Skip(session.PhaseIndex + 1)
                .Aggregate(session.Remaining, static (remaining, phase) => remaining + phase.Duration);
            session.Advance(remainingPlan, (next, consumed) =>
            {
                var simulatedAt = TimeSpan.FromMilliseconds(lastTick) + consumed;
                statistics.Advance(simulatedCursor, simulatedAt, simulatedPhase, false, synthetic: true);
                simulatedCursor = simulatedAt;
                simulatedPhase = next;
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

        var cursor = TimeSpan.FromMilliseconds(lastTick);
        var phase = session.Current?.Kind;
        var paused = session.IsPaused;
        session.Advance(TimeSpan.FromMilliseconds(now - lastTick), (next, consumed) =>
        {
            var boundaryAt = TimeSpan.FromMilliseconds(lastTick) + consumed;
            statistics.Advance(cursor, boundaryAt, phase, paused);
            cursor = boundaryAt;
            phase = next;
            distance!.Boundary(next, boundaryAt, session.MainActiveTime);
            motion.Boundary(boundaryAt);
            candidateDistance = null;
        });
        statistics.Advance(cursor, TimeSpan.FromMilliseconds(now), phase, paused);
        lastTick = now;
    }

    private void Tick()
    {
        AdvanceClock();
        Publish();
        if (session!.IsFinished) StopSelf();
        else handler!.PostDelayed(tick!, 1000);
    }

    private void Publish(string? command = null, AutomaticMotionEvent? automaticEvent = null, HeartRateObservation? heartRateObservation = null)
    {
        if (session!.IsFinished && !finishLogged) command ??= "finish";
        Snapshot = CreateSnapshot();
        ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, BuildNotification());
        if (session.IsFinished) StopHeartRateSource();
        journal.Checkpoint(id, started, lastTick - startedMonotonic,
            session, initialPlan, templateMetadata, Snapshot, command, automaticEvent, heartRateMetadata, heartRateObservation);
#if DEBUG
        if (session.IsFinished && OperatingSystem.IsAndroidVersionAtLeast(33)) nativeProbe?.Stop();
#endif
        finishLogged |= session.IsFinished;
        Changed?.Invoke();
#if DEBUG
        global::Android.Util.Log.Info("LoopRun", System.Text.Json.JsonSerializer.Serialize(Snapshot));
#endif
    }

    private RunSnapshot CreateSnapshot() => new(id, session!.Current?.Kind.ToString() ?? "Finished",
        session.IsPaused, (long)session.TotalActiveTime.TotalSeconds,
        (long)Math.Ceiling(session.Remaining.TotalSeconds), locations, gpsStatus, template.TemplateId, distance?.Snapshot,
        motion?.Snapshot(session.PauseReason), statistics.Snapshot(TimeSpan.FromMilliseconds(lastTick - startedMonotonic),
            distance!.Snapshot, session.Current?.Kind is RunPhase.Work or RunPhase.Recovery), template,
        session.Current?.TargetRpe, session.Completed.Count(static phase => phase.Plan.Kind == RunPhase.Work)
            + (session.Current?.Kind == RunPhase.Work ? 1 : 0), session.Plan.Count(static phase => phase.Kind == RunPhase.Work), heartRate?.Snapshot(TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100)));

    private void StopHeartRateSource()
    {
        heartRateSource?.Stop();
        heartRateSource = null;
    }

    private void StartHeartRateSource()
    {
        if (session is null || session.IsFinished || heartRateConfiguration.DeviceAddress is not { } address) return;

        var expectedRunId = id;

        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(31) || CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) == Permission.Granted)
                StartEligibleForeground(connectedDevice: true);

            heartRateSource = new AndroidHeartRateSource(this, handler!, heartRateConfiguration,
                observation => ReceiveHeartRate(expectedRunId, observation));
            heartRateSource.Connect(address);
        }
        catch (Java.Lang.RuntimeException)
        {
            StopHeartRateSource();
            ReceiveHeartRate(expectedRunId, new(TimeSpan.FromMilliseconds(SystemClock.ElapsedRealtime()), DateTimeOffset.UtcNow, null, "foreground-or-permission-unavailable"));
        }
    }

    private void ReceiveHeartRate(Guid expectedRunId, HeartRateObservation observation)
    {
        if (session is null || session.IsFinished || expectedRunId != id) return;

        AdvanceClock();
        statistics.ObserveHeartRate(heartRate!.Observe(observation));
        Publish(heartRateObservation: observation);
    }

    private bool HasLocationPermission() =>
        CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) == Permission.Granted ||
        CheckSelfPermission(global::Android.Manifest.Permission.AccessCoarseLocation) == Permission.Granted;

    private bool CanUseLocation()
    {
        var granted = CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) == Permission.Granted ||
            CheckSelfPermission(global::Android.Manifest.Permission.AccessCoarseLocation) == Permission.Granted;
        var manager = (LocationManager?)GetSystemService(LocationService);

        return granted && manager is not null && manager.IsProviderEnabled(LocationManager.GpsProvider) &&
            (!OperatingSystem.IsAndroidVersionAtLeast(28) || manager.IsLocationEnabled);
    }

    private void StartEligibleForeground(bool connectedDevice = false, bool timerOnly = false)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(NotificationId, BuildNotification());

            return;
        }

        connectedDeviceRequested |= connectedDevice;
        var type = !timerOnly && CanUseLocation() ? ForegroundService.TypeLocation : OperatingSystem.IsAndroidVersionAtLeast(34)
            ? ForegroundService.TypeSpecialUse : ForegroundService.TypeNone;

        if (connectedDeviceRequested && OperatingSystem.IsAndroidVersionAtLeast(31) && CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) == Permission.Granted)
            type |= ForegroundService.TypeConnectedDevice;

        StartForeground(NotificationId, BuildNotification(), type);
    }

    private void RefreshLocationSubscription()
    {
        var available = false;
        locationManager ??= (LocationManager?)GetSystemService(LocationService);

        try
        {
            StartEligibleForeground();
            available = CanUseLocation();

            if (!HasLocationPermission() && locationSubscribed)
            {
                try { locationManager?.RemoveUpdates(this); }
                catch (Java.Lang.SecurityException) { }
                locationSubscribed = false;
            }

            // A granted listener also receives provider enable/disable callbacks while GPS is off.
            if (HasLocationPermission() && !locationSubscribed && locationManager is not null)
            {
                locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 0, this, Looper.MainLooper!);
                locationSubscribed = true;
            }
        }
        catch (Java.Lang.RuntimeException)
        {
            available = false;
            try { StartEligibleForeground(timerOnly: true); }
            catch (Java.Lang.RuntimeException) { }
        }

        if (!available)
        {
            gpsStatus = "GPS unavailable · timer continues; allow location in Android settings";

            if (locationAvailable)
            {
                distance?.Unavailable("GPS unavailable");
                motion?.Unavailable();
                candidateDistance = null;
            }
        }
        else if (!locationAvailable)
        {
            var restoredAt = TimeSpan.FromMilliseconds(SystemClock.ElapsedRealtime());
            distance?.Reanchor(restoredAt, "GPS restored");
            motion?.Boundary(restoredAt);
            candidateDistance = null;
            gpsStatus = "Waiting for GPS";
        }

        locationAvailable = available;
    }

    private Notification BuildNotification()
    {
        var open = new Intent(this, typeof(MainActivity));
        var pendingOpen = PendingIntent.GetActivity(this, 0, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var state = Snapshot!;
        var builder = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle(state.Paused
                ? (session!.PauseReason == PauseReason.Automatic ? "Autopaused" : "Manually paused")
                : state.Phase)
            .SetContentText(state.Paused
                ? (session!.PauseReason == PauseReason.Automatic ? "Move to resume automatically." : "Use Resume to continue.")
                : $"{state.RemainingSeconds}s remaining · {state.GpsStatus}")
            .SetContentIntent(pendingOpen)
            .SetVisibility(NotificationVisibility.Public)
            .SetOngoing(true);
        if (state.Paused) builder.SetSubText($"{state.Phase} · {state.RemainingSeconds}s remaining");

        var command = state.Paused ? "resume" : "pause";
        builder.AddAction(new Notification.Action.Builder(null, state.Paused ? "Resume" : "Pause",
            PendingIntent.GetService(this, 1, new Intent(this, typeof(RunService)).SetAction(command),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        if (session!.PauseReason == PauseReason.Automatic)
        {
            builder.AddAction(new Notification.Action.Builder(null, "Pause manually",
                PendingIntent.GetService(this, 4, new Intent(this, typeof(RunService)).SetAction("pause"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        }

        if (!state.Paused && session!.Current?.Kind is RunPhase.Warmup or RunPhase.Cooldown)
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
        if (!locationAvailable)
        {
            locations++;
            Publish();

            return;
        }

        var receivedAt = TimeSpan.FromMilliseconds(lastTick);
        var fix = new DistanceFix(TimeSpan.FromMilliseconds(location.ElapsedRealtimeNanos / 1_000_000),
            location.Latitude, location.Longitude, location.HasAccuracy ? location.Accuracy : null);
        var moved = !session.IsPaused && distance!.Observe(fix, receivedAt, session.MainActiveTime);
        if (!session.IsPaused) statistics.Observe(distance!.Observation);

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
            statistics.Exclude(automaticEvent!.OnsetMonotonicTime!.Value, automaticEvent.ExcludedThroughMonotonicTime!.Value);
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

    public void OnProviderDisabled(string provider)
    {
        if (session is null || session.IsFinished) return;

        RefreshLocationSubscription();
        Publish();
    }

    public void OnProviderEnabled(string provider)
    {
        if (session is null || session.IsFinished) return;

        RefreshLocationSubscription();
        Publish();
    }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    public override void OnDestroy()
    {
#if DEBUG
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) nativeProbe?.Stop();
#endif
        StopHeartRateSource();
        if (tick is not null) handler?.RemoveCallbacks(tick);
        try { locationManager?.RemoveUpdates(this); }
        catch (Java.Lang.SecurityException) { }
        StopForeground(StopForegroundFlags.Remove);

        Snapshot = null;
        Changed?.Invoke();

        base.OnDestroy();
    }
}
