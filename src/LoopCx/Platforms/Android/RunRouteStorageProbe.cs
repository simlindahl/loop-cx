#if DEBUG
using System.Globalization;
using System.Text.Json;
using Android.Content;
using Android.Database.Sqlite;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;

namespace LoopCx;

// Own scratch database only; no service, provider or normal journal access.
internal static class RunRouteStorageProbe
{
    internal static void Run(Context context)
    {
        if ((int)Android.OS.Build.VERSION.SdkInt != 35 || Android.OS.Build.Hardware != "ranchu"
            || Android.OS.Build.Product != "sdk_gphone64_x86_64" || Android.OS.Build.Model != "sdk_gphone64_x86_64"
            || Android.OS.Build.SupportedAbis?.FirstOrDefault() != "x86_64"
            || Android.OS.Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) != true)
            return;

        var name = "run-save-corrupt-probe-" + Guid.NewGuid().ToString("N") + ".db";
        var id = Guid.NewGuid();
        var finishedId = Guid.NewGuid();
        var partialId = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        string? readSnapshot = null;
        var report = Path.Combine(context.FilesDir!.AbsolutePath, "route-storage-probe.json");
        try
        {
            using (var seed = RunJournal.OpenCorruptionProbe(name))
            {
                try
                {
                    var db = seed.WritableDatabase!;
                    db.ExecSQL("ALTER TABLE locations DROP COLUMN provenance");
                    db.ExecSQL("INSERT INTO locations VALUES ('legacy-owned', 1000, 1000000000, 1, 2, NULL)");
                    db.ExecSQL("INSERT INTO export_queue VALUES ('legacy-owned', 'action_required', 'frozen attempt', 'unchanged')");
                    db.Version = 6;
                }
                finally { seed.Close(); }
            }
            using (var journal = RunJournal.OpenCorruptionProbe(name))
            {
                try
                {
                    var db = journal.WritableDatabase!;
                    Require(db.Version == 7, "Migration version");
                    using (var old = db.RawQuery("SELECT run_id, utc_ms, monotonic_ns, latitude, longitude, accuracy, provenance FROM locations", null)!)
                        Require(old.MoveToFirst() && old.GetString(0) == "legacy-owned" && old.GetLong(1) == 1000
                            && old.GetLong(2) == 1000000000 && old.GetDouble(3) == 1 && old.GetDouble(4) == 2
                            && old.IsNull(5) && old.IsNull(6) && !old.MoveToNext(), "Legacy cells/null preserved");
                    var utc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    var capture = new RunRouteCapture(id);
                    var raw = capture.Capture(new(1000, 1000000000, 1, 2, 5), new(true, null, TimeSpan.FromSeconds(1)), "Acquiring");
                    var buffer = new RunSaveBuffer();
                    buffer.Add(raw);
                    buffer.Capture(new(id, 1, utc, utc.AddSeconds(1), 1000, true, "[]", "{}", null, "{\"SaveRevision\":1}"), "finish", null);
                    db.ExecSQL("CREATE TEMP TRIGGER route_abort BEFORE INSERT ON locations BEGIN SELECT RAISE(ABORT, 'owned route failure'); END");
                    Require(!buffer.Save(journal.Commit) && buffer.Status.CanRetry, "Native transaction abort is retryable");
                    Require(Count(db, "runs", id) == 0 && Count(db, "run_events", id) == 0 && Count(db, "export_queue", id) == 0, "Atomic rollback");
                    db.ExecSQL("DROP TRIGGER route_abort");
                    RunSaveBatch? committed = null;
                    Require(!buffer.Save(batch => { journal.Commit(batch); committed = batch; throw new IOException("Lost local commit return"); }, true), "Uncertain return is not Saved");
                    Require(buffer.Save(batch => { Require(ReferenceEquals(batch, committed) && ReferenceEquals(batch.Locations[0], raw), "Frozen retry identity"); journal.Commit(batch); }, true), "AlreadyCommitted retry");
                    Require(Count(db, "runs", id) == 1 && Count(db, "locations", id) == 1 && Count(db, "run_events", id) == 1 && Count(db, "export_queue", id) == 1, "Exactly once retry");
                    using var point = db.RawQuery("SELECT provenance FROM locations WHERE run_id = ?", [id.ToString()])!;
                    Require(point.MoveToFirst() && RunPointProvenance.Parse(point.GetString(0)!) == raw.Provenance, "Exact frozen point facts");
                    SeedReadFixture(journal, finishedId, false, false);
                    SeedReadFixture(journal, partialId, true, false);
                    SeedReadFixture(journal, legacyId, false, true);
                    VerifyReads(journal, finishedId, partialId, legacyId);
                    // Deliberately corrupt ONLY owned route metadata. The ordinary detail/v1 path must not parse it.
                    db.ExecSQL($"UPDATE locations SET provenance = 'malformed owned probe JSON' WHERE run_id = '{finishedId}'");
                    var beforeBadRead = Cells(db);
                    try { _ = journal.ReadRoute(finishedId); throw new InvalidOperationException("Malformed route was accepted"); }
                    catch (JsonException) { }
                    _ = journal.ReadFinishedRun(finishedId);
                    Require(journal.ReadFinishedRuns().Finished.Any(row => row.Id == finishedId), "Summary survives route corruption");
                    _ = journal.ExportJson(finishedId);
                    Require(Cells(db) == beforeBadRead, "Malformed route/detail/v1 reads preserve every scratch cell");
                    readSnapshot = Cells(db);
                }
                finally { journal.Close(); }
            }
            using (var reopened = RunJournal.OpenCorruptionProbe(name))
            {
                try
                {
                    var db = reopened.ReadableDatabase!;
                    Require(Count(db, "locations", id) == 1 && Count(db, "run_events", id) == 1, "Cold reopen");
                    using var old = db.RawQuery("SELECT status, attempt_json, last_error FROM export_queue WHERE run_id = 'legacy-owned'", null)!;
                    Require(old.MoveToFirst() && old.GetString(0) == "action_required" && old.GetString(1) == "frozen attempt" && old.GetString(2) == "unchanged", "Old outbox unchanged");
                    Require(Cells(db) == readSnapshot, "Cold reopen preserves all raw/checkpoint/outbox cells");
                    var beforeReopenRead = Cells(db);
                    Require(reopened.ReadRoute(partialId).Main.Supported && reopened.ReadRoute(legacyId).Whole.UnknownQuality,
                        "Real route query survives cold reopen for confirmed partial and legacy");
                    _ = reopened.ReadFinishedRun(finishedId);
                    _ = reopened.ExportJson(finishedId);
                    Require(Cells(db) == beforeReopenRead, "Cold reopened reads are immutable");
                }
                finally { reopened.Close(); }
            }
            File.WriteAllText(report, JsonSerializer.Serialize(new { Result = "PASS", Database = name, RunId = id, FinishedRouteId = finishedId, ConfirmedPartialRouteId = partialId, LegacyRouteId = legacyId, Fixture = "Synthetic route read probe; explicit frozen historical plan, no current Settings reads or mutations", DirectReadRoute = true, ColdReopen = true, ProviderCalls = 0 }));
        }
        catch (Exception error)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { Result = "FAIL", Database = name, Error = error.GetType().Name, error.Message }));
        }
        // Retain the uniquely owned scratch tuple for independent inspection and scoped cleanup.
    }

    private static void SeedReadFixture(RunJournal journal, Guid id, bool partial, bool legacy)
    {
        var utc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimedPhase[] plan = [new(RunPhase.Warmup, TimeSpan.FromSeconds(10), 2), new(RunPhase.Work, TimeSpan.FromSeconds(20), 6),
            new(RunPhase.Recovery, TimeSpan.FromSeconds(5), 2), new(RunPhase.Cooldown, TimeSpan.FromSeconds(10), 2)];
        long[] ends = [10000, 30000, 35000, 45000];
        var facts = ends.Select((at, index) => new PhaseBoundaryFact(index, index == 3 ? null : index + 1,
            plan[index].Kind, index == 3 ? null : plan[index + 1].Kind, at, at,
            utc.AddMilliseconds(at), utc.AddMilliseconds(at), true)).Take(partial ? 2 : 4).ToArray();
        var elapsed = partial ? 33000 : 50000;
        var checkpoint = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            SaveRevision = 1,
            Plan = plan,
            Completed = plan.Take(partial ? 2 : 4).Select(phase => new CompletedPhase(phase, phase.Duration)).ToArray(),
            PhaseBoundaries = facts,
            PhaseActiveTime = partial ? TimeSpan.FromSeconds(3) : TimeSpan.Zero,
            TotalActiveTime = TimeSpan.FromSeconds(partial ? 33 : 45),
            MainActiveTime = TimeSpan.FromSeconds(partial ? 23 : 25),
            StartContext = legacy ? null : new RunStartContext("Frozen synthetic zone", 3600, 1000),
            Snapshot = new { Phase = partial ? "Recovery" : "Finished", Motion = new { StartedMonotonicTime = TimeSpan.FromSeconds(1) } }
        });
        var capture = new RunRouteCapture(id);
        RunSaveLocation Point(int seconds, TimeSpan? previous, bool ignored = false) => capture.Capture(
            new(utc.AddSeconds(seconds).ToUnixTimeMilliseconds(), (seconds + 1L) * 1000000000, 1, 2, 5),
            new(!ignored, previous, TimeSpan.FromSeconds(seconds + 1)) { Ignored = ignored }, "Synthetic callback");
        var points = new List<RunSaveLocation> { Point(5, null) };
        capture.Break("Phase boundary");
        points.Add(Point(10, null));
        points.Add(Point(9, null, true));
        points.Add(Point(20, TimeSpan.FromSeconds(11)));
        points.Add(Point(32, TimeSpan.FromSeconds(21)));
        if (!partial) { capture.Break("Phase boundary"); points.Add(Point(40, null)); }
        var buffer = new RunSaveBuffer();
        foreach (var point in points) buffer.Add(legacy ? point with { Provenance = null } : point);
        buffer.Capture(new(id, 1, utc, utc.AddMilliseconds(elapsed), elapsed, !partial, JsonSerializer.Serialize(plan), "null", null, checkpoint), "synthetic-route-fixture", null);
        Require(buffer.Save(journal.Commit), "Owned read fixture committed");
        if (partial)
        {
            using var update = journal.WritableDatabase!.CompileStatement("UPDATE runs SET status = 'interrupted', recovery_saved_utc = ? WHERE id = ?")!;
            update.BindString(1, utc.AddMinutes(1).ToString("O", CultureInfo.InvariantCulture));
            update.BindString(2, id.ToString());
            update.ExecuteUpdateDelete();
        }
    }

    private static void VerifyReads(RunJournal journal, Guid finished, Guid partial, Guid legacy)
    {
        var before = Cells(journal.ReadableDatabase!);
        var result = journal.ReadRoute(finished);
        var pointIds = result.Whole.Points.ToDictionary(point => point.SnapshotOrder, point => point.Provenance!.PointId);
        Require(result.Id == finished && result.Main.Supported && result.Whole.Points.Count == 5 && result.Main.Points.Count == 3
            && result.Main.Points.Select(point => point.Provenance!.PointId).SequenceEqual([2L, 4, 5])
            && result.Synthetic && result.Main.Links.Select(link => (pointIds[link.FromOrder], pointIds[link.ToOrder])).SequenceEqual([(2L, 4L), (4L, 5L)]),
            "Actual detached finished query preserves scopes/order/link across ignored point and frozen historical phase timing");
        var recordedPart = journal.ReadRoute(partial);
        Require(recordedPart.Id == partial && recordedPart.Main.Supported && recordedPart.Main.Points.Count == 3
            && recordedPart.Whole.Points.Count == 4, "Actual confirmed partial query preserves recorded cutoff, no actual stop invented");
        var old = journal.ReadRoute(legacy);
        Require(old.Id == legacy && !old.Main.Supported && old.Synthetic && old.Whole.UnknownQuality && old.Whole.Links.Count == 0
            && old.Whole.Points.Count == 6, "Actual legacy query leaves missing clock/quality unknown");
        _ = journal.ReadFinishedRun(finished);
        _ = journal.ExportJson(finished);
        Require(Cells(journal.ReadableDatabase!) == before, "ReadRoute/detail/v1 do not mutate any raw/checkpoint/outbox cells");
    }

    private static string Cells(SQLiteDatabase db)
    {
        var result = new Dictionary<string, List<string?[]>>();
        foreach (var table in new[] { "runs", "locations", "run_events", "heart_rate", "export_queue" })
        {
            var rows = new List<string?[]>();
            using var cursor = db.RawQuery($"SELECT * FROM {table} ORDER BY rowid", null)!;
            while (cursor.MoveToNext())
                rows.Add(Enumerable.Range(0, cursor.ColumnCount).Select(column => cursor.IsNull(column) ? null : cursor.GetType(column) + ":" + cursor.GetString(column)).ToArray());
            result.Add(table, rows);
        }
        return JsonSerializer.Serialize(result);
    }

    private static long Count(Android.Database.Sqlite.SQLiteDatabase db, string table, Guid id)
    {
        using var row = db.RawQuery($"SELECT COUNT(*) FROM {table} WHERE {(table == "runs" ? "id" : "run_id")} = ?", [id.ToString()])!;
        _ = row.MoveToFirst();
        return row.GetLong(0);
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }
}
#endif
