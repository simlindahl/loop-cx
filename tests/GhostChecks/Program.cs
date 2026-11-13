using System.Text.Json;
using LoopCx.Features.GhostRun;

static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
static void Near(double? actual, double expected, string message) =>
    Require(actual is { } value && Math.Abs(value - expected) < 0.000001, message);
static void Reject(Action action, string message)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new InvalidOperationException(message);
}
static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

var key = new RouteKey(Id(1), Id(2));
var geometry = new[] { new RouteVertex(10, 10, 0), new RouteVertex(10.001, 10.001, 1000), new RouteVertex(10, 10, 2000) };
var route = new GhostRoute(key, "Synthetic loop", Id(3), "synthetic-v1", geometry);
geometry[1] = geometry[1] with { Metres = 999 };
Require(route.Geometry[1].Metres == 1000, "Geometry is frozen against source mutation; loop endpoint is allowed.");

GhostAttempt Attempt(int id, double seconds, DateTimeOffset local, AttemptQuality quality = AttemptQuality.Verified,
    RouteKey? routeKey = null, string rules = "synthetic-v1", string zone = "Europe/Stockholm") =>
    new(Id(id), Id(id + 100), routeKey ?? key, local, zone, S(seconds), quality, rules,
        new[] { new GhostPoint(S(0), 0), new GhostPoint(S(seconds), 2000) });

var yearBest = Attempt(10, 1650, new DateTimeOffset(2026, 11, 10, 7, 0, 0, TimeSpan.FromHours(1)));
var personalBest = Attempt(11, 1600, new DateTimeOffset(2025, 9, 12, 7, 0, 0, TimeSpan.FromHours(2)));
var records = GhostReferences.Select(route, new[] { yearBest, personalBest }, 2026);
Require(records.YearBest == yearBest && records.PersonalBest == personalBest && !records.SameReference,
    "2026 year best 27:30 and 2025 PB 26:40 select different recordings.");
Require(records.ReferenceFor(GhostMode.YearBest) == yearBest && records.ReferenceFor(GhostMode.PersonalBest) == personalBest,
    "Only the two decided modes are supported.");
var nextYear = GhostReferences.Select(route, new[] { yearBest, personalBest }, 2027);
Require(nextYear.YearBest is null && nextYear.PersonalBest == personalBest, "No year record must not silently substitute PB.");
var none = GhostReferences.Select(route, Array.Empty<GhostAttempt>(), 2026);
Require(none.YearBest is null && none.PersonalBest is null, "First time has no ghost.");
Require(GhostReferences.Select(route, new[] { yearBest, yearBest }, 2026).SameReference,
    "Repeated insertion of the same recording is idempotent.");
var conflicting = new GhostAttempt(Id(12), yearBest.SourceRunId, key, yearBest.StartedLocal,
    yearBest.TimeZoneId, S(100), AttemptQuality.Verified, "synthetic-v1",
    new[] { new GhostPoint(S(0), 0), new GhostPoint(S(100), 2000) });
Reject(() => GhostReferences.Select(route, new[] { yearBest, conflicting }, 2026),
    "Conflicting duplicate recordings must be rejected.");
var faster = Attempt(13, 1500, yearBest.StartedLocal);
_ = GhostReferences.Select(route, new[] { yearBest, personalBest, faster }, 2026);
Require(records.YearBest == yearBest && records.PersonalBest == personalBest, "A frozen reference never changes during a race.");

var excluded = new[] {
    Attempt(20, 100, yearBest.StartedLocal, AttemptQuality.Interrupted),
    Attempt(21, 100, yearBest.StartedLocal, AttemptQuality.Incomplete),
    Attempt(22, 100, yearBest.StartedLocal, AttemptQuality.InsufficientEvidence),
    Attempt(23, 100, yearBest.StartedLocal, routeKey: new RouteKey(key.Id, Id(500))),
    Attempt(24, 100, yearBest.StartedLocal, routeKey: new RouteKey(Id(501), key.Version)),
    Attempt(25, 100, yearBest.StartedLocal, rules: "different-rule")
};
Require(GhostReferences.Select(route, excluded.Append(yearBest), 2026).PersonalBest == yearBest,
    "Route versions, identity, rule versions and verification verdicts are respected.");
var boundary = Attempt(30, 1400, new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.FromHours(-5)), zone: "America/New_York");
Require(boundary.StartedLocal.UtcDateTime.Year == 2026 && boundary.SavedLocalYear == 2025,
    "A later timezone conversion must not rewrite the saved local year.");
Require(GhostReferences.Select(route, new[] { boundary }, 2026).YearBest is null, "Year selection uses saved local start.");
var tiedLate = Attempt(42, 1650, yearBest.StartedLocal.AddMinutes(1));
var tiedHighId = Attempt(41, 1650, yearBest.StartedLocal);
var tiedLowId = Attempt(40, 1650, yearBest.StartedLocal);
foreach (var list in new[] { new[] { tiedLate, tiedHighId, tiedLowId }, new[] { tiedLowId, tiedHighId, tiedLate } })
    Require(GhostReferences.Select(route, list, 2026).YearBest == tiedLowId, "Ties use first UTC start then stable ID.");

var restoredRoute = JsonSerializer.Deserialize<GhostRoute>(JsonSerializer.Serialize(route))!;
var restoredAttempt = JsonSerializer.Deserialize<GhostAttempt>(JsonSerializer.Serialize(boundary))!;
Require(restoredRoute.Key == route.Key && restoredRoute.Geometry.SequenceEqual(route.Geometry), "Route JSON round-trip retains version and ordering.");
Require(restoredAttempt.SavedLocalYear == 2025 && restoredAttempt.StartedLocal.EqualsExact(boundary.StartedLocal) &&
    restoredAttempt.TimeZoneId == boundary.TimeZoneId && restoredAttempt.Points.SequenceEqual(boundary.Points),
    "Attempt JSON round-trip retains local date, timezone and elapsed evidence.");
Reject(() => new GhostRoute(key, "Bad", Id(3), "v1", new[] { new RouteVertex(10, 10, 0), new RouteVertex(10, 10, double.NaN) }), "Reject non-finite geometry.");

GhostAttempt Recording(params GhostPoint[] points) =>
    new(Id(60), Id(160), key, yearBest.StartedLocal, "Europe/Stockholm", points[^1].Elapsed,
        AttemptQuality.Verified, "synthetic-v1", points);
var stopped = new GhostPlayback(Recording(new(S(0), 0), new(S(60), 100), new(S(120), 100),
    new(S(180), 300), new(S(200), 350), new(S(220), 300), new(S(240), 400)), S(60));
Near(stopped.PositionAt(S(90)), 100, "Replay preserves a recorded stop.");
Require(stopped.ArrivalAt(100) == S(60), "Arrival uses the beginning of a stop.");
Near(stopped.PositionAt(S(150)), 200, "Replay preserves changing pace.");
Near(stopped.PositionAt(S(210)), 325, "Replay retains recorded backwards movement.");
Require(stopped.ArrivalAt(350) == S(200), "First arrival is stable across later backtracking.");
Near(stopped.PositionAt(S(300)), 400, "Ghost stays at the finish while race time continues.");
Near(stopped.Compare(S(300), 400).SecondsAhead, -60, "Time gap keeps running after the ghost finishes.");

var benchmark = new GhostPlayback(Recording(new(S(0), 0), new(S(580), 1900), new(S(612), 2000), new(S(700), 2300)), S(600));
var gap = benchmark.Compare(S(600), 2000);
Near(gap.SecondsAhead, 12, "2 km at 10:12 versus runner at 10:00 is 12 seconds ahead.");
Near(gap.MetresAhead, 37.5, "Metres ahead uses both positions at the same elapsed time.");
Require(gap.SecondsAhead != gap.MetresAhead, "Seconds gap is not inferred from current pace or metres.");
var uncertain = new GhostPlayback(Recording(new(S(0), 0), new(S(5), 10), new(S(20), 100),
    new(S(25), 110, false), new(S(30), 120)), S(10));
Require(uncertain.PositionAt(S(10)) is null && uncertain.PositionAt(S(27)) is null, "Never interpolate long gaps or invalid fixes.");
Require(uncertain.ArrivalAt(100) is null, "A first arrival hidden by an earlier gap remains unknown.");
Require(uncertain.Compare(S(10), 20) == new GhostGap(null, null), "Unknown ghost position hides comparison.");
Require(benchmark.Compare(S(600), null) == new GhostGap(null, null), "Unknown runner position hides comparison.");
Reject(() => benchmark.PositionAt(S(-1)), "Negative race time is invalid.");
Reject(() => benchmark.Compare(S(1), double.NaN), "Non-finite runner position is invalid.");
Console.WriteLine("PASS: immutable data/JSON, exact route version, PB/year selection, local year, ties, no records, frozen reference and duplicate protection.");
Console.WriteLine("PASS: elapsed-time replay, stops, backtracking, first arrival, 12-second lead, post-finish timing and unknown GPS gaps.");

var lateStart = new GhostPlayback(Recording(new(S(5), 10), new(S(10), 20)), S(10));
Require(lateStart.PositionAt(S(1)) is null, "Never extrapolate before the first measured point.");
Require(lateStart.ArrivalAt(20) is null, "A missing beginning cannot establish the first arrival.");

RouteCaptureChecks.Run();
