using Android.Content;
using Android.Content.PM;
using Android.Health.Connect;
using Android.Health.Connect.DataTypes;
using Android.OS;
using LoopCx.Features.HealthConnectExport;
using System.Runtime.Versioning;

namespace LoopCx;

[SupportedOSPlatform("android34.0")]
internal sealed class AndroidHealthConnectWriter(Context context)
{
#if DEBUG
    private const bool DebugBuild = true;
#else
    private const bool DebugBuild = false;
#endif
    internal sealed record Result(string State, AcceptedBatch? Acknowledgement, string? Error);
    internal sealed class AcceptedBatch
    {
        internal IReadOnlyList<HCRecordReceipt> Receipts { get; }
        private AcceptedBatch(IReadOnlyList<HCRecordReceipt> receipts) { Receipts = receipts; }
        // The only call site is the real OutcomeReceiver. No serialized or caller-supplied origin flag exists.
        internal static AcceptedBatch FromSdkResponse(HCWritePlanV1 plan, InsertRecordsResponse response)
        {
            var returned = response.Records ?? throw new InvalidDataException("Provider response omitted records.");
            var receipts = returned.Select(static record => new HCRecordReceipt(record switch
            {
                ExerciseSessionRecord => HCRecordKind.RunningSession,
                DistanceRecord => HCRecordKind.Distance,
                HeartRateRecord => HCRecordKind.HeartRate,
                _ => throw new InvalidDataException("Provider returned an unexpected record type.")
            }, record.Metadata?.ClientRecordId ?? "", record.Metadata?.ClientRecordVersion ?? -1,
                record.Metadata?.Id ?? "")).ToArray();
            HCDurableAttempt.ValidateReceipts(plan, receipts);
            return new(Array.AsReadOnly(receipts));
        }
    }

    internal async Task<Result> WriteAsync(HCWritePlanV1 plan)
    {
        var built = HealthConnectRecordMapper.Create(plan, DebugBuild);
        if (built.State != "constructed") return new(built.State, null, built.Reason);
        if (!HasAllPermissions(plan)) return new("action_required", null, "A required Health Connect WRITE grant is missing.");
        var manager = (HealthConnectManager?)context.GetSystemService(Context.HealthconnectService);
        if (manager is null) return new("action_required", null, "Health Connect is unavailable.");
        var receiver = new Receiver(plan, () => HasAllPermissions(plan));
        try
        {
            // No assumption that this batch is remotely atomic. Retry preserves every client ID/version.
            manager.InsertRecords(built.Records.ToList(), context.MainExecutor!, receiver);
            return await receiver.Completion.Task;
        }
        catch (Java.Lang.SecurityException)
        { return new("action_required", null, "A required Health Connect WRITE grant was revoked."); }
        catch (Java.Lang.RuntimeException)
        { return new("pending", null, "The provider operation did not complete."); }
    }

    private bool HasAllPermissions(HCWritePlanV1 plan) => plan.Records.Select(static record => record.Kind switch
    {
        HCRecordKind.RunningSession => "android.permission.health.WRITE_EXERCISE",
        HCRecordKind.Distance => "android.permission.health.WRITE_DISTANCE",
        HCRecordKind.HeartRate => "android.permission.health.WRITE_HEART_RATE",
        _ => throw new InvalidDataException("Unsupported intended record permission.")
    }).Distinct(StringComparer.Ordinal).All(permission => context.CheckSelfPermission(permission) == Permission.Granted);

    private sealed class Receiver(HCWritePlanV1 plan, Func<bool> hasPermissions) : Java.Lang.Object, IOutcomeReceiver
    {
        internal TaskCompletionSource<Result> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? value)
        {
            try
            {
                if (value is not InsertRecordsResponse response) throw new InvalidDataException("Unexpected provider response.");
                Completion.TrySetResult(new("accepted", AcceptedBatch.FromSdkResponse(plan, response), null));
            }
            catch (InvalidDataException problem) { Completion.TrySetResult(new("action_required", null, problem.Message)); }
        }
        public void OnError(Java.Lang.Object? error) => Completion.TrySetResult(new(hasPermissions() ? "pending" : "action_required", null,
            "Health Connect did not acknowledge the complete intended batch."));
    }
}
