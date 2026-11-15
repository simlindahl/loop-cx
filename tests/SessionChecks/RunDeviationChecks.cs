using LoopCx.Features.ActiveRun;

internal static class RunDeviationChecks
{
    private static TimeSpan Seconds(double value)
    {
        return TimeSpan.FromSeconds(value);
    }

    private static DeviationObservation Pace(double time, double value = 340)
    {
        return new(0, 1, RunPhase.Work, false,
        Seconds(time), Seconds(time), new(CoreTargetMode.Pace, 300, 400),
        new(LivePacePolicy.Pilot, value, "tracking", Seconds(10), Seconds(time - 10), Seconds(time), Seconds(time)), null);
    }

    private static DeviationObservation Hr(double time, int bpm)
    {
        return Pace(time) with
        {
            Targets = new(CoreTargetMode.HeartRateZone, 2, 1),
            HeartRate = new(new(1, null, HeartRatePolicy.Pilot,
            [new(1, 80, 99), new(2, 100, 119), new(3, 120, 139), new(4, 140, 159), new(5, 160, 190)]),
            bpm, "valid", Seconds(time))
        };
    }

    private static CoachingCue? Feed(RunDeviation policy, Func<int, DeviationObservation> input, int from = 0, int through = 30)
    {
        CoachingCue? result = null;
        for (var second = from; second <= through; second++) result = policy.Observe(input(second));
        return result;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        // Arrange
        var directions = Enum.GetValues<DeviationDirection>();

        // Act
        var messages = directions.Select(direction => new CoachingCue(0, direction).Text).ToArray();

        // Assert
        Require(messages.Length == 4 && messages.All(message => !string.IsNullOrWhiteSpace(message)
            && !message.Contains("RPE", StringComparison.OrdinalIgnoreCase)), "Each measured direction needs English, non-RPE guidance.");

        foreach (var boundary in new[] { 270d, 300d, 330d })
            Require(Feed(new(), time => Pace(time, boundary), through: 60) is null, "Exact ten-percent boundaries are not deviations.");

        foreach (var (pace, direction) in new[] { (269.999, DeviationDirection.Faster), (330.001, DeviationDirection.Slower) })
        {
            var policy = new RunDeviation();
            Require(Feed(policy, time => Pace(time, pace), through: 29) is null, "Confirmation must not occur before thirty active seconds.");
            Require(policy.Observe(Pace(29.999, pace)) is null, "Subsecond boundary must remain strict.");
            var cue = policy.Observe(Pace(30, pace));
            Require(cue?.Direction == direction, "Supported continuous deviation must confirm at thirty seconds.");
            policy.Attempted(cue!);
            Require(Feed(policy, time => Pace(time, pace), 31, 90) is null, "One continuous deviation must not repeat.");
        }

        foreach (var boundary in new[] { 100, 119 })
            Require(Feed(new(), time => Hr(time, boundary), through: 60) is null, "Inclusive personal-zone boundaries are within target.");
        Require(Feed(new(), time => Hr(time, 99))?.Direction == DeviationDirection.BelowZone, "Below-zone uses frozen personal boundaries.");
        Require(Feed(new(), time => Hr(time, 120))?.Direction == DeviationDirection.AboveZone, "Above-zone uses frozen personal boundaries.");
        Require(!new CoachingCue(0, DeviationDirection.BelowZone).Text.Contains("speed up", StringComparison.OrdinalIgnoreCase), "Lagging HR must not instruct SIT acceleration.");

        var resets = new Func<DeviationObservation, DeviationObservation>[]
        {
            input => input with { Paused = true },
            input => input with { PhaseIndex = 1 },
            input => input with { SplitNumber = 2 },
            input => input with { Phase = RunPhase.Recovery, PhaseIndex = 1 },
            input => input with { Targets = new(CoreTargetMode.Pace, 500) },
            input => Pace(input.TotalActive.TotalSeconds, 260),
            input => Pace(input.TotalActive.TotalSeconds, 300),
            input => input with { Pace = input.Pace! with { Quality = "acquiring", SecondsPerKilometre = null } },
            input => input with { Targets = new() },
            input => input with { Phase = RunPhase.Cooldown }
        };
        foreach (var reset in resets)
        {
            var policy = new RunDeviation();
            _ = Feed(policy, time => Pace(time), through: 20);
            Require(policy.Observe(reset(Pace(21))) is null, "Boundary/reset must invalidate accumulated confirmation.");
            Require(Feed(policy, time => Pace(time), 22, 51) is null, "Reset cannot pool earlier confirmation seconds.");
            Require(policy.Observe(Pace(52)) is not null, "Thirty new supported active seconds can confirm after reset.");
        }

        var retro = new RunDeviation();
        _ = Feed(retro, time => Pace(time), through: 20);
        Require(retro.Observe(Pace(21) with { TotalActive = Seconds(10) }) is null, "Retrospective ten-second correction resets confirmation.");
        Require(Feed(retro, time => Pace(time) with { TotalActive = Seconds(time - 11) }, 22, 50) is null,
            "Excluded Auto time cannot survive in confirmation.");
        Require(retro.Observe(Pace(51) with { TotalActive = Seconds(40) }) is not null,
            "Only thirty new corrected active seconds confirm after rewind.");

        // Fresh new receipts do not prove continuity across a missed freshness window.
        foreach (var heartRate in new[] { false, true })
        {
            DeviationObservation Input(int time)
            {
                return heartRate ? Hr(time, 120) : Pace(time);
            }

            var policy = new RunDeviation();
            _ = Feed(policy, Input, through: 20);
            Require(policy.Observe(Input(30)) is null, "A new fresh sample after expiry must reset even without an observed null tick.");
            Require(Feed(policy, Input, 31, 59) is null, "Sensor gap must not count towards thirty consecutive seconds.");
            Require(policy.Observe(Input(60)) is not null, "New contiguous support can confirm after the gap.");
        }

        var held = new RunDeviation();
        Require(Feed(held, time => Pace(time) with { Pace = Pace(time - (time % 3)).Pace }) is not null,
            "Overlapping fresh supported windows can maintain continuity without inventing a callback-delay rule.");

        var pending = new RunDeviation();
        var confirmed = Feed(pending, time => Pace(time))!;
        Require(pending.IsCurrent(confirmed, Pace(31)), "Pending coaching remains valid with overlapping fresh evidence.");
        foreach (var reset in resets.Take(10))
            Require(!pending.IsCurrent(confirmed, reset(Pace(31))), "Pending coaching must be invalidated before actual speech.");
        Require(!pending.IsCurrent(confirmed, Pace(34) with { Pace = Pace(30).Pace }), "Pending stale sample must not speak.");
        pending.Attempted(confirmed);
        _ = pending.Observe(Pace(31, 300));
        Require(!pending.IsCurrent(confirmed, Pace(32)), "Old generation cannot revive after within-target reset.");

        var noProfile = Hr(0, 120) with { HeartRate = Hr(0, 120).HeartRate! with { Configuration = HeartRateConfiguration.Empty } };
        Require(new RunDeviation().Observe(noProfile) is null, "No zone defaults may be inferred from missing profile.");
        Require(Feed(new(), time => Hr(time, 120) with { HeartRate = Hr(time, 120).HeartRate! with { Quality = "contact-invalid" } }) is null,
            "Invalid contact cannot confirm deviation.");
        Require(Feed(new(), time => Pace(time, 300) with { HeartRate = Hr(time, 120).HeartRate }) is null,
            "Unselected HR must not coach a pace-target run.");

        Require(AudioPriority.CanReplace(AudioCueKind.Coaching, AudioCueKind.Summary)
            && AudioPriority.CanReplace(AudioCueKind.Phase, AudioCueKind.Coaching)
            && AudioPriority.CanReplace(AudioCueKind.Phase, AudioCueKind.Phase)
            && !AudioPriority.CanReplace(AudioCueKind.Summary, AudioCueKind.Coaching)
            && !AudioPriority.CanReplace(AudioCueKind.Coaching, AudioCueKind.Phase), "Phase > coaching > summary, with no equal-priority backlog.");
        var summary = new RunAudioPolicy(new(1, Interval: 30));
        _ = summary.Observe(0, RunPhase.Work, false, TimeSpan.Zero, 0);
        Require(summary.Observe(1, RunPhase.Recovery, false, Seconds(30), 0)?.PhasePriority == true
            && summary.Observe(1, RunPhase.Recovery, false, Seconds(31), 0) is null, "Phase collision consumes summary without replay.");
        Require(RunAudioText.Phase(RunPhase.Work, Seconds(61), 1, 1).Contains("1 minute 1 second", StringComparison.Ordinal)
            && RunAudioText.Phase(RunPhase.Recovery, Seconds(122), 1, 1).Contains("2 minutes 2 seconds", StringComparison.Ordinal), "Speech time units must agree with their quantities.");

        Console.WriteLine("PASS: deviation strict boundaries, frozen zones, continuity/gaps, reset/retro, one-cue latch, pending validity and audio priority.");
    }
}
