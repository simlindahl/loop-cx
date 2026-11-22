using LoopCx.Features.ActiveRun;
using LoopCx.Features.GhostRun;
using LoopCx.Features.GhostRun.Ui;

internal static class GhostLiveMapChecks
{
    private static readonly RouteKey Key = new(Guid.Parse("56565656-5656-5656-5656-565656565656"), Guid.Parse("44444444-4444-4444-4444-444444444444"));

    public static void Run()
    {
        OrderedCoordinatesAreInterpolatedAlongTheFrozenSegment();
        OnlySupportedCurrentMatchAndFrozenOpponentProduceMarkers();
        Console.WriteLine("PASS: Ghost live map exact route interpolation, wrapping and truthful same-route markers.");
    }

    private static void OrderedCoordinatesAreInterpolatedAlongTheFrozenSegment()
    {
        // Arrange
        var route = Route();
        var drawing = new GhostRouteDrawing(route);

        // Act
        var dateline = drawing.CoordinateAt(100);
        var afterTurn = drawing.CoordinateAt(250);
        var outside = drawing.CoordinateAt(301);
        var missing = drawing.CoordinateAt(null);

        // Assert
        Require(dateline is { Latitude: 0 } crossed && Math.Abs(Math.Abs(crossed.Longitude) - 180) < 0.000001
            && afterTurn is { } turned && Math.Abs(turned.Latitude - .05) < 0.000001
            && Math.Abs(turned.Longitude - -179.9) < 0.000001 && outside is null && missing is null,
            "Coordinates follow the ordered cumulative-distance segment and cross the antimeridian by the short path.");
    }

    private static void OnlySupportedCurrentMatchAndFrozenOpponentProduceMarkers()
    {
        // Arrange
        var route = Route();
        var elapsed = TimeSpan.FromSeconds(12);
        var tracking = new GhostRaceSnapshot(Guid.NewGuid(), route.Name, route.Key, GhostMode.YearBest,
            new(elapsed, elapsed, false, GhostClockState.Running), PauseReason.None,
            new(route.Key, route.RuleVersion, RouteMatchState.Tracking, 44, 1, TimeSpan.Zero, null, null),
            55, new(11, 1), Guid.NewGuid(), TimeSpan.FromMinutes(3), 2026, null);

        // Act
        var supported = GhostRouteMarkers.From(route, tracking);
        var unavailable = GhostRouteMarkers.From(route, tracking with
        {
            Match = tracking.Match with { State = RouteMatchState.Uncertain, Problem = RouteMatchProblem.OffRoute }
        });
        var staleRoute = GhostRouteMarkers.From(route, tracking with { Route = route.Key with { Version = Guid.NewGuid() } });
        var firstAttempt = GhostRouteMarkers.From(route, tracking with { ReferenceId = null });
        var staleOpponent = GhostRouteMarkers.From(route, tracking with { GhostMetres = null });

        // Assert
        Require(supported == new GhostRouteMarkers(44, 55) && unavailable == new GhostRouteMarkers(null, 55)
            && staleRoute == default && firstAttempt == new GhostRouteMarkers(44, null)
            && staleOpponent == new GhostRouteMarkers(44, null),
            "Off-route/obsolete evidence cannot hold a visible runner marker, and opponent position requires its frozen reference.");
    }

    private static GhostRoute Route()
    {
        return new(Key, "Synthetic turn", Key.Id, "synthetic-map",
            [new(0, 179.9, 0), new(0, -179.9, 200), new(.1, -179.9, 300)]);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
