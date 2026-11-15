using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;
using LoopCx.Features.History;

internal static class GhostStoredSourceChecks
{
    internal static void Run()
    {
        // Arrange
        var id = Guid.Parse("45454545-4545-4545-4545-454545454545");
        var utc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimedPhase[] plan = [new(RunPhase.Work, TimeSpan.FromSeconds(40), 4)];
        ExportJournalSnapshot Source(bool tails, bool ignored = false)
        {
            var ticks = tails ? Enumerable.Range(0, 10).Select(index => 2 + index * 4).ToArray()
                : Enumerable.Range(0, 11).Select(index => index * 4).ToArray();
            var points = new List<ExportLocation>();
            long identity = 0, previous = 0;
            foreach (var second in ticks)
            {
                identity++;
                points.Add(new(identity, utc.AddSeconds(second).ToUnixTimeMilliseconds(), (second + 1L) * 1000000000,
                    59 + second * 0.00001, 18, 5)
                { Provenance = new(1, id, identity, "valid", previous == 0 ? null : previous, previous == 0 ? "Run start" : null) });
                previous = identity;
                if (ignored && identity == 1)
                {
                    identity++;
                    points.Add(new(identity, utc.ToUnixTimeMilliseconds(), 1000000000, 59, 18, 5)
                    { Provenance = new(1, id, identity, "ignored", null, "Duplicate delivery") });
                }
            }
            var checkpoint = JsonSerializer.SerializeToElement(new
            {
                SchemaVersion = 1,
                Snapshot = new { Phase = "Finished" },
                Plan = plan,
                Completed = plan.Select(phase => new CompletedPhase(phase, phase.Duration)),
                PhaseActiveTime = TimeSpan.Zero,
                TotalActiveTime = TimeSpan.FromSeconds(40),
                MainActiveTime = TimeSpan.FromSeconds(40),
                StartContext = new RunStartContext("Europe/Stockholm", 3600, 1000),
                PhaseBoundaries = new[] { new PhaseBoundaryFact(0, null, RunPhase.Work, null, 40000, 40000, utc.AddSeconds(40), utc.AddSeconds(40), false) }
            });
            return new(id, utc.ToString("O"), utc.AddSeconds(40).ToString("O"), 40000, "finished", JsonSerializer.SerializeToElement(plan),
                JsonSerializer.SerializeToElement(CoreTemplate.C2), null, checkpoint, "disabled_synthetic", [], points, []);
        }
        var source = Source(true, true);
        var before = ExportRunV2.Project(source);

        // Act
        var captured = GhostStoredSource.Capture(source, "Stored route");
        var retry = GhostStoredSource.Capture(source, "Stored route");
        var envelope = GhostSourceEvidenceCodec.Read(captured.RawJson);
        var complete = GhostStoredSource.Capture(Source(false), "Complete route");
        var records = GhostReferences.Select(complete.Geometry.Route, [complete.Attempt], 2025);

        // Assert
        Require(captured.Attempt.Elapsed == TimeSpan.FromSeconds(40) && captured.Geometry.ObservedSpan == TimeSpan.FromSeconds(36),
            "Full Main boundary elapsed must not be replaced with first/last GPS span.");
        Require(!captured.Attempt.Points[0].Known && !captured.Attempt.Points[^1].Known
            && captured.Attempt.Quality == AttemptQuality.InsufficientEvidence && captured.QualityReason is not null,
            "Unknown prefix/suffix are explicit and cannot become invented known race endpoints.");
        Require(envelope.Locations.Count == source.Locations.Count && envelope.Locations[1].Provenance!.Outcome == "ignored"
            && captured.Geometry.Raw.Count == source.Locations.Count - 1,
            "Ignored duplicate is excluded by v2 derivation but preserved in original callback order.");
        Require(captured.RawJson == retry.RawJson && captured.Attempt.Id == retry.Attempt.Id && captured.Geometry.Route.Key == retry.Geometry.Route.Key,
            "Retry freezes identical source evidence and stable route/version/attempt identities.");
        Require(GhostSourceEvidenceCodec.Write(envelope) == captured.RawJson && ExportRunV2.Project(source) == before,
            "Source codec roundtrip and capture do not mutate original journal/AI facts.");
        Require(records.YearBest?.Id == complete.Attempt.Id && records.PersonalBest?.Id == complete.Attempt.Id,
            "A positively complete policy-supported source can be a benchmark; not a physical calibration claim.");
        foreach (var bad in new[] { source with { Status = "interrupted" }, source with { Template = null },
            source with { Locations = source.Locations.Select(point => point with { Provenance = null }).ToArray() } })
        {
            try { _ = GhostStoredSource.Capture(bad, "Rejected source"); }
            catch (InvalidDataException) { continue; }
            throw new InvalidOperationException("Unsupported source must have a concrete rejection, not fabricated eligibility.");
        }
        var gapped = source.Locations.ToArray();
        gapped[2] = gapped[2] with { Provenance = gapped[2].Provenance! with { Outcome = "rejected", DrawablePredecessor = null, BreakReason = "GPS rejected" } };
        gapped[3] = gapped[3] with { Provenance = gapped[3].Provenance! with { DrawablePredecessor = null, BreakReason = "Reanchor" } };
        try { _ = GhostStoredSource.Capture(source with { Locations = gapped }, "Uncertain geometry"); }
        catch (InvalidDataException) { gapped = []; }
        Require(gapped.Length == 0, "Rejected source evidence cannot silently become a continuous geometry or Verified source.");
        var interiorUnknown = new GhostAttempt(complete.Attempt.Id, complete.Attempt.SourceRunId, complete.Attempt.Route,
            complete.Attempt.StartedLocal, complete.Attempt.TimeZoneId, complete.Attempt.Elapsed, AttemptQuality.Verified,
            complete.Attempt.RuleVersion, complete.Attempt.Points.Select((point, index) => index == 1 ? point with { Known = false } : point).ToArray());
        Require(GhostReferences.Select(complete.Geometry.Route, [interiorUnknown], 2025).PersonalBest is not null,
            "An independently policy-verified complete attempt is not globally excluded merely by an unknown internal comparison sample.");
        var unsupported = JsonNode.Parse(captured.RawJson)!;
        unsupported["Version"] = 3;
        try { _ = GhostSourceEvidenceCodec.Read(unsupported.ToJsonString()); }
        catch (InvalidDataException) { unsupported = null; }
        Require(unsupported is null, "Unsupported stored-source versions fail closed without rewriting old evidence.");
        const string legacy = "[{\"Elapsed\":\"00:00:00\",\"Utc\":\"2025-01-01T00:00:00+00:00\",\"Latitude\":59,\"Longitude\":18,\"AccuracyMetres\":5,\"Section\":1,\"GapBefore\":false}]";
        Require(JsonSerializer.Serialize(JsonSerializer.Deserialize<RecordedRouteFix[]>(legacy)) == legacy,
            "Legacy raw-array bytes must stay identical for existing InsertFrozen retry comparisons.");
        Console.WriteLine("PASS: stored Ghost full Main versus GPS span, unknown ends, positive eligibility, raw ignored retention and exact legacy/v2 codec bytes.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
