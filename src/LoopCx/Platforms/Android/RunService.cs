using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;

namespace LoopCx;

public sealed record RunSnapshot(Guid Id, string Phase, bool Paused, long ActiveSeconds,
    long? RemainingSeconds, int Locations, string GpsStatus, string? TemplateId, DistanceState? Distance = null, MotionState? Motion = null, RunStatisticsState? Statistics = null, CoreTemplate? FrozenTemplate = null,
    int? TargetRpe = null, int? WorkRepetition = null, int? WorkRepetitions = null, HeartRateState? HeartRate = null, LivePaceState? LivePace = null,
    RunSaveStatus? Storage = null)
{
    [System.Text.Json.Serialization.JsonIgnore] public string ActivityKind { get; init; } = "core";
    [System.Text.Json.Serialization.JsonIgnore] public GhostRaceSnapshot? Ghost { get; init; }
}

[Property("android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE", Value = "User-started ongoing timed workout phases and visible controls while screen is locked, when GPS is unavailable or declined.")]
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeConnectedDevice | ForegroundService.TypeSpecialUse)]
public sealed partial class RunService : Service, ILocationListener
{
    private AndroidRunAudio? audio;
    private RunAudioPolicy? audioPolicy;
    private readonly RunDeviation deviation = new();
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
    internal static void ReportStorageOpenFailure()
    {
        StartError = "Local storage could not be opened. Training has not started.";
        Changed?.Invoke();
    }

    public static void Start(CoreStartRequest request)
    {
        var context = global::Android.App.Application.Context;
        context.StartForegroundService(new Intent(context, typeof(RunService)).SetAction("start-core")
            .PutExtra("core-start", System.Text.Json.JsonSerializer.Serialize(request)));
    }

    public static RunSnapshot? Snapshot { get; private set; }
    public static event Action? Changed;
    public static void DismissSaved()
    {
        if (Snapshot?.Storage?.State != "saved") return;
        Snapshot = null;
        Changed?.Invoke();
    }
    private TimedSession? session;
    private RunDistance? distance;
    private RunMotion? motion;
    private RunHeartRate? heartRate;
    private AndroidHeartRateSource? heartRateSource;
    private HeartRateConfiguration heartRateConfiguration = HeartRateConfiguration.Empty;
    private string? heartRateMetadata;
    private readonly RunStatistics statistics = new();
    private readonly RunPaceTrend paceTrend = new();
    private readonly RunLivePace livePace = new(LivePacePolicy.Pilot);
    private RunDistance.CandidateDistance? candidateDistance;
    private Guid id;
    private DateTimeOffset started;
    private long startedMonotonic;
    private RunStartContext? startContext;
    private Guid? healthConnectConsentRevision;
    private RunTerminalContext? terminalContext;
    private readonly List<PhaseBoundaryFact> phaseBoundaries = [];
    private string initialPlan = "";
    private CoreTemplate template = CoreTemplate.C1;
    private string templateMetadata = "";
    private RunJournal journal = null!;
    private RunRouteCapture? routeCapture;
    private readonly RunSaveBuffer saveBuffer = new();
    private long saveRevision;
    private bool terminalSaveCaptured;
    private bool finishExportScheduled;
    private bool journalAvailable;

    public override void OnCreate()
    {
        base.OnCreate();
        try { journal = RunJournal.Shared; journalAvailable = true; }
        catch (Exception error) when (error is IOException or global::Android.Database.Sqlite.SQLiteException)
        {
            ReportStorageOpenFailure();
        }
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
        if (!journalAvailable)
        {
            CompleteGhostStart(intent, "Local storage could not be opened. The race has not started.");
            Changed?.Invoke();
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        var command = intent?.Action;
        if (command == "start-ghost" || ghost is not null) return HandleGhostCommand(intent, command);
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

                var effectiveAudio = preferences.AudioFor(request);
                var resolved = CoreTemplate.Resolve(request with { Audio = effectiveAudio }, preferences.WarmupCooldown);
                var savedHeartRate = HeartRatePreferences.Load();
                heartRateConfiguration = savedHeartRate.Configuration with { Zones = Array.AsReadOnly(savedHeartRate.Configuration.Zones.ToArray()) };
                heartRate = new RunHeartRate(heartRateConfiguration, savedHeartRate.ConfigurationError);
                heartRateMetadata = System.Text.Json.JsonSerializer.Serialize(savedHeartRate with { Configuration = heartRateConfiguration });
                session = resolved.Session;
                template = resolved.Template with { Audio = effectiveAudio };
                audioPolicy = new(effectiveAudio);
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
            routeCapture = new(id);
            handler = new Handler(Looper.MainLooper!);
            audio = new(handler, id);
            (started, healthConnectConsentRevision) = HealthConnectSettings.FreezeStart();
            startedMonotonic = SystemClock.ElapsedRealtime();
            var startZone = TimeZoneInfo.Local;
            startContext = new(startZone.Id, (int)startZone.GetUtcOffset(started).TotalSeconds, startedMonotonic);
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
            tick = Tick;
            handler.PostDelayed(tick, 1000);
            StartHeartRateSource();
        }
        if (!session.IsFinished) AdvanceClock();
#if DEBUG
        if (command is "storage-failure-on" or "storage-failure-off" && Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64")
            journal.SetSaveFailureForProbe(id, command == "storage-failure-on");
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
                distance!.UpdateFreshness(simulatedAt);
                RecordPhaseBoundary(simulatedPhase, next, simulatedAt, synthetic: true);
                simulatedPhase = next;
            });
            effective = true;
        }
#endif
        if (session.Current?.Kind != phaseBeforeCommand)
        {
            RecordPhaseBoundary(phaseBeforeCommand, session.Current?.Kind, TimeSpan.FromMilliseconds(lastTick));
            motion!.Boundary(TimeSpan.FromMilliseconds(lastTick));
            candidateDistance = null;
        }
        else if (effective && command is "pause" or "resume")
        {
            livePace.Reset(session.IsPaused ? "paused" : "acquiring");
            paceTrend.Reset(TimeSpan.FromMilliseconds(lastTick));
            distance!.Interrupt(TimeSpan.FromMilliseconds(lastTick), "Manual pause boundary");
            routeCapture?.Break("Manual pause boundary");
            motion!.Boundary(TimeSpan.FromMilliseconds(lastTick));
            candidateDistance = null;
        }
        Publish(effective ? command : null, explicitRetry: command == "retry-saving");
        return StartCommandResult.NotSticky;
    }

    private void AdvanceClock()
    {
        var now = SystemClock.ElapsedRealtime();
        if (!session!.IsPaused)
        {
            distance!.UpdateFreshness(TimeSpan.FromMilliseconds(now));
            if (!distance.Observation.Valid && !distance.Observation.Ignored)
            {
                livePace.Reset();
                routeCapture?.Break(distance.Snapshot.Status);
            }
            if (!distance.Observation.Valid && !distance.Observation.Ignored) paceTrend.Reset(TimeSpan.FromMilliseconds(lastTick));
        }
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
            RecordPhaseBoundary(phase, next, boundaryAt);
            phase = next;
            motion.Boundary(boundaryAt);
            candidateDistance = null;
        });
        statistics.Advance(cursor, TimeSpan.FromMilliseconds(now), phase, paused);
        lastTick = now;
    }

    private void RecordPhaseBoundary(RunPhase? previous, RunPhase? next, TimeSpan at, bool synthetic = false)
    {
        distance!.Boundary(next, at, session!.MainActiveTime);

        if (!(previous is RunPhase.Work or RunPhase.Recovery && next is RunPhase.Work or RunPhase.Recovery))
            routeCapture?.Break("Phase boundary");
        livePace.Boundary(previous, next);
        paceTrend.Boundary(at, session.PhaseIndex);

        if (previous is RunPhase.Work or RunPhase.Recovery && next is not (RunPhase.Work or RunPhase.Recovery))
        {
            statistics.FinalizeMain(distance.Snapshot);
        }

        // The complete prefix survives failed checkpoints; serializing it again never appends a duplicate.
        var nextIndex = session.PhaseIndex;
        if (previous is not null && (phaseBoundaries.Count == 0 || phaseBoundaries[^1].PreviousPhaseIndex < nextIndex - 1))
        {
            var effectiveElapsed = (long)at.TotalMilliseconds - startedMonotonic;
            var boundary = new PhaseBoundaryFact(nextIndex - 1, next is null ? null : nextIndex, previous.Value, next,
                effectiveElapsed, SystemClock.ElapsedRealtime() - startedMonotonic, DateTimeOffset.UtcNow,
                started.AddMilliseconds(effectiveElapsed), synthetic);
            phaseBoundaries.Add(boundary);
            if (next is null)
            {
                try { terminalContext = RunTerminalContext.Capture(boundary, TimeZoneInfo.FindSystemTimeZoneById(Java.Util.TimeZone.Default!.ID!)); }
                catch (Exception problem) when (problem is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    terminalContext = null; // Unknown remains unknown; never retry using a later delivery-time zone.
                }
            }
        }
    }

    private void Tick()
    {
        if (ghost is not null) { TickGhost(); return; }
        if (!session!.IsFinished) AdvanceClock();
        Publish();
        if (!session.IsFinished) handler!.PostDelayed(tick!, 1000);
    }

    private void Publish(string? command = null, AutomaticMotionEvent? automaticEvent = null, HeartRateObservation? heartRateObservation = null, bool explicitRetry = false)
    {
        if (session!.IsFinished && !finishLogged) command ??= "finish";
        if (session.IsFinished) StopHeartRateSource();
        if (!terminalSaveCaptured)
        {
            var snapshot = CreateSnapshot();
            var revision = ++saveRevision;
            var checkpoint = System.Text.Json.JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                SaveRevision = revision,
                Snapshot = snapshot,
                AutomaticEvent = automaticEvent,
                StartContext = startContext,
                HealthConnectConsentRevision = healthConnectConsentRevision,
                PhaseBoundaries = phaseBoundaries,
                TerminalContext = terminalContext,
                session.Plan,
                session.Completed,
                session.PhaseActiveTime,
                session.TotalActiveTime,
                session.MainActiveTime
            });
            saveBuffer.Capture(new(id, revision, started, DateTimeOffset.UtcNow, lastTick - startedMonotonic,
                session.IsFinished, initialPlan, templateMetadata, heartRateMetadata, checkpoint), command, heartRateObservation);
            terminalSaveCaptured = session.IsFinished;
        }
        saveBuffer.Save(journal.Commit, explicitRetry);
        Snapshot = CreateSnapshot() with { Storage = saveBuffer.Status };
        PublishAudio(Snapshot);
        ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, BuildNotification());
#if DEBUG
        if (session.IsFinished && OperatingSystem.IsAndroidVersionAtLeast(33)) nativeProbe?.Stop();
#endif
        finishLogged |= session.IsFinished && saveBuffer.Status.State == "saved";
        Changed?.Invoke();
#if DEBUG
        global::Android.Util.Log.Info("LoopRun", System.Text.Json.JsonSerializer.Serialize(Snapshot));
#endif
        if (session.IsFinished && saveBuffer.Status.State == "saved")
        {
            if (!finishExportScheduled) { finishExportScheduled = true; HealthConnectScheduling.Resume(); }
            StopSelf();
        }
    }

    private DeviationObservation DeviationInput()
    {
        var now = TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100);
        return new(session!.PhaseIndex, distance!.Snapshot.Splits.Count + 1, session.Current?.Kind,
            session.IsPaused, session.TotalActiveTime, now, template.Targets,
            livePace.Snapshot(now, session.IsPaused, session.IsFinished), heartRate?.Snapshot(now));
    }

    private void PublishAudio(RunSnapshot state)
    {
        if (audioPolicy is null || audio is null) return;
        if (state.Paused || session!.IsFinished) audio.Cancel();
        var cue = audioPolicy.Observe(session!.PhaseIndex, session.Current?.Kind, state.Paused,
            session.MainActiveTime, state.Statistics!.Main.MeasuredMetres);
        var coaching = deviation.Observe(DeviationInput());
        audio.ValidateCurrent();

        if (cue?.PhasePriority == true)
        {
            audio.Speak(RunAudioText.Phase(session.Current?.Kind, session.Remaining,
                state.WorkRepetition ?? 0, state.WorkRepetitions ?? 0), AudioCueKind.Phase);
        }
        else if (coaching is not null)
        {
            if (audio.Speak(coaching.Text, AudioCueKind.Coaching, () => deviation.IsCurrent(coaching, DeviationInput())))
                deviation.Attempted(coaching);
        }
        else if (cue is not null)
        {
            audio.Speak(RunAudioText.Summary(state.Statistics.Main, state.Statistics.CurrentSplit,
                state.LivePace?.SecondsPerKilometre, state.HeartRate?.CurrentBpm), AudioCueKind.Summary);
        }
    }

    private RunSnapshot CreateSnapshot() => new(id, session!.Current?.Kind.ToString() ?? "Finished",
        session.IsPaused, (long)session.TotalActiveTime.TotalSeconds,
        (long)Math.Ceiling(session.Remaining.TotalSeconds), locations, gpsStatus, template.TemplateId, distance?.Snapshot,
        motion?.Snapshot(session.PauseReason), statistics.Snapshot(TimeSpan.FromMilliseconds(lastTick - startedMonotonic),
            distance!.Snapshot, session.Current?.Kind is RunPhase.Work or RunPhase.Recovery), template,
        session.Current?.TargetRpe, session.Completed.Count(static phase => phase.Plan.Kind == RunPhase.Work)
            + (session.Current?.Kind == RunPhase.Work ? 1 : 0), session.Plan.Count(static phase => phase.Kind == RunPhase.Work), heartRate?.Snapshot(TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100)),
        LivePaceSnapshot());

    private LivePaceState LivePaceSnapshot()
    {
        var now = TimeSpan.FromMilliseconds(lastTick);
        var live = livePace.Snapshot(now, session!.IsPaused, session.IsFinished);
        return live with { Trend = paceTrend.Snapshot(now, live, session.IsPaused, session.IsFinished) };
    }

    private void StopHeartRateSource()
    {
        heartRateSource?.Stop();
        heartRateSource = null;
    }

    private void StartHeartRateSource()
    {
        if ((ghost is null ? session is null || session.IsFinished : ghost.Ended) || heartRateConfiguration.DeviceAddress is not { } address) return;

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
        if (ghost is not null)
        {
            if (ghost.Ended || expectedRunId != id) return;
            ghost.Observe(SystemClock.ElapsedRealtime());
            heartRate!.Observe(observation);
            PublishGhost(observation: observation);
            return;
        }
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

    private void StartEligibleForeground(bool connectedDevice = false, bool timerOnly = false, Notification? initial = null)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(NotificationId, initial ?? BuildNotification());

            return;
        }

        connectedDeviceRequested |= connectedDevice;
        var type = !timerOnly && CanUseLocation() ? ForegroundService.TypeLocation : OperatingSystem.IsAndroidVersionAtLeast(34)
            ? ForegroundService.TypeSpecialUse : ForegroundService.TypeNone;

        if (connectedDeviceRequested && OperatingSystem.IsAndroidVersionAtLeast(31) && CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) == Permission.Granted)
            type |= ForegroundService.TypeConnectedDevice;

        StartForeground(NotificationId, initial ?? BuildNotification(), type);
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
            ghost?.Unavailable();
            gpsStatus = "GPS unavailable · allow location in Android settings";
            livePace.Reset();
            paceTrend.Reset(TimeSpan.FromMilliseconds(lastTick));

            if (locationAvailable)
            {
                distance?.Unavailable("GPS unavailable");
                routeCapture?.Break("GPS unavailable");
                motion?.Unavailable();
                candidateDistance = null;
            }
        }
        else if (!locationAvailable)
        {
            var restoredAt = TimeSpan.FromMilliseconds(SystemClock.ElapsedRealtime());
            livePace.Reset("acquiring");
            paceTrend.Reset(TimeSpan.FromMilliseconds(lastTick));
            distance?.Reanchor(restoredAt, "GPS restored");
            routeCapture?.Break("GPS restored");
            motion?.Boundary(restoredAt);
            candidateDistance = null;
            gpsStatus = "Waiting for GPS";
        }

        locationAvailable = available;
    }

    private Notification BuildNotification()
    {
        var state = Snapshot!;
        var open = new Intent(this, typeof(MainActivity)).SetAction(RunNotificationEntry.Action)
            .SetData(global::Android.Net.Uri.Parse($"loopcx://run/{state.Id}"))
            .PutExtra(RunNotificationEntry.RunIdExtra, state.Id.ToString());
        var pendingOpen = PendingIntent.GetActivity(this, 0, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var pauseReason = !state.Paused ? PauseReason.None
            : state.Motion?.PauseReason == PauseReason.Automatic ? PauseReason.Automatic : PauseReason.Manual;
        if (state.Ghost is { } race) return BuildGhostNotification(state, race, pendingOpen);
        var text = RunNotificationText.Create(state.Phase, state.RemainingSeconds!.Value, pauseReason,
            state.HeartRate?.CurrentBpm, state.LivePace?.SecondsPerKilometre);
        var builder = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle(text.Title)
            .SetContentText(text.Text)
            .SetStyle(new Notification.BigTextStyle().BigText(text.ExpandedText))
            .SetContentIntent(pendingOpen)
            .SetVisibility(NotificationVisibility.Public)
            .SetOngoing(true);
        if (state.Storage?.Error is not null)
            builder.SetContentText(state.Storage.CanRetry ? "Recording not saved · open LOOP Cx to retry" : "Local storage unavailable · recording not saved")
                .SetStyle(new Notification.BigTextStyle().BigText("Recent recording could not be saved. Recent data may be lost if the app closes."));
        if (state.Phase == "Finished") return builder.Build();
        if (state.Paused) builder.SetSubText(text.PhaseCountdown);

        var command = state.Paused ? "resume" : "pause";
        builder.AddAction(new Notification.Action.Builder(null, state.Paused ? "Resume" : "Pause",
            PendingIntent.GetService(this, 1, new Intent(this, typeof(RunService)).SetAction(command),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        if (pauseReason == PauseReason.Automatic)
        {
            builder.AddAction(new Notification.Action.Builder(null, "Pause manually",
                PendingIntent.GetService(this, 4, new Intent(this, typeof(RunService)).SetAction("pause"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        }

        if (!state.Paused && state.Phase is "Warmup" or "Cooldown")
        {
            builder.AddAction(new Notification.Action.Builder(null, "+1 min",
                PendingIntent.GetService(this, 2, new Intent(this, typeof(RunService)).SetAction("extend"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
            builder.AddAction(new Notification.Action.Builder(null,
                state.Phase == "Warmup" ? "Start main run" : "Finish",
                PendingIntent.GetService(this, 3, new Intent(this, typeof(RunService)).SetAction("skip"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        }
        return builder.Build();
    }

    public void OnLocationChanged(global::Android.Locations.Location location)
    {
        if (ghost is not null) { GhostLocation(location); return; }
        if (session is null || session.IsFinished || session.PauseReason == PauseReason.Manual) return;

        var raw = new RunSaveLocation(location.Time, location.ElapsedRealtimeNanos, location.Latitude, location.Longitude,
            location.HasAccuracy ? location.Accuracy : null);
        AdvanceClock();
        if (session.IsFinished || !locationAvailable)
        {
            saveBuffer.Add(routeCapture!.Capture(raw, null, session.IsFinished ? "Finalization" : "Source unavailable"));
            locations++;
            Publish();
            return;
        }
        var receivedAt = TimeSpan.FromMilliseconds(lastTick);
        var fix = new DistanceFix(TimeSpan.FromMilliseconds(location.ElapsedRealtimeNanos / 1_000_000),
            location.Latitude, location.Longitude, location.HasAccuracy ? location.Accuracy : null);
        var assessed = !session.IsPaused;
        var moved = assessed && distance!.Observe(fix, receivedAt, session.MainActiveTime);
        var assessment = assessed ? distance!.Observation : null;
        var assessmentReason = assessed ? distance!.Snapshot.Status : "Automatic pause";
        if (!session.IsPaused)
        {
            statistics.Observe(distance!.Observation);

            livePace.Observe(distance.Observation, distance.AcceptedEdge, receivedAt);
            if (!session.IsFinished) paceTrend.Observe(distance.Observation, distance.AcceptedEdge, receivedAt, LivePacePolicy.Pilot);
        }

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
            livePace.Reset("paused");
            paceTrend.Reset(TimeSpan.FromMilliseconds(lastTick));
            routeCapture?.Break("Automatic pause boundary");
            statistics.Exclude(automaticEvent!.OnsetMonotonicTime!.Value, automaticEvent.ExcludedThroughMonotonicTime!.Value);
            distance!.ConfirmStandstill(candidate, receivedAt,
                receivedAt > automaticEvent!.ExcludedThroughMonotonicTime);
            candidateDistance = null;
        }
        else if (decision == MotionDecision.Resume && session.ResumeAutomatically())
        {
            automaticEvent = motion.Event;
            livePace.Reset("acquiring");
            paceTrend.Reset(TimeSpan.FromMilliseconds(lastTick));
            distance!.Reanchor(receivedAt, "Automatic resume boundary");
            routeCapture?.Break("Automatic resume boundary");
            motion.Boundary(receivedAt);
            candidateDistance = null;
        }
        else if (motion.Candidate is null)
        {
            candidateDistance = null;
        }

        saveBuffer.Add(routeCapture!.Capture(raw, assessment, assessmentReason));
        locations++;
        gpsStatus = location.HasAccuracy ? $"GPS ±{location.Accuracy:0} m" : "GPS accuracy unknown";
        Publish(automaticEvent?.Kind, automaticEvent);
    }

    public void OnProviderDisabled(string provider)
    {
        if (ghost is not null)
        {
            if (!ghost.Ended) { ghost.Unavailable(); RefreshLocationSubscription(); PublishGhost(); }
            return;
        }
        if (session is null || session.IsFinished) return;

        RefreshLocationSubscription();
        Publish();
    }

    public void OnProviderEnabled(string provider)
    {
        if (ghost is not null)
        {
            if (!ghost.Ended) { ghost.Unavailable(); RefreshLocationSubscription(); PublishGhost(); }
            return;
        }
        if (session is null || session.IsFinished) return;

        RefreshLocationSubscription();
        Publish();
    }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    public override void OnDestroy()
    {
        audio?.Dispose();
        audio = null;
#if DEBUG
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) nativeProbe?.Stop();
        if (journalAvailable)
        {
            try { journal.SetSaveFailureForProbe(id, false); }
            catch (global::Android.Database.Sqlite.SQLiteException) { }
        }
#endif
        StopHeartRateSource();
        if (tick is not null) handler?.RemoveCallbacks(tick);
        try { locationManager?.RemoveUpdates(this); }
        catch (Java.Lang.SecurityException) { }
        StopForeground(StopForegroundFlags.Remove);

        if (Snapshot?.Id == id && saveBuffer.Status.State != "saved") Snapshot = null;
        Changed?.Invoke();

        base.OnDestroy();
    }
}
