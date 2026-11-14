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

            // Refuse unresolved/old markers before obtaining the manager, even with fresh approval.
            if (File.Exists(marker)) ReadOwned(marker).RequireSettled(plan);
            // All environment/consent/grant gates precede even obtaining the manager.
            var manager = (HealthConnectManager?)context.GetSystemService(Context.HealthconnectService)
                ?? throw new InvalidOperationException("Health Connect unavailable.");
            if (File.Exists(marker))
            {
                ReadOwned(marker).RequireSettled(plan);
                await DeleteAndVerify(context, manager, plan);
                File.Delete(marker);
                Report("reconciled-own-records-invoke-again", new { plan.PayloadHash });
                return;
            }

            try
            {
                var built = HealthConnectRecordMapper.BuildIsolatedProviderFixture(plan);
                // Simulate lost local ACK by replaying the same fixture; never write production outbox ACK.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var operation = attempt + 1;
                    WriteOwned(marker, new(2, plan.PayloadHash, plan.Records.Select(static item => item.ClientRecordId).ToArray(),
                        operation, "insert_pending", null));
                    var response = await Invoke<InsertRecordsResponse>(receiver =>
                        manager.InsertRecords(built.Records.ToList(), context.MainExecutor!, receiver), success =>
                        {
                            var current = ReadOwned(marker);
                            WriteOwned(marker, current.ObserveCallback(operation, success, DateTimeOffset.UtcNow));
                        });
                    var accepted = AndroidHealthConnectWriter.AcceptedBatch.FromSdkResponse(plan, response);
                    HCDurableAttempt.ValidateReceipts(plan, accepted.Receipts);
                    await ReadOwn(context, manager, plan, true);
                }
            }
            finally
            {
                // A locally faulted task or process restart is not terminal provider evidence.
                ReadOwned(marker).RequireSettled(plan);
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

    private static HCVerificationOwnership ReadOwned(string path) =>
        JsonSerializer.Deserialize<HCVerificationOwnership>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Missing ownership evidence.");

    private static void WriteOwned(string path, HCVerificationOwnership owned)
    {
        // A torn write fails JSON/state validation on re-entry; it must never become a cleanup pass.
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        file.Write(JsonSerializer.SerializeToUtf8Bytes(owned));
        file.Flush(true);
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

    private static async Task<T> Invoke<T>(Action<IOutcomeReceiver> operation, Action<bool>? terminalCallback = null) where T : Java.Lang.Object
    {
        var receiver = new Receiver<T>(terminalCallback);
        operation(receiver);
        return await receiver.Completion.Task;
    }

    private sealed class Receiver<T>(Action<bool>? terminalCallback) : Java.Lang.Object, IOutcomeReceiver where T : Java.Lang.Object
    {
        internal TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result)
        {
            if (!Observe(true)) return;
            if (result is T value) Completion.TrySetResult(value);
            else if (result is null && typeof(T) == typeof(Java.Lang.Object)) Completion.TrySetResult(null!);
            else Completion.TrySetException(new InvalidOperationException("Unexpected provider callback shape."));
        }
        public void OnError(Java.Lang.Object? error)
        {
            if (Observe(false)) Completion.TrySetException(new InvalidOperationException("Provider rejected the isolated operation."));
        }
        private bool Observe(bool success)
        {
            try { terminalCallback?.Invoke(success); return true; }
            catch (Exception error) when (error is IOException or JsonException or InvalidDataException)
            {
                Completion.TrySetException(error);
                return false;
            }
        }
    }
}
#endif
