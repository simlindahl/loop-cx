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
public sealed partial class MainActivity : MauiAppCompatActivity
{
    private AndroidMotionPreference? motionPreference;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (global::Android.App.Application.Context.ContentResolver is { } resolver)
            motionPreference = new AndroidMotionPreference(resolver);
        try { _ = RunJournal.Shared; }
        catch (Exception error) when (error is IOException or global::Android.Database.Sqlite.SQLiteException)
        {
            RunService.ReportStorageOpenFailure();
        }
#if DEBUG
        HandleDebugCommand(Intent);
#endif
    }

    protected override void OnResume()
    {
        base.OnResume();
        AndroidMotionPreference.Refresh();
        if (RunService.Snapshot is { Phase: not "Finished" }) RunService.Command("refresh-sensors");
    }

    protected override void OnDestroy()
    {
        CancelHeartRateSelection();
        motionPreference?.Dispose();
        motionPreference = null;
        base.OnDestroy();
    }

    public override bool OnKeyDown(global::Android.Views.Keycode keyCode, global::Android.Views.KeyEvent? e)
    {
        if (keyCode == global::Android.Views.Keycode.VolumeUp && RunService.Snapshot?.Phase is "Warmup" or "Cooldown")
        {
            if (e?.RepeatCount == 0) RunService.Command("extend");

            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

#if DEBUG
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleDebugCommand(intent);
    }

    private void HandleDebugCommand(Intent? intent)
    {
        var archiveSeed = intent?.GetStringExtra("loop-ghost-archive-seed");
        intent?.RemoveExtra("loop-ghost-archive-seed");
        if (archiveSeed is not null && Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64")
        {
            if (Guid.TryParse(archiveSeed, out var seed)) _ = Task.Run(() => NativeGhostArchiveProbe.Run(seed));
            return;
        }
        var exportRun = intent?.GetStringExtra("loop-export-json-run");
        intent?.RemoveExtra("loop-export-json-run");
        if (exportRun is not null && Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64")
        {
            var exportDirectory = global::Android.App.Application.Context.FilesDir!.AbsolutePath;
            // Test the same synchronous backend on a worker while normal service callbacks continue.
            _ = Task.Run(() =>
            {
                try
                {
                    if (!Guid.TryParse(exportRun, out var exportId)) throw new ArgumentException("Invalid run identity.");
                    var json = RunJournal.Shared.ExportJson(exportId);
                    File.WriteAllText(Path.Combine(exportDirectory, $"run-export-{exportId}.json"), json, new System.Text.UTF8Encoding(false));
                    global::Android.Util.Log.Info("LoopRun", "Local JSON test snapshot saved privately.");
                }
                catch (Exception problem) when (problem is ArgumentException or KeyNotFoundException or IOException or System.Text.Json.JsonException)
                {
                    global::Android.Util.Log.Warn("LoopRun", "Local JSON test snapshot failed.");
                }
            });
            return;
        }
        var hrConfiguration = intent?.GetStringExtra("loop-heart-rate-configuration");
        intent?.RemoveExtra("loop-heart-rate-configuration");
        // Test setup only. It feeds the same production preferences/start/source path, never a fake BPM callback.
        if (hrConfiguration is not null && Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64" &&
            RunService.Snapshot is null)
        {
            try
            {
                HeartRatePreferences.Save(System.Text.Json.JsonSerializer.Deserialize<LoopCx.Features.ActiveRun.HeartRateConfiguration>(hrConfiguration)
                    ?? throw new ArgumentException("Missing heart-rate configuration."));
            }
            catch (Exception error) when (error is ArgumentException or System.Text.Json.JsonException)
            {
                global::Android.Util.Log.Warn("LoopRun", "Heart-rate test configuration rejected.");
            }
        }
        var probe = intent?.GetStringExtra("loop-native-probe");
        intent?.RemoveExtra("loop-native-probe");
        if (probe == "storage-corruption")
        {
            RunSaveCorruptionProbe.Run(this);
            return;
        }
        if (probe == "health-outbox")
        {
            HealthConnectOutboxProbe.Run(this);
            return;
        }
        if (probe == "health-records" && OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            HealthConnectRecordMapper.RunIsolatedProbe(this);
            return;
        }
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
        if (command == "hr-source-failure" && Build.Hardware == "ranchu" && Build.Product == "sdk_gphone64_x86_64")
            RunService.Command(command);
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
