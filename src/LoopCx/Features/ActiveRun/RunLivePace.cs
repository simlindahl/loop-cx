namespace LoopCx.Features.ActiveRun;

public sealed record LivePacePolicy(string Version, TimeSpan Window, TimeSpan MinimumSupport, TimeSpan MaximumAge)
{
    // Technical pilot: physical calibration remains required.
    public static LivePacePolicy Pilot { get; } = new("trusted-edge-live-pace-pilot-v1", TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
}

public sealed record LivePaceState(LivePacePolicy Policy, double? SecondsPerKilometre, string Quality,
    TimeSpan SupportedTime, TimeSpan? WindowFrom, TimeSpan? WindowTo, TimeSpan? LastAcceptedAt);

/// <summary>Bounded contiguous trusted movement edges; no second GPS validator or future extrapolation.</summary>
public sealed class RunLivePace
{
    private readonly LivePacePolicy policy;
    private readonly List<AcceptedDistanceEdge> edges = [];
    private TimeSpan? lastAcceptedAt;
    private string quality = "acquiring";

    public RunLivePace(LivePacePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(policy.Version) || policy.Window <= TimeSpan.Zero ||
            policy.MinimumSupport <= TimeSpan.Zero || policy.MinimumSupport > policy.Window || policy.MaximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentException("Live pace requires a versioned positive bounded policy.", nameof(policy));
        }

        this.policy = policy;
    }

    public void Reset(string reason = "unavailable")
    {
        edges.Clear();
        lastAcceptedAt = null;
        quality = reason;
    }

    public void Boundary(RunPhase? previous, RunPhase? next)
    {
        if (!(previous is RunPhase.Work or RunPhase.Recovery && next is RunPhase.Work or RunPhase.Recovery)) Reset();
    }

    public void Observe(DistanceObservation observation, AcceptedDistanceEdge? edge, TimeSpan receivedAt)
    {
        if (observation.Ignored) return;

        if (!observation.Valid)
        {
            Reset();

            return;
        }

        if (edge is not null) Observe(edge, receivedAt);
    }

    public void Observe(AcceptedDistanceEdge edge, TimeSpan receivedAt)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (lastAcceptedAt is { } latest && edge.To <= latest) return;

        var duration = edge.To - edge.From;

        if (duration <= TimeSpan.Zero || duration > policy.Window || edge.To > receivedAt ||
            !double.IsFinite(edge.Metres) || edge.Metres <= 0)
        {
            Reset();

            return;
        }

        if (edges.Count > 0 && edges[^1].To != edge.From) Reset();

        edges.Add(edge);
        lastAcceptedAt = edge.To;
        quality = "acquiring";
        Trim(receivedAt - policy.Window);
    }

    public LivePaceState Snapshot(TimeSpan now, bool paused = false, bool finished = false)
    {
        if (paused || finished)
        {
            Reset(paused ? "paused" : "unavailable");

            return new(policy, null, quality, TimeSpan.Zero, null, null, null);
        }

        Trim(now - policy.Window);

        if (lastAcceptedAt is not { } latest)
            return new(policy, null, quality, TimeSpan.Zero, null, null, null);

        if (now < latest || now - latest > policy.MaximumAge)
            return new(policy, null, "stale", TimeSpan.Zero, null, null, latest);

        if (edges.Count == 0)
            return new(policy, null, "acquiring", TimeSpan.Zero, null, null, latest);

        var from = edges[0].From > now - policy.Window ? edges[0].From : now - policy.Window;
        double metres = 0;
        long ticks = 0;

        foreach (var edge in edges)
        {
            var start = edge.From > from ? edge.From : from;
            var end = edge.To < now ? edge.To : now;

            if (end <= start) continue;

            var partTicks = (end - start).Ticks;
            ticks += partTicks;
            // Linear partition only within an already accepted continuous edge.
            metres += edge.Metres * partTicks / (edge.To - edge.From).Ticks;
        }

        var support = TimeSpan.FromTicks(ticks);
        var pace = support >= policy.MinimumSupport && metres > 0 && double.IsFinite(metres)
            ? support.TotalSeconds * 1000 / metres : (double?)null;
        if (pace is { } value && (!double.IsFinite(value) || value <= 0)) pace = null;

        return new(policy, pace, pace is null ? "acquiring" : "tracking", support, from, latest, latest);
    }

    private void Trim(TimeSpan before)
    {
        _ = edges.RemoveAll(edge => edge.To <= before);
    }
}
