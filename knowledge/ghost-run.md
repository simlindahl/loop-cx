---
type: Product Specification
title: Ghost Run
---
# Ghost Run

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

## Ghost Run

<a id="omfattning-och-ingång"></a>
### Scope and entry point

Ghost Run is a separate feature, accessible from Training without changing the existing three main navigation destinations. After a completed continuous workout, the runner can choose Save route to Ghost Run in session details. This applies only to main-set distance, not warm-up/cool-down. Intervals, pacers/target pace and arbitrary previous attempts are excluded. Ghost Run uses English, Basalt, Space Grotesk and Material Icons. Use the [approved twenty-one-state reference](../design/mockups/ghost-run/index.html#ghost-routes) under its [decision and limits](../design/decision.md#ghost-layout-approval); the matching older gallery examples are historical context.

<a id="race-identity-and-applicability"></a>
#### Race identity and applicability

Ghost Run is a competitive activity separate from Core/Cx conditioning. Starting, executing, displaying, recording and exporting a race must identify Ghost Run, its route/version and frozen reference/race rules. Do not use C2/Zone 2, an easy-run purpose, RPE 4 or a Core template revision as race identity. RPE and optional pace/HR-zone targets are not applicable: neither ask for nor display them. Represent that explicitly as null/absent under the versioned contract, never RPE 0 or a fabricated C2 template. Measured pace/HR remain valid measurements, distinct from training targets.

A race can use a route captured from C2 without becoming C2. The source remains a completed continuous workout's contiguous main route only; intervals, imported tracks, automatic route detection and arbitrary builders are not added. Preserve source warm-up/cool-down exclusion, original metadata and raw data. Shared timing, recording or training-active accounting does not transfer a Core classification to the race. The existing race-clock, pause, route/reference and quality rules below remain unchanged; this product decision does not approve revised detailed Ghost layouts or release the reader runtime hold.

<a id="spara-och-välja-rutt"></a>
### Save and select a route

Save a named route with stable ID, version ID, ordered geometry, fixed start, finish and direction, and the source session's main phase. Review GPS evidence before saving; a significant gap must not become an invented continuous route. Show an understandable reason if the route cannot be saved. The source session becomes the first comparison reference if the full attempt can be verified. Creating the route does not change raw data or the original session.

Require the same route version, start, finish and direction. Changed geometry creates a new version and is not automatically compared with old records. Equal distance alone is insufficient. Arbitrary start points, reverse direction, multiple laps, a route editor, import and automatic route detection across all history are outside the first version. A loop or out-and-back route must not finish on an early pass near the finish.

<a id="motståndare-och-årsurval"></a>
### Opponent and year selection

Offer Year best and Personal best. Year best is the lowest verified elapsed main-set time among eligible attempts that started in the current calendar year. Use the attempt's stored local start date and time zone; later time-zone changes must not move dates. For a new start, determine the current year from local time and freeze it at start. Personal best is the lowest verified elapsed main-set time ever on the same route version. Break ties deterministically, for example by earliest start and then session ID.

Show when the same attempt holds both records. Without a year best, offer Set first time without a ghost or personal best if available. Without either record, let the runner set a first time without a ghost. Freeze the reference session, route version, record time and comparison rules at start; never change the ghost mid-session. A new valid result updates applicable records only after finishing and final storage. Never count the same recording twice.

<a id="start-tävlingsklocka-och-paus"></a>
### Start, race clock and pause

Starting requires a sufficiently reliable position near the correct start. Show Move to the start or GPS uncertain when the start cannot be verified. Start Ghost Run resets the race clock and starts runner and ghost together. Any warm-up precedes this start; cool-down follows the finish.

The route finish determines the end of the Ghost Run main set and replaces ordinary time/distance targets. Use monotonic elapsed time from main-set start to verified finish crossing. All stops, manual pauses and autopause count in race time. Show Race time keeps running during stops and pauses before starting.

Pause training pauses ordinary active training counters and coaching under the base rules, but does not pause the race clock, ghost or GPS recording required for route verification. Save these positions separately as race evidence; do not add paused distance to ordinary active counters. Do not automatically resume training after a manual pause. Playback preserves standstill and pause time. A pause alone never disqualifies a record.

This is an explicit Ghost Run exception to the base specification's frozen active measurements during pause. A verified finish crossing must be able to finish the race even while main-set training is paused. End attempt saves an incomplete recording without a record. Process death must not create a verified result by filling missing distance or time; preserve the recorded part with an interruption marker.

<a id="uppspelning-och-jämförelse"></a>
### Playback and comparison

Save raw coordinates, measurement time, accuracy, UTC, monotonic/relative elapsed time, phases, pauses, data gaps and quality status. Derive ordered route position s(t) for the current and reference attempts. Use the same versioned geometry and documented calculation rule for both; do not silently change old results when filters change. Reference time is elapsed time including stops, and the ghost follows recorded pace variations. Interpolate only between sufficiently close valid points; large gaps mean unknown evidence.

Metres ahead = runner route position minus ghost route position at the same race time. Seconds ahead = reference arrival time at the runner's current route position minus current race time. Positive means ahead. Arrival time is the first verified arrival in the correct sequence, even when the reference stands still there. Do not use straight-line distance or metres divided by current pace for the time difference. Keep recorded running distance separate from progress along the route.

Match a plausible sequence of route segments using previous position, feasible movement, direction and GPS quality. The nearest point on the whole route is insufficient at crossings, parallel sections and turnarounds. No shortcut may create a false lead or record. Display rounded metres/seconds and approximate or unknown comparisons when quality requires it. After the ghost finishes, its marker stays there while the race clock and time comparison continue until the runner finishes. Freeze the final result at the runner's verified finish crossing.

<a id="giltighet-och-osäkerhet"></a>
### Validity and uncertainty

A record requires the correct start/finish, the complete route in order and enough evidence to exclude a significant shortcut or wrong direction. GPS loss means unknown position, not standstill. With uncertain GPS or clear route deviation, the clock and ghost continue but comparison values are hidden. Restore them only when the correct position is reliable again. Re-entry may restore the training comparison but does not itself prove a skipped section was completed. Pauses or a brief deviation do not automatically exclude a record if the entire route can still be verified.

Save incomplete or insufficiently verified attempts with a concrete reason and do not update records. A small data gap does not automatically disqualify, but must not hide a possible shortcut. Do not fix universal thresholds before testing: document and test start/finish tolerance, route corridor, persistent deviation, gap length and update frequency using synthetic tracks and a physical Pixel.

<a id="livekarta-ljud-och-låst-skärm"></a>
### Live map, audio and locked screen

Show an ordered route line with direction/start/finish, a solid accent runner marker and a slightly transparent white ghost with a dark outline. Shape and text complement color; both markers must remain distinguishable when overlapping. Time difference is primary; approximate distance in metres is secondary. Follow the runner with recentering and an optional route overview. A distant opponent must not force an unreadable zoom level. Show map status separately from GPS status.

Give short deviation announcements and optional spoken comparisons, coordinated with the ordinary audio queue and without stale announcements. The first version covers the route line, direction and deviation announcements, not turn-by-turn navigation or rerouting. The Android service owns the race clock, GPS, playback and comparison. Blazor/map and notification consume the same state; separate UI timers must not determine results. The lock-screen notification shows race time and comparison where Android allows, plus Pause training/Resume training. Verify exact system layout on the phone.

Load route and reference data locally before starting; no new server is needed. Recording and comparison must work without a network/map key. An offline background map is not guaranteed; show the route geometry and markers as a simple overview when the background is unavailable. Stop rendering/animation when the map is hidden and restore the same session on reopening. Measure energy use for ordinary recording, Ghost Run with the screen off and a visible map under comparable Pixel/WHOOP/Spotify conditions.

<a id="acceptansprov-och-leverans"></a>
### Acceptance tests and delivery

| Test | Expected result |
|---|---|
| Save a continuous main set with warm-up/cool-down | A Ghost Run route from the main phase only. Preserve the source session; it becomes the first result if verified. Interval sessions do not offer the action. |
| Race a route captured from C2 | Race UI/metadata/export identifies Ghost Run, route/version and frozen reference, with RPE and optional pace/zone targets not applicable. Measured HR/pace remain available. The source C2 history retains its original RPE/template and exports. |
| Year best 27:30 in 2026, PB 26:40 in 2025 | Select the correct recording for each mode. Clearly label one record holding both. No pacer or arbitrary attempt selection. |
| Year change, time-zone change and tied times | Preserve the stored local start year, freeze year selection at a new start and use stable reference selection for ties. |
| No year best or no records | Set the first time for the year without a ghost; offer PB only if one exists. The first valid result becomes the reference. |
| Reference reaches 2 km at 10:12, runner at 10:00 | 12 s ahead. Calculate metres separately from their respective positions at 10:00. |
| 25 min movement and 2 min pause with a verified complete route | Race result 27 min and continued PB eligibility; active counters follow pause rules. Ghost and race GPS continue during pause and screen lock. |
| Reference stands still; finish crossed during training pause | Playback preserves the stop. Use first arrival for the time difference. Finishing can end the race during training pause. |
| Crossing, turnaround, parallel sections, loop and shortcut | No jump to the wrong route segment, no early finish and no record from skipping a segment. |
| GPS loss, route deviation, network loss and process death | Mark unknown comparisons; invent no measurements. Network loss does not stop a local race. Save uncertain/interrupted attempts with a reason and no record. |
| Ghost finishes first or runner wins | Ghost stops at the finish. Calculate final time and records from the runner's actual finish crossing; choose a new reference for the next attempt. |
| Reopen/storage retry after finishing | Same result and reference; no duplicate records or export entries. |
| Pixel/WHOOP/Spotify with screen on/off | Same race clock, position sequence and session. Report audio, readability, notification actions and battery use with actual evidence. |

Deliver small vertical slices: route storage/record selection, deterministic playback and route matching, session/pause/persistence integration, then live UI and physical verification. The identified twenty-one-state detailed direction is now owner-approved; do not ask again for the same layout. Implement within that scope and the existing architecture, review and acceptance rules. New material redesigns still follow AGENTS.md. Static approval and synthetic verification do not establish runtime race/GPS/storage behavior or physical records. PR review and merge follow the delegated workflow in AGENTS.md.


### Native archive adapter boundary

The isolated archive adapter uses the existing local session database and the canonical [archive DDL](../storage/ghost/schema-1.sql), with an independent archive revision marker and no session-schema version change. It installs atomically only when explicitly used. Save a captured route/version and its source attempt together; identical stable identities/evidence are idempotent, conflicting copies roll back without replacing archived rows. Readers return the frozen geometry, rule/quality policy, raw/progress evidence and saved local start/zone for existing domain selection. Successful storage is never proof of Verified quality; unsupported legacy zone provenance is not backfilled. See [the native archive contract](../docs/ghost-archive.md).

Acceptance for this bounded slice is real Android SQLite initialization, route/source-attempt rollback, identical/conflicting retries, reopen and preservation of existing run/raw/outbox data. The session database version remains unchanged; an unknown/partial Ghost namespace is rejected. No route-save screen, race-service integration, UI prototype, map provider or health operation is delivered by this adapter.
