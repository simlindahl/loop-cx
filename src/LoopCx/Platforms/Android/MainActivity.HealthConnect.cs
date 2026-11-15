using Android.Content.PM;

namespace LoopCx;

public sealed partial class MainActivity
{
    private const int HealthWriteRequest = 93;
    private TaskCompletionSource<bool>? healthWriteRequest;

    internal Task<bool> RequestHealthWriteAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(34) || !HealthConnectSettings.Access().Available)
            return Task.FromResult(false);
        if (healthWriteRequest is not null) return healthWriteRequest.Task;
        if (HealthConnectSettings.Access().Missing.Count == 0) return Task.FromResult(true);
        healthWriteRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = healthWriteRequest.Task;
        try { RequestPermissions(HealthConnectSettings.Permissions, HealthWriteRequest); }
        catch (Java.Lang.RuntimeException) { CompleteHealthWrite(); }
        return pending;
    }

    private void CompleteHealthWrite()
    {
        var completion = healthWriteRequest;
        healthWriteRequest = null;
        completion?.TrySetResult(HealthConnectSettings.Access() is { Available: true, Missing.Count: 0 });
        HealthConnectSettings.Refresh();
    }
}
