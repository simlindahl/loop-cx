<a id="kör-första-android-bygget-på-windows"></a>
# Run the first Android build on Windows

Install .NET SDK 10.0.100 (x64), JDK 21 and Android Studio. Set JAVA_HOME to your JDK and ANDROID_HOME to your Android SDK. Install Android SDK Platform 36, Build Tools 36.0.0 and Platform Tools in SDK Manager. Add platform-tools to PATH. Create a Pixel emulator with Google APIs API 35 and x86_64 in Device Manager and start it.

Run in PowerShell from the repository root after selecting the pinned SDK. Set ANDROID_HOME and JAVA_HOME to the actual Android SDK and JDK21 directories. Select the dedicated emulator explicitly even when a phone is connected.

```powershell
dotnet --version # Must match global.json: 10.0.100
dotnet workload install maui-android
dotnet build src/LoopCx/LoopCx.csproj -c Debug `
  -p:AndroidPackageFormats=apk -p:RuntimeIdentifier=android-x64 `
  "-p:AndroidSdkDirectory=$env:ANDROID_HOME" "-p:JavaSdkDirectory=$env:JAVA_HOME"
$adb = Join-Path $env:ANDROID_HOME 'platform-tools/adb.exe'
& $adb devices -l
$emulatorSerial = 'emulator-5580' # Verify the dedicated AVD first; never substitute the phone.
& $adb -s $emulatorSerial install -r 'src/LoopCx/bin/Debug/net10.0-android36.0/android-x64/se.loopcx.running-Signed.apk'
& $adb -s $emulatorSerial shell am start -W -n se.loopcx.running/.MainActivity
```

The home screen should show **Start a run**. This verifies the path from Android startup to a rendered Razor component. GitHub Actions builds a debug-signed x64 APK and tests the same heading through Android's accessibility tree. The APK is available in the run's artifacts.

This first check covers app startup. GPS with a locked screen, WHOOP over BLE, audio and Health Connect are tracked in issue #1. Windows strict x64 build, dedicated API35 emulator startup and the existing strong synthetic service/SQLite regression passed on 13 November 2026 at `193fb70909737011016a50e8bce5ee1a1980e2d7`; see the bounded native feasibility section below.

## USB phone installation

After a valuable feature merge, the team lead offers Simon installation of the merged app over USB under [AGENTS.md](../AGENTS.md#phone-installation-offers). Offer the new behavior to try and identify outstanding physical acceptance. Installation is optional and requires Simon to accept the offer; it does not block merge unless a physical test is itself mandatory acceptance.

After acceptance, ask Simon to connect and unlock the phone, enable USB debugging and approve the computer if needed. Verify the intended device with `adb devices -l` and select its serial explicitly when multiple devices are connected. Build or select an APK for the merged commit and the phone's actual ABI; the x64 emulator artifact is not a phone build. Use the existing package identity and a compatible signing key, and install with `adb -s <serial> install -r <apk-path>` to retain app data. If signing or version compatibility prevents an update, report the blocker; do not uninstall, clear data or downgrade without separate consent. Confirm the installed version and app startup on the phone, and report physical test results separately from emulator evidence.

Before updating, verify signing from actual artifacts rather than an assumed debug-key path:

1. Read the installed package path with `adb -s <serial> shell pm path <package>`, pull that APK to the existing local artifact directory and inspect its public certificate with `apksigner verify --print-certs <installed-apk>`. Compare any earlier delivery artifact separately.
2. Inspect the public certificate of the existing candidate keystore with `keytool -list`; use the normal secure credential mechanism. It must match the installed certificate. Do not create, replace or export a key to make an update work. Report a mismatch and preserve the installed app.
3. Build the actual merged source for the device ABI. Verify the final, separately identified APK with `apksigner verify --print-certs <final-apk>` and calculate its SHA-256. The final certificate must match both the installed APK and the existing key. An incrementally generated `-Signed.apk` is not proof: stale signing output may survive changed build properties. If necessary, sign a separate output explicitly using the verified existing key, then verify that output again.
4. Record the source commit/tree, exact local build/package command, ABI, final APK path/hash, public certificate fingerprint, results and limitations in the existing local artifact manifest. Preserve failed attempts as diagnostics and distinguish them from the installed package. Keep private key material, credentials and personal key paths out of Git; omit credentials from recorded commands and logs too.

Immediately before installation, check this package's own RunService, including paused recordings. Defer if active. Where accessible, record only the own database and sidecar file presence, sizes and hashes before installation and again after installation **before launch**; do not extract raw session/GPS contents for this check. Install the verified package with `install -r`, then verify the installed version, standard icon and startup. If the phone is locked, wait for Simon to unlock it; do not bypass the lock or change launcher settings. Do not start a workout merely to verify an icon update. Record themed-launcher behavior, sensors, audio and other untested physical acceptance separately. Link the result from the [current PR handoff](team-workflow.md#current-review-handoff).

## Android session scaffold

The service-backed scaffold starts the C1 timed interval protocol or the approved C2 continuous protocol, records GPS updates and supports pause/resume and preparation commands. The emulator smoke test verifies one session through duplicate starts, screen lock and reopening, then completion and a second workout in the same process. Completion retires the service and restores the start action. The debug-only completion command accelerates this regression; release builds do not expose it. This is a functional scaffold, not the approved final UI or a physically verified GPS/BLE/audio/Health Connect delivery.

## Core start and C1 readable flow slice

The Core start and C1 scaffold use the approved neutral Basalt tokens and the gallery's locally bundled Space Grotesk font. Native MAUI CSS globs are disabled for this Blazor host so `.razor.css` is processed and packaged as the linked `LoopCx.styles.css`; the build checks that actual APK asset and the bundled font license before starting emulator tests. The start card, phase heading, min:sec countdown, GPS status and active time show only existing service state. Visible Start, Pause/Resume, +1 min, Start main run and Finish actions retain existing command and permission behavior. Controls have a 48 CSS-pixel minimum target and wrap rather than hiding actions. C2 starts one 45-minute work phase at the approved target RPE 3, with no warm-up or cool-down. Zone 2 is intent, not measured guidance. Favourites management, additional statistics and preliminary detailed training layouts remain outside this slice. Both starts retain the existing location-permission prerequisite; timed recording without that permission remains an outstanding delivery gap.

The MAUI ContentPage explicitly applies container safe-area insets for system bars and cutouts ([MAUI safe areas](https://learn.microsoft.com/dotnet/maui/user-interface/safe-area?view=net-maui-10.0)); CSS does not guess a status-bar height. The Android WebView text zoom follows the current system font scale when its handler is created; activity recreation retains the service-owned recording. This is a bounded readability slice, not completion of issue #3 or approval of a full runtime design.

After the stronger service regression, `scripts/android-ui-smoke.py` runs only in a dedicated emulator. It uses real accessible Start/Pause/Resume controls, checks 48 dp bounds using emulator density, verifies one session across reopening, and captures offline/normal/large-system-text screenshots, accessibility XML and a source/environment manifest under `verification/c1-ui`. It restores font scale and connectivity in a finally block. At large text it scrolls controls into view before asserting unchanged 48 dp targets. UI capture failures have a bounded retry with screenshots and error/crash diagnostics, and the manifest distinguishes the PR head/base from the actual checked-out source and tree. The large-text check requires the accessible one-line Paused heading to grow meaningfully after font scale 2.0, not just a changed setting. Review screenshots for insets and wrapping; the bounds check does not claim an exact doubling of every glyph. Bundled font presence and an offline screenshot do not independently prove which font the WebView loaded. TalkBack, physical outdoor readability and sensor acceptance remain separate checks. Local ARM64 build evidence does not replace emulator acceptance.

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
remain separate physical acceptance; Windows foundation build/startup and strong synthetic checks passed at the revision recorded above.

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

## Frozen Core template metadata (schema 3)

A nullable `runs.template_metadata` JSON column freezes TemplateId, Source, Category, TemplateRevision and IntendedHeartRateZone when a session starts. C1 and C2 share the existing session, command and journal path. Duplicate starts, including a different template, do not replace the running plan or metadata. The stored `initial_plan` remains the original phase-array format and checkpoint schema remains 1. Migration from database schema 1 or 2 preserves old rows with NULL metadata; it does not invent historical template identity. Events retain their existing columns and finalization/export-queue transaction.

## Launcher icon

The [approved launcher decision](../design/decision.md#android-launcher-icon--13-november-2026) identifies the source artwork and approval. MAUI generates density-specific and adaptive Android resources from `Resources/AppIcon/appicon.svg` and `appiconfg.svg`. Both layers use the approved 108-unit canvas with foreground scale 1; Android supplies the launcher mask. The pinned MAUI generator also emits a monochrome entry referencing the same foreground silhouette; no unsupported separate monochrome-file setting is used. Keep the mark inside its existing safe area rather than padding it again.

Verify generated resources and the installed standard icon after rebuilding. Do not uninstall or clear app data to refresh launcher artwork. Standard icon checks do not establish themed-launcher behavior; do not change personal launcher settings during verification. The earlier icon-only delivery did not fix the functional scaffold or its status-bar overlap. The current Core readability slice applies host safe-area insets; its runtime screenshot evidence must verify the result.

## Bounded native feasibility (#1)

Windows foundation evidence used SDK10.0.100/workload10.0.100, JDK21.0.8, Android platform36 and official emulator37.2.13/API35 Google APIs x86_64 image revision9. WHPX was already usable; no driver/security change was made. The isolated AVD booted and the unchanged strong synthetic GPS, service, SQLite, interruption and event checks passed. This Windows result complements the Linux Actions emulator; neither proves a real sensor or outdoor behavior.

Reproduce the Windows x64 build with the pinned SDK, `-c Debug -p:AndroidPackageFormats=apk -p:RuntimeIdentifier=android-x64`, and explicit `AndroidSdkDirectory`/`JavaSdkDirectory` pointing to the installed Android SDK and JDK21. Start the dedicated AVD, verify its serial and use `adb -s <verified-emulator-serial> install -r <x64-apk>` then `adb -s <verified-emulator-serial> shell am start -W -n se.loopcx.running/.MainActivity`. Never use an unqualified `-t:Run` or ADB command when a personal phone is also connected. Exact local commands, paths, hashes and source/tree evidence remain in the private artifact manifest.

Debug native probes enter through MainActivity's `loop-native-probe` extra. `ble` requests standard Nearby devices permission and, on a subsequent explicit invocation after permission grant, the OS Companion Device Manager Heart Rate chooser. Select/cancel deliberately. An existing active synthetic session is required; probes do not create hidden recordings. The service owns GATT notifications, stale/unknown observations, bounded selected-device reconnects and disposal. A probe-created OS association is removed on stop; existing associations are preserved. `speech` explicitly requests one English TTS phrase through transient audio focus; it never changes music playback/volume. These are diagnostic bindings, not finished coaching or sensor UI.

The approved offline **About this test** page is available from the protected Android Health Connect rationale alias. `health-permission` opens the OS exercise-write permission flow only on the allowlisted debug emulator; `health-rationale` opens the same informational page for verification. Back finishes its activity to the caller. `health-connect` performs no operation without the dedicated hardware guard, available manager, write permission and a single-use private opt-in supplied by `scripts/android-native-smoke.py`. The harness verifies exact serial `emulator-5580`, AVD `LOOP_Cx_QA_API35_193fb70`, API35/ranchu/x86_64, no accounts and no WHOOP installation. It reads/deletes only its unique client/returned record IDs; no broad history access or broad deletion is used. API34+ platform binding feasibility does not promise universal supported-device production export.

Use `python scripts/android-native-smoke.py --mode denied --out <private-artifact-directory>` after denying the OS permission, and `--mode roundtrip` only after granting it in this isolated AVD. Every ADB operation in the harness fixes the serial. A pending native callback is not cleanup success: retain the isolated emulator and reconcile only the probe's owned record, rather than declaring absence while an insert is still in flight. Normal journal synthetic export remains disabled and no exporter/retry worker is added. No health probe may write on the personal phone.

Remaining physical E acceptance must separately record actual results or NOT RUN, owner and equipment reason: real WHOOP broadcast/reconnection; real GPS/lock/slow jogging and standstill (#2); Spotify/audio audibility/focus and lock-notification actions; measured battery per hour; Health Connect→WHOOP credit/duplicates (#4). Ghost physical acceptance remains separately owned by #18. Missing implementation is not a physical-test exemption. Current diagnostic acceptance is recorded in the delivery PR; this section does not claim those unperformed outcomes.

### Physical acceptance retained separately (E)

The bounded native emulator proof does not establish these equipment-dependent results. Preserve exact tested source/conditions when each is performed; an unimplemented production path cannot be called a physical NOT RUN exemption.

| Scenario | Current physical result and reason | Next owner / requirement |
|---|---|---|
| Real WHOOP HR broadcast, selected connect, loss and reconnect | NOT RUN: controlled virtual GATT fixture is not the runner's band | QA + Simon with WHOOP; #1 feasibility |
| Locked Pixel GPS, slow jogging and standing recovery | NOT RUN: synthetic emulator positions do not test real radio/noise | QA + Simon; #1 early proof and #2 motion/autopause quality |
| Spotify + spoken prompt, focus/volume and phase priority | NOT RUN: no-audio emulator proves callbacks, not hearing/music behavior | QA + Simon on Pixel; #1 |
| Collapsed/expanded real lock-screen controls and reopening | NOT RUN: emulator notification dispatch is distinct from personal lock-screen settings | QA + Simon with permitted notifications; #1 |
| Battery per hour under recorded screen/GPS/WHOOP/Spotify conditions | NOT RUN: no measured real-run duration or equipment conditions | QA + Simon; #1, with Ghost comparison separately #18 |
| Health Connect→WHOOP activity, times/distance/goal credit and autodetection duplicates | NOT RUN: isolated synthetic own-record roundtrip has no real account/import | Simon + QA after production exporter exists; #4 |
