---
type: Product Specification
title: LOOP Cx for Android
---
<a id="loop-cx-för-android--specifikation-v1"></a>
# LOOP Cx for Android · specification v1
12 November 2026 · Implementation and verification baseline

<a id="produkt-och-leverans"></a>
## Product and delivery
An English-language running app for a runner who rotates between a few favourite workouts. After initial setup, a saved workout must start from the home screen with one tap. The same recording must work with the screen visible, Spotify in the foreground and the phone locked. Deliver an Android app, an installable APK for private use, reproducible Windows instructions and a verification report. The first target phone is a Pixel with a WHOOP band. No server is needed. Private APK distribution is the proposed default.

The app's own screens, controls, accessible names, errors, notifications and voice coaching use English in v1. Feature names in this specification describe functionality. Preserve the user's own workout names. Android controls the language of system-owned dialogs.

## LOOP Core
LOOP Cx is the official running app for C1 and C2. The home screen shows C1 · Intervals and C2 · Zone 2, each with a start action, followed by personal favourites. C1: 10 min progressive warm-up, 5 × 3 min controlled work, 2 min easy movement between efforts and 5 min easy cool-down (38 min total). C2: easy running in WHOOP zone 2, normally 45–60 min; initial value 45 min. Show the purpose and record the source, category and template revision. Personal variations can be saved separately. Global 3/2 min defaults apply to custom workouts. Support for the complete 10K plugin requires a separate decision.

<a id="teknikbeslut"></a>
## Technology decisions
Use C#, .NET MAUI Blazor Hybrid and a local SQLite database. A PWA offers simpler distribution and web development, local storage and a cached interface, but web geolocation requires a visible document. Reliable location recording in a pocket, Bluetooth heart rate and Health Connect determine the choice. MAUI requires the Android SDK, an emulator, platform permissions and maintenance of Android bindings. Measure battery use on the phone. No separate web host is needed.

An Android foreground service, started when the user starts the workout, owns the clock, GPS, heart rate and phase transitions. The Blazor view and notification read the same state and send commands to the same session. Reopening, rerendering or duplicate taps must not create two recordings. Keep workout rules and statistics in plain C# and Android calls at the system boundary. Use a monotonic clock for durations and also timestamp in UTC for export.

<a id="kodorganisation"></a>
### Code organization

Build a modular monolith behind Blazor: one installable app with clear feature boundaries in C#. Organize code into vertical slices by what the runner does. Keep Razor components, input, validation and use-case-specific persistence close together. Folders and namespaces are sufficient to start with.

Example: a change to target-RPE validation for a favourite belongs in the slice for editing favourite workouts. Rules that also apply when starting a workout belong to the workout model and are reused from there. The active session owns phase transitions and pause rules; Blazor and lock-screen controls invoke the same behavior. Android dependencies for GPS and Bluetooth stay at the platform boundary so the workout engine can be verified with simulated measurements.

Choose the shortest clear path from UI to behavior and data. Add an interface, layer or separate project only when it solves a concrete need, such as replacing real sensor data with a deterministic test source. Clean Architecture layers are not required. A review should identify where each rule belongs and why every intermediate step is needed.

<a id="vyer-och-utformning"></a>
## Screens and layout
- **Start a workout.** Show ordered favourites directly, with a name, protocol and individual Start button. Create, edit, reorder and delete through a secondary entry point. Also support a one-off workout. Allow starting offline. Show GPS and heart-rate status without requiring an extra confirmation when everything is ready.
- **Training view.** At the top, show the phase, target RPE, optional pace or zone target, repetition number and remaining time/distance when the end is defined. Two clearly labelled groups show the main set and current split, each with time, distance, average pace and average heart rate. Show live pace and heart rate largest at the bottom. During warm-up, show that phase's own data. Reset main-set counters to zero at main-set start. Main-set results are final during cool-down.
- **History.** Most recent run first. Details show the main set, total including warm-up/cool-down, splits, time in heart-rate zones, a map of the complete GPS track, export status and Share workout data. Editing a favourite or zone boundary must never change a past result.
- **Settings.** WHOOP connection, manual zone boundaries, Health Connect status and default warm-up/cool-down durations. A favourite's audio settings must carry over when the workout is repeated.
- **Approved visual direction.** Minimalist Material Design with Space Grotesk and tabular numerals, a neutral background, high contrast and one accent for the active phase or primary action. Use Android text scaling, clear units and touch targets of at least 48 dp. Check reading order with TalkBack, large text and outdoor readability. Status needs a text label even when color is used.

<a id="pass-och-statistik"></a>
## Workouts and statistics
1. A continuous workout ends at the selected active duration, selected distance or manually. Automatically split at each full kilometre of the main set. Also save the final incomplete split with its actual distance.
2. Interval workouts use N work efforts and N−1 recoveries. Work and recovery use the same duration type, either time or distance, with separate values. Phase transitions and splits are automatic. Save each work and recovery phase separately so effort pace can be compared. N=1 has no recovery.
3. Require an integer target RPE of 1–10 for work and for recovery when present. Each phase may additionally have a target pace in min/km or a heart-rate zone. RPE is intended effort. Display and export it as a target.
4. Warm-up and cool-down can be enabled independently per workout. Defaults are 3 and 2 min respectively, changed in Settings. Proposed rule: read the current default at start and freeze it for the active run. During either phase, each separate tap on +1 min adds 60 seconds. Volume up does the same when the training view is open. Start main set and Finish and save can be used at any time during their respective phases. Preserve data already recorded.
5. The whole run includes warm-up, work, recovery and cool-down. The main set includes work and planned recovery. Save both elapsed and active time. Average pace is active time divided by distance; average heart rate is a time-weighted mean of valid readings. Report gaps as unknown time. Never divide by zero. Calculate zone statistics with the boundaries frozen at start.
6. Autopause after five seconds of reliably observed standstill and resume automatically on movement. Exclude the entire recovery phase. During timed recovery, standing still counts in full. During distance recovery, timing continues while remaining distance awaits movement. Manual pause takes precedence over automation and requires manual resume. Freeze active time, recorded distance, phase countdown and coaching during pause. The recommended initial rule starts the pause at detection. Lost GPS means unknown movement, not standstill.
7. Filter implausible GPS jumps. Mark interruptions in the map track and do not extrapolate lost distance. During a distance-based phase with lost GPS, active time continues and the phase waits for measurable distance. Document and field-test thresholds for movement, resume, GPS quality and live-pace smoothing.

<a id="återkoppling-under-passet"></a>
## Feedback during workouts
The runner selects speech at a custom positive time interval (minutes and seconds) or distance interval in metres. Read the four main-set values, the four current-split values, live pace and heart rate in English. Speech frequency and split length are independent. Announce phase transitions and target RPE. Give short phase announcements priority over longer summaries and avoid overlapping or stale announcements while music plays. The proposed default counts periodic summaries from main-set start using active time or main-set distance.

Give brief coaching for a pace deviation greater than 10 percent or heart rate outside the selected zone for 30 consecutive active seconds within the same split. The proposed interpretation uses smoothed live pace in seconds/km and valid current heart rate. At 5:00 min/km, the thresholds are 4:30 and 5:30. Reset deviation time on a new split, pause, data gap or return within target. Give one announcement per continuous deviation. RPE without a measurable target provides phase announcements and ordinary summaries.

An ongoing Android notification on the lock screen and in the notification shade shows phase, remaining time/distance, heart rate and pace within the available system space. Warm-up: +1 min and Start main set. Main set: Pause and, for an open-ended workout, Start cool-down or Finish and save. Recovery: Pause. Paused: Resume. Cool-down: +1 min and Finish and save. Tapping the notification opens the current view. Test actual visibility in collapsed and expanded states with lock-screen notifications allowed.

<a id="integration-lagring-och-fel"></a>
## Integration, storage and errors
- **WHOOP.** Read the band's Bluetooth LE heart rate with heart-rate broadcast enabled. Let the user connect the band on first use and enter personal zones. Verify ordered boundaries without unintended gaps or overlaps. Reconnect after interruption. Show a dash for missing heart rate, continue the workout and suppress heart-rate coaching until new readings arrive. Live recording requires no WHOOP web API or custom cloud service.
- **Android.** Request precise location and the relevant Bluetooth scan/connect permissions when the features are used. Declare foreground service types and permissions required by the selected Android version for location and a connected device. Start the service from a visible app. Request notification permission for the mini-UI. If access is denied, show the consequence and a path to settings. Continue with available features. A timed workout can work without GPS; explain and let the user change the plan when distance is essential.
- **Local database.** Save favourites, settings, the frozen workout plan and zones, phase boundaries, raw sensor readings, pauses, quality markers and export status. Write continuously during the workout. After process death, the user must at least be able to save the recorded part with an interruption marker. Never add unsupported time or distance. Finishing must atomically finalize the session and enqueue export.
- **Health Connect.** Request write permissions when integration is enabled. Automatically export a running session for the whole run, distance and available heart-rate data. Use stable external record IDs for the session and child records. Retry safely after interruption or changed permissions. Show pending/exported/action required. Exported means written to Health Connect. WHOOP imports separately and uses its own heart-rate readings. Check with a real account how workout count, time, distance and time in zones are credited and whether duplication occurs. Importing other apps' workouts is outside v1.
- **Map.** Google Maps SDK for Android shows the local GPS track only in history. The standard SDK has unlimited free use according to the current pricing list but requires a Google Cloud project with billing and an API key. Restrict the key to the Android package/signature and correct API. Recording, statistics and export must work without network access or a map key. Show clear map status.
- **AI export.** Share one UTF-8 JSON file per run through Android's share sheet. Version the schema. Include stable session ID, UTC timestamps and local time zone, plan with all targets and zones, actual phases/splits, active and elapsed time, total and main set, zone statistics, GPS with accuracy, heart rate, pauses, data gaps and manual phase changes. Specify units explicitly and distinguish measured from derived values. Use null for unknown. Deliver a schema and a small sample file the agent can validate. Coordinates are included in the full export.

<a id="windows-emulator-och-leveransordning"></a>
## Windows, emulator and delivery order
1. Pin a supported .NET SDK and compatible MAUI workload, Android SDK and JDK. Document actual versions in the repository. Install the SDK and Android emulator on Windows, create a virtual Pixel and start it. From the project, run dotnet workload restore and, if needed, the MSBuild target InstallAndroidDependencies. Build and run with the Android emulator as the target from the IDE or dotnet build -t:Run -f <project Android TFM>. Replace placeholders with tested commands in README. Also document publishing, signing and installing the APK on a physical phone.
2. Test a vertical feature on the phone early: start the service, lock the screen, collect GPS/BLE, give an audio announcement and use a notification button. Also check that the Health Connect binding can write a test activity. Document device-specific blockers before refining the remaining UI.
3. Implement the workout engine and local storage with a deterministic clock. Add an Android emulator development mode with synthetic route, heart rate, movement/standstill and sensor loss. The agent must be able to jump directly to interval 3, standing recovery, autopause or cool-down, for example. Scenarios must start reproducibly with a documented command. Isolate test data and disable real health export for simulated sessions.
4. Connect views, favourites, speech, notification, export and map. Deliver a short test report with actual results and mark physical checks that require the runner's equipment. Do not present synthetic data as evidence of physical behavior.

<a id="acceptansprov"></a>
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

<a id="uiux--designkontrakt-för-v1"></a>
## UI/UX – design contract for v1
Minimalist Material Design is this project's selection of Material Design conventions. Use a flat list, a primary filled button, secondary text buttons, subtle dividers and a navigation bar with three named destinations. One accent color; use elevation only to distinguish overlapping surfaces. Show focus, pressed, selected and disabled states.
- **Typography:** bundle Space Grotesk locally for app-owned screens, regular and bold; test å/ä/ö, system text scaling and stable numeral width.
- **Icons:** use Google's Material Icons, Filled, 24 dp by default. Use play_arrow for start, pause for pause and history/settings for navigation. Label actions with visible text or an accessible name. Hide decorative icons from screen readers. Standard notifications follow Android system typography and layout.
- **Design tests:** check Space Grotesk and icons in airplane mode, the selected tab with text and shape, and direct start from each Core template.

Minimalism is measured by the number of decisions the runner makes. After initial setup: one start action from favourites, zero mandatory taps for ordinary phase transitions, automatic saving on completion. Separate primary actions from editing. The home screen shows 2–3 favourites without opening a menu. An empty list offers Create first workout. Cancelling favourite editing must preserve the previous version.
- Navigation: Training, History and Settings. During a workout, prioritize phase and controls. Reopening from a notification shows the same run. The map appears in session details after completion.
- Visual hierarchy: phase, remaining time/distance and target at the top; main set and split with the same order and units in the middle; live pace/heart rate largest at the bottom. Use understandable empty values for missing data. Allow wrapping into two columns at larger text sizes.
- Typography scale: Space Grotesk with tabular numerals. Heading 28–32 sp, live measurement 40–48 sp, group heading 18 sp, body 16 sp, label 12–14 sp. Spacing in 4/8/16/24 dp, outer margins 20–24 dp. Dark default theme: Basalt, as specified in the color table below. No decorative chart in the training view.
- Actions: at least 48 × 48 dp touch targets. Phase-specific button names explain their effect. Distinguish manual pause from autopause with text. The app must work with TalkBack and large system text without losing values or actions.
- Acceptance: test first start, recurring favourite, standing recovery, warm-up +1 min, finish, disconnected heart-rate band and pending export. Check contrast (4.5:1 for normal text), reading order and text at 200%. Lock-screen examples prioritize content and actions; Android determines exact rendering.
- Review material: [static HTML mockups](../design/mockups/index.html) with [requirements mapping and reading guide](../design/mockups/README.md) are the ongoing visual reference for the whole app. Simon approved the layout direction on 12 November and chose Basalt on 13 November 2026. New screens and detailed layouts are preliminary. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history. HTML does not replace workout rules, acceptance tests or Android verification. Record decisions in design/decision.md before implementation.

<a id="basalt--beslutade-färger"></a>
### Basalt – approved colors

| Role | Color | Usage |
|---|---|---|
| Background | #10120F | App background |
| Surface | #1B1E18 | Groups and navigation bars |
| Primary text | #EEEFE6 | Headings, timer and measurements |
| Secondary text | #A6AE9A | Labels and units |
| Accent | #C4DA73 | Primary actions, active phase and selected destination |
| On accent | #10120F | Text and icons on filled accent buttons |

The palette replaces revision 5's blue colors; layout, English, Space Grotesk and Material Icons remain. Use the same color roles in every app-owned screen. System-owned Android notifications follow system colors and layout. Color must not carry meaning alone; also use text and shape. Distinguish disabled actions from active ones with state and accessibility information.

Acceptance: home and training views use the table's colors without a remaining blue accent. Primary/secondary text and accent text achieve at least 4.5:1 against both background and surface. The calculated minimum contrast is 7.34:1 (secondary text on surface); dark text on accent achieves 12.21:1. Verify text scaling, focus and outdoor readability in the app; static mockups do not prove these behaviors.

<a id="projektstyrning"></a>
## Project governance
[GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the shared work list. Issues describe requirements; PRs contain implementation and evidence. Follow the [status rules in AGENTS.md](../AGENTS.md#work-tracking-and-status) for Todo → Doing → Waiting → Done, direct status updates through GitHub CLI and verified read-back. Git is the source for requirements and design. A locally created scaffold is not proof that the remote delivery exists.

## LOOP knowledge — review proposal

An offline reading feature under **Settings → What is LOOP?**, aimed at experienced exercisers adapting training to a busy life. Retain Training, History and Settings as the only main destinations. The feature introduces the complete Core rotation, not just the app's running sessions. It must work without an account, app setup, WHOOP or another wearable.

The proposed landing page has a short introduction, a primary Get started action and optional workout and learning links. Get started is a four-part reading guide: understand the rotation; choose exercises and load; complete the first session; continue and adapt. It does not configure workouts, mark lessons complete or infer the next Core step from the running log. Bundle all explanatory content and references locally; opening an external research source is optional and requires internet.

Content baseline: [What is LOOP? draft](what-is-loop.md). Simon approved the guide and detailed screens at revision `4a4c97170c858c2206b507b0cd9296795b5ccc84` on 13 November 2026. See the [review brief](../docs/loop-knowledge-review.md) and [approval conditions](../design/decision.md#what-is-loop--content-and-layout-review). App implementation remains on hold pending a separate instruction.

Review acceptance:
- From Settings, open What is LOOP?, read Get started, then return to the introduction and Settings without a new main tab.
- Explain C1 → S1 + M → C2 → S2 + M, all four workout cards, safe technical failure, gym/home substitutions and continuing after interruptions.
- Keep two working sets to technical failure as the Core default; identify this as a design choice rather than a universal research requirement.
- Explanations remain usable offline. External source links are clearly identified. No private second-brain links or personal race data appear.
- Proposed views use Basalt, local Space Grotesk, visible navigation labels and wrapping text. Static review does not establish Android accessibility or screen-reader behavior.
