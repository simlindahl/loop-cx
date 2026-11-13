using LoopCx.Features.GhostRun;

public static class RaceClockChecks
{
    public static void Run()
    {
        static TimeSpan S(double value) => TimeSpan.FromSeconds(value);
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        static void Reject(Action action, string message)
        {
            try { action(); } catch (ArgumentOutOfRangeException) { return; }
            throw new InvalidOperationException(message);
        }

        var race = new GhostRaceClock(S(1000));
        race.Observe(S(2500)); // 25 minutes
        race.PauseTraining(S(2500));
        var waiting = race.Observe(S(2620)); // 2 minutes paused
        Check(waiting.RaceElapsed == S(1620) && waiting.TrainingActive == S(1500) &&
            waiting.TrainingPaused && waiting.State == GhostClockState.Running,
            "25 minutes moving and 2 paused means 27-minute race and 25-minute training.");
        var finishPaused = race.CompleteAt(S(2620));
        Check(finishPaused.State == GhostClockState.Finished && finishPaused.TrainingPaused,
            "A matched finish can complete the race while training remains manually paused.");
        Check(race.Observe(S(9000)) == finishPaused && race.ResumeTraining(S(9001)) == finishPaused &&
            race.CompleteAt(S(9002)) == finishPaused && race.EndAttempt(S(9003)) == finishPaused &&
            race.InterruptAtLastObservation() == finishPaused, "Every late callback preserves a finished result.");

        var commands = new GhostRaceClock(S(0));
        commands.PauseTraining(S(10));
        var frozenPauses = commands.Pauses;
        commands.PauseTraining(S(15));
        commands.Observe(S(20));
        Check(commands.Current.TrainingPaused && commands.Current.TrainingActive == S(10),
            "Time observations do not automatically resume manual pause.");
        commands.ResumeTraining(S(30));
        commands.ResumeTraining(S(35));
        Check(commands.Pauses.Count == 1 && commands.Pauses[0] == new TrainingPause(S(10), S(30)),
            "Duplicate pause/resume commands retain a single pause interval.");
        Check(frozenPauses.Count == 1 && frozenPauses[0].EndedAt is null, "Previously captured pause evidence never mutates.");
        var after = commands.Observe(S(40));
        Check(after.RaceElapsed == S(40) && after.TrainingActive == S(20) && !after.TrainingPaused,
            "Race and active time remain separate after resume.");
        Reject(() => commands.Observe(S(39)), "Reject regressing monotonic time.");
        Check(commands.Current == after, "A rejected timestamp changes no state.");

        var delayed = new GhostRaceClock(S(0));
        delayed.PauseTraining(S(10));
        delayed.ResumeTraining(S(30));
        delayed.Observe(S(50));
        var actualFinish = delayed.CompleteAt(S(20));
        Check(actualFinish.RaceElapsed == S(20) && actualFinish.TrainingActive == S(10) && actualFinish.TrainingPaused,
            "A delayed GPS finish clips training time and pause state at the actual finish timestamp.");
        var beforePause = new GhostRaceClock(S(100));
        beforePause.PauseTraining(S(120));
        var earlierFinish = beforePause.CompleteAt(S(110));
        Check(earlierFinish.RaceElapsed == S(10) && earlierFinish.TrainingActive == S(10) && !earlierFinish.TrainingPaused,
            "A delayed finish before a later pause must not inherit that pause.");

        var interrupted = new GhostRaceClock(S(100));
        interrupted.Observe(S(110));
        var retained = interrupted.InterruptAtLastObservation();
        Check(retained.RaceElapsed == S(10) && retained.State == GhostClockState.Interrupted &&
            interrupted.Observe(S(9999)) == retained, "Process interruption preserves only the last known part.");
        var abandoned = new GhostRaceClock(S(0)).EndAttempt(S(12));
        Check(abandoned.RaceElapsed == S(12) && abandoned.State == GhostClockState.Incomplete, "Ending early is distinct from finishing.");
        Reject(() => new GhostRaceClock(S(-1)), "A negative clock origin is invalid.");
        var invalidFinish = new GhostRaceClock(S(100));
        Reject(() => invalidFinish.CompleteAt(S(99)), "Finish cannot precede the start.");
        Check(invalidFinish.Current.RaceElapsed == S(0) && invalidFinish.Current.State == GhostClockState.Running,
            "Invalid finish preserves the running state.");

        var key = new RouteKey(Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var reference = new GhostAttempt(Guid.Parse("20000000-0000-0000-0000-000000000003"), Guid.Parse("20000000-0000-0000-0000-000000000004"), key, DateTimeOffset.UnixEpoch, "UTC",
            S(40), AttemptQuality.Verified, "synthetic-clock-v1",
            new[] { new GhostPoint(S(0), 0), new GhostPoint(S(20), 100), new GhostPoint(S(40), 200) });
        var ghost = new GhostPlayback(reference, S(20));
        var clock = new GhostRaceClock(S(0));
        clock.PauseTraining(S(10));
        var paused = clock.Observe(S(20));
        Check(ghost.PositionAt(paused.RaceElapsed) == 100 && ghost.PositionAt(paused.TrainingActive) == 50,
            "Replay advances with the race clock while training counters are paused.");

        Console.WriteLine("PASS: 25+2-minute race clock, manual pause, duplicate commands, frozen pause evidence and finish during pause.");
        Console.WriteLine("PASS: delayed GPS finish clips historical training time, interrupted/end-attempt states and elapsed-time ghost replay during pause.");
    }
}
