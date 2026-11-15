#if DEBUG
using System.Text.Json;
using Android.Content;
using LoopCx.Features.ActiveRun;

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
                }
                finally { reopened.Close(); }
            }
            File.WriteAllText(report, JsonSerializer.Serialize(new { Result = "PASS", Database = name, RunId = id, ProviderCalls = 0 }));
        }
        catch (Exception error)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { Result = "FAIL", Database = name, Error = error.GetType().Name, error.Message }));
        }
        // Retain the uniquely owned scratch tuple for independent inspection and scoped cleanup.
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
