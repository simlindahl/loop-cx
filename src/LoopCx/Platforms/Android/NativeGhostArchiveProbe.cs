#if DEBUG
using System.Text.Json;
using Android.Database.Sqlite;
using LoopCx.Features.GhostRun;

namespace LoopCx;

// Private synthetic adapter exercise only; never starts a workout or certifies a route.
internal static class NativeGhostArchiveProbe
{
    internal static void Run(Guid seed)
    {
        var directory = global::Android.App.Application.Context.FilesDir!.AbsolutePath;
        try
        {
            if (seed == Guid.Empty) throw new ArgumentException("Use a dedicated nonempty synthetic seed.");
            Guid Identity(byte suffix)
            {
                var bytes = seed.ToByteArray();
                bytes[^1] ^= suffix;

                return new Guid(bytes);
            }
            var routeKey = new RouteKey(seed, Identity(1));
            var sourceId = Identity(2);
            var localStart = new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.FromHours(-5));
            var raw = new[]
            {
                new RecordedRouteFix(TimeSpan.Zero, localStart.ToUniversalTime(), 59, 18, 1, RecordedRunSection.Main),
                new RecordedRouteFix(TimeSpan.FromSeconds(10), localStart.ToUniversalTime().AddSeconds(10), 59.0001, 18, 1, RecordedRunSection.Main)
            };
            var policy = new RouteCapturePolicy("synthetic-native-archive-v1", TimeSpan.FromSeconds(20), 5, 10);
            CapturedMainRoute Capture(RouteKey key, string name) => MainRouteCapture.Build(key, name, sourceId,
                RecordedWorkoutKind.Continuous, true, raw, policy).Captured ?? throw new InvalidOperationException("Synthetic capture failed.");
            var captured = Capture(routeKey, "Native synthetic Årsrundan");
            var source = new GhostAttempt(Identity(3), sourceId, routeKey, localStart, "America/New_York",
                captured.ObservedSpan, AttemptQuality.InsufficientEvidence, policy.RuleVersion, captured.Progress);
            const string reason = "Synthetic storage evidence is not route verification.";
            var archive = new AndroidGhostArchive(RunJournal.Shared);
            var initial = archive.SaveCapturedRoute(captured, source, reason);
            if (archive.SaveCapturedRoute(captured, source, reason) != GhostArchiveWrite.AlreadyStored)
                throw new InvalidOperationException("An identical retry created another archive row.");
            var rejectedName = false;
            try { archive.SaveCapturedRoute(Capture(routeKey, "Conflicting name"), source, reason); }
            catch (InvalidDataException) { rejectedName = true; }
            if (!rejectedName) throw new InvalidOperationException("A conflicting route replaced frozen evidence.");
            var failedVersion = new RouteKey(seed, Identity(4));
            var failedCapture = Capture(failedVersion, "Rollback-only version");
            var conflictingAttempt = new GhostAttempt(source.Id, sourceId, failedVersion, localStart, source.TimeZoneId,
                source.Elapsed, source.Quality, source.RuleVersion, source.Points);
            var rolledBack = false;
            try { archive.SaveCapturedRoute(failedCapture, conflictingAttempt, reason); }
            catch (InvalidDataException) { rolledBack = archive.ReadRoute(failedVersion) is null; }
            if (!rolledBack) throw new InvalidOperationException("Failed route/attempt finalization left a route version behind.");
            var conflictingSource = new GhostAttempt(Identity(5), sourceId, routeKey, localStart, source.TimeZoneId,
                source.Elapsed, source.Quality, source.RuleVersion, source.Points);
            var rejectedSource = false;
            try { archive.SaveAttempt(conflictingSource, reason, raw); }
            catch (InvalidDataException) { rejectedSource = true; }
            if (!rejectedSource) throw new InvalidOperationException("One source recording was counted twice.");
            var interrupted = new GhostAttempt(Identity(6), Identity(7), routeKey, localStart, source.TimeZoneId,
                source.Elapsed, AttemptQuality.Interrupted, source.RuleVersion, source.Points);
            archive.SaveAttempt(interrupted, "Synthetic interrupted evidence", raw);
            var reopened = new AndroidGhostArchive(RunJournal.Shared);
            var restored = reopened.ReadRoute(routeKey) ?? throw new InvalidOperationException("Missing frozen route.");
            var attempts = reopened.ReadAttempts(routeKey);
            var records = reopened.ReadRecords(routeKey, 2025);
            if (!reopened.ReadRoutes().Any(entry => entry.Route.Key == routeKey) || restored.Route.Name != captured.Route.Name || !restored.Route.Geometry.SequenceEqual(captured.Route.Geometry)
                || attempts.Count != 2 || attempts.Any(entry => entry.Attempt.SavedLocalYear != 2025 || entry.Attempt.TimeZoneId != source.TimeZoneId
                    || !entry.Raw.SequenceEqual(raw) || !entry.Attempt.Points.SequenceEqual(source.Points))
                || records.YearBest is not null || records.PersonalBest is not null)
                throw new InvalidOperationException("Frozen archive evidence/year/quality changed while reading.");
            using var foreignKeys = RunJournal.Shared.ReadableDatabase!.RawQuery("PRAGMA foreign_keys", null)!;
            if (!foreignKeys.MoveToFirst() || foreignKeys.GetInt(0) != 1) throw new InvalidOperationException("Foreign keys are not enabled.");
            // Negative fixtures use separate new files, never altered journal metadata.
            var unknownPath = Path.Combine(directory, $"ghost-unknown-{Guid.NewGuid()}.db");
            using var unknownDb = SQLiteDatabase.OpenOrCreateDatabase(unknownPath, null)!;
            unknownDb.SetForeignKeyConstraintsEnabled(true);
            var unknownArchive = new AndroidGhostArchive(unknownDb);
            _ = unknownArchive.ReadRoutes();
            unknownDb.ExecSQL("UPDATE ghost_schema SET version=4294967297 WHERE singleton=1");
            var rejectedWideVersion = false;
            try { _ = unknownArchive.ReadRoutes(); }
            catch (InvalidDataException problem) { rejectedWideVersion = problem.Message.Contains("revision", StringComparison.Ordinal); }
            using var savedVersion = unknownDb.RawQuery("SELECT version FROM ghost_schema", null)!;
            if (!rejectedWideVersion || !savedVersion.MoveToFirst() || savedVersion.GetLong(0) != 4294967297L)
                throw new InvalidOperationException("An unknown 64-bit archive revision was accepted or changed.");
            var partialPath = Path.Combine(directory, $"ghost-partial-{Guid.NewGuid()}.db");
            using var partialDb = SQLiteDatabase.OpenOrCreateDatabase(partialPath, null)!;
            partialDb.SetForeignKeyConstraintsEnabled(true);
            partialDb.ExecSQL("CREATE TABLE ghost_routes (own_synthetic_marker TEXT)");
            partialDb.ExecSQL("INSERT INTO ghost_routes VALUES ('preserve-negative-fixture')");
            var rejectedPartial = false;
            try { _ = new AndroidGhostArchive(partialDb).ReadRoutes(); }
            catch (InvalidDataException) { rejectedPartial = true; }
            using var partialMarker = partialDb.RawQuery("SELECT own_synthetic_marker FROM ghost_routes", null)!;
            using var partialTables = partialDb.RawQuery("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name GLOB 'ghost_*'", null)!;
            if (!rejectedPartial || !partialMarker.MoveToFirst() || partialMarker.GetString(0) != "preserve-negative-fixture"
                || !partialTables.MoveToFirst() || partialTables.GetInt(0) != 1)
                throw new InvalidOperationException("A partial archive fixture was repaired or changed instead of rejected.");
            var report = new
            {
                Success = true,
                Seed = seed,
                Initial = initial.ToString(),
                IdenticalRetry = true,
                ConflictRejected = rejectedName,
                SourceConflictRejected = rejectedSource,
                AtomicRouteAttemptRollback = rolledBack,
                Attempts = attempts.Count,
                PreservedLocalYear = 2025,
                NoAutomaticVerified = true,
                Unknown64BitRevisionRejected = rejectedWideVersion,
                PartialNamespaceUnchanged = rejectedPartial,
                UnknownScratchFile = Path.GetFileName(unknownPath),
                PartialScratchFile = Path.GetFileName(partialPath),
                SessionDatabaseVersion = RunJournal.Shared.ReadableDatabase.Version,
                Scope = "Synthetic native adapter only; no race UI/service/physical verification."
            };
            File.WriteAllText(Path.Combine(directory, $"ghost-archive-{seed}.json"), JsonSerializer.Serialize(report), new System.Text.UTF8Encoding(false));
            global::Android.Util.Log.Info("LoopGhostArchive", "Private synthetic archive check completed.");
        }
        catch (Exception problem)
        {
            File.WriteAllText(Path.Combine(directory, $"ghost-archive-{seed}.json"), JsonSerializer.Serialize(new { Success = false, Error = problem.ToString() }), new System.Text.UTF8Encoding(false));
            global::Android.Util.Log.Warn("LoopGhostArchive", "Private synthetic archive check failed.");
        }
    }
}
#endif
