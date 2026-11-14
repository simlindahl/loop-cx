using System.Text.Json;
using LoopCx.Features.HealthConnectExport;

internal static class HCOutboxChecks
{
    internal static void Run()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var id = Guid.Parse("55555555-5555-5555-5555-555555555555");
        HCPlannedRecord[] records = [new("isolated-outbox-session", 1, HCRecordKind.RunningSession, start, start.AddMinutes(1), 0, 0, [], null, [], "Synthetic simulation only."),
            new("isolated-outbox-distance", 1, HCRecordKind.Distance, start, start.AddMinutes(1), 0, 0, [], 100, [], "Partial measured fixture."),
            new("isolated-outbox-hr", 1, HCRecordKind.HeartRate, start, start.AddMinutes(1), 0, 0, [], null, [new(1, start.AddSeconds(1), 100)], "Receipt fixture.")];
        var gate = new HCSourceGate(true, true, true);
        var warnings = Array.Empty<string>();
        var plan = new HCWritePlanV1(1, id, gate, records, warnings, HCWritePlanV1.Hash(1, id, gate, records, warnings));
        var first = HCDurableAttempt.Begin(plan, null, true);
        var persisted = JsonSerializer.Serialize(first); // A crash leaves this exact frozen payload in storage.
        var reopened = JsonSerializer.Deserialize<HCDurableAttempt>(persisted)!;
        var retry = HCDurableAttempt.Begin(plan, reopened, true);
        if (retry.Token == first.Token || retry.AttemptCount != 2 || retry.FrozenPlan.PayloadHash != plan.PayloadHash
            || !retry.FrozenPlan.Records.Select(static record => record.ClientRecordId).SequenceEqual(records.Select(static record => record.ClientRecordId)))
        {
            throw new InvalidOperationException("Restart must reclaim by new token with the exact original payload and IDs.");
        }

        HCRecordReceipt[] receipts = [.. records.Select(static record => new HCRecordReceipt(record.Kind, record.ClientRecordId, record.ClientRecordVersion, "simulated-" + record.ClientRecordId))];
        HCDurableAttempt.ValidateReceipts(plan, receipts);
        var simulated = retry with { State = "simulated_complete", Receipts = receipts };
        simulated.Validate(true);
        static void Reject(Action operation)
        {
            try { operation(); } catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Malformed or wrongly attributed attempts must fail closed.");
        }
        Reject(() => simulated.Validate(false));
        Reject(() => (retry with { State = "acknowledged", Receipts = receipts }).Validate(true));
        Reject(() => (retry with { Version = 2 }).Validate(true));
        Reject(() => (retry with { FrozenPlan = null! }).Validate(true));
        Reject(() => (retry with { FrozenPlan = plan with { PayloadHash = "altered" } }).Validate(true));
        Reject(() => HCDurableAttempt.ValidateReceipts(plan, receipts[..2]));
        Reject(() => HCDurableAttempt.ValidateReceipts(plan, [receipts[1], receipts[0], receipts[2]]));
        Reject(() => HCDurableAttempt.ValidateReceipts(plan, [receipts[0] with { ClientRecordVersion = 2 }, receipts[1], receipts[2]]));
        Reject(() => HCDurableAttempt.ValidateReceipts(plan, [receipts[0], receipts[1], receipts[2] with { ProviderRecordId = "" }]));
        var remote = new Dictionary<string, HCPlannedRecord>();
        foreach (var record in first.FrozenPlan.Records.Take(2)) remote[record.ClientRecordId] = record;
        // Simulate remote acceptance then lost local ACK; replay updates the same identities, not new records.
        foreach (var record in retry.FrozenPlan.Records) remote[record.ClientRecordId] = record;
        if (remote.Count != 3 || simulated.State != "simulated_complete")
            throw new InvalidOperationException("Simulated replay must be idempotent without claiming provider export.");
        Console.WriteLine("PASS: serializable frozen attempt/reclaim, stable IDs, partial/lost-ACK replay and strict response/simulation guards.");
    }
}
