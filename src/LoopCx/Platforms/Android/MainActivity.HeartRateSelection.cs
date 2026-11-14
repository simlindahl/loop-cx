using Android.App;
using Android.Bluetooth;
using Android.Companion;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Runtime.Versioning;

namespace LoopCx;

internal sealed record HeartRateSelection(string? Address, string? Name, string Status);

public sealed partial class MainActivity
{
    private TaskCompletionSource<HeartRateSelection>? heartRateSelection;
    private HashSet<string> priorSensorAssociations = new(StringComparer.OrdinalIgnoreCase);
    private const int HeartRatePermissionRequest = 91;
    private const int HeartRateChooserRequest = 92;

    internal Task<HeartRateSelection> SelectHeartRateAsync()
    {
        if (heartRateSelection is not null) return heartRateSelection.Task;
        heartRateSelection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = heartRateSelection.Task;
        if (OperatingSystem.IsAndroidVersionAtLeast(31) &&
            (CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) != Permission.Granted ||
             CheckSelfPermission(global::Android.Manifest.Permission.BluetoothScan) != Permission.Granted))
            RequestPermissions([global::Android.Manifest.Permission.BluetoothConnect, global::Android.Manifest.Permission.BluetoothScan], HeartRatePermissionRequest);
        else OpenHeartRateChooser();

        return result;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != HeartRatePermissionRequest || heartRateSelection is null) return;
        if (grantResults.Length > 0 && grantResults.All(static result => result == Permission.Granted)) OpenHeartRateChooser();
        else CompleteHeartRateSelection(new(null, null, "Bluetooth access needed. Runs can continue without heart rate."));
    }

    private void OpenHeartRateChooser()
    {
        try
        {
            var manager = (CompanionDeviceManager?)GetSystemService(CompanionDeviceService);
            if (manager is null) { CompleteHeartRateSelection(new(null, null, "Sensor selection is unavailable on this device.")); return; }
            var filter = new BluetoothLeDeviceFilter.Builder().SetScanFilter(new global::Android.Bluetooth.LE.ScanFilter.Builder()
                .SetServiceUuid(new ParcelUuid(Java.Util.UUID.FromString("0000180d-0000-1000-8000-00805f9b34fb")))!.Build())!.Build();
            var request = new AssociationRequest.Builder().AddDeviceFilter(filter)!.Build();
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var prior = new HashSet<int>();
                foreach (var association in manager.MyAssociations) prior.Add(association.Id);
                manager.Associate(request, MainExecutor!, new SensorChooser(this, prior));
            }
            else
            {
                priorSensorAssociations = manager.Associations.ToHashSet(StringComparer.OrdinalIgnoreCase);
                manager.Associate(request, new LegacySensorChooser(this), null);
            }
        }
        catch (Java.Lang.RuntimeException)
        {
            CompleteHeartRateSelection(new(null, null, "Sensor selection is unavailable. Check Bluetooth and Android access, then try again."));
        }
    }

    private void LaunchHeartRateChooser(IntentSender launcher)
    {
        if (heartRateSelection is null) return;
        try { StartIntentSenderForResult(launcher, HeartRateChooserRequest, null, 0, 0, 0); }
        catch (Exception problem) when (problem is IntentSender.SendIntentException or Java.Lang.RuntimeException)
        { CompleteHeartRateSelection(new(null, null, "Sensor selection could not open. Try again.")); }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != HeartRateChooserRequest) return;
        if (resultCode != Result.Ok) { CompleteHeartRateSelection(new(null, null, "Sensor selection cancelled. Your saved source is unchanged.")); return; }
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var device = data?.GetParcelableExtra(CompanionDeviceManager.ExtraDevice) as BluetoothDevice;
            var address = device?.Address?.ToUpperInvariant();
            if (address is not null && !priorSensorAssociations.Contains(address))
            {
                try { ((CompanionDeviceManager?)GetSystemService(CompanionDeviceService))?.Disassociate(address); }
                catch (Java.Lang.RuntimeException) { CompleteHeartRateSelection(new(null, null, "Android sensor selection cleanup failed. Your saved source is unchanged.")); return; }
            }
            CompleteSelectedSensor(address, device?.Name);
        }
    }

    private void CompleteSelectedSensor(string? address, string? name)
    {
        if (address is null || !BluetoothAdapter.CheckBluetoothAddress(address))
            CompleteHeartRateSelection(new(null, null, "No compatible sensor was selected. Your saved source is unchanged."));
        else CompleteHeartRateSelection(new(address, name, "Selected · connects when a run starts"));
    }

    private void CompleteHeartRateSelection(HeartRateSelection result)
    {
        var completion = heartRateSelection;
        heartRateSelection = null;
        completion?.TrySetResult(result);
    }

    private void CancelHeartRateSelection() => CompleteHeartRateSelection(new(null, null, "Sensor selection cancelled. Your saved source is unchanged."));

    [SupportedOSPlatform("android33.0")]
    private sealed class SensorChooser(MainActivity owner, HashSet<int> prior) : CompanionDeviceManager.Callback
    {
        private readonly WeakReference<MainActivity> activity = new(owner);
        private readonly Context applicationContext = owner.ApplicationContext!;
        public override void OnAssociationPending(IntentSender chooserLauncher)
        { if (activity.TryGetTarget(out var target)) target.LaunchHeartRateChooser(chooserLauncher); }
        public override void OnAssociationCreated(AssociationInfo association)
        {
            if (!prior.Contains(association.Id))
            {
                try { ((CompanionDeviceManager?)applicationContext.GetSystemService(CompanionDeviceService))?.Disassociate(association.Id); }
                catch (Java.Lang.RuntimeException)
                {
                    if (activity.TryGetTarget(out var failed)) failed.CompleteHeartRateSelection(new(null, null, "Android sensor selection cleanup failed. Your saved source is unchanged."));
                    return;
                }
            }
            if (activity.TryGetTarget(out var target)) target.CompleteSelectedSensor(association.DeviceMacAddress?.ToString()?.ToUpperInvariant(), association.DisplayName);
        }
        public override void OnFailure(Java.Lang.ICharSequence? error)
        { if (activity.TryGetTarget(out var target)) target.CompleteHeartRateSelection(new(null, null, "No sensor selected. Enable heart-rate broadcast, keep the sensor nearby and try again.")); }
    }

    [UnsupportedOSPlatform("android33.0")]
    private sealed class LegacySensorChooser(MainActivity owner) : CompanionDeviceManager.Callback
    {
        private readonly WeakReference<MainActivity> activity = new(owner);
        public override void OnDeviceFound(IntentSender chooserLauncher)
        { if (activity.TryGetTarget(out var target)) target.LaunchHeartRateChooser(chooserLauncher); }
        public override void OnFailure(Java.Lang.ICharSequence? error)
        { if (activity.TryGetTarget(out var target)) target.CompleteHeartRateSelection(new(null, null, "No sensor selected. Enable heart-rate broadcast, keep the sensor nearby and try again.")); }
    }
}
