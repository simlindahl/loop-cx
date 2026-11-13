#if DEBUG
using Android.Content;
using Android.Content.PM;
using Android.Health.Connect;
using Android.Health.Connect.DataTypes;
using Android.OS;
using System.Runtime.Versioning;

namespace LoopCx;

// No RunJournal/export-queue linkage. Only a dedicated disposable emulator may write.
[SupportedOSPlatform("android35.0")]
internal static class HealthConnectProbe
{
    private const string Permission = "android.permission.health.WRITE_EXERCISE";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task Run(Context context)
    {
        if (!await Gate.WaitAsync(0)) return;
        try
        {
            if (Build.Hardware != "ranchu" || Build.Product != "sdk_gphone64_x86_64" ||
                Build.Model != "sdk_gphone64_x86_64" || Build.SupportedAbis?.FirstOrDefault() != "x86_64" ||
                Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) != true)
            {
                Report("blocked-non-allowlisted-hardware");
                return;
            }

            var optIn = Path.Combine(context.FilesDir!.AbsolutePath, "hc-isolated-probe.optin");
            if (!File.Exists(optIn) || await File.ReadAllTextAsync(optIn) != "emulator-5580|LOOP_Cx_QA_API35_193fb70|accounts=0|whoop=absent")
            {
                Report("blocked-no-harness-opt-in");
                return;
            }

            File.Delete(optIn); // One use; the harness must independently re-verify before another attempt.
            var manager = (HealthConnectManager?)context.GetSystemService(Context.HealthconnectService);
            if (manager is null) { Report("unavailable"); return; }
            if (context.CheckSelfPermission(Permission) != Android.Content.PM.Permission.Granted) { Report("permission-denied"); return; }
            await OwnRecordRoundTrip(context, manager);
        }
        catch (Exception exception) when (exception is Java.Lang.RuntimeException or IOException or InvalidOperationException or TimeoutException)
        {
            Report("failed:" + exception.GetType().Name);
        }
        finally { Gate.Release(); }
    }

    private static async Task OwnRecordRoundTrip(Context context, HealthConnectManager manager)
    {
        var type = Java.Lang.Class.FromType(typeof(ExerciseSessionRecord));
        var ownedPath = Path.Combine(context.FilesDir!.AbsolutePath, "hc-probe-owned-id");
        if (File.Exists(ownedPath))
        {
            var existing = await File.ReadAllTextAsync(ownedPath);
            if (!existing.StartsWith("loop-isolated-probe-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(existing["loop-isolated-probe-".Length..], "N", out _))
                throw new InvalidOperationException("Unknown owned probe marker; refuse mutation.");
            await Cleanup(context, manager, type, existing, null);
            Report("reconciled-previous-probe;invoke-again-for-new-test");
            return;
        }
        var clientId = "loop-isolated-probe-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(ownedPath, clientId);
        var now = Java.Time.Instant.Now()!;
        var metadata = new Metadata.Builder().SetClientRecordId(clientId)!.SetClientRecordVersion(1)!.Build()!;
        var record = new ExerciseSessionRecord.Builder(metadata, now.MinusSeconds(60)!, now,
            ExerciseSessionTypeEnum.Running)!.SetTitle("Isolated LOOP compatibility test")!.Build()!;
        var executor = context.MainExecutor!;
        string? returnedId = null;
        try
        {
            var inserted = await Invoke<InsertRecordsResponse>(receiver => manager.InsertRecords([record], executor, receiver));
            returnedId = inserted.Records?.SingleOrDefault()?.Metadata?.Id;
            if (string.IsNullOrEmpty(returnedId)) throw new InvalidOperationException("Insert returned no own record ID.");
            var request = new ReadRecordsRequestUsingIds.Builder(type).AddId(returnedId)!.Build()!;
            var read = await Invoke<ReadRecordsResponse>(receiver => manager.ReadRecords(request, executor, receiver));
            if (read.Records?.Count != 1 || (read.Records[0] as ExerciseSessionRecord)?.Metadata?.ClientRecordId != clientId)
                throw new InvalidOperationException("Own record readback failed.");
            Report("own-insert-read-pass");
        }
        finally
        {
            await Cleanup(context, manager, type, clientId, returnedId);
        }
    }

    private static async Task Cleanup(Context context, HealthConnectManager manager, Java.Lang.Class type, string clientId, string? returnedId)
    {
        var executor = context.MainExecutor!;
        try
        {
            // Client ID is already unique and owned even when insert response delivery fails.
            var filter = returnedId is null ? RecordIdFilter.FromClientRecordId(type, clientId)! : RecordIdFilter.FromId(type, returnedId)!;
            await Invoke<Java.Lang.Object>(receiver => manager.DeleteRecords([filter], executor, receiver));
            var request = new ReadRecordsRequestUsingIds.Builder(type).AddClientRecordId(clientId)!.Build()!;
            var remaining = await Invoke<ReadRecordsResponse>(receiver => manager.ReadRecords(request, executor, receiver));
            if (remaining.Records?.Count != 0) throw new InvalidOperationException("Own probe cleanup not verified.");
            File.Delete(Path.Combine(context.FilesDir!.AbsolutePath, "hc-probe-owned-id"));
            Report("own-delete-absence-pass");
        }
        catch (Exception exception) when (exception is Java.Lang.RuntimeException or InvalidOperationException or TimeoutException)
        {
            Report("CLEANUP-FAILED:" + exception.GetType().Name);
        }
    }

    private static async Task<T> Invoke<T>(Action<IOutcomeReceiver> call) where T : Java.Lang.Object
    {
        var receiver = new Result<T>();
        call(receiver);
        // Do not time out a write and race cleanup against an insert still in flight.
        // The harness bounds observation; a pending callback is never a cleanup pass.
        return await receiver.Completion.Task;
    }

    private static void Report(string state) => global::Android.Util.Log.Info("LoopHealthProbe", state);

    private sealed class Result<T> : Java.Lang.Object, IOutcomeReceiver where T : Java.Lang.Object
    {
        internal TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is T typed) Completion.TrySetResult(typed);
            else if (result is null && typeof(T) == typeof(Java.Lang.Object))
                Completion.TrySetResult(null!); // Android OutcomeReceiver<Void> succeeds with null; caller ignores this value.
            else Completion.TrySetException(new InvalidOperationException("Unexpected native result."));
        }
        public void OnError(Java.Lang.Object? error) => Completion.TrySetException(new InvalidOperationException("Health Connect operation rejected."));
    }
}
#endif
