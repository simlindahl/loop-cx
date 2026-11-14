using Android.Content;
using Android.Database.Sqlite;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;

namespace LoopCx;

// Android's built-in SQLite keeps this storage boundary free of an extra native package.
public sealed class RunJournal : SQLiteOpenHelper
{
#if DEBUG
    private const string JournalFileName = "runs-debug.db";
#else
    private const string JournalFileName = "runs.db";
#endif
    private static RunJournal? shared;
    public static RunJournal Shared => shared ??= new RunJournal();

    private RunJournal() : base(global::Android.App.Application.Context, JournalFileName, null, 5)
    {
        // Only runs left by an earlier process are interrupted. Never infer missing time.
        WritableDatabase!.ExecSQL("UPDATE runs SET status = 'interrupted' WHERE status = 'recording'");
    }

    public override void OnConfigure(SQLiteDatabase? database)
    {
        base.OnConfigure(database);
        database!.SetForeignKeyConstraintsEnabled(true);
    }

    public override void OnCreate(SQLiteDatabase? database)
    {
        var db = database!;
        db.ExecSQL("CREATE TABLE runs (id TEXT PRIMARY KEY, started_utc TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, status TEXT NOT NULL, initial_plan TEXT NOT NULL, checkpoint TEXT NOT NULL, template_metadata TEXT)");
        db.ExecSQL("CREATE TABLE locations (run_id TEXT NOT NULL, utc_ms INTEGER NOT NULL, monotonic_ns INTEGER NOT NULL, latitude REAL NOT NULL, longitude REAL NOT NULL, accuracy REAL)");
        CreateExportQueue(db);
        CreateEvents(db);
        CreateHeartRate(db);
    }

    internal static void CreateExportQueue(SQLiteDatabase db) =>
        db.ExecSQL("CREATE TABLE export_queue (run_id TEXT PRIMARY KEY, status TEXT NOT NULL, attempt_json TEXT, last_error TEXT)");

    private static void CreateEvents(SQLiteDatabase db) =>
        db.ExecSQL("CREATE TABLE run_events (sequence INTEGER PRIMARY KEY, run_id TEXT NOT NULL, command TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, checkpoint TEXT NOT NULL)");

    private static void CreateHeartRate(SQLiteDatabase db)
    {
        db.ExecSQL("CREATE TABLE heart_rate (sequence INTEGER PRIMARY KEY, run_id TEXT NOT NULL, receipt_utc TEXT NOT NULL, receipt_monotonic_ticks INTEGER NOT NULL, bpm INTEGER, quality TEXT NOT NULL)");
        db.ExecSQL("ALTER TABLE runs ADD COLUMN heart_rate_metadata TEXT");
    }

    public override void OnUpgrade(SQLiteDatabase? db, int oldVersion, int newVersion)
    {
        if (oldVersion < 2) CreateEvents(db!);
        if (oldVersion < 3) db!.ExecSQL("ALTER TABLE runs ADD COLUMN template_metadata TEXT");
        if (oldVersion < 4) CreateHeartRate(db!);
        if (oldVersion < 5)
        {
            db!.ExecSQL("ALTER TABLE export_queue ADD COLUMN attempt_json TEXT");
            db.ExecSQL("ALTER TABLE export_queue ADD COLUMN last_error TEXT");
        }
    }

    public void Commit(RunSaveBatch batch)
    {
        try { CommitTransaction(batch); }
        catch (SQLiteDatabaseCorruptException error)
        {
            throw new RunSaveReadException("Local database is corrupt. No repair or overwrite was attempted.", error);
        }
        catch (SQLiteCantOpenDatabaseException error)
        {
            throw new RunSaveReadException("Local database could not be opened. Saving cannot be retried in this run.", error);
        }
        catch (SQLiteException error) { throw new IOException("Recent recording could not be saved.", error); }
    }

#if DEBUG
    internal void SetSaveFailureForProbe(Guid runId, bool enabled)
    {
        var db = WritableDatabase!;
        db.ExecSQL("DROP TRIGGER IF EXISTS temp.loop_save_probe_failure");
        if (enabled)
            db.ExecSQL($"CREATE TEMP TRIGGER loop_save_probe_failure BEFORE INSERT ON runs WHEN NEW.id = '{runId}' BEGIN SELECT RAISE(ABORT, 'isolated save failure'); END");
    }
#endif

    private void CommitTransaction(RunSaveBatch batch)
    {
        var state = batch.Latest;
        var db = WritableDatabase!;
        db.BeginTransaction();
        try
        {
            try
            {
                using var existing = db.RawQuery("SELECT checkpoint FROM runs WHERE id = ?", [state.Id.ToString()])!;
                if (existing.MoveToFirst())
                {
                    var stored = existing.GetString(0)!;
                    if (RunSaveRevision.AlreadyCommitted(stored, state))
                    {
                        // A previous commit may have succeeded before its local return failed.
                        db.SetTransactionSuccessful();
                        return;
                    }
                }
            }
            catch (SQLiteException error) when (error is SQLiteDiskIOException or SQLiteAccessPermException)
            {
                throw new RunSaveReadException("Stored checkpoint could not be read. No repair or overwrite was attempted.", error);
            }
            using var values = new ContentValues();
            values.Put("id", state.Id.ToString());
            values.Put("started_utc", state.StartedUtc.ToString("O"));
            values.Put("observed_utc", state.ObservedUtc.ToString("O"));
            values.Put("elapsed_ms", state.ElapsedMilliseconds);
            values.Put("status", state.Finished ? "finished" : "recording");
            values.Put("initial_plan", state.OriginalPlan);
            values.Put("template_metadata", state.TemplateMetadata);
            if (state.HeartRateMetadata is not null) values.Put("heart_rate_metadata", state.HeartRateMetadata);
            else values.PutNull("heart_rate_metadata");
            values.Put("checkpoint", state.Checkpoint);
            if (db.InsertWithOnConflict("runs", null, values, Conflict.Replace) < 0)
                throw new IOException("The run checkpoint could not be saved.");
            foreach (var location in batch.Locations)
            {
                using var raw = new ContentValues();
                raw.Put("run_id", state.Id.ToString()); raw.Put("utc_ms", location.UtcMilliseconds);
                raw.Put("monotonic_ns", location.MonotonicNanoseconds);
                raw.Put("latitude", location.Latitude); raw.Put("longitude", location.Longitude);
                if (location.Accuracy is { } accuracy) raw.Put("accuracy", accuracy); else raw.PutNull("accuracy");
                db.InsertOrThrow("locations", null, raw);
            }
            foreach (var observation in batch.HeartRate)
            {
                using var raw = new ContentValues();
                raw.Put("run_id", state.Id.ToString());
                raw.Put("receipt_utc", observation.ReceiptUtc.ToString("O"));
                raw.Put("receipt_monotonic_ticks", observation.ReceiptMonotonicTime.Ticks);
                if (observation.Bpm is { } bpm) raw.Put("bpm", bpm); else raw.PutNull("bpm");
                raw.Put("quality", observation.Quality);
                db.InsertOrThrow("heart_rate", null, raw);
            }
            foreach (var recordedEvent in batch.Events)
            {
                using var recorded = new ContentValues();
                recorded.Put("run_id", state.Id.ToString()); recorded.Put("command", recordedEvent.Command);
                recorded.Put("observed_utc", recordedEvent.ObservedUtc.ToString("O"));
                recorded.Put("elapsed_ms", recordedEvent.ElapsedMilliseconds); recorded.Put("checkpoint", recordedEvent.Checkpoint);
                db.InsertOrThrow("run_events", null, recorded);
            }
            if (state.Finished)
            {
                using var queued = db.CompileStatement("INSERT OR IGNORE INTO export_queue (run_id, status) VALUES (?, ?)")!;
                queued.BindString(1, state.Id.ToString());
#if DEBUG
                queued.BindString(2, "disabled_synthetic");
#else
                queued.BindString(2, "pending");
#endif
                queued.ExecuteInsert();
            }
            db.SetTransactionSuccessful();
        }
        finally { db.EndTransaction(); }
    }
    public string ExportJson(Guid id)
    {
        // All tables are materialized on this transaction's thread before projection or sharing.
        var db = ReadableDatabase!;
        ExportJournalSnapshot snapshot;
        db.BeginTransactionNonExclusive();
        try
        {
            snapshot = ReadExportSnapshot(db, id);
            db.SetTransactionSuccessful();
        }
        finally { db.EndTransaction(); }

        return ExportRunV1.Project(snapshot);
    }

    // Caller owns the single transaction; all values are detached before returning.
    internal static ExportJournalSnapshot ReadExportSnapshot(SQLiteDatabase db, Guid id)
    {
        if (!db.InTransaction()) throw new InvalidOperationException("A consistent export snapshot requires a transaction.");
        using var run = db.RawQuery("SELECT started_utc, observed_utc, elapsed_ms, status, initial_plan, template_metadata, heart_rate_metadata, checkpoint FROM runs WHERE id = ?", [id.ToString()])!;
        if (!run.MoveToFirst()) throw new KeyNotFoundException("The recorded run does not exist.");
        var events = new List<ExportEvent>();
        using (var rows = db.RawQuery("SELECT sequence, command, observed_utc, elapsed_ms, checkpoint FROM run_events WHERE run_id = ? ORDER BY sequence", [id.ToString()])!)
            while (rows.MoveToNext()) events.Add(new(rows.GetLong(0), rows.GetString(1)!, rows.GetString(2)!, rows.GetLong(3), ParseJson(rows.GetString(4)!)));
        var locations = new List<ExportLocation>();
        using (var rows = db.RawQuery("SELECT rowid, utc_ms, monotonic_ns, latitude, longitude, accuracy FROM locations WHERE run_id = ? ORDER BY rowid", [id.ToString()])!)
            while (rows.MoveToNext()) locations.Add(new(rows.GetLong(0), rows.GetLong(1), rows.GetLong(2), rows.GetDouble(3), rows.GetDouble(4), rows.IsNull(5) ? null : rows.GetDouble(5)));
        var heartRate = new List<ExportHeartRate>();
        using (var rows = db.RawQuery("SELECT sequence, receipt_utc, receipt_monotonic_ticks, bpm, quality FROM heart_rate WHERE run_id = ? ORDER BY sequence", [id.ToString()])!)
            while (rows.MoveToNext()) heartRate.Add(new(rows.GetLong(0), rows.GetString(1)!, rows.GetLong(2), rows.IsNull(3) ? null : rows.GetInt(3), rows.GetString(4)!));
        using var queue = db.RawQuery("SELECT status FROM export_queue WHERE run_id = ?", [id.ToString()])!;
        var exportStatus = queue.MoveToFirst() ? queue.GetString(0) : null;
        return new(id, run.GetString(0)!, run.GetString(1)!, run.GetLong(2), run.GetString(3)!,
            ParseJson(run.GetString(4)!), run.IsNull(5) ? null : ParseJson(run.GetString(5)!),
            run.IsNull(6) ? null : ParseJson(run.GetString(6)!), ParseJson(run.GetString(7)!), exportStatus,
            events, locations, heartRate);
    }

    private static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

}
