using LoopCx.Features.GhostRun;

public static class RouteCaptureChecks
{
    public static void Run()
    {
        static Guid Id(int n) => Guid.Parse($"10000000-0000-0000-0000-{n:D12}");
        static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        var key = new RouteKey(Id(1), Id(2));
        var policy = new RouteCapturePolicy("capture-sphere-synthetic-v1", S(10), 10, 20);
        RecordedRouteFix Fix(double seconds, double longitude, RecordedRunSection section = RecordedRunSection.Main,
            double? accuracy = 2, bool gap = false) =>
            new(S(seconds), DateTimeOffset.UnixEpoch + S(seconds), 10, longitude, accuracy, section, gap);
        RouteCaptureResult Capture(IReadOnlyList<RecordedRouteFix> fixes, bool finished = true,
            RecordedWorkoutKind kind = RecordedWorkoutKind.Continuous, RouteCapturePolicy? limits = null) =>
            MainRouteCapture.Build(key, "Synthetic main route", Id(3), kind, finished, fixes, limits ?? policy);
        void Problem(RouteCaptureResult result, RouteCaptureProblem expected, string message) =>
            Check(!result.Success && result.Captured is null && result.Problem == expected &&
                !string.IsNullOrWhiteSpace(result.Detail), message);

        var input = new[] {
            Fix(0, 9.999, RecordedRunSection.Warmup),
            Fix(10, 10),
            Fix(15, 10), // stopped: time is retained, geometry has no duplicate edge
            Fix(20, 10.0005),
            Fix(25, 10.001),
            Fix(30, 10.001),
            Fix(35, 10.003, RecordedRunSection.Cooldown)
        };
        var copy = input.ToArray();
        var result = Capture(input);
        Check(result.Success && result.Problem is null && result.Detail is null, "Valid main track can supply route evidence.");
        var saved = result.Captured!;
        Check(saved.Raw.Count == 5 && saved.Raw.All(p => p.Section == RecordedRunSection.Main), "Exclude all warmup and cooldown fixes.");
        Check(saved.Route.Geometry.Count == 3, "Stopped points must not become zero-length geometry.");
        Check(saved.Route.Geometry[0].Longitude == 10 && saved.Route.Geometry[^1].Longitude == 10.001, "Start, finish and direction come only from observed main fixes.");
        Check(saved.Route.LengthMetres is > 109 and < 110, "Derived spherical distance uses explicit metres.");
        Check(saved.Progress.Select(p => p.Elapsed).SequenceEqual(new[] { S(0), S(5), S(10), S(15), S(20) }),
            "Main elapsed time begins at the first measured main fix, not the warmup.");
        Check(saved.Progress[0].Metres == 0 && saved.Progress[1].Metres == 0 &&
            saved.Progress[^1].Metres == saved.Progress[^2].Metres, "Retain leading and trailing recorded stops.");
        Check(saved.ObservedSpan == S(20), "Observed span retains all stopped time.");
        Check(input.SequenceEqual(copy), "Capturing never changes the source recording.");
        Check(saved.Route.Key == key && saved.Route.SourceRunId == Id(3) &&
            saved.Route.RuleVersion == policy.RuleVersion && saved.Policy == policy, "Retain route identity, source and frozen policy.");
        input[2] = Fix(15, 15);
        Check(saved.Raw[1].Longitude == 10, "Capture freezes the main data independently of source array mutation.");

        Problem(Capture(copy, kind: RecordedWorkoutKind.Intervals), RouteCaptureProblem.NotContinuous, "Intervals cannot supply a Ghost Run route.");
        Problem(Capture(copy, finished: false), RouteCaptureProblem.NotFinished, "Interrupted source cannot supply a completed route.");
        Problem(Capture(new[] { Fix(0, 10, RecordedRunSection.Warmup), Fix(5, 10, RecordedRunSection.Cooldown) }),
            RouteCaptureProblem.NoMainTrack, "No main route is not an empty successful capture.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10, RecordedRunSection.Cooldown), Fix(10, 10.0001) }),
            RouteCaptureProblem.NonContiguousMain, "Do not join two main blocks across cooldown.");
        Problem(Capture(new[] { Fix(0, 10) }), RouteCaptureProblem.InsufficientMovement, "A single fix supplies no geometry.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10) }), RouteCaptureProblem.InsufficientMovement, "A stationary trace is not a route.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, double.NaN) }), RouteCaptureProblem.InvalidFix, "Reject invalid coordinates.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10.0001) with { Latitude = 91 } }), RouteCaptureProblem.InvalidFix, "Reject latitude outside Earth.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10.0001) with { Utc = DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(1)) } }),
            RouteCaptureProblem.InvalidFix, "Raw UTC must be explicit; do not silently relabel local clock time.");
        Problem(Capture(new[] { Fix(0, 10), Fix(0, 10.0001) }), RouteCaptureProblem.NonMonotonicTime, "Repeated elapsed timestamp is not ordered evidence.");
        Problem(Capture(new[] { Fix(5, 10), Fix(4, 10.0001) }), RouteCaptureProblem.NonMonotonicTime, "Elapsed clock cannot run backwards.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10.0001, accuracy: null) }), RouteCaptureProblem.UncertainGps, "Missing accuracy remains uncertain.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10.0001, accuracy: 11) }), RouteCaptureProblem.UncertainGps, "Reject accuracy outside explicit policy.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10.0001, accuracy: double.NaN) }), RouteCaptureProblem.UncertainGps, "Non-finite accuracy remains uncertain.");
        Problem(Capture(new[] { Fix(0, 10), Fix(5, 10.0001, gap: true) }), RouteCaptureProblem.Gap, "An explicit gap is not interpolated into geometry.");
        Problem(Capture(new[] { Fix(0, 10), Fix(11, 10.0001) }), RouteCaptureProblem.Gap, "Reject a gap beyond the policy.");
        Check(Capture(new[] { Fix(0, 10), Fix(10, 10.0001, accuracy: 10) }).Success, "Exact gap and accuracy boundaries are deterministic.");
        Problem(Capture(new[] { Fix(0, 10), Fix(1, 10.01) }), RouteCaptureProblem.ImplausibleJump, "Reject an implausible GPS jump.");

        Problem(Capture(new[] { Fix(0, 10, gap: true), Fix(5, 10.0001) }), RouteCaptureProblem.Gap,
            "An explicit gap at main start must not be silently discarded.");

        var utcClockJump = new[] { Fix(0, 10), Fix(5, 10.0001) with { Utc = DateTimeOffset.UnixEpoch.AddHours(-1) } };
        Check(Capture(utcClockJump).Captured!.ObservedSpan == S(5), "UTC changes never replace monotonic elapsed duration.");
        Check(Capture(utcClockJump).Captured!.Raw[^1].Utc == utcClockJump[^1].Utc, "Retain raw UTC evidence without rewriting it.");
        var warmupGap = new[] { Fix(0, double.NaN, RecordedRunSection.Warmup, gap: true), Fix(30, 10), Fix(35, 10.0001) };
        Check(Capture(warmupGap).Success, "Warmup quality must not create an invented edge or reject measured main geometry.");

        var loop = Capture(new[] { Fix(0, 10), Fix(5, 10.0005), Fix(10, 10) }).Captured!;
        Check(loop.Route.Geometry.Count == 3 && loop.Route.Geometry[0].Longitude == loop.Route.Geometry[^1].Longitude &&
            loop.Route.LengthMetres > loop.Route.Geometry[1].Metres, "Out-and-back route keeps ordered positions rather than collapsing equal coordinates.");
        var reverse = Capture(new[] { Fix(0, 10.001), Fix(5, 10.0005), Fix(10, 10) }).Captured!;
        Check(reverse.Route.Geometry[0].Longitude > reverse.Route.Geometry[^1].Longitude, "Do not reverse the recorded direction.");

        Console.WriteLine("PASS: continuous main-only capture, immutable raw evidence, route order/direction, measured distance and recorded stops.");
        Console.WriteLine("PASS: concrete rejection reasons for missing/invalid/GPS-gap evidence, monotonic timing, explicit quality boundaries and no fabricated edges.");
    }
}
