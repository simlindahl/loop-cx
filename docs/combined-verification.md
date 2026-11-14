<a id="samlad-verifiering-och-granskningsordning"></a>
# Combined verification and review order
Requirements: [product specification](../knowledge/product.md), issues #1 and #18. This document combines verification evidence; product rules and design decisions are unchanged.

<a id="kandidater"></a>
## Historical foundation dependency order
| Track | Review order | Available work |
|---|---|---|
| Android/session | #8 → #9 → #14 → #17 | Timed workout engine, service/GPS, continuous SQLite checkpoints, recovery and command log |
| Ghost Run | #19 → #20 → #21 → #22 → #23 → #24 | Requirements/mockups, frozen references, main-phase evidence, race clock, ordered matching and SQLite data model |
| Combined check | Merged #25 after #24 | Actual combined source revision and shared build/acceptance tests |

Review and merge follow the delegated workflow in [AGENTS.md](../AGENTS.md#work-tracking-and-status). Each predecessor must be delivered before its dependent PR merges. The concrete Ghost layout has its [separate recorded approval](../design/decision.md#ghost-layout-approval); standalone rule verification does not establish runtime UI integration.

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
## Current delivered baseline and remaining acceptance

At merged PR #50 (`1b5caa1`), initial Core C2 starts one 45-minute work phase at target RPE 3, with no preparation phases; the C1 readable slice, immutable template metadata and large-text emulator evidence are delivered. This does not implement favourites, duration editing or complete statistics. At merged PR #64 (`9334e94`), Windows x64 build/AVD and controlled BLE/GATT, one-shot TTS, actual notification dispatch and isolated own-record Health Connect feasibility passed. [Android delivery](android.md) distinguishes exact-source evidence and retained physical E tests. Neither a virtual peripheral nor a no-audio emulator proves real WHOOP or Spotify behavior.

Remaining delivery includes favourites/editing and additional targets, distance/autopause/statistics rules, a complete training/history view, production BLE/WHOOP/coaching/zones, map, JSON and a production idempotent Health Connect exporter/retries. The isolated probe is not that exporter. Android Ghost archive readers/writers/migration, frozen service-owned references, race GPS during training pause and atomic finalization remain; standalone rules/host DDL do not prove native Ghost integration. New detailed layouts retain their approval gates.

Physical Pixel/WHOOP/Spotify, real lock-screen behavior, battery and WHOOP import/goal credit remain individually recorded as NOT RUN where unverified; Windows foundation acceptance has passed. [#1](https://github.com/simlindahl/loop-cx/issues/1) is the accepted early foundation, not all M1 acceptance. Remaining owning issues include #2, #3, #4 and #18; current status belongs to GitHub Projects and the current delivery handoff. Normal synthetic health export remains disabled.

Repository publication/licensing and active protection were completed separately under [publication review](publication-review.md); the private source and Projects board remain private. The accepted `9334e94` ARM64 phone update preserved signing and private data and startup was verified; its exact artifact proof belongs to the current delivery handoff. This never establishes sensor or outdoor acceptance. Historical runs above retain their original scope and are not relabelled as results of later source revisions.
