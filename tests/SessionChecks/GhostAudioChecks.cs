using System.Diagnostics.CodeAnalysis;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.GhostRun;

internal static class GhostAudioChecks
{
    private static readonly Guid Owner = Guid.Parse("2e62407f-36b3-480a-a41b-91c22dee5fca");
    private static readonly Guid Opponent = Guid.Parse("d85fb036-4eae-4097-923a-8eed8373ea53");

    public static void Run()
    {
        OnlyReliableReentryRearmsOffRouteWarning();
        PausedDepartureAndLateHandoffAreDropped();
        ComparisonIsOptInAndUsesFreshFrozenGap();
        WarningReplacesPendingComparisonAndConsumesDueBoundary();
        ActualMatcherSuppliesWarningsAndClockExpiresDelayedHandoff();
        Console.WriteLine("PASS: Ghost audio scoped departure, pause/finish/ownership and optional comparison validity.");
    }

    private static void OnlyReliableReentryRearmsOffRouteWarning()
    {
        // Arrange
        var policy = new GhostAudioPolicy(Owner);
        var gap = State(2, problem: RouteMatchProblem.Gap);
        var offRoute = State(3, problem: RouteMatchProblem.OffRoute);

        // Act
        var uncertain = policy.Observe(gap, fresh: true, measuredMetres: 0);
        var first = policy.Observe(offRoute, fresh: true, measuredMetres: 0);
        var continued = policy.Observe(offRoute, fresh: true, measuredMetres: 0);
        _ = policy.Observe(gap, fresh: false, measuredMetres: 0);
        var afterGap = policy.Observe(offRoute, fresh: true, measuredMetres: 0);
        _ = policy.Observe(State(4), fresh: true, measuredMetres: 0);
        var afterReentry = policy.Observe(State(5, problem: RouteMatchProblem.OffRoute), fresh: true, measuredMetres: 0);

        // Assert
        Require(uncertain is null && first is { Kind: AudioCueKind.Coaching }
            && continued is null && afterGap is null && afterReentry is { Kind: AudioCueKind.Coaching }
            && !policy.IsCurrent(first, offRoute, fresh: true),
            "Only a confirmed off-route episode creates a warning. GPS loss alone does not rearm it or revive old speech.");
    }

    private static void PausedDepartureAndLateHandoffAreDropped()
    {
        // Arrange
        var policy = new GhostAudioPolicy(Owner);
        var offRoute = State(3, problem: RouteMatchProblem.OffRoute);
        var paused = offRoute with
        {
            PauseReason = PauseReason.Manual,
            Clock = offRoute.Clock with { TrainingPaused = true }
        };

        // Act
        var warning = policy.Observe(offRoute, fresh: true, measuredMetres: 0);
        Require(warning is not null, "An owned active departure must produce a candidate.");
        var fresh = policy.IsCurrent(warning, offRoute, fresh: true);
        var expired = policy.IsCurrent(warning, offRoute, fresh: false);
        var replaced = policy.IsCurrent(warning, offRoute with { Id = Guid.NewGuid() }, fresh: true);
        var ended = policy.IsCurrent(warning, offRoute with
        {
            Clock = offRoute.Clock with { State = GhostClockState.Incomplete }
        }, fresh: true);
        var pausedWarning = policy.Observe(paused, fresh: true, measuredMetres: 0);
        var resumed = policy.Observe(offRoute, fresh: true, measuredMetres: 0);
        _ = policy.Observe(State(4), fresh: true, measuredMetres: 0);
        var pausedDeparture = policy.Observe(paused, fresh: true, measuredMetres: 0);
        var resumedDeparture = policy.Observe(offRoute, fresh: true, measuredMetres: 0);

        // Assert
        Require(fresh && !expired && !replaced && !ended && pausedWarning is null && resumed is null
            && pausedDeparture is null && resumedDeparture is null && !policy.IsCurrent(warning, offRoute, fresh: true),
            "Pause, end, replacement and stale evidence must reject pending handoff; a paused departure is never replayed on Resume.");
    }

    private static void ComparisonIsOptInAndUsesFreshFrozenGap()
    {
        // Arrange
        var off = new GhostAudioPolicy(Owner);
        var enabled = new GhostAudioPolicy(Owner, new(1, AudioSummaryTrigger.ActiveTime, 5));
        _ = enabled.Observe(State(0), fresh: true, measuredMetres: 0);

        // Act
        var defaultOff = off.Observe(State(5, secondsAhead: 12), fresh: true, measuredMetres: 0);
        var ahead = enabled.Observe(State(5, secondsAhead: 12), fresh: true, measuredMetres: 0);
        var staleText = ahead is not null && enabled.IsCurrent(ahead, State(6, secondsAhead: -2), fresh: true);
        var behind = enabled.Observe(State(10, secondsAhead: -2), fresh: true, measuredMetres: 0);
        var level = enabled.Observe(State(15, secondsAhead: 0), fresh: true, measuredMetres: 0);
        var noOpponent = enabled.Observe(State(20, secondsAhead: 12) with { ReferenceId = null }, fresh: true, measuredMetres: 0);
        var invalid = enabled.Observe(State(25, secondsAhead: double.NaN), fresh: true, measuredMetres: 0);
        var uncertain = enabled.Observe(State(30, secondsAhead: 12, problem: RouteMatchProblem.Gap), fresh: true, measuredMetres: 0);
        var stale = enabled.Observe(State(35, secondsAhead: 12), fresh: false, measuredMetres: 0);

        // Assert
        Require(defaultOff is null && ahead?.Text == "12 seconds ahead." && !staleText
            && behind?.Text == "2 seconds behind." && level?.Text == "Level."
            && noOpponent is null && invalid is null && uncertain is null && stale is null,
            "Only explicit enabled cadence can speak current ahead, behind or level evidence for the frozen opponent.");
    }

    private static void WarningReplacesPendingComparisonAndConsumesDueBoundary()
    {
        // Arrange
        var enabled = new GhostAudioPolicy(Owner, new(1, AudioSummaryTrigger.Distance, 500));
        _ = enabled.Observe(State(0), fresh: true, measuredMetres: 0);
        var comparison = enabled.Observe(State(4, secondsAhead: 3), fresh: true, measuredMetres: 500);
        Require(comparison is { Kind: AudioCueKind.Summary }, "Setup needs a due comparison.");
        var offRoute = State(5, secondsAhead: 3, problem: RouteMatchProblem.OffRoute);

        // Act
        var warning = enabled.Observe(offRoute, fresh: true, measuredMetres: 1000);
        var validOldComparison = enabled.IsCurrent(comparison, offRoute, fresh: true);
        var rejoined = enabled.Observe(State(6, secondsAhead: 2), fresh: true, measuredMetres: 1001);
        var validOldWarning = warning is not null && enabled.IsCurrent(warning, State(6), fresh: true);
        var afterCorrection = enabled.Observe(State(7, secondsAhead: 2), fresh: true, measuredMetres: 999);
        var duplicateBoundary = enabled.Observe(State(8, secondsAhead: 2), fresh: true, measuredMetres: 1002);

        // Assert
        Require(warning is { Kind: AudioCueKind.Coaching } && AudioPriority.CanReplace(warning.Kind, comparison.Kind)
            && !validOldComparison && rejoined is null && !validOldWarning && afterCorrection is null && duplicateBoundary is null,
            "A fresh departure outranks a pending/due comparison, with no stale handoff, correction replay or backlog after re-entry.");
    }

    private static GhostRaceSnapshot State(int activeSeconds, double? secondsAhead = null, RouteMatchProblem? problem = null)
    {
        var route = new RouteKey(Owner, Opponent);
        var active = TimeSpan.FromSeconds(activeSeconds);

        return new(Owner, "Synthetic circuit", route, GhostMode.YearBest,
            new(active, active, false, GhostClockState.Running), PauseReason.None,
            new(route, "synthetic", problem is null ? RouteMatchState.Tracking : RouteMatchState.Uncertain,
                problem is null ? 20 : null, 1, TimeSpan.Zero, null, problem),
            20, new(null, secondsAhead), Opponent, TimeSpan.FromMinutes(4), 2026, null);
    }

    private static void ActualMatcherSuppliesWarningsAndClockExpiresDelayedHandoff()
    {
        // Arrange
        var route = new GhostRoute(new(Owner, Opponent), "Synthetic route", Opponent, "audio-test",
            [new(0, 0, 0), new(0, .0002, 22.23901604670658)]);
        var threeSeconds = TimeSpan.FromSeconds(3);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1,
            threeSeconds, 4, 5, 500), threeSeconds, threeSeconds, HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot);
        var race = new GhostRace(Owner, route, GhostMode.YearBest, null, profile, new(99900, 100000, 0, 0, 0, .1),
            100000, DateTimeOffset.UnixEpoch, "UTC", TimeSpan.Zero, null);
        var policy = new GhostAudioPolicy(Owner);

        GhostAudioCue? Position(long at, double latitude)
        {
            race.Location(new(at - 100000, at * 1000000, latitude, .00001, .1), at);
            return policy.Observe(race.Snapshot, race.HasFreshRouteEvidence(at), measuredMetres: 0);
        }

        // Act
        var onRoute = Position(101000, 0);
        var departure = Position(102000, .00001);
        Require(departure is not null, "The real matcher must produce a confirmed departure cue.");
        var justSupported = policy.IsCurrent(departure, race.Snapshot, race.HasFreshRouteEvidence(105000));
        var expiredBeforeTick = policy.IsCurrent(departure, race.Snapshot, race.HasFreshRouteEvidence(105001));
        var persistent = Position(103000, .00001);
        var reentry = Position(104000, 0);
        var nextDeparture = Position(105000, .00001);

        // Assert
        Require(onRoute is null && departure.Kind == AudioCueKind.Coaching && justSupported && !expiredBeforeTick
            && persistent is null && reentry is null && nextDeparture is { Kind: AudioCueKind.Coaching },
            "Audio follows actual matcher transitions and rejects expired evidence even before the next service tick.");
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
