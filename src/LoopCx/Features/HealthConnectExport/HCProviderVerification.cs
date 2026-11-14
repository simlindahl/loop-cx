namespace LoopCx.Features.HealthConnectExport;

// A fixed synthetic fixture, never a production-eligible journal source.
public static class HCProviderVerification
{
    public const string Scope = "loop-hc-three-record-own-id-roundtrip-v1";
    public const string Target = "emulator-5580|LOOP_Cx_QA_API35_193fb70|accounts=0|whoop=absent";
    public static HCWritePlanV1 Fixture()
    {
        var id = Guid.Parse("a328fe0d-49db-4ecf-9eab-d91dc8aed229");
        var start = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var source = new HCSourceGate(true, true, true);
        const string prefix = "loop-hc-isolated-a328fe0d49db4ecf9eabd91dc8aed229:";
        HCPlannedRecord[] records =
        [
            new(prefix + "session", 1, HCRecordKind.RunningSession, start, start.AddMinutes(1), 0, 0,
                [new(start.AddSeconds(20), start.AddSeconds(30))], null, [], "Synthetic compatibility fixture; not a real workout."),
            new(prefix + "distance", 1, HCRecordKind.Distance, start, start.AddMinutes(1), 0, 0,
                [], 100, [], "Synthetic 100-metre fixture, not a physical measurement."),
            new(prefix + "hr", 1, HCRecordKind.HeartRate, start, start.AddMinutes(1), 0, 0,
                [], null, [new(1, start.AddSeconds(5), 100), new(2, start.AddSeconds(15), 120)],
                "Synthetic receipt-time samples, not physiological observations.")
        ];
        string[] warnings = ["Isolated synthetic test only; never production exported."];
        return new(1, id, source, records, warnings, HCWritePlanV1.Hash(1, id, source, records, warnings));
    }

    public static void ValidateApproval(HCProviderApproval? approval, DateTimeOffset now)
    {
        if (approval is null || approval.Scope != Scope || approval.Target != Target
            || approval.FixtureHash != Fixture().PayloadHash || approval.Version != 1
            || string.IsNullOrWhiteSpace(approval.OwnerDecisionReference) || approval.Nonce == Guid.Empty
            || approval.IssuedUtc > now || now - approval.IssuedUtc > TimeSpan.FromMinutes(5)
            || approval.ExpiresUtc <= now || approval.ExpiresUtc - approval.IssuedUtc > TimeSpan.FromMinutes(5))
        {
            throw new InvalidDataException("A fresh explicit owner-approved three-record scope and exact fixture hash are required.");
        }
    }
}

public sealed record HCProviderApproval(int Version, string Scope, string Target, string FixtureHash,
    string OwnerDecisionReference, Guid Nonce, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc);
