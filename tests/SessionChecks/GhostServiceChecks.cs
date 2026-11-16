using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;
using LoopCx.Features.GhostRun.Ui;
using LoopCx.Features.HealthConnectExport;
using LoopCx.Features.History;

internal static class GhostServiceChecks
{
    internal static void Run()
    {
        static TimeSpan S(double n) => TimeSpan.FromSeconds(n);
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Reject(Action action)
        {
            try { action(); } catch (Exception error) when (error is InvalidDataException or ArgumentException) { return; }
            throw new InvalidOperationException("Unsupported Ghost start or Core interpretation must be rejected.");
        }
        var sourceId = Guid.Parse("56565656-5656-5656-5656-565656565656");
        var key = new RouteKey(Guid.Parse("67676767-6767-6767-6767-676767676767"), Guid.Parse("78787878-7878-7878-7878-787878787878"));
        const double length = 22.23901604670658;
        var route = new GhostRoute(key, "Synthetic route", sourceId, "ghost-test-v1",
            [new(0, 0, 0), new(0, .0001, length / 2), new(0, .0002, length)]);
        var utc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1, S(3), 4, 5, 500),
            S(3), S(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot, LivePacePolicy.Pilot);
        var start = new GhostStartPosition(99900, 100000, utc.ToUnixTimeMilliseconds(), 0, 0, .1);
        var consent = Guid.Parse("89898989-8989-8989-8989-898989898989");
        GhostRace New(GhostStartPosition? position = null) => new(Guid.NewGuid(), route, GhostMode.YearBest, null,
            profile, position ?? start, 100000, utc, "Europe/Stockholm", S(3600), consent);
        Reject(() => New(start with { MeasuredBootMilliseconds = 99000 }));
        Reject(() => New(start with { RequestedBootMilliseconds = 95000, MeasuredBootMilliseconds = 96000 }));
        Reject(() => New(start with { Longitude = .001 }));
        var race = New();
        for (var second = 1; second <= 20; second++)
        {
            if (second == 5) race.Pause(105000);
            var receipt = 100000 + second * 1000L + (second == 20 ? 500 : 0);
            race.Observe(receipt); // Timer observation can precede a delayed measured finish.
            race.Location(new(utc.AddSeconds(second).ToUnixTimeMilliseconds(), (100000 + second * 1000L) * 1000000,
                0, second * .00001, .1), receipt);
        }
        var finish = race.Snapshot;
        Require(race.Ended && finish.Clock.State == GhostClockState.Finished && finish.Clock.RaceElapsed == S(20)
            && finish.Clock.TrainingActive == S(5) && finish.Clock.TrainingPaused,
            "GPS and verified finish keep working during manual training pause; late tick does not inflate finish.");
        var checkpoint = race.Checkpoint(1, 120500);
        var json = JsonSerializer.Serialize(checkpoint);
        var reread = GhostRaceCheckpoint.Read(json);
        Require(reread.HealthConnectConsentRevision == consent && reread.Attempt().Elapsed == S(20)
            && reread.Attempt().Quality == AttemptQuality.Verified && reread.Raw.Count == 21,
            "Frozen Ghost kind/clock/consent and original race GPS survive checkpoint codec; no Core plan is invented.");
        Require(GhostReferences.Select(route, [reread.Attempt()], 2025).PersonalBest is not null,
            "A complete ordered matched route can become algorithmically Verified, including a paused finish.");
        race.End(130000); race.Resume(130001);
        Require(race.Snapshot.Clock == finish.Clock, "Late End/Resume cannot change the frozen finish.");
        var auto = new GhostRaceClock(S(100));
        auto.PauseTrainingAutomatically(S(110), S(120), S(123));
        Require(auto.Current.RaceElapsed == S(23) && auto.Current.TrainingActive == S(13),
            "Exact ten-second retrospective training exclusion preserves the three-second callback tail and all race elapsed.");
        auto.ResumeTraining(S(125));
        Require(auto.Current.RaceElapsed == S(25) && auto.Current.TrainingActive == S(13), "Only the actual receipt pause adds excluded training time.");
        auto.Observe(S(140));
        auto.CompleteAt(S(121));
        Require(auto.Current.RaceElapsed == S(21) && auto.Current.TrainingActive == S(11),
            "A delayed finish clips pause union at its actual crossing, not a later timer/callback.");
        var automatic = New();
        for (var second = 1; second <= 11; second++)
        {
            var receipt = 100000 + second * 1000L + 200;
            automatic.Location(new(utc.AddSeconds(second).ToUnixTimeMilliseconds(), (100000 + second * 1000L) * 1000000,
                0, 0, .1), receipt);
        }
        Require(automatic.Snapshot.PauseReason == PauseReason.Automatic
            && automatic.Snapshot.Clock.RaceElapsed == S(11.2) && automatic.Snapshot.Clock.TrainingActive == S(1.2),
            "Actual reused movement policy confirms ten seconds without consuming its delayed active receipt tail.");
        automatic.Pause(111300);
        automatic.Location(new(utc.AddSeconds(12).ToUnixTimeMilliseconds(), 112000000000, 0, .000035, .1), 112000);
        Require(automatic.Snapshot.PauseReason == PauseReason.Manual && automatic.Snapshot.Clock.TrainingPaused
            && automatic.Snapshot.Match.Metres > 0,
            "Manual lock takes precedence over Auto while actual GPS and race progress continue.");
        var benchmark = new GhostAttempt(Guid.NewGuid(), sourceId, key, utc.ToOffset(S(3600)), "Europe/Stockholm",
            S(20), AttemptQuality.Verified, route.RuleVersion, Enumerable.Range(0, 21).Select(second => new GhostPoint(S(second), length * second / 20)).ToArray());
        GhostRace Compared() => new(Guid.NewGuid(), route, GhostMode.PersonalBest, benchmark, profile,
            start, 100000, utc, "Europe/Stockholm", S(3600), consent);
        RunSaveLocation Position(double second, double? longitude = null) => new(utc.AddSeconds(second).ToUnixTimeMilliseconds(),
            (100000 + (long)(second * 1000)) * 1000000, 0, longitude ?? second * .00001, .1);
        var providerBreak = Compared();
        for (var second = 1; second <= 19; second++) providerBreak.Location(Position(second), 100000 + second * 1000);
        Require(providerBreak.Snapshot.Gap.SecondsAhead is not null, "The pre-break fixture has an actually supported comparison.");
        providerBreak.Unavailable();
        providerBreak.Location(Position(18.5), 119100); // An out-of-order delivery must not consume the interruption.
        providerBreak.Location(Position(20), 120000); // Fresh, within max gap, but across the explicit interruption.
        Require(!providerBreak.Ended && providerBreak.Snapshot.Match.Metres is null
            && providerBreak.Checkpoint(1, 120000).Raw[^1].GapBefore,
            "Provider loss is retained in raw evidence and prevents bridging a short interval straight to a verified finish.");
        providerBreak.Location(Position(21, .00019), 121000); // Re-enter the actually covered prefix.
        Require(providerBreak.Snapshot.Match.State == RouteMatchState.Tracking, "Existing gap re-entry can restore support without blanket disqualification.");
        providerBreak.Location(Position(22, .0002), 122000);
        Require(providerBreak.Ended && providerBreak.Snapshot.Clock.State == GhostClockState.Finished,
            "Only supported re-entry followed by the ordered finish can complete the interrupted route.");
        var repeatedBreak = Compared();
        for (var second = 1; second <= 19; second++) repeatedBreak.Location(Position(second), 100000 + second * 1000);
        repeatedBreak.Unavailable();
        repeatedBreak.Location(Position(20, .00019), 120000);
        Require(repeatedBreak.Snapshot.Match.State == RouteMatchState.Tracking, "The duplicate fixture first has a genuine gap re-entry.");
        repeatedBreak.Unavailable();
        repeatedBreak.Location(Position(20, .00019), 120100); // Exact cached Tracking input, now after a second interruption.
        Require(repeatedBreak.Snapshot.Match.Metres is null, "A duplicate re-entry cannot supply support after a new provider break.");
        repeatedBreak.Location(Position(21, .0002), 121000);
        Require(!repeatedBreak.Ended && repeatedBreak.Checkpoint(1, 121000).Raw[^1].GapBefore
            && repeatedBreak.Checkpoint(1, 121000).Raw.Count == 23,
            "The raw duplicate is retained, but cannot consume the second interruption and bridge straight to finish.");
        var chronological = Compared();
        chronological.Location(Position(1), 101000);
        chronological.Location(Position(2), 102000);
        chronological.Location(Position(.5), 102100); // Older but not yet stale at receipt.
        chronological.Location(Position(.5), 104000); // Old age3.5s; newest actual support is still only2s old.
        chronological.Observe(104500);
        Require(chronological.Snapshot.Match.State == RouteMatchState.Tracking && chronological.Snapshot.Match.Metres is not null
            && chronological.Checkpoint(1, 104500).Raw.Count == 5 && !chronological.Checkpoint(1, 104500).Raw[^1].GapBefore,
            "Even a stale older packet is retained without erasing newer still-fresh support or regressing its measurement clock.");
        chronological.Location(Position(.5), 104600, sourceAvailable: false);
        Require(chronological.Snapshot.Match.Metres is null && chronological.Checkpoint(2, 104600).Raw[^1].GapBefore,
            "An explicit provider outage still breaks support even when delivered with an older packet.");
        var staleEnd = Compared(); staleEnd.Location(Position(1), 101000);
        Require(staleEnd.Snapshot.Gap.SecondsAhead is not null, "The stale-End fixture starts with a real comparison.");
        staleEnd.End(105000); // No intermediate stale UI/timer observation.
        Require(staleEnd.Snapshot.Match.Metres is null && staleEnd.Snapshot.Gap == new GhostGap(null, null),
            "Explicit End cannot resurrect a comparison whose last support expired without an intermediate tick.");
        var offEnd = Compared(); offEnd.Location(Position(1), 101000); offEnd.Unavailable(); offEnd.End(102000);
        Require(offEnd.Snapshot.Match.Metres is null && offEnd.Snapshot.Gap == new GhostGap(null, null),
            "Explicit End retains unavailable position and comparison.");
        race.Unavailable();
        Require(race.Snapshot.Match.State == RouteMatchState.Finished && race.Snapshot.Match.Metres == length,
            "An actually verified finish retains its already frozen finish evidence.");
        using (var cancelled = new CancellationTokenSource())
        using (var admission = new GhostStartAdmission(cancelled.Token))
        {
            Require(admission.Pending, "The service preflight may initially observe a pending request.");
            Task.Run(cancelled.Cancel).GetAwaiter().GetResult(); // No main-loop pumping is available here.
            Require(!admission.TryAccept() && admission.Completion.IsCanceled,
                "Cancellation on another thread wins immediately, including the preflight-to-acceptance gap, with no queued UI invalidation.");
        }
        using (var late = new CancellationTokenSource())
        using (var admission = new GhostStartAdmission(late.Token))
        {
            Require(admission.TryAccept(), "The service can accept an uncancelled prepared request once.");
            Task.Run(late.Cancel).GetAwaiter().GetResult();
            admission.Complete(null);
            Require(!admission.TryAccept() && !admission.Completion.IsCanceled
                && admission.Completion.GetAwaiter().GetResult() is null,
                "Cancellation after acceptance cannot discard the established owner or hide its successful response.");
        }
        var ended = New(); ended.End(102000);
        Require(ended.Checkpoint(2, 102000).Attempt().Quality == AttemptQuality.Incomplete, "Explicit End never becomes a record.");
        var zero = New(); zero.End(100000);
        var zeroCheckpoint = GhostRaceCheckpoint.Read(JsonSerializer.Serialize(zero.Checkpoint(1, 100000)));
        Require(zeroCheckpoint.ArchiveDisposition == "zero-elapsed-incomplete" && zeroCheckpoint.Snapshot.Clock.RaceElapsed == TimeSpan.Zero,
            "Exactly-zero explicit End is preserved as incomplete journal evidence, without fabricated time or archive attempt.");
        Reject(() => zeroCheckpoint.Attempt());
        Reject(() => GhostRaceCheckpoint.Read(JsonSerializer.Serialize(zeroCheckpoint with { TerminalKind = null })));
        Reject(() => GhostRaceCheckpoint.Read(JsonSerializer.Serialize(zeroCheckpoint with { ArchiveDisposition = "attempt" })));
        Require(GhostStoredRun.Read(zeroCheckpoint.Id, "incomplete", JsonSerializer.Serialize(zeroCheckpoint)).Checkpoint.ArchiveDisposition == "zero-elapsed-incomplete",
            "Cold History can read the explicit zero result without an archive attempt/Core fallback.");
        Reject(() => GhostStoredRun.Read(zeroCheckpoint.Id, "finished", JsonSerializer.Serialize(zeroCheckpoint)));
        var interrupted = New(); interrupted.Observe(103000);
        var interruptedJson = JsonSerializer.Serialize(interrupted.Checkpoint(1, 103000));
        Require(GhostStoredRun.Read(interrupted.Snapshot.Id, "interrupted", interruptedJson).Checkpoint.Snapshot.Clock.RaceElapsed == S(3),
            "Process-death History retains only the stored prefix, without fabricating an end or restoring a running owner.");
        var frozenBuffer = new RunSaveBuffer();
        var pending = new RunSaveCheckpoint(checkpoint.Id, 1, checkpoint.StartedUtc, checkpoint.StartedUtc.AddSeconds(20.5),
            20500, true, "[]", "{\"ActivityKind\":\"ghost\"}", null, json);
        frozenBuffer.Capture(pending, "finish", null);
        RunSaveBatch? firstBatch = null;
        Require(!frozenBuffer.Save(batch => { firstBatch = batch; throw new IOException("Synthetic uncertain commit return"); })
            && frozenBuffer.Status.CanRetry && frozenBuffer.Status.State == "save_failed",
            "An uncertain local return keeps the exact Ghost terminal batch retryable, not Saved.");
        Require(frozenBuffer.Save(batch => Require(ReferenceEquals(firstBatch, batch)
            && RunSaveRevision.AlreadyCommitted(json, batch.Latest)
            && GhostRaceCheckpoint.Read(batch.Latest.Checkpoint).HealthConnectConsentRevision == consent,
            "The same captured Ghost revision/consent/end survives retry after a lost return."), explicitRetry: true)
            && frozenBuffer.Status.State == "saved", "Only a successful retry return acknowledges the same terminal save.");
        using var document = JsonDocument.Parse(json);
        Reject(() => ExportRunV1.RequireCore(document.RootElement));
        Reject(() => StoredRouteProjection.Read(checkpoint.Id, document.RootElement, 20500, []));
        var export = new ExportJournalSnapshot(checkpoint.Id, utc.ToString("O"), utc.AddSeconds(20.5).ToString("O"),
            20500, "finished", JsonSerializer.SerializeToElement(Array.Empty<object>()), null, null, document.RootElement.Clone(), null, [], [], []);
        Require(HCWritePlanner.Create(export, false).Plan is null, "Ghost never falls through to a Core HC write plan.");
        var paceRun = Compared();
        for (var second = 1; second <= 6; second++) paceRun.Location(Position(second), 100000 + second * 1000);
        Require(paceRun.LivePace?.SecondsPerKilometre is > 0, "Ghost measured pace comes from the actual accepted training-distance edges.");
        paceRun.Pause(106500); paceRun.Location(Position(7), 107000);
        Require(paceRun.LivePace?.SecondsPerKilometre is null && paceRun.LivePace?.Quality == "paused",
            "Paused race GPS cannot feed a stale training-distance observation back into measured pace.");
        paceRun.Resume(108000);
        Require(paceRun.LivePace?.SecondsPerKilometre is null && paceRun.LivePace?.Quality == "acquiring", "Resume starts fresh measured-pace acquisition.");
        var legacyProfile = profile with { LivePace = null };
        var legacyProfileJson = JsonSerializer.Serialize(legacyProfile);
        Require(!legacyProfileJson.Contains("LivePace", StringComparison.Ordinal)
            && JsonSerializer.Serialize(JsonSerializer.Deserialize<GhostComparisonProfile>(legacyProfileJson)) == legacyProfileJson,
            "Missing legacy pace policy stays absent with exact old frozen-profile retry bytes.");
        var withoutPolicy = new GhostRace(Guid.NewGuid(), route, GhostMode.YearBest, null, legacyProfile,
            start, 100000, utc, "Europe/Stockholm", S(3600), consent);
        Require(withoutPolicy.LivePace is null, "No current pace policy is silently invented for missing frozen metadata.");
        var delayedDistance = Compared();
        for (var second = 1; second <= 19; second++) delayedDistance.Location(Position(second), 100000 + second * 1000);
        var prefixDistance = delayedDistance.Checkpoint(1, 119000).MeasuredTrainingDistance!.TotalMeasuredMetres;
        delayedDistance.Location(Position(20, .000205), 120500);
        var distanceCheckpoint = delayedDistance.Checkpoint(2, 120500);
        Require(delayedDistance.Ended && distanceCheckpoint.MeasuredTrainingDistance!.TotalMeasuredMetres == prefixDistance
            && distanceCheckpoint.MeasuredTrainingDistance.MainHasGaps && prefixDistance < route.LengthMetres,
            "An interpolated finish before the crossing fix retains the actual training-distance prefix with unknown tail, never route length/proportional distance.");
        var syntheticZone = TimeZoneInfo.CreateCustomTimeZone("Synthetic terminal zone", S(7200), "Synthetic", "Synthetic");
        var terminal = GhostTerminalContext.Capture(distanceCheckpoint, utc.AddSeconds(20.5), syntheticZone);
        var frozenTerminal = distanceCheckpoint with { TerminalContext = terminal };
        var terminalJson = JsonSerializer.Serialize(frozenTerminal);
        Require(GhostRaceCheckpoint.Read(terminalJson).TerminalContext == terminal
            && terminal.StartDerivedEffectiveUtc == distanceCheckpoint.StartedUtc + distanceCheckpoint.Snapshot.Clock.RaceElapsed
            && terminal.ObservedUtc != terminal.StartDerivedEffectiveUtc && terminal.EffectiveUtcOffsetSeconds == 7200,
            "Terminal observation and derived effective finish/offset remain distinct frozen facts on codec/retry.");
        var legacyCheckpoint = JsonNode.Parse(terminalJson)!;
        legacyCheckpoint.AsObject().Remove("MeasuredTrainingDistance"); legacyCheckpoint.AsObject().Remove("TerminalContext");
        legacyCheckpoint["Profile"]!.AsObject().Remove("LivePace");
        var legacyRead = GhostRaceCheckpoint.Read(legacyCheckpoint.ToJsonString());
        Require(legacyRead.MeasuredTrainingDistance is null && legacyRead.TerminalContext is null && legacyRead.Profile.LivePace is null,
            "Old Ghost checkpoint1 keeps missing actual metrics/zone/policy unknown.");
        Require(auto.TrainingActiveAt(S(115)) == S(10), "Historical sample-time training uses the same retrospective pause union, not receipt active time.");
        var drawing = new GhostRouteDrawing(route);
        Require(drawing.PointAt(0) == drawing.Start && drawing.PointAt(length) == drawing.Finish
            && drawing.PointAt(null) is null && drawing.PointAt(length + 1) is null
            && drawing.PointAt(length / 2)!.X > drawing.Start.X,
            "Local geometry uses actual ordered chainage, preserves start/direction and hides unknown/out-of-route markers.");
        Console.WriteLine("PASS: actual Ghost race composition, fresh start guards, paused GPS/finish, delayed clipping, exact retro/tail, frozen consent/codec and Core/HC isolation.");
    }
}
