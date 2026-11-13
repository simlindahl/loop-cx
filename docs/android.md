<a id="kör-första-android-bygget-på-windows"></a>
# Run the first Android build on Windows

Install .NET SDK 10.0.100 (x64), JDK 21 and Android Studio. Set JAVA_HOME to your JDK and ANDROID_HOME to your Android SDK. Install Android SDK Platform 36, Build Tools 36.0.0 and Platform Tools in SDK Manager. Add platform-tools to PATH. Create a Pixel emulator with Google APIs API 35 and x86_64 in Device Manager and start it.

Run in PowerShell from the repository root, with the emulator as the only connected device.

```powershell
adb devices
dotnet workload install maui-android
dotnet build src/LoopCx/LoopCx.csproj -t:Run -c Debug -p:RuntimeIdentifier=android-x64
```

The home screen should show **Start a run**. This verifies the path from Android startup to a rendered Razor component. GitHub Actions builds a debug-signed x64 APK and tests the same heading through Android's accessibility tree. The APK is available in the run's artifacts.

This first check covers app startup. GPS with a locked screen, WHOOP over BLE, audio and Health Connect are tracked in issue #1. The Windows commands also still need to be tested on Windows.

## USB phone installation

After a valuable feature merge, the lead developer offers Simon installation of the merged app over USB under [AGENTS.md](../AGENTS.md#phone-installation-offers). Offer the new behavior to try and identify outstanding physical acceptance. Installation is optional and requires Simon to accept the offer; it does not block merge unless a physical test is itself mandatory acceptance.

After acceptance, ask Simon to connect and unlock the phone, enable USB debugging and approve the computer if needed. Verify the intended device with `adb devices -l` and select its serial explicitly when multiple devices are connected. Build or select an APK for the merged commit and the phone's actual ABI; the x64 emulator artifact is not a phone build. Use the existing package identity and a compatible signing key, and install with `adb -s <serial> install -r <apk-path>` to retain app data. If signing or version compatibility prevents an update, report the blocker; do not uninstall, clear data or downgrade without separate consent. Confirm the installed version and app startup on the phone, and report physical test results separately from emulator evidence.

## Android session scaffold

The service-backed scaffold starts the C1 timed protocol, records GPS updates and supports pause/resume and preparation commands. The emulator smoke test verifies one session through duplicate starts, screen lock and reopening, then completion and a second workout in the same process. Completion retires the service and restores the start action. The debug-only completion command accelerates this regression; release builds do not expose it. This is a functional scaffold, not the approved final UI or a physically verified GPS/BLE/audio/Health Connect delivery.

<a id="sessionsjournal-och-återhämtning"></a>
## Session journal and recovery
The service saves a SQLite checkpoint for each published state: stable ID, UTC start,
latest observation, monotonic elapsed time, original plan, current plan (including
extensions), completed phases and active counters. GPS observations are saved with
UTC, monotonic timestamp and accuracy. This is raw data, not calculated distance.

On first opening after process death, mark a remaining recording as `interrupted`.
Do not fill in missing time or distance or resume the session automatically.
The recorded part remains for future history views; recovery UI is not included yet.
Write finalization and one unique export queue entry in the same transaction. No exporter exists yet.

Debug uses `runs-debug.db`; release uses `runs.db`. Debug queue entries are always
`disabled_synthetic`; no Health Connect export is implemented or enabled.
The SQLite test in `scripts/android-smoke.sh` uses force-stop, copies
the database through run-as and compares the checkpoint byte for byte after restart.
It also checks preserved GPS rows and atomic finalization with export disabled.
The debug command `complete` accelerates the session to completion and is absent from release.

Outstanding: history/recovery view, frozen heart-rate zones, heart-rate data, favourites, sensor quality,
storage errors in the UI and Health Connect retries. Physical Pixel/WHOOP tests
and Windows tests have not run.

<a id="tidsstämplade-manuella-ändringar-schema-2"></a>
## Timestamped manual changes (schema 2)
Database version 2 adds `run_events` without rewriting earlier measurements.
Save pause, resume, +1 min and manual phase transitions in the same transaction
as the post-command checkpoint. Each event includes UTC, monotonic elapsed
time, active time, phase and plan. Repeated pause/resume commands and phase actions
that do not apply to the current phase create no false events. Two taps on +1 min
are, however, two valid changes. Start and finish are also logged.

Events are also preserved exactly after process death. Older schema 1 sessions have
no complete pause log; history must not fabricate one afterwards.
Pause status and event timestamps distinguish elapsed
pause time from active time even if the wall clock changes during the run.

`scripts/android-smoke.sh` replaces only the emulator's **debug database** with a
synthetic schema 1 fixture before testing. Run it only in a dedicated test emulator.
The test verifies migration without data loss, effective command order, duplicates,
phase changes and an unchanged log after process death. The release database is unaffected.
