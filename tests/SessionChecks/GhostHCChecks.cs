using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;
using LoopCx.Features.HealthConnectExport;

internal static class GhostHCChecks
{
    private static readonly double[] EndReceiptSeconds = [0, 5, 10, 10.1];
    internal static void Run()
    {
        // Arrange
        var utc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var route = new GhostRoute(new(Guid.Parse("abababab-abab-abab-abab-abababababab"), Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd")),
            "Host fixture", Guid.NewGuid(), "host-mapping-check-v1",
            [new(0, 0, 0), new(0, .0001, 11.11950802335329), new(0, .0002, 22.23901604670658)]);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1, S(3), 4, 5, 500),
            S(3), S(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot, LivePacePolicy.Pilot);
        var race = new GhostRace(Guid.Parse("12121212-1212-1212-1212-121212121212"), route, GhostMode.YearBest, null, profile,
            new(99900, 100000, utc.ToUnixTimeMilliseconds(), 0, 0, .1), 100000, utc, "UTC", TimeSpan.Zero, Guid.NewGuid(), new(1, GhostOriginKind.NormalService));

        for (var second = 1; second <= 20; second++)
        {
            if (second == 5) race.Pause(105000);
            var receipt = 100000 + (second * 1000L) + (second == 20 ? 500 : 0);
            race.Observe(receipt);
            race.Location(new(utc.AddSeconds(second).ToUnixTimeMilliseconds(), (100000 + (second * 1000L)) * 1000000,
                0, second * .00001, .1), receipt);
        }

        var checkpoint = race.Checkpoint(1, 120500);
        checkpoint = checkpoint with { TerminalContext = GhostTerminalContext.Capture(checkpoint, utc.AddSeconds(20.5), TimeZoneInfo.Utc) };
        ExportHeartRate Hr(long sequence, double seconds, double bootSeconds, int bpm = 100)
        {
            return new(sequence, utc.AddSeconds(seconds).ToString("O", CultureInfo.InvariantCulture), S(100 + bootSeconds).Ticks, bpm, "valid");
        }

        ExportJournalSnapshot Source(GhostRaceCheckpoint value)
        {
            return new(value.Id, utc.ToString("O", CultureInfo.InvariantCulture),
                    utc.AddSeconds(20.5).ToString("O", CultureInfo.InvariantCulture), 20500, "finished", JsonSerializer.SerializeToElement(Array.Empty<TimedPhase>()),
                    null, null, JsonSerializer.SerializeToElement(value), "local_only", [], [], [Hr(1, 0, 0), Hr(2, 10, 10), Hr(3, 20, 20), Hr(4, 20.5, 20.5)]);
        }

        var source = Source(checkpoint);
        var original = JsonSerializer.Serialize(source);

        // Act
        var result = GhostHCWritePlanner.Create(source, false);
        var plan = result.Plan ?? throw new InvalidOperationException(result.Reason);
        var session = plan.Records.Single(record => record.Kind == HCRecordKind.RunningSession);
        var hr = plan.Records.Single(record => record.Kind == HCRecordKind.HeartRate);

        // Assert
        Require(plan.MappingVersion == 2 && plan.HasValidHash() && plan.Records.All(record => record.ClientRecordVersion == 2)
            && session.ClientRecordId == $"loop-cx:{source.Id:D}:hc-v2:session", "Mapping2/client2 uses the actual race identity and stable hash.");
        Require(session.EndUtc == utc.AddSeconds(20) && session.Pauses.Single() == new HCPause(utc.AddSeconds(5), utc.AddSeconds(20))
            && hr.HeartRateSamples.Select(sample => sample.SourceSequence).SequenceEqual([1L, 2L, 3L]),
            "Delayed finish retains full race time, actual paused HR and both inclusive endpoints, excluding the late receipt.");
        Require(plan.Records.All(record => record.Provenance!.Contains("matched-finish", StringComparison.Ordinal))
            && original == JsonSerializer.Serialize(source) && GhostHCWritePlanner.Create(source, false).Plan!.PayloadHash == plan.PayloadHash,
            "Typed terminal meaning and source bytes remain immutable and deterministic.");

#if DEBUG
        var fixture = GhostHCConstructionFixture.Plan();
        GhostHCConstructionFixture.Validate(fixture);
        _ = GhostRaceCheckpoint.Read(GhostHCConstructionFixture.Source().Checkpoint.GetRawText());
        var alteredRecords = fixture.Records.Select((record, index) => index == 0 ? record with { ClientRecordVersion = 3 } : record).ToArray();
        var altered = fixture with
        {
            Records = alteredRecords,
            PayloadHash = HCWritePlanV1.Hash(2, fixture.RunId, fixture.Source, alteredRecords, fixture.Warnings)
        };
        var rejected = false;

        try { GhostHCConstructionFixture.Validate(altered); }
        catch (InvalidDataException) { rejected = true; }

        Require(rejected && GhostHCWritePlanner.Create(GhostHCConstructionFixture.Source(), true).State == "disabled_synthetic",
            "Only the fixed synthetic SDK fixture is accepted; a rehashed metadata mutation and ordinary Debug projection cannot bypass guards.");
        Console.WriteLine("Ghost SDK fixed fixture SHA256: " + fixture.PayloadHash);
#endif
        Require(plan.PayloadHash != "37fb46df437563c98bd426360cb9ea391cd42bc3b6330a6619e70132d49422f2"
            && GhostHCWritePlanner.Create(source with { Checkpoint = JsonSerializer.SerializeToElement(checkpoint with { Origin = null }), ExportStatus = "pending" }, false).State == "local_only",
            "Historical no-origin Ghost evidence now stays local-only; new origin intentionally binds a different hash without changing Core1.");
        foreach (var kind in new[] { GhostOriginKind.DebugService, GhostOriginKind.SyntheticFixture })
        {
            var savedOrigin = source with { Checkpoint = JsonSerializer.SerializeToElement(checkpoint with { Origin = new(1, kind) }), ExportStatus = "pending" };
            var coldOrigin = JsonSerializer.Deserialize<ExportJournalSnapshot>(JsonSerializer.Serialize(savedOrigin))!;
            Require(GhostHCWritePlanner.Create(coldOrigin, false).State == "disabled_synthetic",
                "A later Release/queue read after serialized reopen cannot relabel debug or synthetic creation origin.");
        }
        ExplicitEndPreservesItsKnownWindowAndIncompleteOutcome();
        CheckProvenanceAndZeroActive(source, checkpoint);
        CheckOptionalAndFailures(source, checkpoint, Hr);
        Console.WriteLine("PASS: pure Ghost HC mapping2/client2, actual matched-finish/explicit-End/pause clocks, optional distance/offsets and dual-clock HR boundaries.");
    }

    private static void ExplicitEndPreservesItsKnownWindowAndIncompleteOutcome()
    {
        // Arrange
        var start = new DateTimeOffset(2025, 2, 1, 12, 0, 0, TimeSpan.Zero);
        var route = new GhostRoute(new(Guid.Parse("15151515-1515-4515-8515-151515151515"), Guid.Parse("25252525-2525-4525-8525-252525252525")),
            "Explicit End host fixture", Guid.NewGuid(), "explicit-end-host-v1",
            [new(0, 0, 0), new(0, .0001, 11.11950802335329), new(0, .0002, 22.23901604670658)]);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1, S(3), 4, 5, 500),
            S(3), S(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot);
        GhostRace NewRace()
        {
            return new(Guid.NewGuid(), route, GhostMode.YearBest, null, profile,
            new(99900, 100000, start.ToUnixTimeMilliseconds(), 0, 0, .1), 100000, start, "UTC", TimeSpan.Zero, Guid.NewGuid(), new(1, GhostOriginKind.NormalService));
        }

        var race = NewRace();

        for (var second = 1; second <= 4; second++)
        {
            race.Location(new(start.AddSeconds(second).ToUnixTimeMilliseconds(), (100000 + (second * 1000L)) * 1000000,
                0, second * .00001, .1), 100000 + (second * 1000L));
        }

        race.Pause(105000);
        race.Resume(107000);
        race.End(110000);
        race.Observe(112000); // Save/observation after End must not extend its effective window.
        var checkpoint = race.Checkpoint(1, 112000);
        checkpoint = checkpoint with { TerminalContext = GhostTerminalContext.Capture(checkpoint, start.AddSeconds(12), TimeZoneInfo.Utc) };
        ExportJournalSnapshot Source(GhostRaceCheckpoint value)
        {
            return new(value.Id, start.ToString("O", CultureInfo.InvariantCulture),
            start.AddMilliseconds(value.ObservedBootMilliseconds - value.StartedBootMilliseconds).ToString("O", CultureInfo.InvariantCulture),
            value.ObservedBootMilliseconds - value.StartedBootMilliseconds, "incomplete", JsonSerializer.SerializeToElement(Array.Empty<TimedPhase>()),
            null, null, JsonSerializer.SerializeToElement(value), "local_only", [], [],
            [.. EndReceiptSeconds.Select((second, index) => new ExportHeartRate(index + 1,
                start.AddSeconds(second).ToString("O", CultureInfo.InvariantCulture), S(100 + second).Ticks, 100 + index, "valid"))]);
        }

        var source = Source(checkpoint);
        var original = JsonSerializer.Serialize(source);

        // Act
        var result = GhostHCWritePlanner.Create(source, false);
        var plan = result.Plan ?? throw new InvalidOperationException(result.Reason);
        var repeated = GhostHCWritePlanner.Create(source, false).Plan!;
        var attempt = checkpoint.Attempt();
        var legacy = GhostHCWritePlanner.Create(Source(checkpoint with { TerminalContext = null }), false).Plan!;

        // Assert
        Require(plan.MappingVersion == 2 && plan.Records.All(record => record.ClientRecordVersion == 2)
            && plan.Records[0].EndUtc == start.AddSeconds(10) && plan.Records[0].Pauses.Single() == new HCPause(start.AddSeconds(5), start.AddSeconds(7))
            && checkpoint.Snapshot.Clock.TrainingActive == S(8), "Actual explicit End maps its known positive window and pause union, not a later save receipt.");
        Require(attempt.Quality == AttemptQuality.Incomplete && GhostReferences.Select(route, [attempt], start.Year).PersonalBest is null
            && plan.Records.All(record => record.Provenance!.Contains("explicit-end", StringComparison.Ordinal)
                && record.Provenance.Contains("Incomplete", StringComparison.Ordinal)), "Export retains Incomplete and cannot create a Ghost record or fabricate matched-finish provenance.");
        Require(plan.Records.Single(record => record.Kind == HCRecordKind.Distance).AcceptedMetres == checkpoint.MeasuredTrainingDistance!.TotalMeasuredMetres
            && checkpoint.MeasuredTrainingDistance.TotalMeasuredMetres > 0
            && plan.Records.Single(record => record.Kind == HCRecordKind.HeartRate).HeartRateSamples.Select(sample => sample.SourceSequence).SequenceEqual([1L, 2L, 3L]),
            "Actual accepted distance and valid paused/endpoint HR survive; post-End receipts are not included.");
        Require(original == JsonSerializer.Serialize(source) && repeated.PayloadHash == plan.PayloadHash
            && repeated.Records.Select(record => record.ClientRecordId).SequenceEqual(plan.Records.Select(record => record.ClientRecordId))
            && legacy.Records.All(record => record.EndOffsetSeconds is null), "Projection and repeated preparation preserve the source, deterministic IDs/hash and unknown legacy offset.");
        Require(GhostHCWritePlanner.Create(Source(checkpoint with { HealthConnectConsentRevision = null }), false).State == "local_only"
            && GhostHCWritePlanner.Create(source, true).State == "disabled_synthetic"
            && GhostHCWritePlanner.Create(source with { ExportStatus = "disabled_synthetic" }, false).Plan is null,
            "Known End does not bypass actual start consent or DEBUG/synthetic gates.");
        Reject(source with { Status = "finished" });
        Reject(source with { Id = Guid.NewGuid() });
        Reject(source with { StartedUtc = start.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture) });
        Reject(source with { ObservedElapsedMilliseconds = 11999 });
        Reject(Source(checkpoint with { TerminalKind = "matched-finish" }));
        Reject(Source(checkpoint with { TerminalContext = checkpoint.TerminalContext! with { StartDerivedEffectiveUtc = start.AddSeconds(11) } }));
        Reject(Source(checkpoint with { Snapshot = checkpoint.Snapshot with { Clock = checkpoint.Snapshot.Clock with { TrainingActive = S(9) } } }));
        var zero = NewRace();
        zero.End(100000);
        Reject(Source(zero.Checkpoint(1, 100000)));
        var running = NewRace();
        running.Observe(101000);
        var interrupted = Source(running.Checkpoint(1, 101000)) with { Status = "interrupted" };
        Require(GhostHCWritePlanner.Create(interrupted, false).Plan is null,
            "An interrupted committed prefix has no actual end and remains without an HC plan; local saved-part confirmation cannot create terminal facts.");
    }

    private static void CheckProvenanceAndZeroActive(ExportJournalSnapshot source, GhostRaceCheckpoint checkpoint)
    {
        // Arrange
        var policy = checkpoint.Profile.TrainingDistance;
        var distance = new DistanceState(policy, 123, 5, true, "Gap", [new(1, 5, S(5), true)]);
        var original = checkpoint with { MeasuredTrainingDistance = distance };
        var alternatePolicy = policy with { MaximumAccuracyMetres = policy.MaximumAccuracyMetres + 1 };
        var alternate = original with
        {
            Profile = original.Profile with { TrainingDistance = alternatePolicy },
            MeasuredTrainingDistance = distance with { Policy = alternatePolicy }
        };
        ExportJournalSnapshot With(GhostRaceCheckpoint value)
        {
            return source with { Checkpoint = JsonSerializer.SerializeToElement(value) };
        }

        var originalSource = With(original);
        var originalBytes = JsonSerializer.Serialize(originalSource);
        var laterObserved = original with
        {
            ObservedBootMilliseconds = original.ObservedBootMilliseconds + 100,
            TerminalContext = original.TerminalContext! with
            {
                ObservedUtc = original.TerminalContext.ObservedUtc.AddMilliseconds(100),
                ObservedBootMilliseconds = original.ObservedBootMilliseconds + 100
            }
        };
        var laterSource = With(laterObserved) with
        {
            LastObservedUtc = laterObserved.TerminalContext!.ObservedUtc.ToString("O", CultureInfo.InvariantCulture),
            ObservedElapsedMilliseconds = source.ObservedElapsedMilliseconds + 100
        };
        var fullPause = original with
        {
            Pauses = [new(S(100), null)],
            Snapshot = original.Snapshot with { Clock = original.Snapshot.Clock with { TrainingActive = TimeSpan.Zero, TrainingPaused = true } }
        };

        // Act
        var first = GhostHCWritePlanner.Create(originalSource, false).Plan!;
        var repeated = GhostHCWritePlanner.Create(originalSource, false).Plan!;
        var changed = GhostHCWritePlanner.Create(With(alternate), false).Plan!;
        var observedPlan = GhostHCWritePlanner.Create(laterSource, false).Plan!;
        var changedGaps = GhostHCWritePlanner.Create(With(original with
        {
            MeasuredTrainingDistance = distance with { Splits = [new(1, 5, S(5), false)] }
        }), false).Plan!;

        // Assert
        Require(first.PayloadHash == repeated.PayloadHash && first.PayloadHash != changed.PayloadHash
            && first.Records.Single(record => record.Kind == HCRecordKind.Distance).Provenance != changed.Records.Single(record => record.Kind == HCRecordKind.Distance).Provenance
            && first.PayloadHash != changedGaps.PayloadHash && originalBytes == JsonSerializer.Serialize(originalSource),
            "Coherent policy or individual gap changes affect frozen provenance/hash; identical replay is stable and source unchanged.");
        Require(observedPlan.Records[0].EndUtc == first.Records[0].EndUtc
            && observedPlan.Records[0].Provenance != first.Records[0].Provenance && observedPlan.PayloadHash != first.PayloadHash,
            "Different actual terminal observations retain a separate frozen provenance/hash even with the same effective end.");
        Reject(With(fullPause));
        Require(GhostHCWritePlanner.Create(With(fullPause with
        {
            MeasuredTrainingDistance = distance with { TotalMeasuredMetres = 0, MainMeasuredMetres = 0, Splits = [] }
        }), false).Plan is not null
            && GhostHCWritePlanner.Create(With(fullPause with { MeasuredTrainingDistance = null }), false).Plan is not null,
            "Zero active training rejects positive measured distance, while supported zero and absence remain distinct valid cases.");
    }

    private static void CheckOptionalAndFailures(ExportJournalSnapshot source, GhostRaceCheckpoint checkpoint,
        Func<long, double, double, int, ExportHeartRate> hr)
    {
        // Arrange
        ExportJournalSnapshot With(GhostRaceCheckpoint value)
        {
            return source with { Checkpoint = JsonSerializer.SerializeToElement(value) };
        }

        var zero = checkpoint with { MeasuredTrainingDistance = new(checkpoint.Profile.TrainingDistance, 0, 0, true, "Gap", []) };
        var missing = checkpoint with { MeasuredTrainingDistance = null, TerminalContext = null };
        var partial = checkpoint with { MeasuredTrainingDistance = new(checkpoint.Profile.TrainingDistance, 123, 5, true, "Gap", []) };

        // Act
        var zeroPlan = GhostHCWritePlanner.Create(With(zero), false).Plan!;
        var missingPlan = GhostHCWritePlanner.Create(With(missing), false).Plan!;
        var partialPlan = GhostHCWritePlanner.Create(With(partial), false).Plan!;

        // Assert
        Require(zeroPlan.Records.Single(record => record.Kind == HCRecordKind.Distance).AcceptedMetres == 0
            && missingPlan.Records.All(record => record.Kind != HCRecordKind.Distance)
            && missingPlan.Records.All(record => record.EndOffsetSeconds is null), "Explicit zero differs from unknown distance and legacy end offset.");
        Require(partialPlan.Records.Single(record => record.Kind == HCRecordKind.Distance).AcceptedMetres == 123
            && partialPlan.Records.Single(record => record.Kind == HCRecordKind.Distance).Provenance!.Contains("partial", StringComparison.Ordinal),
            "Only actual TotalMeasuredMetres is projected, with its real gap; neither main metres nor route chainage substitutes.");
        Require(GhostHCWritePlanner.Create(With(checkpoint with { HealthConnectConsentRevision = null }), false).State == "local_only"
            && GhostHCWritePlanner.Create(source, true).State == "disabled_synthetic", "Start consent and Debug production gates remain separate.");
        Reject(With(checkpoint with { TerminalContext = checkpoint.TerminalContext! with { ObservedBootMilliseconds = 120501 } }));
        Reject(With(checkpoint with { Pauses = [] }));
        Reject(source with { HeartRate = [hr(1, 20, 20.001, 100)] });
        Reject(source with { HeartRate = [hr(1, -0.001, 0, 100)] });
        Reject(source with { HeartRate = [hr(1, 10, 8.999, 100)] });
        Require(GhostHCWritePlanner.Create(source with { HeartRate = [hr(1, 10, 9, 100)] }, false).Plan is not null,
            "Both inside accepts the exact 1000ms clock guard, not larger disagreement.");
        Require(GhostHCWritePlanner.Create(source with { HeartRate = [hr(1, -1, -1, 100), hr(2, 21, 21, 100)] }, false).Plan!.Records.All(record => record.Kind != HCRecordKind.HeartRate),
            "Both outside are omitted without inventing samples.");
        var duplicate = source with { HeartRate = [hr(2, 1.0002, 1.0002, 100), hr(1, 1.0001, 1.0001, 100)] };
        Require(GhostHCWritePlanner.Create(duplicate, false).Plan!.Records.Single(record => record.Kind == HCRecordKind.HeartRate).HeartRateSamples.Single().SourceSequence == 1,
            "Identical platform-millisecond BPM keeps first sequence deterministically.");
        Reject(source with { HeartRate = [hr(1, 1.0001, 1.0001, 100), hr(2, 1.0002, 1.0002, 101)] });
        Reject(source with { Id = Guid.NewGuid() });
    }

    private static void Reject(ExportJournalSnapshot source)
    {
        Require(GhostHCWritePlanner.Create(source, false).State == "action_required", "Contradictory evidence must require action.");
    }

    private static TimeSpan S(double seconds)
    {
        return TimeSpan.FromSeconds(seconds);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
