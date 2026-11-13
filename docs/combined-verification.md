<a id="samlad-verifiering-och-granskningsordning"></a>
# Combined verification and review order
Requirements: [product specification](../knowledge/product.md), issues #1 and #18. This document combines verification evidence; product rules and design decisions are unchanged.

<a id="kandidater"></a>
## Candidates
| Track | Review order | Available work |
|---|---|---|
| Android/session | #8 → #9 → #14 → #17 | Timed workout engine, service/GPS, continuous SQLite checkpoints, recovery and command log |
| Ghost Run | #19 → #20 → #21 → #22 → #23 → #24 | Requirements/mockups, frozen references, main-phase evidence, race clock, ordered matching and SQLite data model |
| Combined check | This PR after #24 | Temporary source composition and shared build/acceptance tests |

Review order is not merge approval. None of these PRs may be merged without Simon. Main does not yet contain the open candidate changes.

<a id="vad-kontrollen-gör"></a>
## What the check does
[Combined candidate verification](../.github/workflows/combined.yml) checks out the Ghost candidate and fetches full Git history. It combines Android candidate `d70fe80d431c1762f8e23d260d4a7e73e86a38c2` in a **temporary working copy** using `git merge --no-commit --no-ff`. It creates no merge commit and pushes nothing. The pinned revision makes the result reproducible; new Android-track changes require an explicit updated check.

Both tracks changed `scripts/android-smoke.sh`. Automatic resolution preserves the Android candidate's stronger service/GPS/SQLite tests. After English translation, a conflict in docs/android.md may additionally keep the Ghost candidate's English documentation. Only the exact known script conflict or that script plus docs/android.md is allowed; all other conflicts stop the job. The runtime commit remains pinned and the composed production blobs were verified identical before and after this documentation adaptation. Preserve these scenarios at a later approved merge; the Ghost track's simpler startup check must not replace them.

The job runs:
1. `dotnet run --project tests/SessionChecks`.
2. `dotnet run --project tests/GhostChecks`.
3. `python3 tests/GhostStorageChecks/check.py`.
4. An Android build of the combined sources.
5. An API 35 emulator: start/duplicate start, time/GPS with screen off, manual pause, resume, reopened Razor view, process death and exactly preserved SQLite evidence.

The `combined-candidate-evidence` artifact contains source revisions, composition statistics and, when the build succeeds, a debug APK for an x64 emulator. The APK is not a verified Pixel delivery. Add actual results and the run link to the PR description after the job completes.

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
