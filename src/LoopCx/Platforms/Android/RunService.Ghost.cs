using System.Text.Json;
using Android.App;
using Android.Content;
using Android.OS;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.GhostRun;

namespace LoopCx;

public sealed partial class RunService
{
    private GhostRace? ghost;
    public sealed record GhostStartRequest(RouteKey Route, GhostMode Mode, GhostStartPosition Position);

    private static readonly object GhostStartGate = new();
    private static (Guid Id, TaskCompletionSource<string?> Completion)? pendingGhostStart;

    public static async Task<string?> StartGhostAsync(GhostStartRequest request, CancellationToken cancellation)
    {
        var requestId = Guid.NewGuid();
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (GhostStartGate)
        {
            if (pendingGhostStart is not null) throw new InvalidOperationException("A Ghost start is already being checked.");
            pendingGhostStart = (requestId, completion);
        }
        using var registration = cancellation.Register(() => Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            lock (GhostStartGate)
            {
                if (pendingGhostStart?.Id != requestId) return;
                pendingGhostStart = null;
                completion.TrySetCanceled(cancellation);
            }
        }));
        try
        {
            global::Android.App.Application.Context.StartForegroundService(
                new Intent(global::Android.App.Application.Context, typeof(RunService)).SetAction("start-ghost")
                    .PutExtra("ghost-request-id", requestId.ToString()).PutExtra("ghost-start", JsonSerializer.Serialize(request)));
            return await completion.Task;
        }
        finally
        {
            lock (GhostStartGate) if (pendingGhostStart?.Id == requestId) pendingGhostStart = null;
        }
    }
    private static bool HasGhostStartRequest(Intent? intent)
    {
        lock (GhostStartGate) return pendingGhostStart is { } pending && intent?.GetStringExtra("ghost-request-id") == pending.Id.ToString();
    }
    private static void CompleteGhostStart(Intent? intent, string? error)
    {
        lock (GhostStartGate)
        {
            if (pendingGhostStart is not { } pending || intent?.GetStringExtra("ghost-request-id") != pending.Id.ToString()) return;
            pendingGhostStart = null;
            pending.Completion.TrySetResult(error);
        }
    }

    private StartCommandResult HandleGhostCommand(Intent? intent, string? command)
    {
        if (command == "start-ghost" && (!HasGhostStartRequest(intent) || ghost is not null))
        {
            CompleteGhostStart(intent, "A recording is already active, or this start request was cancelled.");
            if (ghost is null && session is null) StopSelf();
            return StartCommandResult.NotSticky;
        }
        if (ghost is null)
        {
            if (session is not null || Snapshot is { Storage.State: not "saved" })
            {
                StartError = "Finish saving the current recording before starting a Ghost Run.";
                CompleteGhostStart(intent, StartError);
                Changed?.Invoke();
                return StartCommandResult.NotSticky;
            }
            try
            {
                var request = JsonSerializer.Deserialize<GhostStartRequest>(intent?.GetStringExtra("ghost-start") ?? "")
                    ?? throw new InvalidDataException("Choose a stored route before starting.");
                if (!CanUseLocation()) throw new InvalidDataException("GPS unavailable. The race has not started.");
                var archive = new AndroidGhostArchive(journal);
                var route = archive.ReadRoute(request.Route) ?? throw new InvalidDataException("The selected route version is unavailable.");
                var now = SystemClock.ElapsedRealtime();
                var zone = TimeZoneInfo.Local;
                var (utc, revision) = HealthConnectSettings.FreezeStart();
                var actualStartUtc = utc.AddMilliseconds(request.Position.MeasuredBootMilliseconds - now);
                var offset = zone.GetUtcOffset(actualStartUtc);
                var reference = archive.ReadRecords(request.Route, actualStartUtc.ToOffset(offset).Year).ReferenceFor(request.Mode);
                var savedHeartRate = HeartRatePreferences.Load();
                heartRateConfiguration = savedHeartRate.Configuration with { Zones = Array.AsReadOnly(savedHeartRate.Configuration.Zones.ToArray()) };
                var accuracy = route.Policy.MaximumAccuracyMetres;
                var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.Route.RuleVersion,
                    accuracy, accuracy, accuracy, route.Policy.MaximumGap, route.Policy.MaximumSpeedMetresPerSecond,
                    route.Policy.MaximumGap.TotalSeconds * route.Policy.MaximumSpeedMetresPerSecond, 100000),
                    MovementPolicy.Pilot.MaximumAge, route.Policy.MaximumGap, heartRateConfiguration, MovementPolicy.Pilot, DistancePolicy.Pilot);
                // Constructor validates actual post-request fresh GPS and correct start before publishing/committing ownership.
                var candidate = new GhostRace(Guid.NewGuid(), route.Route, request.Mode, reference, profile,
                    request.Position, now, utc, zone.Id, offset, revision);
                ghost = candidate;
                var initial = ghost.Checkpoint(1, now);
                id = initial.Id; started = initial.StartedUtc; startedMonotonic = initial.StartedBootMilliseconds;
                lastTick = now;
                handler = new Handler(Looper.MainLooper!);
                heartRate = new(heartRateConfiguration, savedHeartRate.ConfigurationError);
                heartRateMetadata = JsonSerializer.Serialize(savedHeartRate with { Configuration = heartRateConfiguration });
                StartError = null;
                ((NotificationManager)GetSystemService(NotificationService)!).CreateNotificationChannel(
                    new NotificationChannel(ChannelId, "Running session", NotificationImportance.Low));
                Snapshot = GhostSnapshot();
                StartEligibleForeground();
            }
            catch (Exception issue) when (issue is IOException or InvalidDataException or ArgumentException or JsonException
                or Java.Lang.RuntimeException)
            {
                StartError = issue is InvalidDataException ? issue.Message : "Ghost Run could not start. Existing recordings were preserved.";
                ghost = null; Snapshot = null;
                CompleteGhostStart(intent, StartError);
                Changed?.Invoke(); StopSelf();
                return StartCommandResult.NotSticky;
            }
            CompleteGhostStart(intent, null);
            // Ownership is established. Later native setup/update failures must not discard a frozen save batch.
            tick = Tick;
            try
            {
                PublishGhost("start");
                RefreshLocationSubscription();
                StartHeartRateSource();
            }
            catch (Java.Lang.RuntimeException)
            {
                StartError = "A device service is unavailable. The current recording and any pending save remain owned here.";
                Snapshot = GhostSnapshot();
                Changed?.Invoke();
            }
            finally
            {
                if (!ghost!.Ended) handler!.PostDelayed(tick, 1000);
            }
            return StartCommandResult.NotSticky;
        }
        var at = SystemClock.ElapsedRealtime();
        ghost.Observe(at);
        string? effective = null;
        if (!ghost.Ended)
        {
            if (command == "pause" && ghost.Snapshot.PauseReason != PauseReason.Manual) { ghost.Pause(at); effective = command; }
            else if (command == "resume" && ghost.Snapshot.PauseReason != PauseReason.None) { ghost.Resume(at); effective = command; }
            else if (command == "end-ghost") { ghost.End(at); effective = "end-attempt"; }
            else if (command == "refresh-sensors") { RefreshLocationSubscription(); }
        }
        PublishGhost(effective, explicitRetry: command == "retry-saving");
        return StartCommandResult.NotSticky;
    }

    private RunSnapshot GhostSnapshot()
    {
        var race = ghost!.Snapshot;
        return new(id, ghost.Ended ? "Finished" : "GhostRun", race.Clock.TrainingPaused,
            (long)race.Clock.TrainingActive.TotalSeconds, null, locations, gpsStatus, null,
            HeartRate: heartRate?.Snapshot(TimeSpan.FromTicks(SystemClock.ElapsedRealtimeNanos() / 100)), Storage: saveBuffer.Status)
        { ActivityKind = "ghost", Ghost = race };
    }
    private void TickGhost()
    {
        if (!ghost!.Ended) ghost.Observe(SystemClock.ElapsedRealtime());
        PublishGhost();
        if (!ghost.Ended) handler!.PostDelayed(tick!, 1000);
    }
    private void GhostLocation(global::Android.Locations.Location location)
    {
        if (ghost!.Ended) return;
        var raw = new RunSaveLocation(location.Time, location.ElapsedRealtimeNanos, location.Latitude, location.Longitude,
            location.HasAccuracy ? location.Accuracy : null);
        ghost.Location(raw, SystemClock.ElapsedRealtime(), locationAvailable);
        saveBuffer.Add(raw);
        locations++;
        gpsStatus = location.HasAccuracy ? $"GPS ±{location.Accuracy:0} m" : "GPS accuracy unknown";
        PublishGhost();
    }
    private void PublishGhost(string? command = null, HeartRateObservation? observation = null, bool explicitRetry = false)
    {
        if (!terminalSaveCaptured)
        {
            var now = SystemClock.ElapsedRealtime();
            var checkpoint = ghost!.Checkpoint(++saveRevision, now);
            saveBuffer.Capture(new(id, saveRevision, started, DateTimeOffset.UtcNow, now - startedMonotonic,
                ghost.Ended, "[]", "{\"ActivityKind\":\"ghost\"}", heartRateMetadata, JsonSerializer.Serialize(checkpoint)),
                command ?? (ghost.Ended ? "finish" : null), observation);
            terminalSaveCaptured = ghost.Ended;
        }
        saveBuffer.Save(journal.Commit, explicitRetry);
        Snapshot = GhostSnapshot();
        try { ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, BuildNotification()); }
        catch (Java.Lang.RuntimeException)
        { StartError = "Notification update unavailable. The recording and pending save remain owned by the running service."; }
        Changed?.Invoke();
        if (ghost!.Ended)
        {
            StopHeartRateSource();
            if (saveBuffer.Status.State == "saved") StopSelf();
        }
    }
    private Notification BuildGhostNotification(RunSnapshot state, GhostRaceSnapshot race, PendingIntent? open)
    {
        var title = ghost!.Ended ? "Ghost Run · final save" : state.Paused ? "Ghost Run · training paused" : "Ghost Run";
        var text = state.Storage?.Error is not null ? "Recording not saved · open LOOP Cx" : $"{race.RouteName} · race {FormatRaceTime(race.Clock.RaceElapsed)}";
        var builder = new Notification.Builder(this, ChannelId).SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle(title).SetContentText(text).SetContentIntent(open).SetOngoing(state.Storage?.State != "saved")
            .SetVisibility(NotificationVisibility.Public);
        if (!ghost.Ended)
            builder.AddAction(new Notification.Action.Builder(null, state.Paused ? "Resume training" : "Pause training",
                PendingIntent.GetService(this, 1, new Intent(this, typeof(RunService)).SetAction(state.Paused ? "resume" : "pause"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build());
        return builder.Build();
    }
    private static string FormatRaceTime(TimeSpan time) => $"{(long)time.TotalMinutes}:{time.Seconds:00}";
}
