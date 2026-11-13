<a id="samlad-verifiering-och-granskningsordning"></a>
# Combined verification and review order
Requirements: [product specification](../knowledge/product.md), issues #1 and #18. This document combines verification evidence; product rules and design decisions are unchanged.

<a id="kandidater"></a>
## Candidates
| Track | Review order | Available work |
|---|---|---|
| Android/session | #8 → #9 → #14 → #17 | Timed workout engine, service/GPS, continuous SQLite checkpoints, recovery and command log |
| Ghost Run | #19 → #20 → #21 → #22 → #23 → #24 | Requirements/mockups, frozen references, main-phase evidence, race clock, ordered matching and SQLite data model |
| Combined check | This PR after #24 | Actual combined source revision and shared build/acceptance tests |

Review and merge follow the delegated workflow in [AGENTS.md](../AGENTS.md#work-tracking-and-status). Each predecessor must be delivered before its dependent PR merges. Detailed Ghost layouts remain preliminary; standalone rule verification does not approve UI integration.

<a id="vad-kontrollen-gör"></a>
## What the check does
[Combined candidate verification](../.github/workflows/combined.yml) verifies the actual checked-out revision containing both tracks. It no longer merges a pinned historical runtime into a temporary tree or resolves conflicts in CI. The artifact records the tested source SHA. Run it on every PR and push to main so code changes cannot silently skip combined checks.

The combined candidate retains the Android track's stronger service/GPS/SQLite smoke test, including completion followed by a second workout, effective command logging and migration. Ghost rules and host SQLite checks run against the same revision as the Android build.

The job runs:
1. `dotnet run --project tests/SessionChecks`.
2. `dotnet run --project tests/GhostChecks`.
3. `python3 tests/GhostStorageChecks/check.py`.
4. An Android build of the combined sources.
5. An API 35 emulator: start/duplicate start, time/GPS with screen off, manual pause, resume, reopened Razor view, process death and exactly preserved SQLite evidence.

The `combined-candidate-evidence` artifact contains the tested source revision and, when the build succeeds, a debug APK for an x64 emulator. The APK is not a verified Pixel delivery. Add actual results and the run link to the PR description after the job completes.

<a id="vad-kontrollen-inte-gör"></a>
## What the check does not do
It does not connect Ghost to the Android service, GPS, map, notification or UI. Ghost rules compile in the same app but are tested as standalone rules. The SQLite schema is tested on the host; the app does not use the Ghost archive yet. Ordinary GPS tests therefore do not verify Ghost GPS during training pause.

<a id="kvarstående-leverans"></a>
## Remaining delivery
- Favourites, C2/additional workout targets, distance/autopause rules and a finished training view.
- BLE/WHOOP, audio/coaching, zone statistics, map, JSON export and Health Connect retries.
- Android readers/writers for the Ghost archive, frozen service-owned start references, continued race GPS during training pause and atomic finalization.
- Visual approval of new detailed layouts before new UI/integration prototypes.
- Physical Pixel/WHOOP/Spotify, notification buttons, Windows and energy measurements: **not run**.

Synthetic health export is disabled. Issues #1, #2, #3, #4 and #18 remain open with outstanding acceptance tests. Track current status through GitHub Projects using `gh project` as specified in AGENTS.md.
