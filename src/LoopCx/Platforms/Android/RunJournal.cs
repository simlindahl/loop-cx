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

    private RunJournal() : base(global::Android.App.Application.Context, JournalFileName, null, 1)
    {
        // Only runs left by an earlier process are interrupted. Never infer missing time.
        WritableDatabase!.ExecSQL("UPDATE runs SET status = 'interrupted' WHERE status = 'recording'");
    }

    public override void OnCreate(SQLiteDatabase? database)
    {
        var db = database!;
        db.ExecSQL("CREATE TABLE runs (id TEXT PRIMARY KEY, started_utc TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, status TEXT NOT NULL, initial_plan TEXT NOT NULL, checkpoint TEXT NOT NULL)");
        db.ExecSQL("CREATE TABLE locations (run_id TEXT NOT NULL, utc_ms INTEGER NOT NULL, monotonic_ns INTEGER NOT NULL, latitude REAL NOT NULL, longitude REAL NOT NULL, accuracy REAL)");
        db.ExecSQL("CREATE TABLE export_queue (run_id TEXT PRIMARY KEY, status TEXT NOT NULL)");
    }

    public override void OnUpgrade(SQLiteDatabase? db, int oldVersion, int newVersion) =>
        throw new NotSupportedException("A database migration is required.");

    public void Checkpoint(Guid id, DateTimeOffset started, long elapsedMilliseconds,
        TimedSession session, string initialPlan, RunSnapshot snapshot)
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
            values.Put("checkpoint", JsonSerializer.Serialize(new {
                SchemaVersion = 1, Snapshot = snapshot, session.Plan, session.Completed,
                session.PhaseActiveTime, session.TotalActiveTime, session.MainActiveTime
            }));
            if (db.InsertWithOnConflict("runs", null, values, Conflict.Replace) < 0)
                throw new IOException("The run checkpoint could not be saved.");
            if (session.IsFinished)
            {
                using var queued = db.CompileStatement("INSERT OR IGNORE INTO export_queue (run_id, status) VALUES (?, ?)");
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
