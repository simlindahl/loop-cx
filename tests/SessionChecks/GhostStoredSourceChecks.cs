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
        var fixture = LoopCx.GhostSyntheticSourceFixture.Create(Guid.Parse("91919191-9191-9191-9191-919191919191"));
        var fixtureState = fixture.Latest;
        using var fixturePlan = JsonDocument.Parse(fixtureState.OriginalPlan);
        using var fixtureTemplate = JsonDocument.Parse(fixtureState.TemplateMetadata);
        using var fixtureCheckpoint = JsonDocument.Parse(fixtureState.Checkpoint);
        var fixtureSource = new ExportJournalSnapshot(fixtureState.Id, fixtureState.StartedUtc.ToString("O"), fixtureState.ObservedUtc.ToString("O"),
            fixtureState.ElapsedMilliseconds, "finished", fixturePlan.RootElement.Clone(), fixtureTemplate.RootElement.Clone(), null,
            fixtureCheckpoint.RootElement.Clone(), "disabled_synthetic",
            fixture.Events.Select((item, index) => new ExportEvent(index + 1, item.Command, item.ObservedUtc.ToString("O"), item.ElapsedMilliseconds,
                JsonSerializer.Deserialize<JsonElement>(item.Checkpoint))).ToArray(),
            fixture.Locations.Select((point, index) => new ExportLocation(index + 1, point.UtcMilliseconds, point.MonotonicNanoseconds,
                point.Latitude, point.Longitude, point.Accuracy)
            { Provenance = point.Provenance }).ToArray(), []);
        var fixtureCapture = GhostStoredSource.Capture(fixtureSource, "Synthetic fixture route");
        Require(fixtureCapture.Attempt.Quality == AttemptQuality.InsufficientEvidence && fixtureCapture.Attempt.Elapsed == TimeSpan.FromMinutes(20)
            && fixtureSource.Locations.Count == 300 && !fixtureCapture.Attempt.Points[0].Known && !fixtureCapture.Attempt.Points[^1].Known,
            "The actual native C2 seed has coherent full Main/boot/UTC/provenance but honest unknown tails/no fabricated benchmark.");
        _ = StoredRunProjection.Detail(fixtureSource, null);
        var probeRace = LoopCx.GhostSyntheticSourceFixture.CreateRace(fixtureCapture.Geometry.Route, Guid.NewGuid());
        _ = GhostRaceCheckpoint.Read(JsonSerializer.Serialize(probeRace.Checkpoint(1, 100000)));
        probeRace.End(102000);
        var probeTerminal = GhostRaceCheckpoint.Read(JsonSerializer.Serialize(probeRace.Checkpoint(2, 102000)));
        Require(probeTerminal.Attempt().Quality == AttemptQuality.Incomplete && probeTerminal.Attempt().Elapsed == TimeSpan.FromSeconds(2),
            "The actual transaction probe fixture passes the production typed start/terminal reader before native fault stages.");
        Require(JsonSerializer.Serialize(fixture) == JsonSerializer.Serialize(LoopCx.GhostSyntheticSourceFixture.Create(fixtureState.Id)),
            "The native fixture freezes exact deterministic checkpoint/raw/event bytes for identical retry.");
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
        foreach (var context in new[] { new RunStartContext("Missing/UnknownZone", 3600, 1000), new RunStartContext("Europe/Stockholm", -3600, 1000) })
        {
            var checkpoint = JsonNode.Parse(Source(false).Checkpoint.GetRawText())!;
            checkpoint["StartContext"] = JsonSerializer.SerializeToNode(context);
            Reject(() => GhostStoredSource.Capture(Source(false) with { Checkpoint = JsonSerializer.SerializeToElement(checkpoint) }, "Bad civil evidence"),
                "Main starting at zero still requires a resolvable saved zone and matching offset.");
        }
        Reject(() => GhostSourceEvidenceCodec.Read(GhostSourceEvidenceCodec.Write(envelope with
        {
            MainStartMilliseconds = 1000,
            MainEndMilliseconds = 41000,
            ObservedElapsedMilliseconds = 41000
        })), "Same-duration shifted Main bounds must match the actual stored phase instance.");
        Reject(() => GhostSourceEvidenceCodec.Read(GhostSourceEvidenceCodec.Write(envelope with
        {
            StartedUtc = utc.AddSeconds(1).ToString("O")
        })), "Changed source UTC cannot retain unrelated recorded effective boundary anchors.");
        Reject(() => GhostSourceEvidenceCodec.Read(GhostSourceEvidenceCodec.Write(envelope with
        {
            MainStartedLocal = envelope.MainStartedLocal.ToOffset(TimeSpan.FromHours(-1))
        })), "Recorded-start civil offset cannot contradict frozen start context.");
        Reject(() => GhostSourceEvidenceCodec.ValidateAttempt(envelope, new GhostAttempt(captured.Attempt.Id, id,
            captured.Attempt.Route, captured.Attempt.StartedLocal.AddYears(1), captured.Attempt.TimeZoneId,
            captured.Attempt.Elapsed, captured.Attempt.Quality, captured.Attempt.RuleVersion, captured.Attempt.Points)),
            "The archive reader must reject an attempt whose local year/start differs from its source.");
        Reject(() => GhostSourceEvidenceCodec.ValidateAttempt(envelope, new GhostAttempt(captured.Attempt.Id, id,
            captured.Attempt.Route, captured.Attempt.StartedLocal, "Other/Zone", captured.Attempt.Elapsed,
            captured.Attempt.Quality, captured.Attempt.RuleVersion, captured.Attempt.Points)),
            "The archive reader must bind the saved attempt zone identity to source evidence.");
        GhostSourceEvidenceCodec.ValidateAttempt(envelope, captured.Attempt);
        var warmedSource = Source(false);
        TimedPhase[] warmedPlan = [new(RunPhase.Warmup, TimeSpan.FromSeconds(10), 2), plan[0]];
        var warmedCheckpoint = JsonNode.Parse(warmedSource.Checkpoint.GetRawText())!;
        warmedCheckpoint["Plan"] = JsonSerializer.SerializeToNode(warmedPlan);
        warmedCheckpoint["Completed"] = JsonSerializer.SerializeToNode(warmedPlan.Select(phase => new CompletedPhase(phase, phase.Duration)));
        warmedCheckpoint["PhaseBoundaries"] = JsonSerializer.SerializeToNode(new[]
        {
            new PhaseBoundaryFact(0, 1, RunPhase.Warmup, RunPhase.Work, 10000, 10000, utc.AddSeconds(10), utc.AddSeconds(10), false),
            new PhaseBoundaryFact(1, null, RunPhase.Work, null, 50000, 50000, utc.AddSeconds(50), utc.AddSeconds(50), false)
        });
        warmedSource = warmedSource with
        {
            OriginalPlan = JsonSerializer.SerializeToElement(warmedPlan),
            Checkpoint = JsonSerializer.SerializeToElement(warmedCheckpoint),
            ObservedElapsedMilliseconds = 50000,
            LastObservedUtc = utc.AddSeconds(50).ToString("O"),
            Locations = warmedSource.Locations.Select(point => point with
            {
                BootMonotonicNanoseconds = point.BootMonotonicNanoseconds + 10000000000,
                UtcMilliseconds = point.UtcMilliseconds + 10000
            }).ToArray()
        };
        var warmedCapture = GhostStoredSource.Capture(warmedSource, "After recorded warm-up");
        var warmedEnvelope = GhostSourceEvidenceCodec.Read(warmedCapture.RawJson);
        GhostSourceEvidenceCodec.ValidateAttempt(warmedEnvelope, warmedCapture.Attempt);
        Require(warmedEnvelope.MainStartMilliseconds == 10000 && warmedEnvelope.MainEndMilliseconds == 50000
            && warmedEnvelope.MainStartedLocal.EqualsExact(warmedCapture.Attempt.StartedLocal)
            && warmedEnvelope.LocalStartProvenance == GhostStoredSource.DerivedCivil,
            "Main after warm-up freezes capture-derived civil evidence; subsequent reads validate saved facts without resolving current zone rules.");
        Reject(() => GhostSourceEvidenceCodec.Read(GhostSourceEvidenceCodec.Write(warmedEnvelope with
        {
            MainStartMilliseconds = 0,
            MainEndMilliseconds = 40000
        })), "An equal-duration window cannot substitute for Main after the actual warm-up boundary.");
        foreach (var terminalMs in new[] { 40000L, 39999L })
        {
            var terminalSource = Source(false);
            var terminalPoints = terminalSource.Locations.ToArray();
            terminalPoints[^1] = terminalPoints[^1] with
            {
                UtcMilliseconds = utc.AddMilliseconds(terminalMs).ToUnixTimeMilliseconds(),
                BootMonotonicNanoseconds = (terminalMs + 1000) * 1000000,
                Provenance = terminalPoints[^1].Provenance! with { Outcome = "not-assessed", DrawablePredecessor = null, BreakReason = "Finalization" }
            };
            terminalSource = terminalSource with
            {
                Locations = terminalPoints,
                ObservedElapsedMilliseconds = 40001,
                LastObservedUtc = utc.AddMilliseconds(40001).ToString("O")
            };
            var terminalCapture = GhostStoredSource.Capture(terminalSource, "Supported prefix");
            var terminalEnvelope = GhostSourceEvidenceCodec.Read(terminalCapture.RawJson);
            Require(terminalCapture.Geometry.Raw.Count == 10 && terminalCapture.Attempt.Elapsed == TimeSpan.FromSeconds(40)
                && terminalCapture.Attempt.Points[^1] == new GhostPoint(TimeSpan.FromSeconds(40), terminalCapture.Geometry.Route.LengthMetres, false)
                && terminalCapture.Attempt.Quality == AttemptQuality.InsufficientEvidence
                && GhostReferences.Select(terminalCapture.Geometry.Route, [terminalCapture.Attempt], 2025).PersonalBest is null
                && terminalEnvelope.Locations[^1].Provenance!.BreakReason == "Finalization",
                "A measured endpoint delivered after actual finalization retains raw evidence and supported prefix, never a fabricated known finish/PB.");
        }
        var interior = Source(false);
        var interiorPoints = interior.Locations.ToArray();
        interiorPoints[2] = interiorPoints[2] with
        {
            Provenance = interiorPoints[2].Provenance! with
            { Outcome = "not-assessed", DrawablePredecessor = null, BreakReason = "Finalization" }
        };
        Reject(() => GhostStoredSource.Capture(interior with { Locations = interiorPoints }, "Not a terminal suffix"),
            "A Finalization label inside a later valid track cannot bypass an interior evidence break.");
        const string legacy = "[{\"Elapsed\":\"00:00:00\",\"Utc\":\"2025-01-01T00:00:00+00:00\",\"Latitude\":59,\"Longitude\":18,\"AccuracyMetres\":5,\"Section\":1,\"GapBefore\":false}]";
        Require(JsonSerializer.Serialize(JsonSerializer.Deserialize<RecordedRouteFix[]>(legacy)) == legacy,
            "Legacy raw-array bytes must stay identical for existing InsertFrozen retry comparisons.");
        CheckLaterBoundaryObservation(Source(false), utc);
        Console.WriteLine("PASS: stored Ghost full Main versus GPS span, unknown ends, positive eligibility, bound source/civil clocks, raw ignored retention and exact legacy/v2 codec bytes.");
    }

    private static void CheckLaterBoundaryObservation(ExportJournalSnapshot lateObservedSource, DateTimeOffset utc)
    {
        // Arrange
        var lateObservedCheckpoint = JsonNode.Parse(lateObservedSource.Checkpoint.GetRawText())!;
        lateObservedCheckpoint["PhaseBoundaries"]![0]!["ObservedElapsedMilliseconds"] = 40009;
        lateObservedCheckpoint["PhaseBoundaries"]![0]!["ObservedUtc"] = utc.AddMilliseconds(40009).ToString("O");
        lateObservedSource = lateObservedSource with
        {
            Checkpoint = JsonSerializer.SerializeToElement(lateObservedCheckpoint),
            ObservedElapsedMilliseconds = 40001,
            LastObservedUtc = utc.AddMilliseconds(40010).ToString("O")
        };
        var lateObservedBytes = ExportRunV2.Project(lateObservedSource);

        // Act
        var lateObservedCapture = GhostStoredSource.Capture(lateObservedSource, "Later boundary callback");
        var lateObservedEnvelope = GhostSourceEvidenceCodec.Read(lateObservedCapture.RawJson);
        var lateObservedRoute = StoredRouteProjection.Read(lateObservedSource.Id, lateObservedSource.Checkpoint,
            lateObservedSource.ObservedElapsedMilliseconds, lateObservedSource.Locations);

        // Assert
        Require(lateObservedCapture.Attempt.Elapsed == TimeSpan.FromSeconds(40) && lateObservedRoute.Main.Supported
            && lateObservedRoute.Main.Points.Count == 10 && lateObservedRoute.Main.Points[^1].SnapshotOrder == 10
            && lateObservedEnvelope.Checkpoint.GetProperty("PhaseBoundaries")[0].GetProperty("ObservedElapsedMilliseconds").GetInt64() == 40009
            && GhostSourceEvidenceCodec.Write(lateObservedEnvelope) == lateObservedCapture.RawJson
            && ExportRunV2.Project(lateObservedSource) == lateObservedBytes,
            "A later callback preserves immutable source and effective Main; History retains its half-open window and excludes the endpoint.");
        foreach (var effectiveBeyondHorizon in new[] { false, true })
        {
            var badCheckpoint = JsonNode.Parse(lateObservedSource.Checkpoint.GetRawText())!;

            if (effectiveBeyondHorizon)
            {
                badCheckpoint["PhaseBoundaries"]![0]!["EffectiveElapsedMilliseconds"] = 40002;
                badCheckpoint["PhaseBoundaries"]![0]!["StartAnchorDerivedEffectiveUtc"] = utc.AddMilliseconds(40002).ToString("O");
            }
            else
            {
                badCheckpoint["PhaseBoundaries"]![0]!["ObservedElapsedMilliseconds"] = 39999;
            }

            var badSource = lateObservedSource with { Checkpoint = JsonSerializer.SerializeToElement(badCheckpoint) };
            Reject(() => GhostStoredSource.Capture(badSource, "Contradictory boundary"),
                "An effective boundary beyond the processed horizon or observed before it cannot supply a Ghost route.");
            Require(!StoredRouteProjection.Read(lateObservedSource.Id, badSource.Checkpoint, badSource.ObservedElapsedMilliseconds, badSource.Locations).Main.Supported,
                "History cannot infer Main membership from the same contradictory boundary.");
        }
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or TimeZoneNotFoundException or InvalidTimeZoneException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
