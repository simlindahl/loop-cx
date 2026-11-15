namespace LoopCx.Features.ActiveRun;

public sealed record PaceTrendState(string Version, int PhaseIndex, string Quality, string? Direction,
    double? PreviousPace, double? CurrentPace, TimeSpan? SupportedThrough);

/// <summary>Ephemeral visual comparison of two fully supported same-phase windows, not a new pace filter.</summary>
public sealed class RunPaceTrend
{
    public const string Version = "accepted-edge-pace-trend-v1";
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
    private readonly List<AcceptedDistanceEdge> edges = [];
    private TimeSpan floor, watermark;
    private int phaseIndex;
    private bool paceAvailable;

    public void Boundary(TimeSpan effectiveAt, int instance)
    {
        phaseIndex = instance;
        Reset(effectiveAt);
    }

    public void Reset(TimeSpan at)
    {
        edges.Clear();
        paceAvailable = false;
        if (at > floor) floor = at;
    }

    public void Observe(DistanceObservation observation, AcceptedDistanceEdge? edge, TimeSpan receivedAt, LivePacePolicy policy)
    {
        if (observation.Ignored) return;
        if (!observation.Valid)
        {
            Reset(receivedAt);
            return;
        }
        if (edge is null || edge.To <= watermark) return;

        var previous = watermark;
        watermark = edge.To;
        if (edge.From < floor)
        {
            edges.Clear();
            return;
        }
        if (edge.To > receivedAt || edge.To <= edge.From || edge.To - edge.From > policy.Window
            || !double.IsFinite(edge.Metres) || edge.Metres <= 0 || receivedAt - edge.To > policy.MaximumAge)
        {
            Reset(receivedAt);
            return;
        }

        // Freshness must be continuous even if a delayed callback bypassed any stale UI tick.
        if (edges.Count > 0 && (edges[^1].To != edge.From || receivedAt - previous > policy.MaximumAge))
            Reset(edge.From);

        edges.Add(edge);
        _ = edges.RemoveAll(item => item.To <= edge.To - Window - Window);
    }

    public PaceTrendState Snapshot(TimeSpan now, LivePaceState live, bool paused, bool finished)
    {
        var quality = paused ? "paused" : finished ? "unavailable" : live.Quality;
        if (paused || finished || live.SecondsPerKilometre is null || live.Quality != "tracking")
        {
            if (paused || finished || quality == "stale") Reset(now);
            else if (paceAvailable) edges.Clear();
            paceAvailable = false;
            return Unknown(quality);
        }
        paceAvailable = true;
        if (edges.Count == 0) return Unknown("acquiring");

        var end = edges[^1].To;
        if (now < end || now - end > live.Policy.MaximumAge)
        {
            Reset(now);
            return Unknown("stale");
        }

        var start = end - Window - Window;
        if (start < floor || edges[0].From > start) return Unknown("acquiring");
        var previous = Pace(start, end - Window);
        var current = Pace(end - Window, end);
        if (previous is null || current is null) return Unknown("acquiring");

        var difference = current.Value - previous.Value;
        var direction = difference <= -5 ? "Faster" : difference >= 5 ? "Slower" : "Steady";
        return new(Version, phaseIndex, "tracking", direction, previous, current, end);
    }

    private PaceTrendState Unknown(string quality)
    {
        return new(Version, phaseIndex, quality, null, null, null, null);
    }

    private double? Pace(TimeSpan from, TimeSpan to)
    {
        double metres = 0;
        long ticks = 0;
        foreach (var edge in edges)
        {
            var start = edge.From > from ? edge.From : from;
            var end = edge.To < to ? edge.To : to;
            if (end <= start) continue;
            var duration = (end - start).Ticks;
            ticks += duration;
            metres += edge.Metres * duration / (edge.To - edge.From).Ticks;
        }

        var pace = metres > 0 ? Window.TotalSeconds * 1000 / metres : double.NaN;
        return ticks == Window.Ticks && double.IsFinite(pace) && pace > 0 ? pace : null;
    }
}
