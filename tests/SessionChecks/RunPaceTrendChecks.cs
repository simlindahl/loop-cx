using System.Text.Json;
using LoopCx.Features.ActiveRun;

internal static class RunPaceTrendChecks
{
    internal static void Run()
    {
        var policy = LivePacePolicy.Pilot;
        static TimeSpan At(int second)
        {
            return TimeSpan.FromSeconds(second);
        }

        LivePaceState Live(int second)
        {
            return new(policy, 300, "tracking", At(10), At(second - 10), At(second), At(second));
        }

        void Edge(RunPaceTrend trend, int from, int to, double pace, int? received = null)
        {
            trend.Observe(new(true, At(from), At(to)), new(At(from), At(to), (to - from) * 1000 / pace), At(received ?? to), policy);
        }

        PaceTrendState Compare(double previous, double current)
        {
            var trend = new RunPaceTrend();
            trend.Boundary(At(0), 3);
            for (var second = 1; second <= 20; second++) Edge(trend, second - 1, second, second <= 10 ? previous : current);
            return trend.Snapshot(At(20), Live(20), false, false);
        }

        if (Compare(320, 315).Direction != "Faster" || Compare(320, 325).Direction != "Slower"
            || Compare(320, 316).Direction != "Steady" || Compare(320, 324).Direction != "Steady"
            || Compare(320, 315.0001).Direction != "Steady" || Compare(320, 324.9999).Direction != "Steady")
        {
            throw new InvalidOperationException("The pilot compares lower sec/km as Faster, including exact five-second boundaries only.");
        }

        var delayed = new RunPaceTrend();
        delayed.Boundary(At(10), 2);
        for (var index = 0; index <= 20; index++)
        {
            var from = TimeSpan.FromSeconds(9.5 + index);
            var to = from + At(1);
            var received = to + TimeSpan.FromMilliseconds(200);
            delayed.Observe(new(true, from, to), new(from, to, 4), received, policy);
        }
        if (delayed.Snapshot(TimeSpan.FromSeconds(30.7), Live(30), false, false).Direction != "Steady")
            throw new InvalidOperationException("After discarding a delayed crossing edge, the next wholly post-boundary edge must begin acquisition despite receipt latency.");

        var expiredBeforeReceipt = new RunPaceTrend();
        for (var second = 1; second <= 30; second++) Edge(expiredBeforeReceipt, second - 1, second, 300);
        expiredBeforeReceipt.Observe(new(true, At(30), TimeSpan.FromSeconds(32.5)), new(At(30), TimeSpan.FromSeconds(32.5), 10), TimeSpan.FromSeconds(33.5), policy);
        if (expiredBeforeReceipt.Snapshot(TimeSpan.FromSeconds(33.5), Live(33), false, false).Direction is not null)
            throw new InvalidOperationException("Previous support expires at receipt time even when the new edge itself is fresh and adjacent.");
        var delayedLive = new RunLivePace(policy);
        var delayedAcquisition = new RunPaceTrend();
        delayedAcquisition.Boundary(At(10), 5);
        PaceTrendState? acquired = null;
        for (var index = 0; index <= 20; index++)
        {
            var from = TimeSpan.FromSeconds(9.5 + index);
            var to = from + At(1);
            var receipt = to + TimeSpan.FromMilliseconds(200);
            var observation = new DistanceObservation(true, from, to);
            var edge = new AcceptedDistanceEdge(from, to, 4);
            delayedLive.Observe(observation, edge, receipt);
            delayedAcquisition.Observe(observation, edge, receipt, policy);
            acquired = delayedAcquisition.Snapshot(receipt, delayedLive.Snapshot(receipt), false, false);
        }
        if (acquired?.Direction != "Steady")
            throw new InvalidOperationException("Real acquiring snapshots must not move the measurement floor ahead on every delayed receipt.");

        var value = new RunPaceTrend();
        value.Boundary(At(0), 1);
        for (var second = 1; second <= 19; second++) Edge(value, second - 1, second, 320);
        if (value.Snapshot(At(19), Live(19), false, false).Direction is not null)
            throw new InvalidOperationException("Incomplete windows cannot become a false Steady.");
        Edge(value, 19, 20, 300);
        var known = value.Snapshot(At(20), Live(20), false, false);
        Edge(value, 19, 20, 100);
        Edge(value, 18, 19, 100);
        if (known != value.Snapshot(At(20), Live(20), false, false)
            || known.SupportedThrough != At(20) || value.Snapshot(At(22), Live(22), false, false).SupportedThrough != At(20))
        {
            throw new InvalidOperationException("Repeated receipts and UI time cannot extend comparison support or overwrite newer evidence.");
        }

        // Delayed fresh receipt after expiry, without an intermediate null/stale snapshot.
        Edge(value, 20, 24, 300);
        if (value.Snapshot(At(24), Live(24), false, false).Direction is not null)
            throw new InvalidOperationException("A fresh edge cannot bridge an expired receipt window without a stale tick.");
        for (var second = 25; second <= 40; second++) Edge(value, second - 1, second, 300);
        if (value.Snapshot(At(40), Live(40), false, false).Direction != "Steady")
            throw new InvalidOperationException("A fully supported new chain must reacquire after the old reference is cleared.");
        if (value.Snapshot(At(44), Live(44), false, false).Quality != "stale")
            throw new InvalidOperationException("Current comparison expires using the existing source maximum age.");
        Edge(value, 40, 45, 300);
        if (value.Snapshot(At(45), Live(45), false, false).Direction is not null)
            throw new InvalidOperationException("Expired support must not return from a crossing old edge.");

        value.Boundary(At(50), 4);
        Edge(value, 49, 51, 300);
        for (var second = 52; second <= 70; second++) Edge(value, second - 1, second, 300);
        if (value.Snapshot(At(70), Live(70), false, false).Direction is not null)
            throw new InvalidOperationException("A whole cross-phase edge is discarded, not clipped into new-phase support.");
        Edge(value, 70, 71, 300);
        if (value.Snapshot(At(71), Live(71), false, false) is not { Direction: "Steady", PhaseIndex: 4 })
            throw new InvalidOperationException("Twenty wholly after-boundary seconds reacquire in the actual phase instance.");
        _ = value.Snapshot(At(71), Live(71), true, false);
        value.Reset(At(81)); // Exact retrospective Auto/pause/resume never restores old support.
        Edge(value, 71, 82, 300);
        if (value.Snapshot(At(82), Live(82), false, false).Direction is not null)
            throw new InvalidOperationException("Pause/retro or a long accepted distance edge cannot establish recent trend.");

        var live = new RunLivePace(policy);
        var trend = new RunPaceTrend();
        for (var second = 1; second <= 25; second++)
        {
            var edge = new AcceptedDistanceEdge(At(second - 1), At(second), 4);
            var observation = new DistanceObservation(true, At(second - 1), At(second));
            live.Observe(observation, edge, At(second));
            trend.Observe(observation, edge, At(second), policy);
            var original = live.Snapshot(At(second));
            var rendered = original with { Trend = trend.Snapshot(At(second), original, false, false) };
            if (original.SecondsPerKilometre != rendered.SecondsPerKilometre || JsonSerializer.Serialize(original) != JsonSerializer.Serialize(rendered))
                throw new InvalidOperationException("Ephemeral trend cannot alter actual live pace or journal/AI/HC serialization.");
        }
        trend.Observe(new(false, At(25), At(26)), null, At(26), policy);
        if (trend.Snapshot(At(26), Live(26), false, false).Direction is not null)
            throw new InvalidOperationException("Actual quality loss clears the trend even if another presentation still has a pace.");

        Console.WriteLine("PASS: accepted-edge trend full windows/deadband, phase crossings, freshness without ticks, duplicates, pause/retro and unchanged live serialization.");
    }
}
