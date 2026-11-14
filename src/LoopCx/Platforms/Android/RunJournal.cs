using Android.Content;
using Android.Database.Sqlite;
using System.Text.Json;
using LoopCx.Features.ActiveRun;

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

    private RunJournal() : base(global::Android.App.Application.Context, JournalFileName, null, 4)
    {
        // Only runs left by an earlier process are interrupted. Never infer missing time.
        WritableDatabase!.ExecSQL("UPDATE runs SET status = 'interrupted' WHERE status = 'recording'");
    }

    public override void OnCreate(SQLiteDatabase? database)
    {
        var db = database!;
        db.ExecSQL("CREATE TABLE runs (id TEXT PRIMARY KEY, started_utc TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, status TEXT NOT NULL, initial_plan TEXT NOT NULL, checkpoint TEXT NOT NULL, template_metadata TEXT)");
        db.ExecSQL("CREATE TABLE locations (run_id TEXT NOT NULL, utc_ms INTEGER NOT NULL, monotonic_ns INTEGER NOT NULL, latitude REAL NOT NULL, longitude REAL NOT NULL, accuracy REAL)");
        db.ExecSQL("CREATE TABLE export_queue (run_id TEXT PRIMARY KEY, status TEXT NOT NULL)");
        CreateEvents(db);
        CreateHeartRate(db);
    }

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
    }

    public void Checkpoint(Guid id, DateTimeOffset started, long elapsedMilliseconds,
        TimedSession session, string initialPlan, string templateMetadata, RunSnapshot snapshot, string? command = null, AutomaticMotionEvent? automaticEvent = null, string? heartRateMetadata = null, HeartRateObservation? heartRateObservation = null)
    {
        var db = WritableDatabase!;
        db.BeginTransaction();
        try
        {
            using var values = new ContentValues();
            values.Put("id", id.ToString());
            values.Put("started_utc", started.ToString("O"));
            values.Put("observed_utc", DateTimeOffset.UtcNow.ToString("O"));
            values.Put("elapsed_ms", elapsedMilliseconds);
            values.Put("status", session.IsFinished ? "finished" : "recording");
            values.Put("initial_plan", initialPlan);
            values.Put("template_metadata", templateMetadata);
            if (heartRateMetadata is not null) values.Put("heart_rate_metadata", heartRateMetadata);
            else values.PutNull("heart_rate_metadata");
            values.Put("checkpoint", JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                Snapshot = snapshot,
                AutomaticEvent = automaticEvent,
                session.Plan,
                session.Completed,
                session.PhaseActiveTime,
                session.TotalActiveTime,
                session.MainActiveTime
            }));
            if (db.InsertWithOnConflict("runs", null, values, Conflict.Replace) < 0)
                throw new IOException("The run checkpoint could not be saved.");
            if (heartRateObservation is { } observation)
            {
                using var raw = new ContentValues();
                raw.Put("run_id", id.ToString());
                raw.Put("receipt_utc", observation.ReceiptUtc.ToString("O"));
                raw.Put("receipt_monotonic_ticks", observation.ReceiptMonotonicTime.Ticks);
                if (observation.Bpm is { } bpm) raw.Put("bpm", bpm);
                else raw.PutNull("bpm");
                raw.Put("quality", observation.Quality);
                db.InsertOrThrow("heart_rate", null, raw);
            }
            // The event and the state it describes must either both survive or both roll back.
            if (command is not null)
            {
                using var recorded = new ContentValues(values);
                recorded.Remove("id");
                recorded.Remove("started_utc");
                recorded.Remove("status");
                recorded.Remove("initial_plan");
                recorded.Remove("template_metadata");
                recorded.Remove("heart_rate_metadata");
                recorded.Put("run_id", id.ToString());
                recorded.Put("command", command);
                db.InsertOrThrow("run_events", null, recorded);
            }
            if (session.IsFinished)
            {
                using var queued = db.CompileStatement("INSERT OR IGNORE INTO export_queue (run_id, status) VALUES (?, ?)")!;
                queued.BindString(1, id.ToString());
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

    public void Location(Guid id, global::Android.Locations.Location location)
    {
        using var values = new ContentValues();
        values.Put("run_id", id.ToString());
        values.Put("utc_ms", location.Time);
        values.Put("monotonic_ns", location.ElapsedRealtimeNanos);
        values.Put("latitude", location.Latitude);
        values.Put("longitude", location.Longitude);
        if (location.HasAccuracy) values.Put("accuracy", location.Accuracy);
        else values.PutNull("accuracy");
        WritableDatabase!.InsertOrThrow("locations", null, values);
    }
}
