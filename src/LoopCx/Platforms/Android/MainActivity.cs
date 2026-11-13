using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace LoopCx;

[Activity(Name = "se.loopcx.running.MainActivity", Theme = "@style/Maui.MainTheme.NoActionBar",
    MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _ = RunJournal.Shared;
#if DEBUG
        HandleDebugCommand(Intent);
#endif
    }

#if DEBUG
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleDebugCommand(intent);
    }

    private void HandleDebugCommand(Intent? intent)
    {
        var probe = intent?.GetStringExtra("loop-native-probe");
        intent?.RemoveExtra("loop-native-probe");
        if (probe == "health-rationale" && OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            StartActivity(new Intent(this, typeof(HealthRationaleActivity)));
            return;
        }
        if (probe == "health-permission" && OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            if (Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64")
                RequestPermissions(["android.permission.health.WRITE_EXERCISE"], 83);
            return;
        }
        if (probe == "health-connect" && OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            _ = HealthConnectProbe.Run(this);
            return;
        }
        if (probe is "ble" or "speech")
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
            if (probe == "speech") RunService.Probe("speech", RunService.Snapshot?.Id);
            else if (CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect) != Permission.Granted ||
                CheckSelfPermission(global::Android.Manifest.Permission.BluetoothScan) != Permission.Granted)
                RequestPermissions([global::Android.Manifest.Permission.BluetoothScan, global::Android.Manifest.Permission.BluetoothConnect], 81);
            else
            {
                try { SelectHeartRateDevice(); }
                catch (Java.Lang.RuntimeException) { NativeFeasibility.Report("ble:chooser-unavailable"); }
            }
            return;
        }

        var command = intent?.GetStringExtra("loop-command");
        if (command is "start" or "start-c2" or "pause" or "resume" or "extend" or "skip" or "complete")
            RunService.Command(command);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    private void SelectHeartRateDevice()
    {
        var manager = (global::Android.Companion.CompanionDeviceManager?)GetSystemService(CompanionDeviceService);
        var filter = new global::Android.Companion.BluetoothLeDeviceFilter.Builder()
            .SetScanFilter(new global::Android.Bluetooth.LE.ScanFilter.Builder()
                .SetServiceUuid(new ParcelUuid(Java.Util.UUID.FromString("0000180d-0000-1000-8000-00805f9b34fb")))!.Build())!.Build();
        var request = new global::Android.Companion.AssociationRequest.Builder().AddDeviceFilter(filter)!.Build();
        if (manager is null) { NativeFeasibility.Report("ble:chooser-unavailable"); return; }
        if (RunService.Snapshot is not { } snapshot) { NativeFeasibility.Report("ble:requires-active-session"); return; }
        var existing = manager.MyAssociations.Select(association => association.Id).ToHashSet();
        try { manager.Associate(request, MainExecutor!, new Association(this, snapshot.Id, existing)); }
        catch (Java.Lang.RuntimeException) { NativeFeasibility.Report("ble:chooser-permission-or-platform-unavailable"); }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    private sealed class Association(MainActivity activity, Guid runId, HashSet<int> existing) : global::Android.Companion.CompanionDeviceManager.Callback
    {
        public override void OnAssociationPending(IntentSender chooserLauncher) =>
            activity.StartIntentSenderForResult(chooserLauncher, 82, null, 0, 0, 0);

        public override void OnAssociationCreated(global::Android.Companion.AssociationInfo associationInfo)
        {
            var address = associationInfo.DeviceMacAddress?.ToString();
            var ownedId = existing.Contains(associationInfo.Id) ? -1 : associationInfo.Id;
            if (address is not null && RunService.Snapshot?.Id == runId)
                RunService.Probe("ble", runId, address, ownedId);
            else if (ownedId >= 0)
                NativeFeasibility.ReleaseAssociation(activity, ownedId);
        }

        public override void OnFailure(Java.Lang.ICharSequence? error) => NativeFeasibility.Report("ble:selection-cancelled-or-failed");
    }
#endif
}
