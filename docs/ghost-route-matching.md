<a id="ghost-run-ordnad-ruttmatchning"></a>
# Ghost Run: ordered route matching
Requirements: [Ghost Run](../knowledge/ghost-run.md#ghost-run), Playback and comparison, and Validity and uncertainty. Part of #18, on top of #22.

`OrderedRouteMatcher` is a standalone C# rule for synthetic verification and future field tests. It reads the same frozen route version as the reference and requires an explicit versioned policy. No Android sensor, map, view or SQLite integration is included.

<a id="beviskedja"></a>
## Evidence chain
Starting is an explicit call with a reliable position near the correct start; ordinary observations do not start a race. Freeze the observed monotonic start timestamp. A future calling service must coordinate this with the user's race start and GPS freshness; no service integration exists here.

The matcher maintains a continuous covered route prefix. Only the expected segment can provide the current route position. An observed movement may pass several route points only if they lie in order along that short movement, within gate tolerance and in the recorded direction. Its endpoint must still be in the correct route corridor. Never use the nearest point on the whole route to advance the segment index.

Gaps, uncertain GPS, implausible speed/step length and route deviation hide the current comparison without advancing the covered prefix. Re-entry farther ahead does not prove a skipped section. After a large gap, returning near the last covered anchor may restore comparison; the remainder must actually be traversed in order. A small gap may be accepted between valid endpoints under the explicitly selected policy. An explicitly marked gap is not bridged into new coverage.

Finishing requires all gates in order. An early pass at the same physical coordinate on a loop/out-and-back route is therefore not a finish. A closely sampled valid crossing between fixes may derive the finish timestamp by projection along the observed movement. Freeze finish time and final state. A synthetic test uses this timestamp with `GhostRaceClock.CompleteAt` during training pause.

<a id="beräkningsgränser"></a>
## Calculation limits
Project coordinates onto a local equirectangular plane with the first route point as origin and Earth radius 6,371,008.8 m. Normalize longitude difference across the date line. Reject routes outside the explicit projection extent or with origin at latitude 85 degrees or higher; reject zero-length geometry segments. Interpolate progress from the route version's stored metre positions. Measured running distance and this route position are different values.

The policy specifies corridor, gate radius, maximum accuracy radius, gap, speed, sample step and projection extent. Tolerances also use each fix's accuracy radius. All test values are synthetic, not default production filters or physically approved thresholds.

This initial rule is conservative. Longer backtracking over passed segments and re-entry far from the last covered anchor may remain unknown. Closely parallel sections within the same GPS uncertainty need further quality and field analysis. Local linear assumptions and interpolated finish crossings need field testing before real records are awarded. Finished means the full chain under the given policy, not independent physical certification. No `GhostAttempt` with Verified status is created automatically.

<a id="verifiering"></a>
## Verification
`dotnet run --project tests/GhostChecks` also runs `RouteMatchChecks`. Tests cover fixed start, duplicate start/fix, ordered segments, a diagonal shortcut, wrong direction, early finish on a loop, turnaround, parallel sections, figure eight, a GPS gap with re-entry at the finish, return to a covered anchor, uncertain GPS, implausible jump, incorrect time order, projection boundary and frozen finish. A closely sampled synthetic finish between fixes completes the race during training pause at the correct time.

SQLite route archive, actual GPS during pause, system-owned notifications, approved UI and physical Pixel/WHOOP/Spotify tests remain outstanding. Health export remains disabled.
