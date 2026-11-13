<a id="ghost-run-fristående-referensregler"></a>
# Ghost Run: standalone reference rules
Requirements: [Ghost Run](../knowledge/product.md#ghost-run), issue #18. Requirements/design dependency: PR #19.

This delivery contains a frozen data model for route versions and attempts, JSON round-trip support, deterministic record selection and playback of already derived route positions. It includes no Android service, GPS, map or UI integration.

`GhostRoute` has a stable ID, version ID, source session, name, calculation revision and ordered coordinates with route position in metres. Order defines start, finish and direction; geometry is copied and cannot be changed through the source collection. `GhostAttempt` preserves its own ID, source session, route version, stored local start with UTC offset and time-zone name, elapsed time, verification status and sample series. System.Text.Json can serialize and read the models back without changing local start or route order. This is a storage contract; SQLite tables and extraction of only a continuous main phase remain outstanding.

`GhostReferences.Select` takes an explicit start year that the caller must freeze from local time at race start. Only verified attempts with the same route/calculation version, correct first/last position and a complete elapsed series are eligible. Year best uses the attempt's already stored local year. PB uses all years. Ties use earliest UTC start, then stable ID. An identical recording is idempotent; conflicting copies of a source session are rejected. Selection is a frozen reference. A missing year best is not automatically replaced by PB.

`GhostPlayback` uses elapsed race time, never active training time. It replays recorded pace variations, stops and backwards movement. The first verified arrival determines seconds ahead; metres ahead is calculated separately at the same race time. The ghost stops at its recorded finish time. Interpolation requires valid endpoints within an explicitly specified gap limit. Unknown intervals and time before the first sample are not extrapolated. An earlier gap that may hide first arrival gives an unknown time difference. Hide the comparison if runner or ghost position is unknown.

Test values are synthetic and gap limits are test parameters only. No universal GPS tolerances or physically verified filters are introduced here.

<a id="verifiering"></a>
## Verification
Run `dotnet run --project tests/GhostChecks` with the SDK from global.json. Actions `Ghost Run rules` runs the same checks. Coverage includes 27:30 year best and 26:40 PB, a year change across a UTC boundary, stable ties, missing records, duplicates, unchanged reference, JSON, recorded stop, backwards movement, first arrival, a 12-second lead at 2 km, ghost after finishing and GPS gaps.

<a id="nästa-avgränsade-steg"></a>
## Next bounded step
SQLite route storage and extraction from a continuous main phase; then sequential route matching with crossings, parallel sections, turnarounds, loops and shortcuts as negative tests. The model currently trusts an explicit verification statement from a future matcher: it does not prove completion of a physical route. Service-owned race GPS during training pause, recovery after process death and physical Pixel/WHOOP/Spotify tests remain outstanding. Detailed layout needs approval before app UI/integration prototypes. No health export is enabled.
