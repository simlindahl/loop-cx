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

The service-backed scaffold starts the C1 timed interval protocol or the approved C2 continuous protocol, records GPS updates and supports pause/resume and warm-up and cool-down commands. The emulator smoke test verifies one session through duplicate starts, screen lock and reopening, then completion and a second workout in the same process. Completion retires the service and restores the start action. The debug-only completion command accelerates this regression; release builds do not expose it. This is a functional scaffold, not the approved final UI or a physically verified GPS/BLE/audio/Health Connect delivery.

## Core start and C1 readable flow slice

The Core start and active-run views use approved neutral Basalt tokens and locally bundled Space Grotesk. The build verifies the packaged scoped stylesheet and font license. Core-first controls include the two cards, protocol/count/target dialogs, RPE help, named shortcuts and management, with global warm-up/cool-down Settings. C2 resolves the selected 20–120-minute duration at fixed target RPE 4, without warm-up or cool-down. Existing Start, Pause/Resume, +1 min and skip commands continue to use the same service. Timed starts can proceed with unknown GPS through the approved eligible foreground-service selection described below; denial is not fabricated as standstill or zero distance. Native Core flows, large text, focus and same-session recovery remain acceptance checks. Detailed Training/History statistics layouts and production HR remain separate from the persisted statistics pipeline.

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

Debug native probes enter through MainActivity's `loop-native-probe` extra. `ble` requests standard Nearby devices permission and, on a subsequent explicit invocation after permission grant, the OS Companion Device Manager Heart Rate chooser. Select/cancel deliberately. An existing active synthetic session is required; probes do not create hidden recordings. The service owns GATT notifications, stale/unknown observations, bounded selected-device reconnects and disposal. A probe-created OS association transfers to the service only for the expected live session. Rejection, permission denial, enqueue/foreground failure and stop release only that owned ID, with cleanup failure reported; existing associations are preserved. `speech` explicitly requests one English TTS phrase through transient audio focus; it never changes music playback/volume. These are diagnostic bindings, not finished coaching or sensor UI.

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

## Ordinary measured distance and main splits (partial #2)

The existing service now feeds ordinary GPS into a pure distance policy/model and persists its immutable `Snapshot.Distance` with every existing journal checkpoint. No database migration or original-plan change is required; older snapshots without this additive field remain historical unknown distance. Raw UTC/nanosecond GPS evidence is unchanged. Processing aligns monotonic observations to the service clock's millisecond resolution; receipt time never determines a delayed fix's phase.

The versioned policy is a synthetic-test pilot, not physically calibrated accuracy. It rejects bad quality/age/jumps, ignores duplicate/out-of-order delivery without destroying newer evidence, preserves a separate slow-movement anchor and breaks ambiguous warm-up/main-set, main-set/cool-down and pause/resume boundaries. Work and recovery remain one continuous main set. Completed kilometre/final-partial splits conserve accepted metres and active main time, including a zero-distance time tail with null pace. Quality gaps remain explicit; these measured totals are not a claim of complete physical distance or full unknown-time statistics.

This slice does not implement distance-target phases, retrospective autopause, full pace/heart-rate/zone/coaching statistics, new screens or Ghost race behavior. The owning specification records the approved later autopause rule: ten seconds confirmed wholly in one eligible phase, all ten seconds retrospectively excluded, without rewriting raw observations or real extension commands. Full #2 and physical threshold calibration remain open. Native verification must follow actual synthetic GPS through the service into SQLite and check interruption/reopen without double counting; host rules alone are not that proof.

<a id="official-c1-revision-2-default"></a>
## Historical C1 revision 2 default

Before Core-first delivery, C1 revision 2 used an 8-minute warm-up and 4-minute cool-down, with five 3-minute efforts/four 2-minute recoveries: 35 total / 23 main. These historical frozen plans and metadata retain template revision 2. New Core-first starts use family revision 3 and the current global warm-up/cool-down Settings. Existing active/saved revision 1 10/5 plans remain 38 minutes; startup still marks unfinished recordings interrupted rather than rehydrating or rewriting them. C2 stays 45/RPE 3/no warm-up and cool-down. Global C1 Settings override is a separate Core-first delivery policy, not a per-run control or delivered Settings UI in this default update. Native checks verify current 480-second warm-up and stored 8/4 plan/revision 2 separately from byte-preserved legacy 38/v1 and NULL metadata rows.


## Timed autopause pilot

The timed C1/C2 service observes a versioned movement pilot independently of distance hysteresis. Reliable stationary observations must cover ten seconds in the same eligible phase instance; Recovery, a real phase/finish boundary or explicit manual Pause cancels confirmation. Exact retrospective subtraction preserves the plan, +1 extensions, raw fixes and real command timestamps. A delayed sample at 10.5 seconds received at 12 excludes only onset 0 through 10: the remaining two seconds are active with honest unknown quality, and automatic pause starts at receipt. Automatic resume also starts at receipt of trustworthy movement; it never counts paused relocation. Explicit Pause while automatic promotes Manual, which requires explicit Resume.

Snapshot motion state records the pilot (including the actual frozen distance speed ceiling), frozen run-start boot-monotonic anchor, reason and candidate. Automatic event checkpoints label onset/excluded-through/sample/detected monotonic times; the corrected run row and event commit atomically. Observed elapsed/persistence fields retain their existing meanings; the later HR schema 4 migration is additive. Raw GPS continues during automatic pause; normal synthetic export remains disabled. No new screen or layout is introduced. Existing app controls and standard notification actions distinguish Autopaused from Manually paused; Auto retains Resume and provides Pause manually, while Manual requires Resume. Paused notification phase/countdown remain in subtext and warm-up and cool-down actions return when running. The existing locked-screen smoke uses a moving GPS route; manual UI screenshots run with saved/restored location disabled and cannot prove autopause. Dedicated emulator GPS/SQLite evidence and physical slow-jog/false-pause calibration are separate gates; physical calibration is NOT RUN.


## Measured statistics coverage

Current timed sessions now project actual service GPS brackets into total/main/current/final split statistics in Snapshot.Statistics version 1 and the same atomic SQLite checkpoint. Quality-valid endpoints are independent of distance hysteresis: stationary valid fixes establish known time. Merged measurement brackets intersect compact active boundaries and subtract exact autopause exclusions; no initial/final/receipt-gap extrapolation. Known plus unknown equals active time in each scope; kilometre boundaries come from the existing distance allocator, not a second interpolator. Main includes Recovery, warm-up and cool-down total only. Full-scope pace is null for incomplete coverage or zero distance. The later production HR backend supplies weighted valid-time means and explicit HR coverage as described below. Missing HR remains null/unknown and is never inferred from debug BLE logs.

Existing elapsed_ms/observed_utc semantics remain unchanged. Statistics originally required no schema change; the later HR backend uses schema 4. Old checkpoints without Statistics have unknown statistics rather than a fabricated backfill. DEBUG complete explicitly marks HasSyntheticTime: accelerated future active intervals are not measured physical evidence. The strong emulator smoke requires actual GPS to reach coverage; UI-only Unknown-GPS smoke verifies persisted all-unknown scopes and accelerated finalization without a duplicate current/final split. Dedicated actual GPS/SQLite/reopen acceptance and physical calibration remain separate; no new UI or real-phone test is implied.


## Core-first starts and sensor-independent timer

New Core starts are resolved and revalidated by the existing service. C1.1/.2/.3 use fixed official protocols and bounded counts; C1 family revision 3 / protocol revision 1 distinguishes them from frozen legacy C1 revisions 1/2. C2 revision 2 uses 20–120 minutes in five-minute steps, target RPE 4 and no warm-up/cool-down. Settings apply a global independent Off/0 or 1–15 whole-minute pair to every new C1 start, including named shortcuts. Saved requests never contain warm-up/cool-down overrides. Actual plan, target intentions, source/revision and shortcut identity freeze with the recording. Versioned local Preferences store selections, Settings and shortcuts; invalid stored values are reported without overwriting the original data. This does not complete all #3 history, accessibility or physical acceptance.

A user-started timed workout can continue with unknown GPS. The approved Android 14+ Special Use fallback declares its normal foreground permission and truthful timer/phase/locked-screen subtype; Location is selected only with granted access and enabled location. No extra health or Bluetooth runtime permission is added to qualify. [Android's foreground-service requirements](https://developer.android.com/develop/background-work/services/fgs/service-types#special-use) still require future Google Play review of the declared use case; acceptance is not guaranteed by local APK success. Current M4 distribution discovery must retain that obligation. Denied/disabled GPS is unknown, never standstill or invented distance; dedicated native denial/disabled checks remain required.


PR #77 did not deliver the approved shared motion families. Their bounded Core/Manage application is described below; full implementation remains tracked in [#62](https://github.com/simlindahl/loop-cx/issues/62) and required before M1 acceptance. Core commands/content update immediately, with no fake progress or sensor-number animation; functional dialog and shortcut checks are not motion-performance evidence.

### Local HR pipeline verification

The production HR backend is independently testable without adding a Settings layout. In a DEBUG build on the dedicated account-free ranchu QA emulator only, `loop-heart-rate-configuration` accepts the versioned configuration JSON while no run is active. It saves the same validated preferences consumed by normal Core start. It does not inject BPM: the selected virtual HR source must advertise, connect and notify through actual Android GATT. No Health Connect probe or write is involved. Never send this diagnostic to a physical phone. The merged backend's native BLE/SQLite/migration/reopen evidence is recorded in [PR #78](https://github.com/simlindahl/loop-cx/pull/78); physical equipment and the complete first-use product flow remain separate acceptance.

The HR fields are available in `RunSnapshot.HeartRate` (frozen setup, live BPM-or-null and quality/error) and `RunSnapshot.Statistics` (total/main/completed/current split weighted mean, known/unknown HR time and configured-zone time). The journal checkpoint contains those fields; schema 4 also persists raw HR and frozen HR setup. This backend slice does not render a full HR statistics/training view, add first-use pairing or expose a zone editor. Seeding the dedicated emulator configuration verifies the real backend path, not a finished user connection flow.

For focused lifecycle recovery on the dedicated DEBUG ranchu emulator, `loop-command=hr-source-failure` stops and releases the actual owned production GATT source and records an explicit synthetic failure marker. A subsequent ordinary visible activity resume invokes the normal sensor refresh and must recreate the selected source in the same active run. This is fault-state setup, not proof that Android itself threw an FGS exception. Both the activity and service gate it to the synthetic emulator; it never creates a workout. A refresh after finish must not reconnect or create a new run. Native failure paths use the same cleanup/release method, so terminal source objects and old-generation callbacks cannot be reused.

The DEBUG `complete` command advances exactly the remaining finite timed plan, including any current warm-up/cool-down extension, rather than a fixed one-hour cap. Every artificial active piece is marked synthetic and remains unsupported sensor time. It can therefore finalize approved C2 durations up to 120 minutes while preserving the original plan, observed wall/monotonic elapsed semantics and disabled synthetic export queue. It is test acceleration, not recorded exercise or physical acceptance.

### Core motion verification boundary

Core/Manage motion uses the existing local CSS and dialog module, without an animation dependency. Commands, accessible values and focus never wait for an effect to finish. Verify actual WebView press feedback with fixed hit boxes, one state transition effect rather than per-tick replay, modal/Manage focus and exit cleanup, real-save-only feedback, 200% scrolling and system reduced motion. Browser-only emulation of reduced motion is not Android system-setting evidence. Full navigation, truthful progress and physical frame/battery checks remain open under issue #62.

Do not install a pre-HR schema-3 build over a QA installation already using schema 4. Compose the merged HR baseline before the final candidate and coordinate the exclusive emulator lease; no database downgrade or wipe is part of motion verification.
