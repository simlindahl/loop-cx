namespace LoopCx.Features.GhostRun;

public readonly record struct RouteKey(Guid Id, Guid Version);
public sealed record RouteVertex(double Latitude, double Longitude, double Metres);
public sealed record GhostPoint(TimeSpan Elapsed, double Metres, bool Known = true);
public enum AttemptQuality { Verified, Incomplete, Interrupted, InsufficientEvidence }
public enum GhostMode { YearBest, PersonalBest }

/// <summary>Frozen main-run geometry; ordering defines start, finish and direction.</summary>
public sealed class GhostRoute
{
    public GhostRoute(RouteKey key, string name, Guid sourceRunId, string ruleVersion,
        IReadOnlyList<RouteVertex> geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (key.Id == Guid.Empty || key.Version == Guid.Empty || sourceRunId == Guid.Empty ||
            string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ruleVersion) || geometry.Count < 2)
            throw new ArgumentException("A route needs identity, provenance, rules and ordered geometry.");
        var copy = geometry.ToArray();
        for (var i = 0; i < copy.Length; i++)
        {
            var p = copy[i];
            if (p is null || !double.IsFinite(p.Latitude) || p.Latitude is < -90 or > 90 ||
                !double.IsFinite(p.Longitude) || p.Longitude is < -180 or > 180 ||
                !double.IsFinite(p.Metres) || (i == 0 ? p.Metres != 0 : p.Metres <= copy[i - 1].Metres))
                throw new ArgumentException("Route vertices need valid coordinates and strictly ordered chainage.");
        }
        Key = key; Name = name; SourceRunId = sourceRunId; RuleVersion = ruleVersion;
        Geometry = Array.AsReadOnly(copy);
    }

    public RouteKey Key { get; }
    public string Name { get; }
    public Guid SourceRunId { get; }
    public string RuleVersion { get; }
    public IReadOnlyList<RouteVertex> Geometry { get; }
    public double LengthMetres => Geometry[^1].Metres;
}

/// <summary>Recorded elapsed-time progress. Verification is supplied by the future route matcher.</summary>
public sealed class GhostAttempt
{
    public GhostAttempt(Guid id, Guid sourceRunId, RouteKey route, DateTimeOffset startedLocal,
        string timeZoneId, TimeSpan elapsed, AttemptQuality quality, string ruleVersion,
        IReadOnlyList<GhostPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (id == Guid.Empty || sourceRunId == Guid.Empty || route.Id == Guid.Empty ||
            route.Version == Guid.Empty || string.IsNullOrWhiteSpace(timeZoneId) ||
            string.IsNullOrWhiteSpace(ruleVersion) || elapsed <= TimeSpan.Zero || points.Count == 0 ||
            !Enum.IsDefined(quality))
            throw new ArgumentException("Attempt identity, saved local start, elapsed time and evidence are required.");
        var copy = points.ToArray();
        for (var i = 0; i < copy.Length; i++)
            if (copy[i] is null || copy[i].Elapsed < TimeSpan.Zero || copy[i].Elapsed > elapsed ||
                !double.IsFinite(copy[i].Metres) || copy[i].Metres < 0 ||
                (i > 0 && copy[i].Elapsed <= copy[i - 1].Elapsed))
                throw new ArgumentException("Progress samples must have strictly increasing elapsed times and finite positions.");
        Id = id; SourceRunId = sourceRunId; Route = route; StartedLocal = startedLocal;
        TimeZoneId = timeZoneId; Elapsed = elapsed; Quality = quality; RuleVersion = ruleVersion;
        Points = Array.AsReadOnly(copy);
    }

    public Guid Id { get; }
    public Guid SourceRunId { get; }
    public RouteKey Route { get; }
    public DateTimeOffset StartedLocal { get; }
    public string TimeZoneId { get; }
    public int SavedLocalYear => StartedLocal.Year;
    public TimeSpan Elapsed { get; }
    public AttemptQuality Quality { get; }
    public string RuleVersion { get; }
    public IReadOnlyList<GhostPoint> Points { get; }

    internal bool SameRecording(GhostAttempt other) =>
        Id == other.Id && SourceRunId == other.SourceRunId && Route == other.Route &&
        StartedLocal.EqualsExact(other.StartedLocal) && TimeZoneId == other.TimeZoneId &&
        Elapsed == other.Elapsed && Quality == other.Quality && RuleVersion == other.RuleVersion &&
        Points.SequenceEqual(other.Points);
}
