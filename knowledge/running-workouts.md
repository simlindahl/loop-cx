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
LOOP Cx is the official running app for C1 and C2. The home screen shows C1 · Intervals and C2 · Zone 2, each with a start action, followed by personal favourites. C1: 10 min progressive warm-up, 5 × 3 min controlled work, 2 min easy movement between efforts and 5 min easy cool-down (38 min total). C2: easy running in WHOOP zone 2, normally 45–60 min. Simon approved the initial Core C2 template on 13 November 2026: one continuous 45-minute work phase, target RPE 3, and no separate warm-up or cool-down. Zone 2 is a target intent; do not imply measured zone monitoring when heart-rate integration is unavailable. The duration remains adjustable under the workout requirements. Show the purpose and record the source, category and template revision. Personal variations can be saved separately. Global 3/2 min defaults apply to custom workouts. Support for the complete 10K plugin requires a separate decision.

<a id="pass-och-statistik"></a>
## Workouts and statistics
1. A continuous workout ends at the selected active duration, selected distance or manually. Automatically split at each full kilometre of the main set. Also save the final incomplete split with its actual distance.
2. Interval workouts use N work efforts and N−1 recoveries. Work and recovery use the same duration type, either time or distance, with separate values. Phase transitions and splits are automatic. Save each work and recovery phase separately so effort pace can be compared. N=1 has no recovery.
3. Require an integer target RPE of 1–10 for work and for recovery when present. Each phase may additionally have a target pace in min/km or a heart-rate zone. RPE is intended effort. Display and export it as a target.
4. Warm-up and cool-down can be enabled independently per workout. Defaults are 3 and 2 min respectively, changed in Settings. Proposed rule: read the current default at start and freeze it for the active run. During either phase, each separate tap on +1 min adds 60 seconds. Volume up does the same when the training view is open. Start main set and Finish and save can be used at any time during their respective phases. Preserve data already recorded.
5. The whole run includes warm-up, work, recovery and cool-down. The main set includes work and planned recovery. Save both elapsed and active time. Average pace is active time divided by distance; average heart rate is a time-weighted mean of valid readings. Report gaps as unknown time. Never divide by zero. Calculate zone statistics with the boundaries frozen at start.
6. Autopause after ten seconds of reliably observed standstill and resume automatically on movement. Simon explicitly approved this confirmation window and retrospective backdating on 13 November 2026: subtract the full ten confirmed seconds from active training time rather than starting the pause only at detection. Exclude the entire recovery phase. During timed recovery, standing still counts in full. During distance recovery, timing continues while remaining distance awaits movement. Manual pause takes precedence over automation and requires manual resume. Freeze active time, recorded distance, phase countdown and coaching during pause. All ten seconds must fit within one eligible phase: a phase transition, recovery or finish cancels the candidate and never reopens a completed session. Explicit manual pause cancels it; manual resume begins a fresh eligible window. A real +1-minute extension preserves motion evidence and its command log; duplicate commands do not change the window. This supersedes the earlier five-second/detection-only proposal; immutable raw observations remain unchanged. Lost GPS means unknown movement, not standstill.
7. Filter implausible GPS jumps. Mark interruptions in the map track and do not extrapolate lost distance. During a distance-based phase with lost GPS, active time continues and the phase waits for measurable distance. Document and field-test thresholds for movement, resume, GPS quality and live-pace smoothing.

<a id="återkoppling-under-passet"></a>
## Feedback during workouts
The runner selects speech at a custom positive time interval (minutes and seconds) or distance interval in metres. Read the four main-set values, the four current-split values, live pace and heart rate in English. Speech frequency and split length are independent. Announce phase transitions and target RPE. Give short phase announcements priority over longer summaries and avoid overlapping or stale announcements while music plays. The proposed default counts periodic summaries from main-set start using active time or main-set distance.

Give brief coaching for a pace deviation greater than 10 percent or heart rate outside the selected zone for 30 consecutive active seconds within the same split. The proposed interpretation uses smoothed live pace in seconds/km and valid current heart rate. At 5:00 min/km, the thresholds are 4:30 and 5:30. Reset deviation time on a new split, pause, data gap or return within target. Give one announcement per continuous deviation. RPE without a measurable target provides phase announcements and ordinary summaries.

An ongoing Android notification on the lock screen and in the notification shade shows phase, remaining time/distance, heart rate and pace within the available system space. Warm-up: +1 min and Start main set. Main set: Pause and, for an open-ended workout, Start cool-down or Finish and save. Recovery: Pause. Paused: Resume. Cool-down: +1 min and Finish and save. Tapping the notification opens the current view. Test actual visibility in collapsed and expanded states with lock-screen notifications allowed.

## Proposed details still awaiting product decisions

The existing wording deliberately leaves these interpretations proposed/recommended: when configurable preparation defaults are read and frozen; the active-time/main-distance origin of periodic summaries; the smoothed-pace/current-heart-rate interpretation used for coaching. Resolve the affected choice before its implementation; this baseline review does not silently promote a proposal to an approved rule. Already approved immutable original plans, frozen zone boundaries, manual-pause precedence and unknown-data behavior remain in force.

## Self-comparison discovery

[#58](https://github.com/simlindahl/loop-cx/issues/58) records the proposed first self-comparison scope and its evidence limits. It is discovery, not implementation approval or a new fitness score; existing recorded facts and frozen plans remain authoritative. Do not infer conditioning improvement from equal planned durations or treat missing measurements as zero.

## Ordinary distance and split pilot

The first service-integrated distance slice accumulates only accepted ordinary GPS edges, separately for the whole run and main set (work plus recovery). Freeze a versioned technical filter policy per session. Pilot values are 30 m maximum horizontal accuracy, 10 s maximum sample age/gap, 12 m/s speed ceiling and retained movement hysteresis of at least 3 m or the sum of endpoint accuracy radii. These are conservative synthetic-test hypotheses, not field-validated thresholds or a guarantee of physical accuracy. Raw observations remain immutable.

Use monotonic measurement timestamps. Ignore duplicate/out-of-order delivery without changing newer trusted state. Reject invalid or excessively old fixes; break and re-anchor across genuinely rejected samples, missing GPS, manual pause and preparation/main/cool-down boundaries rather than allocating an ambiguous edge to the callback's current phase. Retain the movement anchor through small valid displacements so slow movement is not permanently discarded. Recovery contributes to main distance and preserves a continuous valid main-set edge; entering recovery alone is not a GPS gap. Preparation does not contribute to main distance. Split accepted main distance at each kilometre, interpolating active time within a continuous accepted edge. Preserve final partial distance and any final active-time tail, with unknown pace where data is missing or distance is zero. Checkpoints retain the policy, accepted-distance totals, split times and quality flags without rewriting original plans or adding invented distance. Distance-target phases and the approved retrospective autopause remain separate subsequent engine work; this pilot does not close #2 or physical calibration acceptance.
