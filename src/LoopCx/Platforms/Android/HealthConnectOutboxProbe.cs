#if DEBUG
using Android.Content;
using Android.Database.Sqlite;
using LoopCx.Features.HealthConnectExport;
using System.Text.Json;

namespace LoopCx;

// Isolated SQLite/fake-response proof only. This code never constructs a health manager or writer.
internal static class HealthConnectOutboxProbe
{
    internal static void Run(Context context)
    {
        if (Android.OS.Build.Hardware != "ranchu" || Android.OS.Build.Product != "sdk_gphone64_x86_64"
            || Android.OS.Build.Model != "sdk_gphone64_x86_64"
            || Android.OS.Build.SupportedAbis?.FirstOrDefault() != "x86_64"
            || Android.OS.Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) != true)
            return;

        var name = "hc-outbox-probe-" + Guid.NewGuid().ToString("N") + ".db";
        var path = context.GetDatabasePath(name)!.AbsolutePath;
        var report = Path.Combine(context.FilesDir!.AbsolutePath, "hc-outbox-probe.json");
        try
        {
            var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var id = Guid.NewGuid();
            var source = new HCSourceGate(true, true, true);
            HCPlannedRecord[] records = [new(id + ":session", 1, HCRecordKind.RunningSession,
                start, start.AddMinutes(1), 0, 0, [], null, [], "Isolated synthetic SQLite simulation, never provider delivery.")];
            var warnings = Array.Empty<string>();
            var plan = new HCWritePlanV1(1, id, source, records, warnings, HCWritePlanV1.Hash(1, id, source, records, warnings));
            HCDurableAttempt first;
            using (var database = SQLiteDatabase.OpenOrCreateDatabase(path, null)!)
            {
                RunJournal.CreateExportQueue(database);
                using var values = new ContentValues();
                values.Put("run_id", id.ToString());
                values.Put("status", "disabled_synthetic");
                database.InsertOrThrow("export_queue", null, values);
                first = AndroidHealthConnectOutbox.ForSimulation(database).PrepareSimulation(plan).Attempt
                    ?? throw new InvalidOperationException("First claim was not persisted.");
            }

            // Remote acceptance is simulated; closing and reopening drops the local ACK deliberately.
            HCRecordReceipt[] receipts = [new(HCRecordKind.RunningSession, records[0].ClientRecordId, 1, "simulated-provider-id")];
            using (var database = SQLiteDatabase.OpenDatabase(path, null, DatabaseOpenFlags.OpenReadwrite)!)
            {
                var store = AndroidHealthConnectOutbox.ForSimulation(database);
                var retry = store.PrepareSimulation(plan).Attempt ?? throw new InvalidOperationException("Restart did not reclaim.");
                Require(retry.Token != first.Token && retry.AttemptCount == 2 && retry.FrozenPlan.PayloadHash == first.FrozenPlan.PayloadHash, "Frozen restart contract");
                Require(!store.CompleteSimulation(id, first.Token, plan.PayloadHash, receipts), "Stale success must not ACK newer attempt");
                Require(!store.Fail(id, first.Token, plan.PayloadHash, "stale", true), "Stale failure must not change newer attempt");
                Require(store.CompleteSimulation(id, retry.Token, plan.PayloadHash, receipts), "Current simulation ACK");
                Require(!store.CompleteSimulation(id, retry.Token, plan.PayloadHash, receipts), "Duplicate ACK");
            }

            using (var database = SQLiteDatabase.OpenDatabase(path, null, DatabaseOpenFlags.OpenReadwrite)!)
            {
                using var row = database.RawQuery("SELECT status, attempt_json FROM export_queue WHERE run_id = ?", [id.ToString()])!;
                Require(row.MoveToFirst() && row.GetString(0) == "disabled_synthetic", "Simulation must never be exported");
                var attempt = JsonSerializer.Deserialize<HCDurableAttempt>(row.GetString(1)!)!;
                attempt.Validate(true);
                Require(attempt.State == "simulated_complete", "Cold reopen keeps simulated completion");
                Require(AndroidHealthConnectOutbox.ForSimulation(database).PrepareSimulation(plan).Attempt is null, "Completed simulation has no further dispatch");
            }

            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                State = "passed",
                Database = name,
                RunId = id,
                ProviderCalls = 0,
                ProductionExported = false,
                Checks = "claim, close/reopen, stable replay, stale success/failure, duplicate ACK, cold simulated completion"
            }), new System.Text.UTF8Encoding(false));
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or Java.Lang.RuntimeException or IOException or JsonException)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { State = "failed", Database = name, Error = error.Message, ProviderCalls = 0 }), new System.Text.UTF8Encoding(false));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
