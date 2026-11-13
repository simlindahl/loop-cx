namespace LoopCx.Features.GhostRun;

public sealed record RouteMatchPolicy(string RuleVersion, double CorridorMetres,
    double GateMetres, double MaximumAccuracyMetres, TimeSpan MaximumGap,
    double MaximumSpeedMetresPerSecond, double MaximumStepMetres, double ProjectionExtentMetres);
public enum RouteMatchState { NotStarted, Tracking, Uncertain, Finished }
public enum RouteMatchProblem { StartRequired, MoveToStart, InvalidFix, NonMonotonicTime, Gap, ImplausibleJump, OffRoute, OutsideProjection }
public sealed record RouteMatchSnapshot(RouteKey Route, string RuleVersion, RouteMatchState State,
    double? Metres, int NextVertex, TimeSpan? StartedAt, TimeSpan? FinishedAt, RouteMatchProblem? Problem);

/// <summary>Conservative ordered-segment evidence under an explicit synthetic/field-test policy.</summary>
public sealed class OrderedRouteMatcher
{
    private readonly GhostRoute route;
    private readonly PlanePoint[] vertices;
    private readonly double longitudeScale;
    private RecordedRouteFix? lastInput;
    private RecordedRouteFix? anchor;
    private PlanePoint anchorPoint;
    private double anchorMetres;
    private int nextVertex = 1;
    private TimeSpan? startedAt;
    private TimeSpan? finishedAt;
    public RouteMatchPolicy Policy { get; }
    public RouteMatchSnapshot Current { get; private set; }

    public OrderedRouteMatcher(GhostRoute route, RouteMatchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(policy);
        var positive = new[] { policy.CorridorMetres, policy.GateMetres, policy.MaximumAccuracyMetres,
            policy.MaximumSpeedMetresPerSecond, policy.MaximumStepMetres, policy.ProjectionExtentMetres };
        if (route.RuleVersion != policy.RuleVersion || string.IsNullOrWhiteSpace(policy.RuleVersion) ||
            positive.Any(v => !double.IsFinite(v) || v <= 0) || policy.MaximumGap <= TimeSpan.Zero ||
            policy.MaximumAccuracyMetres > Math.Min(policy.CorridorMetres, policy.GateMetres))
            throw new ArgumentException("Matching needs the route's rule version and explicit positive quality limits.");
        this.route = route; Policy = policy;
        if (Math.Abs(route.Geometry[0].Latitude) >= 85)
            throw new ArgumentException("This local-plane rule does not support polar routes.");
        longitudeScale = Math.Cos(route.Geometry[0].Latitude * Math.PI / 180);
        vertices = route.Geometry.Select(v => Project(v.Latitude, v.Longitude)).ToArray();
        if (vertices.Any(v => Length(v) > policy.ProjectionExtentMetres) ||
            vertices.Skip(1).Where((v, i) => Length(v - vertices[i]) < 0.000001).Any())
            throw new ArgumentException("Route lies outside the projection policy or has a zero-length geometry edge.");
        Current = Snapshot(RouteMatchState.NotStarted, null, null);
    }

    public RouteMatchSnapshot Start(RecordedRouteFix fix)
    {
        if (startedAt is not null) return Current;
        if (fix is not null && fix == lastInput) return Current;
        var problem = Validate(fix, out var point);
        if (problem is not null) return Unknown(problem.Value);
        if (fix.GapBefore) return Unknown(RouteMatchProblem.Gap);
        if (Length(point - vertices[0]) + fix.AccuracyMetres!.Value > Policy.GateMetres)
            return Unknown(RouteMatchProblem.MoveToStart);
        startedAt = fix.Elapsed; anchor = fix; anchorPoint = point; anchorMetres = 0;
        return Current = Snapshot(RouteMatchState.Tracking, 0, null);
    }

    public RouteMatchSnapshot Observe(RecordedRouteFix fix)
    {
        if (finishedAt is not null || (fix is not null && fix == lastInput)) return Current;
        if (startedAt is null) return Unknown(RouteMatchProblem.StartRequired);
        var problem = Validate(fix, out var point);
        if (problem is not null) return Unknown(problem.Value);
        var previous = anchor!;
        var elapsed = fix.Elapsed - previous.Elapsed;
        var step = point - anchorPoint;
        var length = Length(step);
        var accuracy = Math.Max(previous.AccuracyMetres!.Value, fix.AccuracyMetres!.Value);
        if (fix.GapBefore || elapsed > Policy.MaximumGap)
        {
            // Only return to the last covered anchor; a re-entry farther ahead proves no missing path.
            if (length + accuracy <= Policy.GateMetres)
            {
                anchor = fix; anchorPoint = point;
                return Current = Snapshot(RouteMatchState.Tracking, anchorMetres, null);
            }
            return Unknown(RouteMatchProblem.Gap);
        }
        if (elapsed <= TimeSpan.Zero) return Unknown(RouteMatchProblem.NonMonotonicTime);
        if (length > Policy.MaximumStepMetres || length / elapsed.TotalSeconds > Policy.MaximumSpeedMetresPerSecond)
            return Unknown(RouteMatchProblem.ImplausibleJump);

        var trialNext = nextVertex;
        double lastFraction = -1;
        if (length > 0.000001)
        {
            while (trialNext < vertices.Length)
            {
                var edge = vertices[trialNext] - vertices[trialNext - 1];
                var fraction = Dot(vertices[trialNext] - anchorPoint, step) / Dot(step, step);
                if (fraction < 0 || fraction > 1 || fraction < lastFraction || Dot(step, edge) <= 0 ||
                    Length(vertices[trialNext] - (anchorPoint + step * fraction)) + accuracy > Policy.GateMetres)
                    break;
                lastFraction = fraction;
                trialNext++;
            }
        }
        if (trialNext == vertices.Length)
        {
            nextVertex = trialNext;
            finishedAt = previous.Elapsed + TimeSpan.FromTicks((long)Math.Round(elapsed.Ticks * lastFraction));
            return Current = Snapshot(RouteMatchState.Finished, route.LengthMetres, null);
        }

        var segment = trialNext - 1;
        var vector = vertices[segment + 1] - vertices[segment];
        var along = Math.Clamp(Dot(point - vertices[segment], vector) / Dot(vector, vector), 0, 1);
        if (Length(point - (vertices[segment] + vector * along)) + fix.AccuracyMetres!.Value > Policy.CorridorMetres)
            return Unknown(RouteMatchProblem.OffRoute);

        // Commit coverage only when the endpoint and every intervening gate match in order.
        nextVertex = trialNext;
        anchor = fix; anchorPoint = point;
        anchorMetres = route.Geometry[segment].Metres +
            along * (route.Geometry[segment + 1].Metres - route.Geometry[segment].Metres);
        return Current = Snapshot(RouteMatchState.Tracking, anchorMetres, null);
    }

    private RouteMatchProblem? Validate(RecordedRouteFix fix, out PlanePoint point)
    {
        point = default;
        if (fix is null || fix.Elapsed < TimeSpan.Zero)
            return RouteMatchProblem.InvalidFix;
        if (lastInput is not null && fix.Elapsed <= lastInput.Elapsed)
            return RouteMatchProblem.NonMonotonicTime;
        lastInput = fix;
        if (fix.Section != RecordedRunSection.Main || !double.IsFinite(fix.Latitude) || fix.Latitude is < -90 or > 90 ||
            !double.IsFinite(fix.Longitude) || fix.Longitude is < -180 or > 180 ||
            fix.AccuracyMetres is not { } accuracy || !double.IsFinite(accuracy) ||
            accuracy < 0 || accuracy > Policy.MaximumAccuracyMetres)
            return RouteMatchProblem.InvalidFix;
        point = Project(fix.Latitude, fix.Longitude);
        return Length(point) > Policy.ProjectionExtentMetres ? RouteMatchProblem.OutsideProjection : null;
    }

    private PlanePoint Project(double latitude, double longitude)
    {
        const double scale = 6371008.8 * Math.PI / 180;
        return new(Math.IEEERemainder(longitude - route.Geometry[0].Longitude, 360) * scale * longitudeScale,
            (latitude - route.Geometry[0].Latitude) * scale);
    }

    private RouteMatchSnapshot Unknown(RouteMatchProblem problem) =>
        Current = Snapshot(RouteMatchState.Uncertain, null, problem);
    private RouteMatchSnapshot Snapshot(RouteMatchState state, double? metres, RouteMatchProblem? problem) =>
        new(route.Key, Policy.RuleVersion, state, metres, nextVertex, startedAt, finishedAt, problem);
    private static double Dot(PlanePoint a, PlanePoint b) => a.X * b.X + a.Y * b.Y;
    private static double Length(PlanePoint value) => Math.Sqrt(Dot(value, value));
    private readonly record struct PlanePoint(double X, double Y)
    {
        public static PlanePoint operator -(PlanePoint a, PlanePoint b) => new(a.X - b.X, a.Y - b.Y);
        public static PlanePoint operator +(PlanePoint a, PlanePoint b) => new(a.X + b.X, a.Y + b.Y);
        public static PlanePoint operator *(PlanePoint a, double amount) => new(a.X * amount, a.Y * amount);
    }
}
