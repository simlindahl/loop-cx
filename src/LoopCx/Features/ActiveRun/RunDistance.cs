namespace LoopCx.Features.ActiveRun;

public sealed record DistancePolicy(string Version, double MaximumAccuracyMetres,
    TimeSpan MaximumGap, TimeSpan MaximumAge, double MaximumSpeedMetresPerSecond, double MinimumMovementMetres)
{
    public static DistancePolicy Pilot { get; } = new("ordinary-gps-pilot-v1", 30,
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), 12, 3);
}

public sealed record DistanceObservation(bool Valid, TimeSpan? PreviousMonotonicTime, TimeSpan MonotonicTime)
{
    public bool Ignored { get; init; }
}

public sealed record AcceptedDistanceEdge(TimeSpan From, TimeSpan To, double Metres);

public sealed record DistanceFix(TimeSpan MonotonicTime, double Latitude, double Longitude, double? AccuracyMetres);
public sealed record DistanceSplit(int Number, double MeasuredMetres, TimeSpan ActiveTime, bool HasGaps)
{
    public double? PaceSecondsPerKilometre => !HasGaps && MeasuredMetres > 0
        ? ActiveTime.TotalSeconds * 1000 / MeasuredMetres : null;
}
public sealed record DistanceState(DistancePolicy Policy, double TotalMeasuredMetres, double MainMeasuredMetres,
    bool MainHasGaps, string Status, IReadOnlyList<DistanceSplit> Splits);

/// <summary>Conservative ordinary-run distance; never invents an edge across lost evidence.</summary>
public sealed class RunDistance
{
    private readonly DistancePolicy policy;
    private readonly List<DistanceSplit> splits = [];
    private DistanceFix? anchor;
    private DistanceFix? previousValid;
    private TimeSpan anchorMainTime;
    private TimeSpan? lastObserved;
    private TimeSpan boundary;
    private RunPhase? phase;
    private TimeSpan splitStarted;
    private double splitMetres;
    private bool splitHasGaps;
    private bool mainHasGaps;
    private double totalMetres;
    private double mainMetres;
    private string status = "Waiting for GPS";
    private bool mainFinished;

    public RunDistance(DistancePolicy policy, TimeSpan started, RunPhase? phase)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(policy.Version) || policy.MaximumGap <= TimeSpan.Zero || policy.MaximumAge <= TimeSpan.Zero ||
            !double.IsFinite(policy.MaximumAccuracyMetres) || policy.MaximumAccuracyMetres <= 0 ||
            !double.IsFinite(policy.MaximumSpeedMetresPerSecond) || policy.MaximumSpeedMetresPerSecond <= 0 ||
            !double.IsFinite(policy.MinimumMovementMetres) || policy.MinimumMovementMetres <= 0)
        {
            throw new ArgumentException("Distance policy requires finite positive limits.", nameof(policy));
        }

        this.policy = policy;
        boundary = started;
        this.phase = phase;
    }

    public AcceptedDistanceEdge? AcceptedEdge { get; private set; }

    public DistanceObservation Observation { get; private set; } = new(false, null, TimeSpan.Zero);

    public DistanceState Snapshot => new(policy, totalMetres, mainMetres, mainHasGaps, status, [.. splits]);

    public void Boundary(RunPhase? nextPhase, TimeSpan at, TimeSpan mainActiveTime)
    {
        AcceptedEdge = null;
        if (IsMain(phase) && IsMain(nextPhase))
        {
            phase = nextPhase; // Main-set distance/time is continuous through planned recovery.
            return;
        }
        if (IsMain(phase) && !IsMain(nextPhase) && !mainFinished)
        {
            if (splitMetres > 0 || mainActiveTime > splitStarted)
                splits.Add(new(splits.Count + 1, splitMetres, mainActiveTime - splitStarted, splitHasGaps));
            mainFinished = true;
        }
        boundary = at;
        anchor = previousValid = null;
        lastObserved = null;
        status = "Phase boundary";
        phase = nextPhase;
    }

    public void Interrupt(TimeSpan at, string reason)
    {
        AcceptedEdge = null;
        boundary = at > boundary ? at : boundary;
        anchor = null;
        previousValid = null;
        lastObserved = null;
        status = reason;
        if (IsMain(phase)) mainHasGaps = splitHasGaps = true;
    }

    public void UpdateFreshness(TimeSpan now)
    {
        if ((lastObserved is { } observed && now - observed > policy.MaximumGap) ||
            (lastObserved is null && now - boundary > policy.MaximumGap))
        {
            Reject("GPS gap");
        }
    }

    public void Unavailable(string reason)
    {
        Reject(reason);
    }

    public bool Observe(DistanceFix fix, TimeSpan receivedAt, TimeSpan mainActiveAtReceipt)
    {
        ArgumentNullException.ThrowIfNull(fix);
        Observation = new(false, null, fix.MonotonicTime);
        AcceptedEdge = null;

        if (phase is null) return false;
        // Repeated or stale delivery cannot replace newer evidence or destroy a valid anchor.
        if (lastObserved is { } previous && fix.MonotonicTime <= previous)
        {
            Observation = Observation with { Ignored = true };

            return false;
        }
        if (fix.MonotonicTime < boundary || fix.MonotonicTime > receivedAt ||
            receivedAt - fix.MonotonicTime > policy.MaximumAge)
        {
            Reject("Invalid sample time");
            return false;
        }
        var gap = lastObserved is { } last && fix.MonotonicTime - last > policy.MaximumGap;
        lastObserved = fix.MonotonicTime;
        if (!double.IsFinite(fix.Latitude) || fix.Latitude is < -90 or > 90 ||
            !double.IsFinite(fix.Longitude) || fix.Longitude is < -180 or > 180 ||
            fix.AccuracyMetres is not { } accuracy || !double.IsFinite(accuracy) ||
            accuracy <= 0 || accuracy > policy.MaximumAccuracyMetres)
        {
            Reject("Uncertain GPS");
            return false;
        }
        var sampleMainTime = IsMain(phase) ? mainActiveAtReceipt - (receivedAt - fix.MonotonicTime) : mainActiveAtReceipt;
        if (sampleMainTime < TimeSpan.Zero) { Reject("Ambiguous active time"); return false; }
        if (gap) Reject("GPS gap");
        if (previousValid is { } previousFix &&
            Metres(previousFix, fix) / (fix.MonotonicTime - previousFix.MonotonicTime).TotalSeconds > policy.MaximumSpeedMetresPerSecond)
        {
            Reject("Implausible GPS jump");
            return false;
        }
        Observation = new(true, previousValid?.MonotonicTime, fix.MonotonicTime);
        previousValid = fix;
        if (anchor is null)
        {
            anchor = fix;
            anchorMainTime = sampleMainTime;
            if (IsMain(phase) && sampleMainTime > TimeSpan.Zero) mainHasGaps = splitHasGaps = true;
            status = "Acquiring distance";
            return false;
        }
        var metres = Metres(anchor, fix);
        var elapsed = (fix.MonotonicTime - anchor.MonotonicTime).TotalSeconds;
        if (elapsed <= 0 || metres / elapsed > policy.MaximumSpeedMetresPerSecond)
        {
            Reject("Implausible GPS jump");
            return false;
        }
        var movementThreshold = Math.Max(policy.MinimumMovementMetres, anchor.AccuracyMetres!.Value + accuracy);
        if (metres < movementThreshold) { status = "GPS tracking"; return false; }
        AcceptedEdge = new(anchor.MonotonicTime, fix.MonotonicTime, metres);
        totalMetres += metres;
        if (IsMain(phase)) AddMain(metres, anchorMainTime, sampleMainTime);
        anchor = fix;
        anchorMainTime = sampleMainTime;
        status = "GPS tracking";

        return true;
    }

    // Only derived counters are captured. Plans, commands and raw observations never roll back.
    public sealed record CandidateDistance(int SplitCount, TimeSpan SplitStarted, double SplitMetres,
        bool SplitHasGaps, bool MainHasGaps, double TotalMetres, double MainMetres);

    public CandidateDistance CaptureCandidate() => new(splits.Count, splitStarted, splitMetres,
        splitHasGaps, mainHasGaps, totalMetres, mainMetres);

    public void ConfirmStandstill(CandidateDistance candidate, TimeSpan at, bool unknownTail)
    {
        splits.RemoveRange(candidate.SplitCount, splits.Count - candidate.SplitCount);
        splitStarted = candidate.SplitStarted;
        splitMetres = candidate.SplitMetres;
        splitHasGaps = candidate.SplitHasGaps;
        mainHasGaps = candidate.MainHasGaps;
        totalMetres = candidate.TotalMetres;
        mainMetres = candidate.MainMetres;
        Reanchor(at, "Automatic pause");

        if (unknownTail && IsMain(phase)) mainHasGaps = splitHasGaps = true;
    }

    public void Reanchor(TimeSpan at, string reason)
    {
        AcceptedEdge = null;
        boundary = at > boundary ? at : boundary;
        anchor = previousValid = null;
        lastObserved = null;
        status = reason;
    }

    private void AddMain(double metres, TimeSpan from, TimeSpan to)
    {
        var consumed = 0d;
        while (splitMetres + metres - consumed >= 1000)
        {
            var part = 1000 - splitMetres;
            consumed += part;
            var crossing = from + TimeSpan.FromTicks((long)((to - from).Ticks * (consumed / metres)));
            splits.Add(new(splits.Count + 1, 1000, crossing - splitStarted, splitHasGaps));
            splitStarted = crossing;
            splitMetres = 0;
            splitHasGaps = false;
        }
        splitMetres += metres - consumed;
        mainMetres += metres;
    }

    private void Reject(string reason)
    {
        AcceptedEdge = null;
        Observation = new(false, null, Observation.MonotonicTime);
        anchor = null;
        previousValid = null;
        status = reason;
        if (IsMain(phase)) mainHasGaps = splitHasGaps = true;
    }

    private static bool IsMain(RunPhase? phase)
    {
        return phase is RunPhase.Work or RunPhase.Recovery;
    }

    internal static double Metres(DistanceFix a, DistanceFix b)
    {
        const double radius = 6371000;
        var radians = Math.PI / 180;
        var latitude = (b.Latitude - a.Latitude) * radians;
        var longitude = (b.Longitude - a.Longitude) * radians;
        var h = Math.Pow(Math.Sin(latitude / 2), 2) + (Math.Cos(a.Latitude * radians) * Math.Cos(b.Latitude * radians) * Math.Pow(Math.Sin(longitude / 2), 2));
        return radius * 2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1)));
    }
}
