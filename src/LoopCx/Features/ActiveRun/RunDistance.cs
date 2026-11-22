using System.Globalization;
using System.Text.Json.Serialization;

namespace LoopCx.Features.ActiveRun;

[JsonConverter(typeof(JsonStringEnumConverter<SplitBasis>))]
public enum SplitBasis
{
    Distance,
    Time
}

public sealed record SplitPolicy(int Version, SplitBasis Basis, int Interval)
{
    public static SplitPolicy DefaultDistance { get; } = new(1, SplitBasis.Distance, 1000);
    public static SplitPolicy DefaultTime { get; } = new(1, SplitBasis.Time, 5);

    [JsonIgnore]
    public string Label => Basis == SplitBasis.Time
        ? FormattableString.Invariant($"{Interval} min")
        : (Interval / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + " km";

    public void Validate()
    {
        var validInterval = Basis switch
        {
            SplitBasis.Distance => Interval is >= 500 and <= 5000 && Interval % 500 == 0,
            SplitBasis.Time => Interval is >= 1 and <= 30,
            _ => false
        };

        if (Version != 1 || !validInterval)
        {
            throw new ArgumentException("Choose a distance from 0.5 to 5 km in half-kilometre steps or a time from 1 to 30 whole minutes.");
        }
    }
}

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
    bool MainHasGaps, string Status, IReadOnlyList<DistanceSplit> Splits)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SplitPolicy? SplitPolicy { get; init; }
}

/// <summary>Conservative ordinary-run distance; never invents an edge across lost evidence.</summary>
public sealed class RunDistance
{
    private readonly DistancePolicy policy;
    private readonly SplitPolicy? splitPolicy;
    private readonly List<DistanceSplit> splits = [];
    private readonly List<double> timeSplitMetres = [];
    private readonly TimeSpan timeInterval;
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

    public RunDistance(DistancePolicy policy, TimeSpan started, RunPhase? phase, SplitPolicy? splitPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(policy.Version) || policy.MaximumGap <= TimeSpan.Zero || policy.MaximumAge <= TimeSpan.Zero ||
            !double.IsFinite(policy.MaximumAccuracyMetres) || policy.MaximumAccuracyMetres <= 0 ||
            !double.IsFinite(policy.MaximumSpeedMetresPerSecond) || policy.MaximumSpeedMetresPerSecond <= 0 ||
            !double.IsFinite(policy.MinimumMovementMetres) || policy.MinimumMovementMetres <= 0)
        {
            throw new ArgumentException("Distance policy requires finite positive limits.", nameof(policy));
        }

        splitPolicy?.Validate();
        this.policy = policy;
        this.splitPolicy = splitPolicy;
        timeInterval = splitPolicy is { Basis: SplitBasis.Time } ? TimeSpan.FromMinutes(splitPolicy.Interval) : TimeSpan.Zero;
        boundary = started;
        this.phase = phase;
    }

    public AcceptedDistanceEdge? AcceptedEdge { get; private set; }

    public DistanceObservation Observation { get; private set; } = new(false, null, TimeSpan.Zero);

    public DistanceState Snapshot => new(policy, totalMetres, mainMetres, mainHasGaps, status, [.. splits])
    {
        SplitPolicy = splitPolicy
    };

    public DistanceState SnapshotAt(TimeSpan mainActiveTime)
    {
        if (timeInterval == TimeSpan.Zero)
        {
            return Snapshot;
        }

        var completed = new List<DistanceSplit>();
        var remaining = mainActiveTime;

        while (remaining >= timeInterval || (mainFinished && remaining > TimeSpan.Zero))
        {
            var index = completed.Count;
            var duration = remaining >= timeInterval ? timeInterval : remaining;
            var metres = index < timeSplitMetres.Count ? timeSplitMetres[index] : 0;
            completed.Add(new(index + 1, metres, duration, splitHasGaps));
            remaining -= duration;
        }

        return Snapshot with { Splits = completed };
    }

    public int SplitNumberAt(TimeSpan mainActiveTime)
    {
        return timeInterval == TimeSpan.Zero
            ? splits.Count + 1
            : (int)(mainActiveTime.Ticks / timeInterval.Ticks) + 1;
    }

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
            if (timeInterval == TimeSpan.Zero && (splitMetres > 0 || mainActiveTime > splitStarted))
            {
                splits.Add(new(splits.Count + 1, splitMetres, mainActiveTime - splitStarted, splitHasGaps));
            }

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

    public bool Observe(DistanceFix fix, TimeSpan receivedAt, TimeSpan mainActiveAtReceipt) =>
        ObserveCore(fix, receivedAt, mainActiveAtReceipt, activeTimeIsAtSample: false);

    // Callers with a historical pause-union clock already know the sample's active time.
    public bool ObserveAtSampleActiveTime(DistanceFix fix, TimeSpan receivedAt, TimeSpan mainActiveAtSample) =>
        ObserveCore(fix, receivedAt, mainActiveAtSample, activeTimeIsAtSample: true);

    private bool ObserveCore(DistanceFix fix, TimeSpan receivedAt, TimeSpan activeTime, bool activeTimeIsAtSample)
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
        var sampleMainTime = IsMain(phase) && !activeTimeIsAtSample ? activeTime - (receivedAt - fix.MonotonicTime) : activeTime;
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
        bool SplitHasGaps, bool MainHasGaps, double TotalMetres, double MainMetres)
    {
        internal double[]? TimeSplitMetres { get; init; }
    }

    public CandidateDistance CaptureCandidate()
    {
        return new(splits.Count, splitStarted, splitMetres, splitHasGaps, mainHasGaps, totalMetres, mainMetres)
        {
            TimeSplitMetres = timeInterval > TimeSpan.Zero ? [.. timeSplitMetres] : null
        };
    }

    public void ConfirmStandstill(CandidateDistance candidate, TimeSpan at, bool unknownTail)
    {
        splits.RemoveRange(candidate.SplitCount, splits.Count - candidate.SplitCount);
        splitStarted = candidate.SplitStarted;
        splitMetres = candidate.SplitMetres;
        splitHasGaps = candidate.SplitHasGaps;
        mainHasGaps = candidate.MainHasGaps;
        totalMetres = candidate.TotalMetres;
        mainMetres = candidate.MainMetres;

        if (candidate.TimeSplitMetres is { } measured)
        {
            timeSplitMetres.Clear();
            timeSplitMetres.AddRange(measured);
        }

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
        if (timeInterval > TimeSpan.Zero)
        {
            AddTimedSplitDistance(metres, from, to);
            mainMetres += metres;

            return;
        }

        var interval = splitPolicy?.Interval ?? SplitPolicy.DefaultDistance.Interval;
        var consumed = 0d;

        while (splitMetres + metres - consumed >= interval)
        {
            var part = interval - splitMetres;
            consumed += part;
            var crossing = from + TimeSpan.FromTicks((long)((to - from).Ticks * (consumed / metres)));
            splits.Add(new(splits.Count + 1, interval, crossing - splitStarted, splitHasGaps));
            splitStarted = crossing;
            splitMetres = 0;
            splitHasGaps = false;
        }
        splitMetres += metres - consumed;
        mainMetres += metres;
    }

    private void AddTimedSplitDistance(double metres, TimeSpan from, TimeSpan to)
    {
        var cursor = from.Ticks;
        var end = to.Ticks;
        var interval = timeInterval.Ticks;
        var consumed = 0d;

        while (cursor < end)
        {
            var index = (int)(cursor / interval);
            var untilBoundary = interval - (cursor % interval);
            var next = Math.Min(end, cursor + untilBoundary);
            var part = next == end ? metres - consumed : metres * (next - cursor) / (end - from.Ticks);

            while (timeSplitMetres.Count <= index)
            {
                timeSplitMetres.Add(0);
            }

            timeSplitMetres[index] += part;
            consumed += part;
            cursor = next;
        }
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
