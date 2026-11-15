using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;
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
            S(3), S(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot);
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
        Console.WriteLine("PASS: actual Ghost race composition, fresh start guards, paused GPS/finish, delayed clipping, exact retro/tail, frozen consent/codec and Core/HC isolation.");
    }
}
