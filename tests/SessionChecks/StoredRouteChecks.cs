using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.History;

internal static class StoredRouteChecks
{
    internal static void Run()
    {
        var id = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var started = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var plan = new[] { new TimedPhase(RunPhase.Warmup, TimeSpan.FromSeconds(10), 2),
            new TimedPhase(RunPhase.Work, TimeSpan.FromSeconds(20), 6), new TimedPhase(RunPhase.Recovery, TimeSpan.FromSeconds(5), 2),
            new TimedPhase(RunPhase.Cooldown, TimeSpan.FromSeconds(10), 2) };
        JsonElement Checkpoint(long workEnd = 30000, bool synthetic = false, long motionStart = 1000, bool partial = false)
        {
            long[] ends = [10000, workEnd, workEnd + 5000, workEnd + 15000];
            var facts = ends.Select((at, index) => new PhaseBoundaryFact(index, index == 3 ? null : index + 1,
                plan[index].Kind, index == 3 ? null : plan[index + 1].Kind, at, at + 10,
                started.AddMilliseconds(at + 10), started.AddMilliseconds(at), synthetic)).ToArray();
            return JsonSerializer.SerializeToElement(new
            {
                SchemaVersion = 1,
                Plan = plan,
                Completed = partial ? plan.Take(2).Select(phase => new CompletedPhase(phase, phase.Duration)).ToArray()
                    : plan.Select(phase => new CompletedPhase(phase, phase.Duration)).ToArray(),
                PhaseBoundaries = partial ? facts.Take(2).ToArray() : facts,
                StartContext = new RunStartContext("Europe/Stockholm", 3600, 1000),
                Snapshot = new { Phase = partial ? "Recovery" : "Finished", Motion = new { StartedMonotonicTime = TimeSpan.FromMilliseconds(motionStart) } }
            });
        }
        ExportLocation Point(long order, int seconds, long? predecessor, string outcome = "valid") =>
            new(order, started.AddSeconds(seconds).ToUnixTimeMilliseconds(), (seconds + 1L) * 1_000_000_000, 1, 2, 5)
            { Provenance = new(1, id, order, outcome, predecessor, predecessor is null ? "Boundary" : null) };
        ExportLocation[] points = [Point(1, 5, null), Point(2, 10, 1), Point(3, 9, null, "ignored"),
            Point(4, 20, 2), Point(5, 32, 4), Point(6, 40, 5)];
        var checkpoint = Checkpoint();
        var before = checkpoint.GetRawText();
        var rawBefore = JsonSerializer.Serialize(points.Select(point => point.Provenance));
        var route = StoredRouteProjection.Read(id, checkpoint, 50000, points);
        Require(route.Main.Supported && route.Main.Points.Select(point => point.SnapshotOrder).SequenceEqual([2L, 4, 5])
            && route.Whole.Points.Count == 5 && route.Main.Links.SequenceEqual([new StoredRouteLink(2, 4), new(4, 5)]),
            "Main excludes warm/cool, retains Work/Recovery and explicit link across ignored row without rebuilding distance.");
        Require(StoredRouteProjection.Read(id, Checkpoint(synthetic: true), 50000, points).Main.Supported,
            "A synthetic label alone cannot reject coherent timing.");
        Require(!StoredRouteProjection.Read(id, Checkpoint(motionStart: 500), 50000, points).Main.Supported,
            "Contradictory frozen boot anchors cannot establish Main membership after reopen/current boot change.");
        JsonElement Changed(Action<JsonNode> change)
        {
            var node = JsonNode.Parse(checkpoint.GetRawText())!;
            change(node);
            return JsonSerializer.SerializeToElement(node);
        }
        foreach (var bad in new[] {
            Changed(node => node["Completed"]![0]!["Plan"]!["Kind"] = 1),
            Changed(node => node["PhaseBoundaries"]![0]!["PreviousPhaseIndex"] = 1),
            Changed(node => { node["PhaseBoundaries"]![1]!["Synthetic"] = true; node["PhaseBoundaries"]![1]!["ObservedElapsedMilliseconds"] = 20000; }) })
            Require(!StoredRouteProjection.Read(id, bad, 50000, points).Main.Supported,
                "Mismatched completed phase, reordered boundary and genuinely accelerated effective/receipt disagreement cannot prove Main.");
        var retrospective = Changed(node => node["PhaseBoundaries"]![0]!["ObservedElapsedMilliseconds"] = 20000);
        Require(StoredRouteProjection.Read(id, retrospective, 50000, points).Main.Points.Any(point => point.SnapshotOrder == 2),
            "Retrospective boundary uses its effective boot instant, not later callback receipt or inferred active-time allocation.");
        var missingCooldownEnd = Changed(node => node["PhaseBoundaries"]!.AsArray().RemoveAt(3));
        Require(StoredRouteProjection.Read(id, missingCooldownEnd, 50000, points).Main.Supported,
            "Already delimited Main does not require a later cooldown terminal boundary.");
        var preStart = points.Select(point => point with { Provenance = null }).Prepend(
            points[0] with { SnapshotOrder = 0, Provenance = null, BootMonotonicNanoseconds = 10 }).ToArray();
        var preStartRoute = StoredRouteProjection.Read(id, checkpoint, 50000, preStart);
        Require(preStartRoute.Main.Supported && preStartRoute.Main.Points.All(point => point.SnapshotOrder != 0)
            && preStartRoute.Whole.Points.Any(point => point.SnapshotOrder == 0),
            "A cached legacy point before start stays Whole-only; it does not prove another boot or invalidate supported Main facts.");
        var earlierBoundary = StoredRouteProjection.Read(id, Checkpoint(workEnd: 12000), 50000, points);
        Require(earlierBoundary.Main.Points.Select(point => point.SnapshotOrder).SequenceEqual([2L]),
            "Earlier effective boundary selects stored points; this fixture does not simulate a runtime skip.");
        var laterBoundary = StoredRouteProjection.Read(id, Checkpoint(workEnd: 40000), 60000, points);
        Require(laterBoundary.Main.Points.Any(point => point.SnapshotOrder == 6), "Later effective boundary selects stored points; this fixture does not simulate an applied-duration extension.");
        var partial = StoredRouteProjection.Read(id, Checkpoint(partial: true), 33000, [.. points.Where(point => point.SnapshotOrder < 6), Point(6, 33, 5) with { BootMonotonicNanoseconds = 34_000_500_000 }]);
        Require(partial.Main.Supported && partial.Main.Points.Any(point => point.SnapshotOrder == 6),
            "Committed current Recovery prefix includes the same owner-truncated millisecond with sub-ms raw receipt, without inventing stop.");
        var legacy = StoredRouteProjection.Read(id, JsonSerializer.SerializeToElement(new { legacy = true }), 50000,
            points.Select(point => point with { Provenance = null }).ToArray());
        Require(!legacy.Main.Supported && legacy.Whole.UnknownQuality && legacy.Whole.Links.Count == 0,
            "Legacy points stay isolated with unknown quality and no guessed Main mapping.");
        var rejected = points.ToArray();
        rejected[2] = Point(3, 11, null, "rejected");
        rejected[3] = Point(4, 20, null);
        Require(!StoredRouteProjection.Read(id, checkpoint, 50000, rejected).Whole.Links.Any(link => link.FromOrder == 2 && link.ToOrder == 4),
            "Rejected capture does not become a bridged route.");
        Require(checkpoint.GetRawText() == before && JsonSerializer.Serialize(points.Select(point => point.Provenance)) == rawBefore,
            "Projection preserves frozen checkpoint and provenance bytes.");
        Console.WriteLine("PASS: detached route scopes, explicit links/ignored delivery, frozen boot/phase boundaries, partial cutoff and legacy unknowns without writes.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
