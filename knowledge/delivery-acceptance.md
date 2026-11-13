---
type: Product Specification
title: Delivery and acceptance
---
# Delivery and acceptance

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

<a id="windows-emulator-och-leveransordning"></a>
## Windows, emulator and delivery order
1. Pin a supported .NET SDK and compatible MAUI workload, Android SDK and JDK. Document actual versions in the repository. Install the SDK and Android emulator on Windows, create a virtual Pixel and start it. From the project, run dotnet workload restore and, if needed, the MSBuild target InstallAndroidDependencies. Build and run with the Android emulator as the target from the IDE or dotnet build -t:Run -f <project Android TFM>. Replace placeholders with tested commands in README. Also document publishing, signing and installing the APK on a physical phone.
2. Test a vertical feature on the phone early: start the service, lock the screen, collect GPS/BLE, give an audio announcement and use a notification button. Also check that the Health Connect binding can write a test activity. Document device-specific blockers before refining the remaining UI.
3. Implement the workout engine and local storage with a deterministic clock. Add an Android emulator development mode with synthetic route, heart rate, movement/standstill and sensor loss. The agent must be able to jump directly to interval 3, standing recovery, autopause or cool-down, for example. Scenarios must start reproducibly with a documented command. Isolate test data and disable real health export for simulated sessions.
4. Connect views, favourites, speech, notification, export and map. Deliver a short test report with actual results and mark physical checks that require the runner's equipment. Do not present synthetic data as evidence of physical behavior.

<a id="acceptansprov"></a>
## Issue #1 bounded native feasibility

This early integration proof supplements the Windows/emulator foundation; it does not complete all M1 integration features. Preserve outstanding physical Pixel/WHOOP/Spotify/battery acceptance separately. No candidate installation on the personal phone precedes the accepted, merged issue #1 delivery.

- Use only private debug commands and unchanged OS-owned permission/device-selection interfaces. Bluetooth selection must be explicit and cancellable; never auto-connect to an unknown nearby device. A standard Heart Rate service/measurement notification, including 8/16-bit values and missing/contact-loss states, must pass through the real GATT subscription path. Bound scans and selected-device reconnect attempts; serialize callbacks with session/generation checks and dispose on stop. Unknown heart rate is not zero. Virtual fixtures prove binding behavior, not physical WHOOP reliability.
- An explicitly requested one-shot speech probe waits for TTS initialization, language availability and audio-focus grant. Release focus on completion/error/cancellation/destruction; do not control Spotify playback or change its volume. Device audibility/music behavior remains a separate physical check.
- Exercise the actual notification PendingIntents against the same service/session and retain duplicate-command guards.
- Health Connect feasibility is limited to the built-in API34+ binding; earlier Android versions report unsupported. A debug-only isolated test may insert, read back and delete only its own uniquely identified exercise record on the dedicated API35 AVD. Require runtime emulator hardware allowlisting, explicit opt-in and a harness checking the exact serial/AVD, zero accounts and absent WHOOP. Intent extras alone cannot authorize writes. Verify cleanup and report failure; never broadly delete or read other apps' health history. The personal phone writer remains disabled.
- Normal synthetic/debug journal exports remain disabled before and after the probe. No production exporter, retry worker, zones editor or new database schema is introduced. Real health data and diagnostic artifacts remain private outside Git.

## Acceptance tests
| Test | Expected result |
|---|---|
| Start the same favourite twice on separate occasions, then edit the favourite | One tap per start. Two separate runs with their original plans in history. |
| 3 min warm-up, 5 × 3 min work with 2 min recovery, 2 min cool-down | 5 work phases, 4 recoveries, main-set time 23 min, total 28 min. Exactly one export of the whole run. |
| +1 min twice, then skip the rest of the warm-up | Planned end moves by 120 s. Actual warm-up is saved and main-set counters start at zero. The same extension works during cool-down. |
| 2.4 km continuous main set with coaching every 500 m | Four distance-triggered reports; splits of 1 km, 1 km and 0.4 km. No warm-up distance is counted. |
| 29 s pace deviation followed by a new split, then 30 s deviation | No time carries into the new split. One announcement when the new deviation reaches 30 s. Also test zone, exact pace threshold and heart-rate loss. |
| Stop during work, standing recovery, manual pause | Autopause after five seconds of confirmed standstill during work and automatic resume on movement. Timed recovery expires despite standstill. Distance recovery ends only at the correct distance. Manual pause requires explicit resume. |
| Disconnect heart rate, lose GPS, phone has no internet | No invented heart rate or distance. A timed workout continues. Gaps appear in export. Saved history opens offline. |
| Finish with health export denied, reopen the app, grant permission and retry twice | Local session exists immediately. One exported running activity after retry; no duplicate distance or heart-rate records. |
| Share JSON and read it with an independent parser | Reconstruct phase order, main-set time and total distance from the time series/phases within documented rounding. |
| Pixel + WHOOP, Spotify, locked screen during a real run | GPS and heart rate continue, audio is audible, mini-UI controls the correct phase and opening the app resumes the same view. Check music volume and record battery use per hour. |
| Health Connect → WHOOP | Verify one activity with correct start/end and distance. Document its actual effect on each runner goal and any conflict with WHOOP autodetection. |

<a id="primärkällor-och-kvarstående-verifiering"></a>
## Primary sources and remaining verification
- [Microsoft – Blazor Hybrid](https://learn.microsoft.com/aspnet/core/blazor/hybrid/) · [MAUI installation](https://learn.microsoft.com/dotnet/maui/get-started/installation)
- [W3C – Geolocation](https://www.w3.org/TR/geolocation/) · [Android – location during a workout](https://developer.android.com/develop/sensors-and-location/location/battery/scenarios) · [foreground service types](https://developer.android.com/develop/background-work/services/fgs/service-types)
- [WHOOP – heart rate over BLE](https://developer.whoop.com/docs/developing/support/) · [WHOOP – Health Connect](https://support.whoop.com/s/article/Google-Health-Integration-For-Android)
- [Android – audio and volume](https://developer.android.com/media/platform/output) · [Google Maps – billing](https://developers.google.com/maps/documentation/android-sdk/usage-and-billing) · [pricing](https://developers.google.com/maps/billing-and-pricing/pricing)

Physical-phone testing remains outstanding. Particularly important measurements are false autopause during slow jogging, GPS noise during standing recovery, lock-screen notification controls and WHOOP's actual goal credit. The specification authorizes the agent to adjust measurement filters based on documented tests. Changes to workout rules or target statistics require a product decision.

<a id="projektstyrning"></a>
## Project governance
[GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the shared work list. Issues describe requirements; PRs contain implementation and evidence. The [canonical project role and decision rules](../AGENTS.md#roles-and-decision-making), loading the current Dev Team plugin through [team setup](../docs/team-workflow.md#setup), govern role ownership, independent review and team progress updates; the team lead may merge under the conditions in AGENTS.md without separate merge approval from Simon. Follow the [status rules in AGENTS.md](../AGENTS.md#work-tracking-and-status) for Todo → Doing → Waiting → Done, direct status updates through GitHub CLI and verified read-back. Git is the source for requirements and design. A locally created scaffold is not proof that the remote delivery exists.
