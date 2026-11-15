using System.Text.Json;
using System.Text.Json.Serialization;
using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.GhostRun;

public sealed record GhostStoredRun(Guid Id, string Status, GhostRaceCheckpoint Checkpoint)
{
    public static GhostStoredRun Read(Guid id, string status, string json)
    {
        var checkpoint = GhostRaceCheckpoint.Read(json);
        var clock = checkpoint.Snapshot.Clock.State;
        if (checkpoint.Id != id || (status switch
        {
            "finished" => clock != GhostClockState.Finished,
            "incomplete" => clock != GhostClockState.Incomplete,
            "interrupted" => clock is not (GhostClockState.Running or GhostClockState.Interrupted),
            _ => true
        })) throw new InvalidDataException("Stored Ghost identity/status disagrees with its checkpoint.");
        return new(id, status, checkpoint);
    }
}

public sealed record GhostStartPosition(long RequestedBootMilliseconds, long MeasuredBootMilliseconds,
    long UtcMilliseconds, double Latitude, double Longitude, double? Accuracy);
public sealed record GhostComparisonProfile(string Version, RouteMatchPolicy Matching, TimeSpan MaximumStartAge,
    TimeSpan MaximumInterpolationGap, HeartRateConfiguration HeartRate, MovementPolicy Motion, DistancePolicy TrainingDistance);
public sealed record GhostRaceSnapshot(Guid Id, string RouteName, RouteKey Route, GhostMode Mode,
    GhostClockSnapshot Clock, PauseReason PauseReason, RouteMatchSnapshot Match, double? GhostMetres,
    GhostGap Gap, Guid? ReferenceId, TimeSpan? ReferenceElapsed, int FrozenYear, string? QualityReason);
public sealed record GhostRaceCheckpoint(string ActivityKind, int GhostCheckpointVersion, Guid Id, long SaveRevision,
    DateTimeOffset StartedUtc, DateTimeOffset StartedLocal, string TimeZoneId, Guid? HealthConnectConsentRevision, long StartedBootMilliseconds,
    long ObservedBootMilliseconds, GhostRoute Route, GhostMode Mode, GhostAttempt? Reference,
    GhostComparisonProfile Profile, GhostRaceSnapshot Snapshot, IReadOnlyList<TrainingPause> Pauses,
    IReadOnlyList<GhostPoint> Progress, IReadOnlyList<RecordedRouteFix> Raw, string ArchiveDisposition, string? TerminalKind)
{
    private static readonly JsonSerializerOptions Options = new() { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static bool IsGhost(JsonElement checkpoint) => checkpoint.TryGetProperty(nameof(ActivityKind), out var kind)
        && kind.ValueKind == JsonValueKind.String && kind.GetString() == "ghost";

    public static GhostRaceCheckpoint Read(string json)
    {
        var value = JsonSerializer.Deserialize<GhostRaceCheckpoint>(json, Options)
            ?? throw new InvalidDataException("Missing Ghost checkpoint.");
        if (value.ActivityKind != "ghost" || value.GhostCheckpointVersion != 1 || value.Id == Guid.Empty
            || value.SaveRevision <= 0 || value.StartedUtc.Offset != TimeSpan.Zero || value.StartedBootMilliseconds < 0
            || value.ObservedBootMilliseconds < value.StartedBootMilliseconds || value.Route is null || value.Profile is null
            || value.Snapshot is null || value.Snapshot.Clock is null || value.Snapshot.Match is null
            || value.Profile.Matching is null || value.Profile.Motion is null || value.Profile.TrainingDistance is null
            || value.Profile.HeartRate is null || value.Profile.MaximumStartAge <= TimeSpan.Zero
            || value.Profile.MaximumInterpolationGap <= TimeSpan.Zero || value.Profile.Version != "ghost-service-pilot-v1"
            || value.Pauses is null || value.Progress is null || value.Raw is null || value.Progress.Count == 0
            || value.Progress.Any(point => point is null)
            || value.Raw.Any(fix => fix is null) || value.Pauses.Any(pause => pause is null)
            || value.StartedLocal.UtcDateTime != value.StartedUtc.UtcDateTime || string.IsNullOrWhiteSpace(value.TimeZoneId)
            || value.Snapshot.Id != value.Id || value.Snapshot.Route != value.Route.Key
            || value.Snapshot.FrozenYear != value.StartedLocal.Year || value.Snapshot.Mode != value.Mode
            || value.Snapshot.ReferenceId != value.Reference?.Id || value.Snapshot.ReferenceElapsed != value.Reference?.Elapsed
            || value.Snapshot.Match.Route != value.Route.Key || value.Snapshot.Match.RuleVersion != value.Route.RuleVersion
            || !Enum.IsDefined(value.Mode) || !Enum.IsDefined(value.Snapshot.Clock.State) || !Enum.IsDefined(value.Snapshot.PauseReason)
            || value.Snapshot.Clock.RaceElapsed < TimeSpan.Zero || value.Snapshot.Clock.TrainingActive < TimeSpan.Zero
            || value.Snapshot.Clock.TrainingActive > value.Snapshot.Clock.RaceElapsed
            || (decimal)value.Snapshot.Clock.RaceElapsed.TotalMilliseconds > (decimal)value.ObservedBootMilliseconds - value.StartedBootMilliseconds
            || value.Progress.Any(point => point.Elapsed < TimeSpan.Zero || point.Elapsed > value.Snapshot.Clock.RaceElapsed
                || !double.IsFinite(point.Metres) || point.Metres < 0 || point.Metres > value.Route.LengthMetres)
            || value.Progress.Zip(value.Progress.Skip(1)).Any(pair => pair.First.Elapsed >= pair.Second.Elapsed))
            throw new InvalidDataException("Malformed or unsupported Ghost checkpoint; no repair was attempted.");
        var state = value.Snapshot.Clock.State;
        var expectedDisposition = state == GhostClockState.Running ? "not-terminal"
            : state == GhostClockState.Incomplete && value.Snapshot.Clock.RaceElapsed == TimeSpan.Zero ? "zero-elapsed-incomplete" : "attempt";
        var expectedTerminal = state == GhostClockState.Running ? null : state == GhostClockState.Finished ? "matched-finish"
            : state == GhostClockState.Incomplete ? "explicit-end" : "interrupted";
        if (value.TerminalKind != expectedTerminal || value.ArchiveDisposition != expectedDisposition
            || state == GhostClockState.Finished && (value.Snapshot.Clock.RaceElapsed <= TimeSpan.Zero
                || value.Snapshot.Match.State != RouteMatchState.Finished || value.Snapshot.Match.FinishedAt != value.Snapshot.Clock.RaceElapsed))
            throw new InvalidDataException("The terminal Ghost disposition or finish evidence is inconsistent.");
        _ = new OrderedRouteMatcher(value.Route, value.Profile.Matching);
        if (value.Reference is { } reference && (GhostReferences.Select(value.Route, [reference], value.StartedLocal.Year).PersonalBest?.Id != reference.Id
            || reference.Elapsed != value.Snapshot.ReferenceElapsed))
            throw new InvalidDataException("The frozen Ghost reference does not match this route.");
        return value;
    }

    public GhostAttempt Attempt()
    {
        if (ArchiveDisposition != "attempt") throw new InvalidDataException("This checkpoint has no representable terminal archive attempt.");
        var quality = Snapshot.Clock.State switch
        {
            GhostClockState.Finished => AttemptQuality.Verified,
            GhostClockState.Incomplete => AttemptQuality.Incomplete,
            _ => AttemptQuality.Interrupted
        };
        return new(Id, Id, Route.Key, StartedLocal, TimeZoneId, Snapshot.Clock.RaceElapsed, quality,
            Route.RuleVersion, Progress);
    }
}

// One service-owned race state, composed from the existing clock/matcher/playback and base motion policy.
public sealed class GhostRace
{
    private readonly GhostRaceClock clock;
    private readonly OrderedRouteMatcher matcher;
    private readonly GhostPlayback? playback;
    private readonly RunMotion motion;
    private readonly RunDistance trainingDistance;
    private RunDistance.CandidateDistance? candidateDistance;
    private readonly List<GhostPoint> progress = [new(TimeSpan.Zero, 0)];
    private readonly List<RecordedRouteFix> raw = [];
    private readonly long startedBoot;
    private readonly DateTimeOffset startedUtc, startedLocal;
    private readonly string zoneId;
    private readonly Guid id;
    private readonly Guid? consentRevision;
    private readonly GhostRoute route;
    private readonly GhostMode mode;
    private readonly GhostAttempt? reference;
    private readonly GhostComparisonProfile profile;
    private PauseReason pauseReason;
    private string? reason;
    private long observedBoot, lastSupportedBoot;
    private bool available = true;
    private bool interruptionPending;

    public GhostRace(Guid id, GhostRoute route, GhostMode mode, GhostAttempt? reference, GhostComparisonProfile profile,
        GhostStartPosition position, long receivedBoot, DateTimeOffset utc, string zoneId, TimeSpan offset, Guid? consentRevision)
    {
        if (id == Guid.Empty || position.MeasuredBootMilliseconds < position.RequestedBootMilliseconds
            || position.MeasuredBootMilliseconds > receivedBoot
            || receivedBoot - position.MeasuredBootMilliseconds > profile.MaximumStartAge.TotalMilliseconds)
            throw new InvalidDataException("GPS uncertain. Wait for a fresh position at the route start.");
        matcher = new(route, profile.Matching);
        var first = new RecordedRouteFix(TimeSpan.Zero, DateTimeOffset.FromUnixTimeMilliseconds(position.UtcMilliseconds),
            position.Latitude, position.Longitude, position.Accuracy, RecordedRunSection.Main);
        if (matcher.Start(first).State != RouteMatchState.Tracking)
            throw new InvalidDataException("Move to the route start with reliable GPS before starting.");
        if (reference is not null && GhostReferences.Select(route, [reference], utc.Year).PersonalBest?.Id != reference.Id)
            throw new InvalidDataException("The selected reference no longer matches this route version.");
        this.id = id; this.route = route; this.mode = mode; this.reference = reference; this.profile = profile;
        observedBoot = receivedBoot;
        lastSupportedBoot = startedBoot = position.MeasuredBootMilliseconds;
        startedUtc = utc.AddMilliseconds(startedBoot - receivedBoot);
        startedLocal = startedUtc.ToOffset(offset);
        this.zoneId = zoneId; this.consentRevision = consentRevision;
        clock = new(TimeSpan.FromMilliseconds(startedBoot));
        motion = new(profile.Motion, TimeSpan.FromMilliseconds(startedBoot));
        trainingDistance = new(profile.TrainingDistance, TimeSpan.FromMilliseconds(startedBoot), RunPhase.Work);
        playback = reference is null ? null : new(reference, profile.MaximumInterpolationGap);
        raw.Add(first);
        clock.Observe(TimeSpan.FromMilliseconds(receivedBoot));
    }

    public bool Ended => clock.Current.State != GhostClockState.Running;
    public GhostRaceSnapshot Snapshot
    {
        get
        {
            var current = clock.Current;
            var supported = current.State == GhostClockState.Finished || available && observedBoot - lastSupportedBoot <= profile.MaximumStartAge.TotalMilliseconds;
            var match = supported ? matcher.Current : matcher.Current with { State = RouteMatchState.Uncertain, Metres = null, Problem = RouteMatchProblem.Gap };
            return new(id, route.Name, route.Key, mode, current, pauseReason, match,
                playback?.PositionAt(current.RaceElapsed), playback?.Compare(current.RaceElapsed, match.Metres) ?? new(null, null),
                reference?.Id, reference?.Elapsed, startedLocal.Year, reason);
        }
    }
    public void Observe(long now)
    {
        clock.Observe(TimeSpan.FromMilliseconds(now));
        observedBoot = now;
        motion.UpdateFreshness(TimeSpan.FromMilliseconds(now));
        if (!clock.Current.TrainingPaused) trainingDistance.UpdateFreshness(TimeSpan.FromMilliseconds(now));
        if (motion.Candidate is null) candidateDistance = null;
    }
    public void Pause(long now)
    {
        if (Ended) return;
        clock.PauseTraining(TimeSpan.FromMilliseconds(now)); pauseReason = PauseReason.Manual;
        motion.Boundary(TimeSpan.FromMilliseconds(now));
        trainingDistance.Interrupt(TimeSpan.FromMilliseconds(now), "Training pause boundary");
        candidateDistance = null;
    }
    public void Resume(long now)
    {
        if (Ended) return;
        clock.ResumeTraining(TimeSpan.FromMilliseconds(now)); pauseReason = PauseReason.None;
        motion.Boundary(TimeSpan.FromMilliseconds(now));
        trainingDistance.Interrupt(TimeSpan.FromMilliseconds(now), "Training pause boundary");
        candidateDistance = null;
    }
    public void Unavailable() { interruptionPending = true; available = false; motion.Unavailable(); trainingDistance.Unavailable("GPS unavailable"); candidateDistance = null; }
    public void End(long now)
    {
        if (Ended) return;
        Observe(now);
        clock.EndAttempt(TimeSpan.FromMilliseconds(now));
        reason = clock.Current.RaceElapsed == TimeSpan.Zero ? "Explicit End at zero elapsed; no archive attempt or record." : "Attempt ended before a verified route finish.";
    }
    public void Location(RunSaveLocation point, long receivedBoot, bool sourceAvailable = true)
    {
        if (Ended) return;
        Observe(receivedBoot);
        var measured = point.MonotonicNanoseconds / 1000000;
        var fix = new RecordedRouteFix(TimeSpan.FromMilliseconds(measured - startedBoot),
            DateTimeOffset.FromUnixTimeMilliseconds(point.UtcMilliseconds), point.Latitude, point.Longitude, point.Accuracy,
            RecordedRunSection.Main, interruptionPending || !sourceAvailable || measured > receivedBoot || receivedBoot - measured > profile.MaximumStartAge.TotalMilliseconds);
        raw.Add(fix);
        if (!sourceAvailable || measured < startedBoot || measured > receivedBoot
            || receivedBoot - measured > profile.MaximumStartAge.TotalMilliseconds)
        {
            Unavailable();
            return;
        }
        // Preserve every raw delivery, but cached/older input cannot acknowledge a new interruption or renew support.
        if (measured <= lastSupportedBoot) return;

        available = true;
        lastSupportedBoot = measured;
        var matched = matcher.Observe(fix);
        if (matched.State == RouteMatchState.Tracking) interruptionPending = false;
        if (fix.Elapsed > progress[^1].Elapsed)
            progress.Add(new(fix.Elapsed, matched.Metres ?? progress[^1].Metres, matched.Metres is not null));
        var distanceFix = new DistanceFix(TimeSpan.FromMilliseconds(measured), point.Latitude, point.Longitude, point.Accuracy);
        var moved = !clock.Current.TrainingPaused && trainingDistance.Observe(distanceFix,
            TimeSpan.FromMilliseconds(receivedBoot), clock.Current.TrainingActive);
        // Ghost has one main movement scope, not a Core session/template or target.
        var decision = motion.Observe(new(TimeSpan.FromMilliseconds(measured), point.Latitude, point.Longitude, point.Accuracy),
            TimeSpan.FromMilliseconds(receivedBoot), 0, RunPhase.Work, pauseReason, moved);
        if (decision == MotionDecision.CandidateStarted) candidateDistance = trainingDistance.CaptureCandidate();
        if (decision == MotionDecision.Pause && pauseReason == PauseReason.None && candidateDistance is { } candidate)
        {
            clock.PauseTrainingAutomatically(motion.Event!.OnsetMonotonicTime!.Value,
                motion.Event.ExcludedThroughMonotonicTime!.Value, TimeSpan.FromMilliseconds(receivedBoot));
            pauseReason = PauseReason.Automatic;
            trainingDistance.ConfirmStandstill(candidate, TimeSpan.FromMilliseconds(receivedBoot),
                TimeSpan.FromMilliseconds(receivedBoot) > motion.Event.ExcludedThroughMonotonicTime);
            candidateDistance = null;
        }
        else if (decision == MotionDecision.Resume && pauseReason == PauseReason.Automatic) Resume(receivedBoot);
        if (matched.FinishedAt is { } finish)
        {
            clock.CompleteAt(TimeSpan.FromMilliseconds(startedBoot) + finish);
            progress.RemoveAll(sample => sample.Elapsed >= finish);
            progress.Add(new(finish, route.LengthMetres));
        }
    }
    public GhostRaceCheckpoint Checkpoint(long revision, long now) => new("ghost", 1, id, revision, startedUtc,
        startedLocal, zoneId, consentRevision, startedBoot, now, route, mode, reference, profile, Snapshot, clock.Pauses, progress.ToArray(), raw.ToArray(),
        !Ended ? "not-terminal" : clock.Current.State == GhostClockState.Incomplete && clock.Current.RaceElapsed == TimeSpan.Zero ? "zero-elapsed-incomplete" : "attempt",
        !Ended ? null : clock.Current.State == GhostClockState.Finished ? "matched-finish" : clock.Current.State == GhostClockState.Incomplete ? "explicit-end" : "interrupted");
}
