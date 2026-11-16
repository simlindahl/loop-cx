#if DEBUG || !ANDROID
using System.Text.Json;
#if ANDROID
using Android.Database.Sqlite;
#endif
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;
using LoopCx.Features.History;

namespace LoopCx;

#if ANDROID
// Fixed synthetic fixtures only. No service, location provider or health manager calls.
internal static class NativeGhostIntegratedProbe
{
    internal static bool Allowed => (int)Android.OS.Build.VERSION.SdkInt == 35
        && Android.OS.Build.Hardware == "ranchu" && Android.OS.Build.Product == "sdk_gphone64_x86_64"
        && Android.OS.Build.Model == "sdk_gphone64_x86_64"
        && Android.OS.Build.SupportedAbis?.FirstOrDefault() == "x86_64"
        && Android.OS.Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) == true;
    private static readonly DateTimeOffset Utc = GhostSyntheticSourceFixture.Utc;
    private const string SourceLabel = GhostSyntheticSourceFixture.Label;

    internal static void Run(Guid seed, string mode)
    {
        if (!Allowed || seed == Guid.Empty || mode is not ("source" or "transactions" or "verify")) return;
        var report = Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, $"ghost-integrated-{seed}-{mode}.json");
        try
        {
            if (RunService.Snapshot is not null) throw new InvalidOperationException("Stop the owned recording before this diagnostic.");
            object result = mode == "source" ? SeedSource(RunJournal.Shared, seed) : Transactions(seed, mode == "verify");
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                Result = "PASS",
                Seed = seed,
                Mode = mode,
                Details = result,
                ProviderCalls = 0,
                PhysicalVerification = false,
                Scope = "Ordinary Shared open is outer scope; fixture/writer checks do not prove service UI or process reopen."
            }), new System.Text.UTF8Encoding(false));
        }
        catch (Exception error)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                Result = "FAIL",
                Seed = seed,
                Mode = mode,
                Error = error.GetType().Name,
                error.Message
            }), new System.Text.UTF8Encoding(false));
        }
    }

    private static object SeedSource(RunJournal journal, Guid id)
    {
        var batch = GhostSyntheticSourceFixture.Create(id);
        var checkpoint = batch.Latest.Checkpoint;
        var template = batch.Latest.TemplateMetadata;
        var db = journal.WritableDatabase!;
        using (var exists = db.RawQuery("SELECT checkpoint,template_metadata FROM runs WHERE id = ?", [id.ToString()])!)
            if (exists.MoveToFirst() && (exists.GetString(0) != checkpoint || exists.GetString(1) != template))
                throw new InvalidDataException("Seed identity already belongs to different evidence; nothing was replaced.");
        var buffer = new RunSaveBuffer();
        foreach (var point in batch.Locations) buffer.Add(point);
        buffer.Capture(batch.Latest, batch.Events[0].Command, null);
        Require(buffer.Save(journal.Commit), "Synthetic C2 commit");
        var capture = journal.PrepareGhostRoute(id, SourceLabel);
        Require(capture.Attempt.Quality == AttemptQuality.InsufficientEvidence, "Unknown tails cannot become a benchmark");
        Require(journal.ReadFinishedRuns().Finished.Any(row => row.Id == id), "Ordinary History sees the source");
        return new
        {
            SourceRunId = id,
            Label = SourceLabel,
            SyntheticStoredDurationMinutes = 20,
            RawPoints = GhostSourceEvidenceCodec.Read(capture.RawJson).Locations.Count,
            NormalHistorySource = true,
            AttemptQuality = capture.Attempt.Quality.ToString(),
            NaturalTimerEvidence = false,
            RouteSaved = false
        };
    }

    private static object Transactions(Guid seed, bool verifyOnly)
    {
        var name = "run-save-corrupt-probe-" + seed.ToString("N") + ".db";
        Guid Identity(byte suffix) { var bytes = seed.ToByteArray(); bytes[^1] ^= suffix; return new(bytes); }
        var sourceId = Identity(1);
        var raceId = Identity(2);
        using var journal = RunJournal.OpenCorruptionProbe(name);
        try
        {
            var db = journal.WritableDatabase!;
            if (verifyOnly)
            {
                var stored = journal.ReadGhostRun(raceId) ?? throw new InvalidDataException("Missing prior terminal race.");
                Require(stored.Status == "incomplete" && stored.Checkpoint.Snapshot.Clock.RaceElapsed == TimeSpan.FromSeconds(2), "Terminal survives reopen");
                Require(Count(db, "runs", "id", raceId) == 1 && Count(db, "ghost_race_starts", "race_id", raceId) == 1
                    && Count(db, "ghost_attempts", "source_run_id", raceId) == 1 && Count(db, "run_events", "run_id", raceId) == 2 && Count(db, "locations", "run_id", raceId) == 1,
                    "Reopened exactly-once joint evidence");
                return new
                {
                    Database = name,
                    SourceRunId = sourceId,
                    RaceId = raceId,
                    ReopenedRead = true,
                    ProcessReopenProven = false,
                    Note = "QA must bind a distinct process lifetime; this invocation alone cannot establish it."
                };
            }
            using (var prior = db.RawQuery("SELECT id FROM runs WHERE id = ?", [raceId.ToString()])!)
                Require(!prior.MoveToFirst(), "Use a fresh transaction seed; verify mode reads existing evidence");
            _ = SeedSource(journal, sourceId);
            var captured = journal.PrepareGhostRoute(sourceId, SourceLabel);
            var archive = new AndroidGhostArchive(journal);
            _ = archive.SaveStoredSource(captured);
            Require(archive.SaveStoredSource(captured) == GhostArchiveWrite.AlreadyStored, "Identical source-v2 archive retry");
            var route = captured.Geometry.Route;
            var first = route.Geometry[0];
            var race = GhostSyntheticSourceFixture.CreateRace(route, raceId);
            var buffer = new RunSaveBuffer();
            void Capture(GhostRaceCheckpoint state, string command) => buffer.Capture(new(raceId, state.SaveRevision, state.StartedUtc,
                Utc.AddMilliseconds(state.ObservedBootMilliseconds - 100000), state.ObservedBootMilliseconds - 100000,
                state.Snapshot.Clock.State != GhostClockState.Running, "[]", "{\"ActivityKind\":\"ghost\"}", null, JsonSerializer.Serialize(state)), command, null);
            void ExerciseStage(bool terminal)
            {
                RunSaveBatch? frozen = null;
                foreach (var fault in new[] { "archive-abort", "lost-return" })
                {
                    journal.ArmGhostTransactionProbe(raceId, fault);
                    Require(!buffer.Save(batch =>
                    {
                        if (frozen is not null) Require(ReferenceEquals(batch, frozen), "Same frozen batch across fault stages");
                        frozen = batch;
                        journal.Commit(batch);
                    }, explicitRetry: true) && buffer.Status.CanRetry, "Injected failure remains pending/Retry");
                    var committed = fault == "lost-return";
                    Require(Count(db, "runs", "id", raceId) == (committed || terminal ? 1 : 0)
                        && Count(db, "ghost_race_starts", "race_id", raceId) == (committed || terminal ? 1 : 0)
                        && Count(db, "ghost_attempts", "source_run_id", raceId) == (committed && terminal ? 1 : 0)
                        && Count(db, "locations", "run_id", raceId) == (committed && terminal ? 1 : 0)
                        && Count(db, "run_events", "run_id", raceId) == (terminal ? (committed ? 2 : 1) : (committed ? 1 : 0)),
                        "Actual joint rollback/commit state");
                    if (!committed && terminal)
                    {
                        using var previous = db.RawQuery("SELECT checkpoint FROM runs WHERE id = ?", [raceId.ToString()])!;
                        Require(previous.MoveToFirst() && GhostRaceCheckpoint.Read(previous.GetString(0)!).SaveRevision == 1,
                            "Terminal rollback retains the original start checkpoint");
                    }
                }
                Require(buffer.Save(batch => { Require(ReferenceEquals(batch, frozen), "Exact frozen batch on Retry"); journal.Commit(batch); }, true),
                    "AlreadyCommitted identical retry succeeds after actual lost return");
            }
            Capture(race.Checkpoint(1, 100000), "synthetic-ghost-start");
            ExerciseStage(false);
            var raw = new RunSaveLocation(Utc.AddSeconds(1).ToUnixTimeMilliseconds(), 101000000000, first.Latitude, first.Longitude, 1);
            race.Location(raw, 101000);
            buffer.Add(raw);
            race.End(102000);
            Capture(race.Checkpoint(2, 102000), "end-attempt");
            ExerciseStage(true);
            Require(journal.ReadGhostRun(raceId)?.Status == "incomplete", "Ordinary detached Ghost reader sees committed result");
            Require(Count(db, "export_queue", "run_id", raceId) == 0, "No Core outbox entry");
            return new
            {
                Database = name,
                SourceRunId = sourceId,
                RaceId = raceId,
                SourceV2Retry = true,
                StartRollback = true,
                StartLostReturn = true,
                TerminalRollback = true,
                TerminalLostReturn = true,
                RealServiceEvidence = false,
                ProcessReopenProven = false
            };
        }
        finally { journal.Close(); }
    }

    private static long Count(SQLiteDatabase db, string table, string column, Guid id)
    {
        using var rows = db.RawQuery($"SELECT COUNT(*) FROM {table} WHERE {column} = ?", [id.ToString()])!;
        return rows.MoveToFirst() ? rows.GetLong(0) : throw new IOException("Missing count");
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidDataException(message);
    }
}
#endif

internal static class GhostSyntheticSourceFixture
{
    internal static readonly DateTimeOffset Utc = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    internal const string Label = "Synthetic Ghost C2 probe";

    internal static GhostRace CreateRace(GhostRoute route, Guid raceId)
    {
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1",
            new(route.RuleVersion, 5, 10, 2, TimeSpan.FromSeconds(3), 4, 5, 500), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3),
            HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot, LivePacePolicy.Pilot);
        var first = route.Geometry[0];
        return new(raceId, route, GhostMode.YearBest, null, profile,
            new(100000, 100000, Utc.ToUnixTimeMilliseconds(), first.Latitude, first.Longitude, 1),
            100000, Utc, "UTC", TimeSpan.Zero, null);
    }
    internal static RunSaveBatch Create(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Use a nonempty owned synthetic identity.");
        TimedPhase[] plan = [new(RunPhase.Work, TimeSpan.FromMinutes(20), 4)];
        var template = JsonSerializer.Serialize(CoreTemplate.C2 with { Source = Label });
        var checkpoint = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            SaveRevision = 1,
            Plan = plan,
            Completed = plan.Select(phase => new CompletedPhase(phase, phase.Duration)).ToArray(),
            PhaseBoundaries = new[] { new PhaseBoundaryFact(0, null, RunPhase.Work, null, 1200000, 1200000,
                Utc.AddMinutes(20), Utc.AddMinutes(20), true) },
            PhaseActiveTime = TimeSpan.Zero,
            TotalActiveTime = TimeSpan.FromMinutes(20),
            MainActiveTime = TimeSpan.FromMinutes(20),
            StartContext = new RunStartContext("UTC", 0, 100000),
            Snapshot = new { Phase = "Finished", Motion = new { StartedMonotonicTime = TimeSpan.FromSeconds(100) } }
        });
        var locations = new List<RunSaveLocation>();
        long previous = 0, identity = 0;
        // Deliberately unknown two-second tails: useful geometry, never a fabricated complete benchmark.
        for (var second = 2; second < 1200; second += 4)
        {
            identity++;
            locations.Add(new(Utc.AddSeconds(second).ToUnixTimeMilliseconds(), (100L + second) * 1000000000,
                59 + second * .000001, 18, 1)
            { Provenance = new(1, id, identity, "valid", previous == 0 ? null : previous, previous == 0 ? "Synthetic start anchor" : null) });
            previous = identity;
        }
        var state = new RunSaveCheckpoint(id, 1, Utc, Utc.AddMinutes(20), 1200000, true,
            JsonSerializer.Serialize(plan), template, null, checkpoint);
        return new(state, locations.ToArray(), [], [new("synthetic-ghost-c2-source", state.ObservedUtc, state.ElapsedMilliseconds, checkpoint)]);
    }
}
#endif
