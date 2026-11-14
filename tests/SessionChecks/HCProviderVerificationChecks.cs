using LoopCx.Features.HealthConnectExport;

internal static class HCProviderVerificationChecks
{
    internal static void Run()
    {
        var plan = HCProviderVerification.Fixture();
        var now = new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
        var valid = new HCProviderApproval(1, HCProviderVerification.Scope, HCProviderVerification.Target,
            plan.PayloadHash, "synthetic-host-approval-reference-not-authority", Guid.NewGuid(), now, now.AddMinutes(4));
        HCProviderVerification.ValidateApproval(valid, now);
        if (plan.PayloadHash != HCProviderVerification.Fixture().PayloadHash || !plan.Source.Synthetic || !plan.Source.DebugBuild
            || plan.Records.Count != 3 || plan.Records.Select(static record => record.ClientRecordId).Distinct().Count() != 3)
        {
            throw new InvalidOperationException("Isolated fixture identity and synthetic provenance must remain fixed.");
        }

        HCProviderApproval?[] invalid = [null, valid with { Scope = "exercise-only" }, valid with { FixtureHash = "changed" },
            valid with { Target = "phone" }, valid with { Version = 2 }, valid with { OwnerDecisionReference = "" },
            valid with { Nonce = Guid.Empty }, valid with { IssuedUtc = now.AddSeconds(1) }, valid with { ExpiresUtc = now },
            valid with { IssuedUtc = now.AddMinutes(-6) }, valid with { ExpiresUtc = now.AddMinutes(6) }];
        foreach (var token in invalid)
        {
            try { HCProviderVerification.ValidateApproval(token, now); }
            catch (InvalidDataException) { continue; }
            throw new InvalidOperationException("Missing, old-scope or stale authority must block before any provider access.");
        }
        var writerCalls = 0;
        var fake = HCFakeReplay.Run(plan, null, true, () => true, _ => { writerCalls++; return HCFakeWriteResult.Accepted; });
        if (writerCalls != 0 || fake.State != "disabled_synthetic") throw new InvalidOperationException("Normal production gates must still reject the fixture.");
        Console.WriteLine("PASS: fixed synthetic provider fixture and fresh exact-scope consent negatives; normal writer remains blocked.");
        Console.WriteLine("HC provider fixture SHA256: " + plan.PayloadHash);
    }
}
