using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopCx.Features.HealthConnectExport;

public enum HCRecordKind { RunningSession, Distance, HeartRate }
public sealed record HCPause(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed record HCRawSample(long SourceSequence, DateTimeOffset ReceiptUtc, int Bpm);
public sealed record HCSourceGate(bool Finished, bool Synthetic, bool DebugBuild);
public sealed record HCPlannedRecord(string ClientRecordId, long ClientRecordVersion, HCRecordKind Kind,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, int? StartOffsetSeconds, int? EndOffsetSeconds,
    IReadOnlyList<HCPause> Pauses, double? AcceptedMetres, IReadOnlyList<HCRawSample> HeartRateSamples, string? Provenance);
public sealed record HCWritePlanV1(int MappingVersion, Guid RunId, HCSourceGate Source,
    IReadOnlyList<HCPlannedRecord> Records, IReadOnlyList<string> Warnings, string PayloadHash)
{
    internal static string Hash(int version, Guid runId, HCSourceGate source,
        IReadOnlyList<HCPlannedRecord> records, IReadOnlyList<string> warnings)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { version, runId, source, records, warnings }))));
    }

    public bool HasValidHash()
    {
        return PayloadHash == Hash(MappingVersion, RunId, Source, Records, Warnings);
    }
}
public sealed record HCPlanResult(string State, string? Reason, HCWritePlanV1? Plan);

// A local test/replay protocol only. No status here means acknowledged production Health Connect export.
public sealed record HCFakeAttempt(HCWritePlanV1 FrozenPlan, string State, int AttemptCount, string? Failure);
public enum HCFakeWriteResult { Accepted, PermissionDenied, TemporaryFailure }
public static class HCFakeReplay
{
    public static HCFakeAttempt Run(HCWritePlanV1 plan, HCFakeAttempt? previous, bool debugBuild,
        Func<bool> canWrite, Func<HCPlannedRecord, HCFakeWriteResult> fakeWriter)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(canWrite);
        ArgumentNullException.ThrowIfNull(fakeWriter);
        if (debugBuild || plan.Source.DebugBuild || plan.Source.Synthetic)
            return new(plan, "disabled_synthetic", previous?.AttemptCount ?? 0, "Source is not eligible for production delivery.");
        if (!plan.Source.Finished || plan.Records.Count == 0 || plan.Records[0].Kind != HCRecordKind.RunningSession
            || plan.Records.Count(static record => record.Kind == HCRecordKind.RunningSession) != 1
            || plan.Records.Select(static record => record.ClientRecordId).Distinct(StringComparer.Ordinal).Count() != plan.Records.Count
            || plan.Records.Any(record => record.ClientRecordVersion != plan.MappingVersion || !Enum.IsDefined(record.Kind) || record.EndUtc <= record.StartUtc)
            || !plan.HasValidHash() || plan.MappingVersion != 1
            || (previous is not null && (!previous.FrozenPlan.HasValidHash() || previous.FrozenPlan.PayloadHash != plan.PayloadHash
                || previous.AttemptCount is < 0 or int.MaxValue)))
        {
            return new(plan, "action_required", previous?.AttemptCount ?? 0, "The frozen mapping or payload changed.");
        }

        if (previous?.State == "simulated_complete") return previous;
        var attempts = checked((previous?.AttemptCount ?? 0) + 1);
        try
        {
            foreach (var record in plan.Records)
            {
                if (!canWrite()) return new(plan, "action_required", attempts, "Write permission is unavailable.");
                var result = fakeWriter(record);
                if (result == HCFakeWriteResult.PermissionDenied) return new(plan, "action_required", attempts, "Write permission was revoked.");
                if (result != HCFakeWriteResult.Accepted) return new(plan, "pending", attempts, "The simulated remote write did not complete.");
            }
            return new(plan, "simulated_complete", attempts, null);
        }
        catch (IOException problem)
        {
            // A remote may have accepted before this failure. Replay the frozen IDs; never infer an ACK.
            return new(plan, "pending", attempts, problem.Message);
        }
    }
}
