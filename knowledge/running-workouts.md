---
type: Product Specification
title: Running workouts
---
# Running workouts

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

<a id="produkt-och-leverans"></a>
## Product and delivery
An English-language running app for a runner who rotates between a few favourite workouts. After initial setup, a saved workout must start from the home screen with one tap. The same recording must work with the screen visible, Spotify in the foreground and the phone locked. Deliver an Android app, an installable APK for private use, reproducible Windows instructions and a verification report. The first target phone is a Pixel with a WHOOP band. No server is needed. Private APK distribution is the proposed default.

The app's own screens, controls, accessible names, errors, notifications and voice coaching use English in v1. Feature names in this specification describe functionality. Preserve the user's own workout names. Android controls the language of system-owned dialogs.

## LOOP Core
LOOP Cx is the official running app for C1 and C2. The home screen shows C1 · Intervals and C2 · Zone 2, each with a start action, followed by personal favourites. C1: 10 min progressive warm-up, 5 × 3 min controlled work, 2 min easy movement between efforts and 5 min easy cool-down (38 min total). C2: easy running in WHOOP zone 2, normally 45–60 min; initial value 45 min. Show the purpose and record the source, category and template revision. Personal variations can be saved separately. Global 3/2 min defaults apply to custom workouts. Support for the complete 10K plugin requires a separate decision.

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
