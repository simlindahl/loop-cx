using Android.Content;
using System.Runtime.Versioning;

namespace LoopCx;

// Explicit future integration entry only; no startup, timer, UI or background-worker trigger is installed.
[SupportedOSPlatform("android34.0")]
internal static class HealthConnectExportDispatch
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<string> DispatchAsync(Guid runId, bool integrationEnabled, Context context)
    {
        if (!integrationEnabled) return "disabled";
        if (!await Gate.WaitAsync(0)) return "busy";
        try
        {
            var store = new AndroidHealthConnectOutbox(RunJournal.Shared);
            var claim = await Task.Run(() => store.PrepareProvider(runId));
            if (claim.Attempt is not { } attempt) return claim.State;
            AndroidHealthConnectWriter.Result result;
            try
            {
                // This bounds local ownership only. The remote operation may still complete after timeout.
                result = await new AndroidHealthConnectWriter(context.ApplicationContext!).WriteAsync(attempt.FrozenPlan)
                    .WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                await Task.Run(() => store.Fail(runId, attempt.Token, attempt.FrozenPlan.PayloadHash,
                    "No provider callback arrived within the local wait window; remote acceptance is unknown.", false));
                return "pending";
            }
            if (result.Acknowledgement is { } acknowledged)
            {
                var accepted = await Task.Run(() => store.CompleteProvider(runId, attempt.Token, attempt.FrozenPlan.PayloadHash, acknowledged));
                return accepted ? "exported" : "stale_or_action_required";
            }
            await Task.Run(() => store.Fail(runId, attempt.Token, attempt.FrozenPlan.PayloadHash,
                result.Error ?? "The write was not acknowledged.", result.State is "action_required" or "disabled_synthetic"));
            return result.State;
        }
        catch (Exception problem) when (problem is IOException or System.Text.Json.JsonException or InvalidDataException
            or Android.Database.Sqlite.SQLiteException)
        {
            // An accepted remote write may precede this failed local ACK. Leave the frozen attempt retryable.
            return "pending";
        }
        finally { Gate.Release(); }
    }
}
