#if DEBUG
using Android.Content;
using Android.Content.PM;
using Android.Health.Connect;
using Android.Health.Connect.DataTypes;
using Android.OS;
using LoopCx.Features.HealthConnectExport;
using System.Runtime.Versioning;
using System.Text.Json;

namespace LoopCx;

// Separate from production dispatch and its source gates. No normal queue is read or updated.
[SupportedOSPlatform("android35.0")]
internal static class HealthConnectProviderVerificationProbe
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string[] Permissions = ["android.permission.health.WRITE_EXERCISE",
        "android.permission.health.WRITE_DISTANCE", "android.permission.health.WRITE_HEART_RATE"];

    internal static async Task Run(Context context, bool dryRun)
    {
        if (!await Gate.WaitAsync(0)) return;
        var report = Path.Combine(context.FilesDir!.AbsolutePath, "hc-provider-verification.json");
        var marker = Path.Combine(context.FilesDir.AbsolutePath, "hc-provider-verification-owned.json");
        try
        {
            if (Build.Hardware != "ranchu" || Build.Product != "sdk_gphone64_x86_64"
                || Build.Model != "sdk_gphone64_x86_64" || Build.SupportedAbis?.FirstOrDefault() != "x86_64"
                || Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) != true)
                throw new InvalidDataException("Non-allowlisted device: no provider access.");
            var plan = HCProviderVerification.Fixture();
            if (dryRun)
            {
                Report("dry-run", new
                {
                    plan.PayloadHash,
                    HCProviderVerification.Scope,
                    HCProviderVerification.Target,
                    ClientIds = plan.Records.Select(static item => item.ClientRecordId),
                    ProviderCalls = 0
                });
                return;
            }

            // This fresh scope is intentionally incompatible with the older exercise-only opt-in.
            var consent = Path.Combine(context.FilesDir.AbsolutePath, "hc-provider-verification-approval.json");
            var approval = File.Exists(consent)
                ? JsonSerializer.Deserialize<HCProviderApproval>(await File.ReadAllTextAsync(consent)) : null;
            HCProviderVerification.ValidateApproval(approval, DateTimeOffset.UtcNow);
            File.Delete(consent); // Single use; controller must recheck environment before preparing another token.
            if (Permissions.Any(permission => context.CheckSelfPermission(permission) != Permission.Granted))
                throw new InvalidDataException("The complete three-record WRITE grant set is required; no child is omitted.");

            // All environment/consent/grant gates precede even obtaining the manager.
            var manager = (HealthConnectManager?)context.GetSystemService(Context.HealthconnectService)
                ?? throw new InvalidOperationException("Health Connect unavailable.");
            if (File.Exists(marker))
            {
                var previous = JsonSerializer.Deserialize<Owned>(await File.ReadAllTextAsync(marker));
                ValidateOwned(previous, plan);
                await DeleteAndVerify(context, manager, plan);
                File.Delete(marker);
                Report("reconciled-own-records-invoke-again", new { plan.PayloadHash });
                return;
            }

            // Persist exact ownership before InsertRecords, including when remote acceptance precedes a crash.
            using (var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new Owned(1, plan.PayloadHash,
                    plan.Records.Select(static item => item.ClientRecordId).ToArray()));
                file.Write(bytes);
                file.Flush(true);
            }
            try
            {
                var built = HealthConnectRecordMapper.BuildIsolatedProviderFixture(plan);
                // Simulate lost local ACK by replaying the same fixture; never write production outbox ACK.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var response = await Invoke<InsertRecordsResponse>(receiver =>
                        manager.InsertRecords(built.Records.ToList(), context.MainExecutor!, receiver));
                    var accepted = AndroidHealthConnectWriter.AcceptedBatch.FromSdkResponse(plan, response);
                    HCDurableAttempt.ValidateReceipts(plan, accepted.Receipts);
                    await ReadOwn(context, manager, plan, true);
                }
            }
            finally
            {
                // Wait for the actual insert callback above; never race timeout cleanup with an active insert.
                await DeleteAndVerify(context, manager, plan);
                File.Delete(marker);
            }
            Report("own-insert-replay-read-delete-absence-pass", new { plan.PayloadHash, ProductionExported = false });
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidDataException
            or InvalidOperationException or Java.Lang.RuntimeException)
        {
            Report("blocked-or-failed", new { Error = error.Message, OwnershipMarkerRetained = File.Exists(marker) });
        }
        finally { Gate.Release(); }

        void Report(string state, object details) => File.WriteAllText(report,
            JsonSerializer.Serialize(new { State = state, Details = details }), new System.Text.UTF8Encoding(false));
    }

    private sealed record Owned(int Version, string FixtureHash, string[] ClientIds);
    private static void ValidateOwned(Owned? owned, HCWritePlanV1 plan)
    {
        if (owned is null || owned.Version != 1 || owned.FixtureHash != plan.PayloadHash || owned.ClientIds is null
            || !owned.ClientIds.SequenceEqual(plan.Records.Select(static item => item.ClientRecordId)))
            throw new InvalidDataException("Unknown ownership marker: refuse read or mutation.");
    }

    private static Java.Lang.Class Type(HCRecordKind kind) => Java.Lang.Class.FromType(kind switch
    {
        HCRecordKind.RunningSession => typeof(ExerciseSessionRecord),
        HCRecordKind.Distance => typeof(DistanceRecord),
        HCRecordKind.HeartRate => typeof(HeartRateRecord),
        _ => throw new InvalidDataException("Unsupported owned record type.")
    });

    private static async Task ReadOwn(Context context, HealthConnectManager manager, HCWritePlanV1 plan, bool present)
    {
        foreach (var record in plan.Records)
        {
            var request = new ReadRecordsRequestUsingIds.Builder(Type(record.Kind)).AddClientRecordId(record.ClientRecordId)!.Build()!;
            var response = await Invoke<ReadRecordsResponse>(receiver => manager.ReadRecords(request, context.MainExecutor!, receiver));
            if (!present && response.Records?.Count != 0) throw new InvalidOperationException("Own-ID absence not verified.");
            if (present && (response.Records?.Count != 1 || response.Records[0] is not Android.Health.Connect.DataTypes.Record own
                || own.Metadata?.ClientRecordId != record.ClientRecordId || own.Metadata?.ClientRecordVersion != record.ClientRecordVersion
                || own.Metadata?.DataOrigin?.PackageName != context.PackageName))
                throw new InvalidOperationException("Own-ID readback/version failed.");
        }
    }

    private static async Task DeleteAndVerify(Context context, HealthConnectManager manager, HCWritePlanV1 plan)
    {
        var filters = plan.Records.Select(static record => RecordIdFilter.FromClientRecordId(Type(record.Kind), record.ClientRecordId)!).ToList();
        await Invoke<Java.Lang.Object>(receiver => manager.DeleteRecords(filters, context.MainExecutor!, receiver));
        await ReadOwn(context, manager, plan, false);
    }

    private static async Task<T> Invoke<T>(Action<IOutcomeReceiver> operation) where T : Java.Lang.Object
    {
        var receiver = new Receiver<T>();
        operation(receiver);
        return await receiver.Completion.Task;
    }

    private sealed class Receiver<T> : Java.Lang.Object, IOutcomeReceiver where T : Java.Lang.Object
    {
        internal TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is T value) Completion.TrySetResult(value);
            else if (result is null && typeof(T) == typeof(Java.Lang.Object)) Completion.TrySetResult(null!);
            else Completion.TrySetException(new InvalidOperationException("Unexpected provider callback shape."));
        }
        public void OnError(Java.Lang.Object? error) => Completion.TrySetException(new InvalidOperationException("Provider rejected the isolated operation."));
    }
}
#endif
