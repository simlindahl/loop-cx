using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.GhostRun;

internal static class GhostNotificationChecks
{
    internal static void Run()
    {
        KnownComparisonUsesCorrectSignsAndNoOpponentIsDistinct();
        ActualPausedProducerAdvancesAndUnavailableClearsComparison();
        Console.WriteLine("PASS: Ghost notification comparison, pause/uncertainty producer and save-confirmation priority.");
    }

    private static void KnownComparisonUsesCorrectSignsAndNoOpponentIsDistinct()
    {
        // Arrange
        var race = RunningRace().Snapshot;
        var before = JsonSerializer.Serialize(race);

        // Act
        var ahead = GhostNotificationText.Create(race with { Gap = new(25, 12) }, null);
        var behind = GhostNotificationText.Create(race with { Gap = new(-25, -12) }, null);
        var level = GhostNotificationText.Create(race with { Gap = new(0, 0) }, null);
        var first = GhostNotificationText.Create(race with { ReferenceId = null, Gap = new(25, 12) }, null);
        var unavailable = GhostNotificationText.Create(race with { Gap = new(null, null), Match = race.Match with { Problem = RouteMatchProblem.OffRoute } }, null);

        // Assert
        Require(ahead.Title == "Ghost Run · 0:05" && ahead.Text == "12 sec ahead"
            && ahead.ExpandedText.Contains("≈ 25 m ahead along route", StringComparison.Ordinal), "Race time and primary seconds lead the secondary approximate distance.");
        Require(behind.Text == "12 sec behind" && !behind.ExpandedText.Contains("-25", StringComparison.Ordinal), "Negative values use absolute behind copy.");
        Require(level.Text == "Level" && level.ExpandedText.Contains("≈ 0 m along route", StringComparison.Ordinal), "Exact zero is level, not ahead or behind.");
        Require(first.Text == "No ghost · setting a first time" && !first.ExpandedText.Contains("25 m", StringComparison.Ordinal)
            && unavailable.Text == "Comparison unavailable" && unavailable.ExpandedText.Contains("Off route", StringComparison.Ordinal), "No opponent differs from an uncertain existing comparison.");
        Require(before == JsonSerializer.Serialize(race), "Notification presentation does not alter frozen snapshot facts.");
    }

    private static void ActualPausedProducerAdvancesAndUnavailableClearsComparison()
    {
        // Arrange
        var race = RunningRace();
        var liveFailure = GhostNotificationText.Create(race.Snapshot, new("error", "write failed"));
        race.Pause(105000);
        race.Observe(106000);

        // Act
        var paused = GhostNotificationText.Create(race.Snapshot, new("recording"));
        race.Observe(107000);
        race.Unavailable();
        var unknown = GhostNotificationText.Create(race.Snapshot, new("recording"));
        race.End(108000);
        var failedEnd = GhostNotificationText.Create(race.Snapshot, new("error", "write failed"));
        var pendingEnd = GhostNotificationText.Create(race.Snapshot, new("recording"));
        var confirmed = GhostNotificationText.Create(race.Snapshot, new("saved"));

        // Assert
        Require(paused.Title == "Ghost Run · 0:06" && paused.Text != "Comparison unavailable" && paused.ExpandedText.Contains("Training paused · race clock continues", StringComparison.Ordinal),
            "Actual service-owned race clock advances during training pause without a presentation timer.");
        Require(unknown.Text == "Comparison unavailable" && !unknown.ExpandedText.Contains("along route", StringComparison.Ordinal)
            && race.Snapshot.Gap.SecondsAhead is null, "Actual provider interruption removes prior seconds and metres; End cannot resurrect them.");
        Require(liveFailure.Text == "Recent recording could not be saved · open LOOP Cx"
            && failedEnd.Text == "Final save not confirmed · open LOOP Cx", "Live and terminal persistence failures override comparison without claiming Saved.");
        Require(pendingEnd.Text == "Saving recording…" && confirmed.Text == "Saved locally", "Terminal acknowledgement is shown only for confirmed storage state.");
    }

    private static GhostRace RunningRace()
    {
        var start = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var key = new RouteKey(Guid.Parse("31313131-3131-4131-8131-313131313131"), Guid.Parse("41414141-4141-4141-8141-414141414141"));
        const double length = 22.23901604670658;
        var route = new GhostRoute(key, "Notification fixture", Guid.NewGuid(), "notification-test-v1",
            [new(0, 0, 0), new(0, .0001, length / 2), new(0, .0002, length)]);
        var reference = new GhostAttempt(Guid.NewGuid(), Guid.NewGuid(), key, start.AddDays(-1), "UTC", S(20), AttemptQuality.Verified,
            route.RuleVersion, [.. Enumerable.Range(0, 21).Select(static second => new GhostPoint(S(second), length * second / 20))]);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1, S(3), 4, 5, 500),
            S(3), S(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot);
        var race = new GhostRace(Guid.NewGuid(), route, GhostMode.YearBest, reference, profile,
            new(99900, 100000, start.ToUnixTimeMilliseconds(), 0, 0, .1), 100000, start, "UTC", TimeSpan.Zero, null);

        for (var second = 1; second <= 5; second++)
        {
            race.Location(new(start.AddSeconds(second).ToUnixTimeMilliseconds(), (100000 + (second * 1000L)) * 1000000,
                0, second * .00001, .1), 100000 + (second * 1000L));
        }

        return race;
    }

    private static TimeSpan S(double seconds)
    {
        return TimeSpan.FromSeconds(seconds);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
