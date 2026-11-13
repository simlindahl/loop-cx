namespace LoopCx.Features.ActiveRun;

public sealed record MovementPolicy(string Version, double MaximumAccuracyMetres, TimeSpan MaximumGap,
    TimeSpan MaximumAge, double StationaryRadiusMetres, double MinimumResumeMetres, double MaximumSpeedMetresPerSecond)
{
    public static MovementPolicy Pilot { get; } = new("timed-motion-pilot-v1", 5,
        TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), 2, 3, 12);
}

public sealed record StandstillCandidate(int PhaseIndex, TimeSpan OnsetMonotonicTime,
    TimeSpan ExcludedThroughMonotonicTime);
public sealed record AutomaticMotionEvent(string Kind, int PhaseIndex, TimeSpan? OnsetMonotonicTime,
    TimeSpan? ExcludedThroughMonotonicTime, TimeSpan SampleMonotonicTime, TimeSpan DetectedMonotonicTime);
public sealed record MotionState(MovementPolicy Policy, TimeSpan StartedMonotonicTime, PauseReason PauseReason, string Observation,
    StandstillCandidate? Candidate);
public enum MotionDecision { None, CandidateStarted, Pause, Resume }

/// <summary>A conservative synthetic pilot: absence of accepted distance is never standstill evidence.</summary>
public sealed class RunMotion
{
    private readonly MovementPolicy policy;
    private readonly TimeSpan started;
    private DistanceFix? origin;
    private DistanceFix? previous;
    private TimeSpan? lastObserved;
    private TimeSpan boundary;
    private string observation = "Unknown";

    public RunMotion(MovementPolicy policy, TimeSpan started = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(policy.Version) || policy.MaximumGap <= TimeSpan.Zero ||
            policy.MaximumAge <= TimeSpan.Zero || !double.IsFinite(policy.MaximumAccuracyMetres) ||
            policy.MaximumAccuracyMetres <= 0 || !double.IsFinite(policy.StationaryRadiusMetres) ||
            policy.StationaryRadiusMetres <= 0 || !double.IsFinite(policy.MinimumResumeMetres) ||
            policy.MinimumResumeMetres <= policy.StationaryRadiusMetres ||
            !double.IsFinite(policy.MaximumSpeedMetresPerSecond) || policy.MaximumSpeedMetresPerSecond <= 0)
        {
            throw new ArgumentException("Movement policy requires finite positive conservative limits.", nameof(policy));
        }

        this.policy = policy;
        this.started = boundary = started;
    }

    public StandstillCandidate? Candidate { get; private set; }
    public AutomaticMotionEvent? Event { get; private set; }
    public MotionState Snapshot(PauseReason reason) => new(policy, started, reason, observation, Candidate);

    public void Boundary(TimeSpan at)
    {
        boundary = at;
        ResetEvidence();
    }

    public void UpdateFreshness(TimeSpan now)
    {
        if (lastObserved is { } last && now - last > policy.MaximumGap) ResetEvidence();
    }

    public void Unavailable() => ResetEvidence();

    public MotionDecision Observe(DistanceFix fix, TimeSpan receivedAt, int phaseIndex,
        RunPhase? phase, PauseReason reason, bool acceptedMovingEdge)
    {
        Event = null;

        // Old packets add no evidence and cannot destroy a newer valid candidate.
        if (lastObserved is { } last && fix.MonotonicTime <= last) return MotionDecision.None;

        if (fix.MonotonicTime < boundary || fix.MonotonicTime > receivedAt ||
            receivedAt - fix.MonotonicTime > policy.MaximumAge ||
            !double.IsFinite(fix.Latitude) || fix.Latitude is < -90 or > 90 ||
            !double.IsFinite(fix.Longitude) || fix.Longitude is < -180 or > 180 ||
            fix.AccuracyMetres is not { } accuracy || !double.IsFinite(accuracy) ||
            accuracy <= 0 || accuracy > policy.MaximumAccuracyMetres)
        {
            ResetEvidence();

            return MotionDecision.None;
        }

        if (lastObserved is { } observed && fix.MonotonicTime - observed > policy.MaximumGap) ResetEvidence();

        if (previous is { } prior && RunDistance.Metres(prior, fix) /
            (fix.MonotonicTime - prior.MonotonicTime).TotalSeconds > policy.MaximumSpeedMetresPerSecond)
        {
            lastObserved = fix.MonotonicTime;
            ResetEvidence();

            return MotionDecision.None;
        }

        lastObserved = fix.MonotonicTime;
        previous = fix;

        if (phase is null or RunPhase.Recovery || reason == PauseReason.Manual)
        {
            ResetEvidence();

            return MotionDecision.None;
        }

        if (reason == PauseReason.Automatic)
        {
            Candidate = null;

            if (origin is null)
            {
                origin = fix;

                return MotionDecision.None;
            }

            if (RunDistance.Metres(origin, fix) <= Math.Max(policy.MinimumResumeMetres,
                origin.AccuracyMetres!.Value + accuracy)) return MotionDecision.None;

            observation = "Moving";
            Event = new("auto-resume", phaseIndex, null, null, fix.MonotonicTime, receivedAt);
            origin = fix;

            return MotionDecision.Resume;
        }

        // Opposite sides of a small cluster can still produce an accepted distance edge.
        // Such an edge disproves confirmation and must never be rolled back as standstill.
        if (acceptedMovingEdge)
        {
            Candidate = null;
            origin = fix;
            observation = "Moving";

            return MotionDecision.None;
        }

        if (Candidate is null || Candidate.PhaseIndex != phaseIndex || origin is null)
        {
            origin = fix;
            Candidate = new(phaseIndex, fix.MonotonicTime, fix.MonotonicTime + TimeSpan.FromSeconds(10));
            observation = "Standstill candidate";

            return MotionDecision.CandidateStarted;
        }

        if (RunDistance.Metres(origin, fix) > policy.StationaryRadiusMetres)
        {
            ResetEvidence();

            return MotionDecision.None;
        }

        if (fix.MonotonicTime < Candidate.ExcludedThroughMonotonicTime) return MotionDecision.None;

        Event = new("auto-pause", phaseIndex, Candidate.OnsetMonotonicTime,
            Candidate.ExcludedThroughMonotonicTime, fix.MonotonicTime, receivedAt);
        Candidate = null;
        origin = fix;
        observation = "Standstill confirmed";

        return MotionDecision.Pause;
    }

    private void ResetEvidence()
    {
        Candidate = null;
        origin = previous = null;
        observation = "Unknown";
    }
}
