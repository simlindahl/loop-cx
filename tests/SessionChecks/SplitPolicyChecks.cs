using System.Text;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.History;

internal static class SplitPolicyChecks
{
    private static readonly double[] CorrectedTimeSplitSeconds = [300, 300, 120];

    public static void Run(string[] arguments)
    {
        CorrectedTimeSplitsConserveMainWithoutGps();
        DistanceSplitsUseTheFrozenInterval();
        CrossingTimeSplitRetainsAcceptedDistanceOnBothSides();
        ResumedDistanceUsesCorrectedTimeAfterStandstill();
        FrozenTimeSplitsSurviveHistoryAndNormalSharing(arguments);
        Console.WriteLine("PASS: frozen Core split policy, GPS-free and measured boundaries, retrospective correction, History and normal JSON v2.");
    }

    private static void CorrectedTimeSplitsConserveMainWithoutGps()
    {
        // Arrange
        var policy = SplitPolicy.DefaultTime;
        var distance = new RunDistance(DistancePolicy.Pilot, Seconds(30), RunPhase.Work, policy);
        var statistics = new RunStatistics();

        // Act
        statistics.Advance(Seconds(0), Seconds(30), RunPhase.Warmup, paused: false);
        statistics.Advance(Seconds(30), Seconds(335), RunPhase.Work, paused: false);
        statistics.Exclude(Seconds(322), Seconds(332));
        var corrected = statistics.Snapshot(Seconds(335), distance.SnapshotAt(Seconds(295)), mainRunning: true);
        statistics.Advance(Seconds(335), Seconds(350), RunPhase.Work, paused: true);
        statistics.Advance(Seconds(350), Seconds(775), RunPhase.Recovery, paused: false);
        statistics.ObserveHeartRate(new(Seconds(351), Seconds(353), 140, null));
        distance.Boundary(RunPhase.Cooldown, Seconds(775), Seconds(720));
        statistics.FinalizeMain(distance.SnapshotAt(Seconds(720)));
        statistics.Advance(Seconds(775), Seconds(800), RunPhase.Cooldown, paused: false);
        var savedDistance = distance.SnapshotAt(Seconds(720));
        var finished = statistics.Snapshot(Seconds(800), savedDistance, mainRunning: false);

        // Assert
        Require(corrected.CompletedSplits.Count == 0 && corrected.CurrentSplit?.ActiveTime == Seconds(295),
            "Retrospective pause must remove the provisional time boundary.");
        Require(savedDistance.SplitPolicy == policy && finished.CurrentSplit is null
            && finished.CompletedSplits.Select(split => split.ActiveTime.TotalSeconds).SequenceEqual(CorrectedTimeSplitSeconds),
            "Corrected Main must produce two full time splits and one final partial.");
        Require(finished.CompletedSplits.Sum(split => split.UnknownGpsActiveTime.Ticks) == finished.Main.ActiveTime.Ticks
            && finished.CompletedSplits.All(split => split.AveragePaceSecondsPerKilometre is null)
            && finished.CompletedSplits[0].AverageHeartRate == 140,
            "Missing GPS must not fabricate pace or suppress known weighted HR.");
    }

    private static void DistanceSplitsUseTheFrozenInterval()
    {
        // Arrange
        var technical = DistancePolicy.Pilot with
        {
            MaximumGap = Seconds(500),
            MaximumAge = Seconds(500),
            MaximumSpeedMetresPerSecond = 30
        };
        var policy = new SplitPolicy(1, SplitBasis.Distance, 1500);
        var distance = new RunDistance(technical, TimeSpan.Zero, RunPhase.Work, policy);

        // Act
        distance.Observe(new(Seconds(0), 0, 0, 1), Seconds(0), Seconds(0));
        distance.Observe(new(Seconds(200), 0, 3400 / 111194.92664455874, 1), Seconds(200), Seconds(200));
        distance.Boundary(null, Seconds(200), Seconds(200));
        var result = distance.SnapshotAt(Seconds(200));

        // Assert
        Require(result.Splits.Count == 3 && result.Splits[0].MeasuredMetres == 1500
            && result.Splits[1].MeasuredMetres == 1500 && Math.Abs(result.Splits[2].MeasuredMetres - 400) < 0.001,
            "A 3.4 km recording at 1.5 km intervals must keep two full splits and a 0.4 km partial.");
    }

    private static void CrossingTimeSplitRetainsAcceptedDistanceOnBothSides()
    {
        // Arrange
        var policy = new SplitPolicy(1, SplitBasis.Time, 1);
        var distance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work, policy);

        // Act
        distance.Observe(new(Seconds(58), 0, 0, 1), Seconds(58), Seconds(58));
        distance.Boundary(RunPhase.Recovery, Seconds(59), Seconds(59));
        distance.Observe(new(Seconds(62), 0, 20 / 111194.92664455874, 1), Seconds(62), Seconds(62));
        distance.Boundary(null, Seconds(62), Seconds(62));
        var result = distance.SnapshotAt(Seconds(62));

        // Assert
        Require(result.Splits.Count == 2 && result.Splits[0].ActiveTime == Seconds(60)
            && result.Splits[1].ActiveTime == Seconds(2)
            && result.Splits.All(split => Math.Abs(split.MeasuredMetres - 10) < 0.001),
            "An accepted edge crossing a time boundary and Work/Recovery transition must conserve measured distance.");
    }

    private static void ResumedDistanceUsesCorrectedTimeAfterStandstill()
    {
        // Arrange
        var distance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work, new(1, SplitBasis.Time, 1));
        var statistics = new RunStatistics();
        const double metresPerDegree = 111194.92664455874;
        distance.Observe(new(Seconds(53), 0, 0, 1), Seconds(53), Seconds(53));
        distance.Observe(new(Seconds(55), 0, 10 / metresPerDegree, 1), Seconds(55), Seconds(55));
        var onset = distance.CaptureCandidate();

        // Act
        statistics.Advance(TimeSpan.Zero, Seconds(67), RunPhase.Work, paused: false);
        var tentative = distance.SnapshotAt(Seconds(67));
        statistics.Exclude(Seconds(55), Seconds(65));
        distance.ConfirmStandstill(onset, Seconds(67), unknownTail: true);
        var corrected = statistics.Snapshot(Seconds(67), distance.SnapshotAt(Seconds(57)), mainRunning: true);
        statistics.Advance(Seconds(67), Seconds(68), RunPhase.Work, paused: true);
        statistics.Advance(Seconds(68), Seconds(73), RunPhase.Work, paused: false);
        distance.Reanchor(Seconds(68), "Automatic resume boundary");
        distance.Observe(new(Seconds(69), 0, 10 / metresPerDegree, 1), Seconds(69), Seconds(58));
        distance.Observe(new(Seconds(73), 0, 30 / metresPerDegree, 1), Seconds(73), Seconds(62));
        var resumed = statistics.Snapshot(Seconds(73), distance.SnapshotAt(Seconds(62)), mainRunning: true);

        // Assert
        Require(tentative.Splits.Count == 1 && corrected.CompletedSplits.Count == 0
            && corrected.CurrentSplit?.ActiveTime == Seconds(57),
            "A delayed standstill confirmation must remove just ten seconds and the affected provisional split.");
        Require(resumed.CompletedSplits.Count == 1 && resumed.CurrentSplit?.ActiveTime == Seconds(2)
            && Math.Abs(resumed.CompletedSplits[0].MeasuredMetres - 20) < 0.001
            && Math.Abs(resumed.CurrentSplit.MeasuredMetres - 10) < 0.001,
            "A resumed accepted GPS edge must split against the corrected clock and retain the pre-onset distance.");
    }

    private static void FrozenTimeSplitsSurviveHistoryAndNormalSharing(string[] arguments)
    {
        // Arrange
        var policy = new SplitPolicy(1, SplitBasis.Time, 1);
        var distance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work, policy);
        var statistics = new RunStatistics();
        var session = TimedSession.Continuous(Seconds(155), 4);
        var id = Guid.Parse("b5118dd7-fdbb-4f49-a3b8-e69a5762601f");
        const string started = "2026-11-15T10:00:00Z";
        const string lastObserved = "2026-11-15T10:02:35Z";
        const string prefixObserved = "2026-11-15T10:02:34Z";
        const string saved = "2026-11-15T10:03:00.0000000+00:00";
        JsonElement Checkpoint(TimeSpan observed)
        {
            var measuredDistance = distance.SnapshotAt(session.MainActiveTime);

            return JsonSerializer.SerializeToElement(new
            {
                SchemaVersion = 1,
                session.Plan,
                session.Completed,
                session.PhaseActiveTime,
                session.TotalActiveTime,
                session.MainActiveTime,
                Snapshot = new
                {
                    Id = id,
                    Phase = session.Current?.Kind.ToString() ?? "Finished",
                    Paused = session.IsPaused,
                    ActiveSeconds = (long)session.TotalActiveTime.TotalSeconds,
                    RemainingSeconds = (long)session.Remaining.TotalSeconds,
                    Distance = measuredDistance,
                    Statistics = statistics.Snapshot(observed, measuredDistance, mainRunning: !session.IsFinished)
                }
            });
        }

        var start = Checkpoint(TimeSpan.Zero);

        // Act
        session.Advance(Seconds(154));
        statistics.Advance(TimeSpan.Zero, Seconds(154), RunPhase.Work, paused: false);
        _ = session.Pause();
        var prefix = Checkpoint(Seconds(154));
        _ = session.Resume();
        session.Advance(Seconds(1), (next, consumed) =>
        {
            var at = Seconds(154) + consumed;
            statistics.Advance(Seconds(154), at, RunPhase.Work, paused: false);
            distance.Boundary(next, at, session.MainActiveTime);
            statistics.FinalizeMain(distance.SnapshotAt(session.MainActiveTime));
        });
        var terminal = Checkpoint(Seconds(155));
        var run = new ExportJournalSnapshot(id, started, lastObserved, 155000, "finished", terminal.GetProperty("Plan"), null, null,
            terminal, "disabled_synthetic",
            [new(1, "start", started, 0, start), new(2, "finished", lastObserved, 155000, terminal)], [], []);
        var interruptedRun = run with
        {
            Status = "interrupted",
            Checkpoint = prefix,
            LastObservedUtc = prefixObserved,
            ObservedElapsedMilliseconds = 154000,
            Events = [new(1, "start", started, 0, start), new(2, "pause", prefixObserved, 154000, prefix)]
        };
        var history = StoredRunProjection.Detail(run, null).Measurements;
        var recovered = InterruptedRunProjection.Read(new(id, started, "interrupted", null, prefix.GetRawText(), null, null),
            prefixObserved, saved).Measurements;
        var finishedJson = RunJsonSharing.Project(run, null, null);
        var partialJson = RunJsonSharing.Project(interruptedRun, saved, activeOwner: null);
        using var shared = JsonDocument.Parse(partialJson);

        // Assert
        Require(history.SplitPolicy == policy && recovered.SplitPolicy == policy && history.Splits is { Count: 3 }
            && recovered.Splits is { Count: 3 } && history.Splits.Take(2).SequenceEqual(recovered.Splits.Take(2))
            && history.Splits[2] is { FinalPartial: true, HasMeasuredDistance: false, Pace: null }
            && history.Splits[2].Active == Seconds(35)
            && recovered.Splits[2] is { FinalPartial: true, HasMeasuredDistance: false, Pace: null }
            && recovered.Splits[2].Active == Seconds(34),
            "Finished and recovered prefix must each retain the frozen policy, both full splits and one unknown-distance partial.");
        Require(shared.RootElement.GetProperty("schemaVersion").GetInt32() == 2
            && JsonElement.DeepEquals(prefix, shared.RootElement.GetProperty("run").GetProperty("checkpoint")),
            "Normal JSON must preserve the exact frozen checkpoint including the split policy and partial facts.");

        var sampleOption = Array.IndexOf(arguments, "--export-split-samples");
        if (sampleOption >= 0 && sampleOption + 1 < arguments.Length)
        {
            var directory = arguments[sampleOption + 1];
            var destination = Directory.CreateDirectory(Path.GetFullPath(directory)).FullName;
            File.WriteAllText(Path.Combine(destination, "timed-finished.json"), finishedJson, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(destination, "timed-interrupted.json"), partialJson, new UTF8Encoding(false));
        }
    }

    private static TimeSpan Seconds(int seconds) => TimeSpan.FromSeconds(seconds);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
