<a id="ghost-run-fristående-tävlingsklocka"></a>
# Ghost Run: standalone race clock
Requirements: [Ghost Run](../knowledge/ghost-run.md#ghost-run), Start, race clock and pause, and Playback and comparison. Part of #18, on top of PR #21.

`GhostRaceClock` takes explicit absolute monotonic timestamps. It calls no system clock, sensor, Android service or UI timer. At later integration, the caller must feed it from the same service-owned session that controls training.

The race clock counts all observed elapsed time from main-set start. Manual training pause remains until explicit resume, without freezing race time or playback. `TrainingActive` is a derived cross-check from the same pause events, not a separate training engine. It covers the ongoing main phase; phase/sensor rules still belong to the session engine. Repeated pause/resume commands do not create duplicate pause intervals. A snapshot of pause evidence is frozen.

A future matcher calls `CompleteAt` with the verified finish-crossing timestamp. This is allowed during training pause. The timestamp may precede the latest timer observation when GPS arrives late. Active time and pause status are then clipped historically to the actual finish time. Later timer, button and finish events must not change the final result.

`EndAttempt` freezes an incomplete attempt. `InterruptAtLastObservation` freezes only the known part with interrupted status; no time after the last observation is added. This models the interruption rule but does not implement process-death detection, reload or SQLite persistence.

The clock's Finished status does not certify route completion. The future matcher owns finish evidence and the archive owns atomic storage and record updates. No real race GPS during pause is connected yet.

<a id="verifiering"></a>
## Verification
Run `dotnet run --project tests/GhostChecks`. Synthetic tests cover 25 min training + 2 min pause = 27 min race time, finishing during pause, idempotent commands, manual resume, frozen pause evidence, backwards time, delayed GPS finish before/during pause, late events after finishing, incomplete/interrupted attempts and playback from race time during training pause.

No new app UI or integration prototype is included. Physical Pixel/WHOOP/Spotify, service GPS during pause and energy measurements have not run. Health export is disabled.
