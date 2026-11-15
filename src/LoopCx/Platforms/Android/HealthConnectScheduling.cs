namespace LoopCx;

// Existing durable queue + process gate; no timer, worker or service is added.
internal static class HealthConnectScheduling
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static int requested;
    internal static void Resume()
    {
        Interlocked.Exchange(ref requested, 1);
        _ = DrainAsync();
    }

    internal static async Task<string> RetryAsync(Guid runId)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(34)) return "unavailable";
        var result = await HealthConnectExportDispatch.DispatchAsync(runId, global::Android.App.Application.Context);
        HealthConnectSettings.Refresh();
        return result;
    }

    private static async Task DrainAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(34) || !HealthConnectSettings.Load().Enabled || !await Gate.WaitAsync(0)) return;
        try
        {
            while (Interlocked.Exchange(ref requested, 0) != 0 && HealthConnectSettings.Load().Enabled)
            {
                var runs = await Task.Run(() => RunJournal.Shared.ReadPendingHealthConnectRuns());
                foreach (var id in runs)
                {
                    if (!HealthConnectSettings.Load().Enabled) break;
                    await RetryAsync(id);
                }
            }
        }
        catch (Exception problem) when (problem is IOException or global::Android.Database.Sqlite.SQLiteException
            or System.Text.Json.JsonException or Java.Lang.RuntimeException or InvalidOperationException)
        {
            global::Android.Util.Log.Warn("LoopRun", "Health Connect queue remains local and retryable.");
        }
        finally
        {
            Gate.Release();
            if (Volatile.Read(ref requested) != 0 && HealthConnectSettings.Load().Enabled) _ = DrainAsync();
        }
    }
}
