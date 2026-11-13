<a id="ghost-run-ruttunderlag-från-kontinuerlig-huvudfas"></a>
# Ghost Run: route evidence from a continuous main phase
Requirements: [Ghost Run](../knowledge/ghost-run.md#ghost-run), Save and select a route and Validity and uncertainty. Part of issue #18, on top of PR #20.

`MainRouteCapture.Build` is a standalone calculation rule. It reads a completed continuous recording with phase-labelled raw GPS fixes and creates frozen route geometry and playback evidence from the main phase only. It reads no Android sensor, database, map or view.

Input contains monotonic elapsed time, UTC, coordinates, accuracy, phase and explicit gap markers. Only one continuous main block is allowed. Reject intervals and unfinished sessions. Warm-up and cool-down are excluded from geometry and must not add a line between phases. Do not interpolate a position at a missing phase boundary.

The quality policy is explicit and versioned: maximum gap, maximum accuracy radius and maximum observed movement speed. No production values are selected by default. Return GPS gaps, missing accuracy, implausible jumps and incorrect time order as separate results with English explanations. Reject an explicit gap even at the first main point.

Preserve each main point's raw coordinate and UTC; time order and duration come from monotonic time. Route position is accumulated spherical distance using Earth radius 6,371,008.8 m (the calculation revision follows the policy). This is a derivation, not a precisely measured ground distance. The selected calculation rule and GPS quality must accompany future storage and comparison.

Preserve identical stationary fixes with timestamps in playback, but do not create zero-length geometry segments. Loops and out-and-back routes preserve order even when a coordinate reappears at a later route position. Never change source-session raw data; copy the source collection.

<a id="avgränsning"></a>
## Scope
The result is **route evidence**, not a verified race result. No automatic first record is created here. Observed time starts at the first measured main fix and ends at the last; it proves neither a missing phase boundary nor when a future matcher should register the first valid finish crossing. Preserve later standstill at the finish as source data without automatically counting it as the race result. SQLite storage, version-ID conflict checks and actual matching of the entire route remain outstanding.

<a id="verifiering"></a>
## Verification
`dotnet run --project tests/GhostChecks` also runs `RouteCaptureChecks`. Synthetic tests cover the main phase without warm-up/cool-down, start/finish/direction, copied raw evidence, stops, spherical distance, out-and-back routes, GPS quality, exact policy boundaries, gaps, jumps, incorrect order and changed UTC time. All tolerances are test parameters. Physical Pixel/WHOOP/Spotify tests have not run. No health export or new UI/integration is enabled.
