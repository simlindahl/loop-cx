namespace LoopCx.Features.GhostRun;

public enum RecordedWorkoutKind { Continuous, Intervals }
public enum RecordedRunSection { Warmup, Main, Cooldown }
public enum RouteCaptureProblem
{
    NotContinuous, NotFinished, NoMainTrack, NonContiguousMain, InsufficientMovement,
    InvalidFix, NonMonotonicTime, Gap, UncertainGps, ImplausibleJump
}

public sealed record RecordedRouteFix(TimeSpan Elapsed, DateTimeOffset Utc,
    double Latitude, double Longitude, double? AccuracyMetres, RecordedRunSection Section,
    bool GapBefore = false);

public sealed record RouteCapturePolicy(string RuleVersion, TimeSpan MaximumGap,
    double MaximumAccuracyMetres, double MaximumSpeedMetresPerSecond);

public sealed class CapturedMainRoute
{
    internal CapturedMainRoute(GhostRoute route, RouteCapturePolicy policy,
        RecordedRouteFix[] raw, GhostPoint[] progress)
    {
        Route = route; Policy = policy;
        Raw = Array.AsReadOnly(raw); Progress = Array.AsReadOnly(progress);
    }

    public GhostRoute Route { get; }
    public RouteCapturePolicy Policy { get; }
    public IReadOnlyList<RecordedRouteFix> Raw { get; }
    public IReadOnlyList<GhostPoint> Progress { get; }
    public TimeSpan ObservedSpan => Progress[^1].Elapsed;
}

public sealed record RouteCaptureResult(CapturedMainRoute? Captured,
    RouteCaptureProblem? Problem, string? Detail)
{
    public bool Success => Captured is not null;
}

/// <summary>Builds route evidence only from measured main-run fixes; never certifies a race result.</summary>
public static class MainRouteCapture
{
    public static RouteCaptureResult Build(RouteKey key, string name, Guid sourceRunId,
        RecordedWorkoutKind kind, bool finished, IReadOnlyList<RecordedRouteFix> recording,
        RouteCapturePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(policy.RuleVersion) || policy.MaximumGap <= TimeSpan.Zero ||
            !double.IsFinite(policy.MaximumAccuracyMetres) || policy.MaximumAccuracyMetres <= 0 ||
            !double.IsFinite(policy.MaximumSpeedMetresPerSecond) || policy.MaximumSpeedMetresPerSecond <= 0)
            throw new ArgumentException("Capture needs explicit, positive quality limits and a rule version.", nameof(policy));
        if (kind != RecordedWorkoutKind.Continuous)
            return Fail(RouteCaptureProblem.NotContinuous, "Only a continuous workout can supply a Ghost Run route.");
        if (!finished) return Fail(RouteCaptureProblem.NotFinished, "Finish and save the source run first.");

        var all = recording.ToArray();
        if (all.Any(p => p is null || !Enum.IsDefined(p.Section)))
            return Fail(RouteCaptureProblem.InvalidFix, "The recording contains an invalid phase or missing fix.");
        var first = Array.FindIndex(all, p => p.Section == RecordedRunSection.Main);
        var last = Array.FindLastIndex(all, p => p.Section == RecordedRunSection.Main);
        if (first < 0) return Fail(RouteCaptureProblem.NoMainTrack, "No measured main-run route is available.");
        var main = all[first..(last + 1)];
        if (main.Any(p => p.Section != RecordedRunSection.Main))
            return Fail(RouteCaptureProblem.NonContiguousMain, "Main-run sections cannot be joined across another phase.");
        if (main.Length < 2) return Fail(RouteCaptureProblem.InsufficientMovement, "More main-run measurements are needed.");

        var vertices = new List<RouteVertex>();
        var progress = new List<GhostPoint>();
        double metres = 0;
        for (var i = 0; i < main.Length; i++)
        {
            var fix = main[i];
            if (!ValidCoordinate(fix) || fix.Elapsed < TimeSpan.Zero || fix.Utc.Offset != TimeSpan.Zero)
                return Fail(RouteCaptureProblem.InvalidFix, "Main-run fixes need valid coordinates, UTC and monotonic elapsed timestamps.");
            if (fix.AccuracyMetres is not { } accuracy || !double.IsFinite(accuracy) ||
                accuracy < 0 || accuracy > policy.MaximumAccuracyMetres)
                return Fail(RouteCaptureProblem.UncertainGps, "GPS accuracy is missing or exceeds this capture policy.");
            if (fix.GapBefore)
                return Fail(RouteCaptureProblem.Gap, "An explicit GPS gap must be resolved before creating a route.");
            if (i > 0)
            {
                var previous = main[i - 1];
                var elapsed = fix.Elapsed - previous.Elapsed;
                if (elapsed <= TimeSpan.Zero)
                    return Fail(RouteCaptureProblem.NonMonotonicTime, "Main-run measurement times must be strictly increasing.");
                if (elapsed > policy.MaximumGap)
                    return Fail(RouteCaptureProblem.Gap, "A main-run GPS gap cannot be turned into an invented route segment.");
                var segmentMetres = Distance(previous.Latitude, previous.Longitude, fix.Latitude, fix.Longitude);
                if (segmentMetres / elapsed.TotalSeconds > policy.MaximumSpeedMetresPerSecond)
                    return Fail(RouteCaptureProblem.ImplausibleJump, "A main-run position jump exceeds this capture policy.");
                metres += segmentMetres;
            }
            // Retain stopped samples in elapsed-time playback, without zero-length geometry edges.
            if (i == 0 || metres > vertices[^1].Metres)
                vertices.Add(new RouteVertex(fix.Latitude, fix.Longitude, metres));
            progress.Add(new GhostPoint(fix.Elapsed - main[0].Elapsed, metres));
        }
        if (vertices.Count < 2)
            return Fail(RouteCaptureProblem.InsufficientMovement, "The main run contains no measured route movement.");
        var route = new GhostRoute(key, name, sourceRunId, policy.RuleVersion, vertices);
        return new(new CapturedMainRoute(route, policy, main, progress.ToArray()), null, null);
    }

    private static bool ValidCoordinate(RecordedRouteFix fix) =>
        double.IsFinite(fix.Latitude) && fix.Latitude is >= -90 and <= 90 &&
        double.IsFinite(fix.Longitude) && fix.Longitude is >= -180 and <= 180;

    // Spherical distance is a versioned derivation; raw coordinates and accuracy remain available.
    private static double Distance(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double radiusMetres = 6371008.8;
        const double radians = Math.PI / 180;
        var dLat = (latitude2 - latitude1) * radians;
        var dLon = (longitude2 - longitude1) * radians;
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
            Math.Cos(latitude1 * radians) * Math.Cos(latitude2 * radians) * Math.Pow(Math.Sin(dLon / 2), 2);
        return radiusMetres * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    private static RouteCaptureResult Fail(RouteCaptureProblem problem, string detail) => new(null, problem, detail);
}
