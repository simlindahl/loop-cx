using Android.Content;
using Android.Database.Sqlite;
using LoopCx.Features.HealthConnectExport;
using System.Text.Json;

namespace LoopCx;

internal sealed record HCOutboxClaim(string State, HCDurableAttempt? Attempt, string? Error = null);

// One compact attempt in the existing queue. No transaction is held while a writer runs.
internal sealed class AndroidHealthConnectOutbox
{
    private readonly SQLiteDatabase database;
    private readonly bool simulation;
#if DEBUG
    private static bool DebugBuild => true;
#else
    private static bool DebugBuild => false;
#endif
    internal AndroidHealthConnectOutbox(RunJournal journal) { database = journal.WritableDatabase!; }
    private AndroidHealthConnectOutbox(SQLiteDatabase scratch) { database = scratch; simulation = true; }
#if DEBUG
    internal static AndroidHealthConnectOutbox ForSimulation(SQLiteDatabase scratch)
    {
        if (Path.GetFileName(scratch.Path)?.StartsWith("hc-outbox-probe-", StringComparison.Ordinal) != true)
            throw new ArgumentException("Simulation requires its own isolated scratch database.");
        return new(scratch);
    }
    internal HCOutboxClaim PrepareSimulation(HCWritePlanV1 plan) => Prepare(plan.RunId, plan);
#endif
    internal HCOutboxClaim PrepareProvider(Guid runId) => Prepare(runId, null);

    private HCOutboxClaim Prepare(Guid runId, HCWritePlanV1? simulatedPlan) => Transaction<HCOutboxClaim>(() =>
    {
        if (!simulation && DebugBuild)
        {
            SetStatus(runId, "disabled_synthetic", "Debug delivery is disabled.");
            return new("disabled_synthetic", null);
        }
        try
        {
            using var row = database.RawQuery("SELECT status, attempt_json FROM export_queue WHERE run_id = ?", [runId.ToString()])!;
            if (!row.MoveToFirst()) return new("action_required", null, "The run has no finalization queue entry.");
            var status = row.GetString(0)!;
            var previous = row.IsNull(1) ? null : JsonSerializer.Deserialize<HCDurableAttempt>(row.GetString(1)!)
                ?? throw new InvalidDataException("Stored attempt is null.");
            previous?.Validate(simulation);
            HCWritePlanV1 plan;
            if (simulation)
            {
                if (status != "disabled_synthetic" || simulatedPlan is null) throw new InvalidDataException("Simulation cannot claim production status.");
                plan = simulatedPlan;
            }
            else
            {
                var snapshot = RunJournal.ReadExportSnapshot(database, runId);
                var projected = HCWritePlanner.Create(snapshot, DebugBuild);
                if (projected.Plan is null)
                {
                    SetStatus(runId, projected.State == "disabled_synthetic" ? projected.State : "action_required", projected.Reason);
                    return new(projected.State, null, projected.Reason);
                }
                plan = projected.Plan;
                if (status is not ("pending" or "action_required" or "exported")) throw new InvalidDataException("Queue status is unsupported.");
            }
            if (plan.RunId != runId || previous is not null && previous.FrozenPlan.PayloadHash != plan.PayloadHash)
                throw new InvalidDataException("The source and frozen retry payload disagree.");
            if (previous is { State: "acknowledged" or "simulated_complete" })
            {
                if (!simulation && status != "exported") throw new InvalidDataException("Acknowledgement and queue status disagree.");
                return new(previous.State, null);
            }
            if (!simulation && status == "exported") throw new InvalidDataException("Exported status lacks a durable acknowledgement.");
            var attempt = HCDurableAttempt.Begin(plan, previous, simulation);
            Save(runId, attempt, simulation ? "disabled_synthetic" : "pending", null);
            return new("claimed", attempt);
        }
        catch (Exception problem) when (problem is JsonException or InvalidDataException or ArgumentException or OverflowException or KeyNotFoundException)
        {
            SetStatus(runId, simulation ? "disabled_synthetic" : "action_required", problem.Message);
            return new("action_required", null, problem.Message);
        }
    });

    [System.Runtime.Versioning.SupportedOSPlatform("android34.0")]
    internal bool CompleteProvider(Guid runId, Guid token, string hash, AndroidHealthConnectWriter.AcceptedBatch acknowledgement)
    {
        if (simulation || DebugBuild) return false;
        return Complete(runId, token, hash, acknowledgement.Receipts);
    }
#if DEBUG
    internal bool CompleteSimulation(Guid runId, Guid token, string hash, IReadOnlyList<HCRecordReceipt> receipts)
    {
        if (!simulation) return false;
        return Complete(runId, token, hash, receipts);
    }
#endif
    private bool Complete(Guid runId, Guid token, string hash, IReadOnlyList<HCRecordReceipt> receipts) => Transaction(() =>
    {
        var current = Current(runId, token, hash);
        if (current is null) return false;
        try
        {
            if (!simulation)
            {
                var source = HCWritePlanner.Create(RunJournal.ReadExportSnapshot(database, runId), DebugBuild);
                if (source.Plan is null || source.Plan.PayloadHash != hash)
                    throw new InvalidDataException("Source eligibility changed before acknowledgement.");
            }
            HCDurableAttempt.ValidateReceipts(current.FrozenPlan, receipts);
            var completed = current with { State = simulation ? "simulated_complete" : "acknowledged", Receipts = receipts.ToArray() };
            Save(runId, completed, simulation ? "disabled_synthetic" : "exported", null);
            return true;
        }
        catch (InvalidDataException problem)
        {
            Save(runId, current with { State = "action_required" }, simulation ? "disabled_synthetic" : "action_required", problem.Message);
            return false;
        }
    });

    internal bool Fail(Guid runId, Guid token, string hash, string error, bool actionRequired) => Transaction(() =>
    {
        var current = Current(runId, token, hash);
        if (current is null) return false;
        Save(runId, current with { State = actionRequired ? "action_required" : "pending" },
            simulation ? "disabled_synthetic" : actionRequired ? "action_required" : "pending", error);
        return true;
    });

    private HCDurableAttempt? Current(Guid runId, Guid token, string hash)
    {
        using var row = database.RawQuery("SELECT attempt_json FROM export_queue WHERE run_id = ?", [runId.ToString()])!;
        if (!row.MoveToFirst() || row.IsNull(0)) return null;
        var attempt = JsonSerializer.Deserialize<HCDurableAttempt>(row.GetString(0)!) ?? throw new InvalidDataException("Missing attempt.");
        attempt.Validate(simulation);
        return attempt.Token == token && attempt.FrozenPlan.PayloadHash == hash && attempt.State == "in_flight" ? attempt : null;
    }
    private void SetStatus(Guid id, string status, string? error)
    {
        using var values = new ContentValues();
        values.Put("status", status);
        if (error is null) values.PutNull("last_error"); else values.Put("last_error", error);
        database.Update("export_queue", values, "run_id = ?", [id.ToString()]);
    }
    private void Save(Guid id, HCDurableAttempt attempt, string status, string? error)
    {
        using var values = new ContentValues();
        values.Put("attempt_json", JsonSerializer.Serialize(attempt));
        values.Put("status", status);
        if (error is null) values.PutNull("last_error"); else values.Put("last_error", error);
        if (database.Update("export_queue", values, "run_id = ?", [id.ToString()]) != 1)
            throw new IOException("The durable export attempt could not be committed.");
    }
    private T Transaction<T>(Func<T> operation)
    {
        database.BeginTransaction();
        try { var result = operation(); database.SetTransactionSuccessful(); return result; }
        finally { database.EndTransaction(); }
    }
}
