using LoopCx.Features.GhostRun;

public static class RouteMatchChecks
{
    public static void Run()
    {
        const double scale = 6371008.8 * Math.PI / 180;
        static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);
        static Guid Id(int n) => Guid.Parse($"30000000-0000-0000-0000-{n:D12}");
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        static void Near(double? value, double expected, string message) =>
            Check(value is { } number && Math.Abs(number - expected) < 0.00001, message);
        static void Reject(Action action, string message)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new InvalidOperationException(message);
        }
        var policy = new RouteMatchPolicy("ordered-plane-synthetic-v1", 1.5, 1.5, 1,
            S(10), 20, 30, 5000);
        GhostRoute Route(params (double X, double Y)[] points)
        {
            double metres = 0;
            var vertices = new List<RouteVertex>();
            for (var i = 0; i < points.Length; i++)
            {
                if (i > 0) metres += Math.Sqrt(Math.Pow(points[i].X - points[i - 1].X, 2) + Math.Pow(points[i].Y - points[i - 1].Y, 2));
                vertices.Add(new(points[i].Y / scale, points[i].X / scale, metres));
            }
            return new(new RouteKey(Id(1), Id(2)), "Synthetic local geometry", Id(3), policy.RuleVersion, vertices);
        }
        RecordedRouteFix Fix(double seconds, double x, double y = 0, double? accuracy = 0.1, bool gap = false) =>
            new(S(seconds), DateTimeOffset.UnixEpoch + S(seconds), y / scale, x / scale, accuracy, RecordedRunSection.Main, gap);
        var line = Route((0, 0), (10, 0), (20, 0), (30, 0));
        var match = new OrderedRouteMatcher(line, policy);
        Check(match.Observe(Fix(0, 0)).Problem == RouteMatchProblem.StartRequired, "Observation must not implicitly start a race.");
        Check(match.Start(Fix(0, 10)).Problem == RouteMatchProblem.MoveToStart, "Start requires the right start position.");
        Check(match.Start(Fix(1, 0)).StartedAt == S(1), "Start time freezes at the accepted start observation.");
        var initial = match.Current;
        Check(match.Start(Fix(2, 0)) == initial, "Duplicate start does not replace the race origin.");
        Near(match.Observe(Fix(2, 5)).Metres, 5, "Track within the first expected segment.");
        var atGate = match.Observe(Fix(3, 10));
        Check(atGate.NextVertex == 2 && atGate.State == RouteMatchState.Tracking, "Visit route vertices in order.");
        Check(match.Observe(Fix(3, 10)) == atGate, "Duplicate fixes are idempotent.");
        Near(match.Observe(Fix(4, 15)).Metres, 15, "Track the next segment.");
        var finish = match.Observe(Fix(6, 30));
        Check(finish.State == RouteMatchState.Finished && finish.NextVertex == 4 && finish.FinishedAt == S(6), "All ordered segments permit a finish.");
        Check(match.Observe(Fix(100, -100)) == finish && match.Start(Fix(101, 0)) == finish, "Late callbacks freeze the matched finish.");

        var square = Route((0, 0), (10, 0), (10, 10), (0, 10), (0, 0));
        var shortcut = new OrderedRouteMatcher(square, policy);
        shortcut.Start(Fix(0, 0));
        var cut = shortcut.Observe(Fix(2, 10, 10));
        Check(cut.State == RouteMatchState.Uncertain && cut.Metres is null && cut.NextVertex == 1,
            "A diagonal shortcut cannot skip the first corner or create a lead.");
        Check(shortcut.Observe(Fix(3, 0)).State != RouteMatchState.Finished, "Returning to the physical finish early does not finish a loop.");
        shortcut.Observe(Fix(4, 10));
        shortcut.Observe(Fix(5, 10, 10));
        shortcut.Observe(Fix(6, 0, 10));
        Check(shortcut.Observe(Fix(7, 0)).State == RouteMatchState.Finished,
            "An off-route excursion alone does not disqualify a subsequently evidenced full route.");

        var wrongDirection = new OrderedRouteMatcher(square, policy);
        wrongDirection.Start(Fix(0, 0));
        Check(wrongDirection.Observe(Fix(1, 0, 10)).Metres is null && wrongDirection.Current.NextVertex == 1,
            "Opposite direction must not map onto a later loop segment.");
        var outBack = new OrderedRouteMatcher(Route((0, 0), (10, 0), (0, 0)), policy);
        outBack.Start(Fix(0, 0));
        outBack.Observe(Fix(1, 5));
        Check(outBack.Observe(Fix(2, 0)).State != RouteMatchState.Finished, "Early start/finish crossing is not a full out-and-back.");
        Check(outBack.Observe(Fix(3, 10)).NextVertex == 2, "Turning point must precede the return leg.");
        Check(outBack.Observe(Fix(4, 0)).State == RouteMatchState.Finished, "Out-and-back finishes in the recorded direction.");

        var parallel = new OrderedRouteMatcher(Route((0, 0), (20, 0), (20, 5), (0, 5)), policy);
        parallel.Start(Fix(0, 0));
        Check(parallel.Observe(Fix(1, 5, 5)).Metres is null && parallel.Current.NextVertex == 1,
            "A nearby later parallel segment cannot replace the expected outbound segment.");
        var figure = Route((0, 0), (10, 10), (0, 20), (-10, 10), (0, 0), (10, -10), (0, -20), (-10, -10), (0, 0));
        var crossing = new OrderedRouteMatcher(figure, policy);
        crossing.Start(Fix(0, 0));
        Check(crossing.Observe(Fix(1, 5, -5)).Metres is null, "At a crossing, a future branch is not a valid current segment.");
        var path = new[] { (10d, 10d), (0d, 20d), (-10d, 10d), (0d, 0d), (10d, -10d), (0d, -20d), (-10d, -10d), (0d, 0d) };
        for (var i = 0; i < path.Length; i++)
        {
            var observed = crossing.Observe(Fix((i + 1) * 2, path[i].Item1, path[i].Item2));
            if (i == 3) Check(observed.State != RouteMatchState.Finished && observed.NextVertex == 5, "Mid-route finish crossing keeps the second loop pending.");
        }
        Check(crossing.Current.State == RouteMatchState.Finished, "Both loops must be evidenced before figure-eight finish.");

        var gap = new OrderedRouteMatcher(line, policy);
        gap.Start(Fix(0, 0));
        gap.Observe(Fix(1, 5));
        Check(gap.Observe(Fix(20, 25)).Problem == RouteMatchProblem.Gap, "A long gap cannot verify a skipped segment.");
        Check(gap.Observe(Fix(21, 30)).State != RouteMatchState.Finished && gap.Current.NextVertex == 1,
            "Re-entry at the finish does not prove the missing route.");
        Near(gap.Observe(Fix(22, 5)).Metres, 5, "Return to the covered anchor can restore comparison without inventing missing progress.");
        gap.Observe(Fix(23, 10));
        gap.Observe(Fix(24, 20));
        Check(gap.Observe(Fix(25, 30)).State == RouteMatchState.Finished, "Actually traversing the remaining route restores full coverage.");

        var quality = new OrderedRouteMatcher(line, policy);
        quality.Start(Fix(0, 0));
        quality.Observe(Fix(1, 5));
        Check(quality.Observe(Fix(2, 10, accuracy: null)).Problem == RouteMatchProblem.InvalidFix, "Missing GPS accuracy hides comparison.");
        Near(quality.Observe(Fix(3, 10)).Metres, 10, "A short gap can bridge only the expected evidenced path under the explicit policy.");
        Check(quality.Observe(Fix(3.5, 20, accuracy: 5)).Metres is null, "Uncertain GPS cannot advance coverage.");
        Check(quality.Observe(Fix(4, 200)).Problem == RouteMatchProblem.ImplausibleJump, "Implausible jumps cannot create progress.");
        var seen = quality.Current;
        Check(quality.Observe(Fix(3.9, 15)).Problem == RouteMatchProblem.NonMonotonicTime &&
            quality.Current.NextVertex == seen.NextVertex, "Out-of-order measurement changes no covered segment.");
        Check(quality.Observe(Fix(5, 6000)).Problem == RouteMatchProblem.OutsideProjection, "A fix outside the configured projection stays unknown.");

        var explicitGap = new OrderedRouteMatcher(line, policy);
        explicitGap.Start(Fix(0, 0));
        explicitGap.Observe(Fix(1, 5));
        Check(explicitGap.Observe(Fix(2, 10, gap: true)).Problem == RouteMatchProblem.Gap,
            "An explicitly marked gap must not be bridged even when its duration is short.");
        Near(explicitGap.Observe(Fix(3, 5)).Metres, 5, "A re-entry at the last covered position retains its correct progress.");

        var delayed = new OrderedRouteMatcher(Route((0, 0), (10, 0)), policy);
        var raceClock = new GhostRaceClock(S(0));
        delayed.Start(Fix(0, 0));
        raceClock.PauseTraining(S(0.5));
        raceClock.Observe(S(2));
        var betweenFixes = delayed.Observe(Fix(2, 20));
        Check(betweenFixes.FinishedAt == S(1), "Dense valid crossing estimates the first finish between observations.");
        var raceResult = raceClock.CompleteAt(betweenFixes.FinishedAt!.Value);
        Check(raceResult.RaceElapsed == S(1) && raceResult.TrainingActive == S(0.5) && raceResult.TrainingPaused,
            "A matched finish completes race time while training remains paused.");

        Reject(() => _ = new OrderedRouteMatcher(line, policy with { RuleVersion = "other" }), "Do not silently change calculation version.");
        Reject(() => _ = new OrderedRouteMatcher(line, policy with { MaximumStepMetres = double.NaN }), "Reject non-finite policy limits.");
        Reject(() => _ = new OrderedRouteMatcher(line, policy with { ProjectionExtentMetres = 5 }), "Do not project an out-of-scope route.");
        Console.WriteLine("PASS: ordered route segments, fixed start/direction, loops, figure-eight crossing, parallel paths and no shortcut finish.");
        Console.WriteLine("PASS: gaps/re-entry preserve covered prefix, GPS uncertainty/speed/timestamps, frozen goal and matched finish during training pause.");
    }
}
