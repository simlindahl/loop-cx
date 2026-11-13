<a id="loop-cx--statiska-mockuper"></a>
# LOOP Cx · static mockups

Open `index.html` directly in a browser. No build, server or network is required. From the repository root on Windows: `Start-Process .\design\mockups\index.html` in PowerShell. GitHub's file view shows the HTML source; it does not run the gallery.

One HTML file with shared inline CSS, embedded Space Grotesk and Google Material Icons. This supports offline viewing and transferring dimensions, color roles, component structure and states to Blazor. The large first CSS rule contains font data; jump to `:root` for design tokens and to the desired screen ID for markup.

<a id="vad-som-gäller"></a>
## What applies

Basalt, English, Space Grotesk, Material Icons and revision 5's direction are approved. Simon approved the exact neutral Basalt palette and launcher background on 13 November 2026; see the [canonical colors and contrast](../../knowledge/ux-design.md#basalt--approved-colors). New screens and detailed layouts are **preliminary** and await Simon's review. Read the [design decisions](../decision.md) and [product specification](../../knowledge/product.md). Requesting mockups does not approve their design. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history.

The gallery index and captions are review tools, not app UI. Clicking an action follows an anchor to a static example. No data changes; forms are read-only and switches illustrate states. No JavaScript, sensor access, Android permissions, GPS, storage, speech or export runs. Times and measurements are synthetic. The lock screen, notification shade and share sheet belong to Android and may look different on a phone. The map is deliberately a labelled placeholder.

<a id="använd-vid-implementation"></a>
## Use during implementation

1. Read the corresponding product requirement and choose a screen ID below.
2. Reuse `--cx-*` tokens and HTML structure as starting points for small Razor components in the correct vertical slice. `.review-*`, `.gallery`, `.phone` and `.statusbar` are review frames only.
3. Move shared CSS and the embedded font to local app resources. Connect behavior to the shared session model; anchor links must not become workout logic.
4. Verify in the Android emulator and on Pixel against acceptance tests. HTML shows intent, not proof of background recording or Android integration.

<a id="vyer-och-kravkoppling"></a>
## Screens and requirements mapping

### Setup

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [First launch](index.html#welcome) | `welcome` | Product and delivery; Android |
| [Permissions](index.html#permissions) | `permissions` | Integration, storage and errors – Android |

### Training

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Training](index.html#training) | `training` | Screens and layout; LOOP Core |
| [No personal favourites](index.html#empty-training) | `empty-training` | Screens and layout – Start a workout |
| [Manage favourites](index.html#manage) | `manage` | Screens and layout – Start a workout |
| [Reordered favourites](index.html#manage-ordered) | `manage-ordered` | Screens and layout – reorder favourites |
| [Delete confirmation](index.html#delete-workout) | `delete-workout` | Screens and layout – delete a favourite |

### Editor

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Choose workout type](index.html#workout-type) | `workout-type` | Workouts and statistics – continuous/interval |
| [Interval favourite](index.html#edit-intervals) | `edit-intervals` | Workouts and statistics 2–4; Feedback |
| [Distance intervals](index.html#edit-distance) | `edit-distance` | Workouts and statistics 2 |
| [Continuous favourite](index.html#edit-easy) | `edit-easy` | Workouts and statistics 1, 3, 4 |
| [Finish condition](index.html#edit-open) | `edit-open` | Workouts and statistics 1; Feedback – open finish |
| [Intensity target](index.html#edit-target) | `edit-target` | Workouts and statistics 3; Feedback |
| [One-off workout](index.html#run-once) | `run-once` | Screens and layout – one-off workout |

### Run

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Warm-up](index.html#warm-up) | `warm-up` | Workouts and statistics 4; LOOP Core |
| [Warm-up extended](index.html#warm-up-extended) | `warm-up-extended` | Workouts and statistics 4 |
| [Work interval](index.html#work) | `work` | Screens and layout – Training view; Workouts and statistics 2 |
| [Timed recovery](index.html#recovery) | `recovery` | Workouts and statistics 2, 6 |
| [Distance recovery](index.html#distance-recovery) | `distance-recovery` | Workouts and statistics 2, 6 |
| [Zone 2 run](index.html#easy-run) | `easy-run` | LOOP Core; Workouts and statistics 1, 3 |
| [Distance-based run](index.html#tempo-run) | `tempo-run` | Workouts and statistics 1, 3 |
| [Open finish](index.html#open-run) | `open-run` | Workouts and statistics 1; Feedback – open finish |
| [Open finish, no cool-down](index.html#open-no-cooldown) | `open-no-cooldown` | Feedback – open finish |
| [Manual pause](index.html#paused) | `paused` | Workouts and statistics 6 |
| [Automatic pause](index.html#autopaused) | `autopaused` | Workouts and statistics 6 |
| [Cool-down](index.html#cool-down) | `cool-down` | Workouts and statistics 4, 5 |
| [Cool-down extended](index.html#cool-down-extended) | `cool-down-extended` | Workouts and statistics 4 |

### History

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Saved locally](index.html#saved) | `saved` | Integration, storage and errors – Local database/Health Connect |
| [Interrupted recording](index.html#interrupted) | `interrupted` | Integration, storage and errors – process death |
| [Run history](index.html#history) | `history` | Screens and layout – History |
| [Empty history](index.html#empty-history) | `empty-history` | Screens and layout – History |
| [Run details](index.html#run-detail) | `run-detail` | Screens and layout – History; Workouts and statistics 5; AI export |
| [Easy run details](index.html#c2-detail) | `c2-detail` | LOOP Core; History; Workouts and statistics 1 |
| [Interrupted run detail](index.html#interrupted-detail) | `interrupted-detail` | Integration, storage and errors – interruption |
| [Phases and splits](index.html#splits) | `splits` | Workouts and statistics 1, 2, 5 |
| [Continuous splits](index.html#continuous-splits) | `continuous-splits` | Workouts and statistics 1 |
| [Recorded zones](index.html#zone-history) | `zone-history` | Workouts and statistics 5; WHOOP |
| [Recorded route](index.html#route) | `route` | Integration, storage and errors – Map |
| [Unavailable map](index.html#route-offline) | `route-offline` | Integration, storage and errors – Map; Workouts and statistics 7 |
| [Share run data](index.html#share) | `share` | Integration, storage and errors – AI export |

### System

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Share sheet boundary](index.html#share-sheet) | `share-sheet` | AI export |
| [Lock screen · warm-up](index.html#notification-warm) | `notification-warm` | Feedback – mini-UI |
| [Lock screen · work](index.html#notification-work) | `notification-work` | Feedback – mini-UI |
| [Lock screen · recovery](index.html#notification-recovery) | `notification-recovery` | Feedback – mini-UI |
| [Lock screen · paused](index.html#notification-paused) | `notification-paused` | Feedback – mini-UI |
| [Lock screen · cool-down](index.html#notification-cool) | `notification-cool` | Feedback – mini-UI |
| [Notification · open finish](index.html#notification-open) | `notification-open` | Feedback – mini-UI |

### Settings

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Settings](index.html#settings) | `settings` | Screens and layout – Settings |
| [WHOOP connection](index.html#devices) | `devices` | Integration, storage and errors – WHOOP |
| [Find a band](index.html#pairing) | `pairing` | Integration, storage and errors – WHOOP |
| [No band found](index.html#pairing-empty) | `pairing-empty` | Integration, storage and errors – WHOOP/Android |
| [Band disconnected](index.html#devices-disconnected) | `devices-disconnected` | Integration, storage and errors – WHOOP |
| [Personal heart-rate zones](index.html#zones) | `zones` | Integration, storage and errors – WHOOP; Workouts and statistics 5 |
| [Zone validation](index.html#zone-error) | `zone-error` | WHOOP – ordered boundaries |
| [Audio coaching](index.html#audio) | `audio` | Feedback during workouts |
| [Distance audio summaries](index.html#audio-distance) | `audio-distance` | Feedback; Acceptance tests 2.4 km |
| [Health Connect settings](index.html#health-settings) | `health-settings` | Integration, storage and errors – Health Connect |

### Export

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Export pending](index.html#export-pending) | `export-pending` | Health Connect – safe retries |
| [Export needs permission](index.html#export-action) | `export-action` | Health Connect – changed permissions |
| [Export complete](index.html#exported) | `exported` | Health Connect; Health Connect → WHOOP |

### Errors

| Screen | Screen ID | Requirement via product.md |
|---|---|---|
| [Permission denied](index.html#permission-denied) | `permission-denied` | Integration, storage and errors – Android |
| [Distance requires GPS](index.html#distance-blocked) | `distance-blocked` | Android – distance required |
| [Sensor loss in a run](index.html#sensor-loss) | `sensor-loss` | Workouts and statistics 7; WHOOP |
| [Distance phase with GPS loss](index.html#distance-wait) | `distance-wait` | Workouts and statistics 7 |

<a id="granskningskontroll"></a>
## Review checks

[Static mockup review](../../.github/workflows/mockups.yml) uses Chromium on GitHub Actions. The check opens the file offline and checks broken anchors, duplicate IDs, the embedded font, scripts and external requests. It measures horizontal overflow at 360, 390, 700, 1024 and 1440 px, follows Start C1 to warm-up and saves representative screenshots and `report.json` as the `mockup-review` Actions artifact. An approximation of 200% text scaling is reported separately; it does not replace Android system text scaling or TalkBack.

Local equivalent with Node and Playwright installed: `node design/mockups/verify.cjs`. `PLAYWRIGHT_MODULE` can select a separate installation. Screenshots and the report are written to `design/mockups/qa/` and must not be committed. Record actual local and Actions results in the PR; browser availability depends on the environment. The verifier also follows Ghost route entry, return to session details, and the pause action in each live reference mode.

<a id="licenser"></a>
## Licenses

Space Grotesk: [SIL Open Font License](FONT-LICENSE.txt), the full variable font from `google/fonts/ofl/spacegrotesk/SpaceGrotesk[wght].ttf`. Material Icons: [Apache 2.0](ICON-LICENSE.txt), the same icon shapes as `design/icons/`. Do not change icon shapes to create a custom icon library.

## What is LOOP? · approved reader reference

Review [the complete English guide](../../knowledge/what-is-loop.md) and [review brief](../../docs/loop-knowledge-review.md) alongside these representative excerpts. Open the gallery locally, select Settings, then What is LOOP? Simon approved the six reader screens at revision `4a4c971` on 13 November 2026; [conditions](../decision.md#what-is-loop--content-and-layout-review) apply. App implementation remains on hold.

| Screen ID | Review purpose |
|---|---|
| `loop-about` | Introduction, Get started and optional paths |
| `loop-start` | Four-part scrolling reading guide |
| `loop-workouts` | Complete rotation overview |
| `loop-strength` | Representative S1 card, safe limits and home examples |
| `loop-learn` | Dose, evidence and recovery context |
| `loop-sources` | Offline explanations with optional external sources |

All map to **LOOP knowledge — review proposal** in [the reader specification](../../knowledge/loop-knowledge.md). The reader is not a setup wizard and records no progress. Long frames represent scrolling content; Android scroll, back navigation and TalkBack remain future implementation checks.

<a id="ghost-run--preliminärt-tillägg"></a>
## Ghost Run · preliminary addition

A separate feature from Training; save a continuous main set from session details. All examples are synthetic. The map sketch uses original example geometry, not real map data or working navigation. The race clock and route measurements continue during training pause. Only year best and personal best are supported. Detailed layout awaits review.

| Screen | Screen ID | Requirement |
|---|---|---|
| [Ghost Run](index.html#ghost-routes) | `ghost-routes` | Ghost Run |
| [Your routes](index.html#ghost-empty) | `ghost-empty` | Ghost Run |
| [Save route to Ghost Run](index.html#ghost-save) | `ghost-save` | Ghost Run |
| [Park loop](index.html#ghost-choose) | `ghost-choose` | Ghost Run |
| [One run. Both records.](index.html#ghost-same) | `ghost-same` | Ghost Run |
| [Set your year best](index.html#ghost-no-year) | `ghost-no-year` | Ghost Run |
| [Race year best](index.html#ghost-ready) | `ghost-ready` | Ghost Run |
| [Race personal best](index.html#ghost-ready-pb) | `ghost-ready-pb` | Ghost Run |
| [Set first time](index.html#ghost-ready-first) | `ghost-ready-first` | Ghost Run |
| [Ghost Run](index.html#ghost-live) | `ghost-live` | Ghost Run |
| [Ghost Run](index.html#ghost-live-pb) | `ghost-live-pb` | Ghost Run |
| [First time this year](index.html#ghost-first-live) | `ghost-first-live` | Ghost Run |
| [Training paused](index.html#ghost-paused) | `ghost-paused` | Ghost Run |
| [GPS uncertain](index.html#ghost-uncertain) | `ghost-uncertain` | Ghost Run |
| [Return to the route](index.html#ghost-off-route) | `ghost-off-route` | Ghost Run |
| [New year best](index.html#ghost-result) | `ghost-result` | Ghost Run |
| [Attempt saved](index.html#ghost-unverified) | `ghost-unverified` | Ghost Run |
| [End this attempt?](index.html#ghost-abandon) | `ghost-abandon` | Ghost Run |
| [Lock screen · Ghost Run](index.html#ghost-lock) | `ghost-lock` | Ghost Run |
| [Move to the start](index.html#ghost-not-ready) | `ghost-not-ready` | Ghost Run |

## UX review proposal · issue #38

The gallery still contains 90 static screens. The review index is collapsed initially and groups Ghost Run with the other screen groups. Alternate-state links are outside the phone frames so review navigation cannot be mistaken for app controls. The count and navigation describe the actual gallery; they add no product destinations.

Ghost Run changes are preliminary design corrections: return from its route list to Training and from Save route to the source run details; show the existing Pause training action in every live reference mode; remove ghost/runner positions from pre-start route overviews, remove the ghost from first-time recording, and show the runner off-route when comparison is unavailable; let comparison rows wrap at word boundaries with enlarged text. Alternate-state links still let reviewers inspect automatic finish, GPS recovery and missing-reference examples. The shared paused screen is a representative state; runtime navigation must retain the selected reference rather than copying a static anchor's example opponent.

These are proposed corrections to the Ghost addition, not a new approval of its detailed layout. They do not change workout rules, create new main tabs or alter the approved revision 5 core layout. The six approved LOOP reader articles and guide content remain unchanged. Record Simon's decision before runtime implementation. Review evidence and limitations belong in the PR for #38; static Chromium layout, keyboard and text-scaling checks do not establish Android/TalkBack or outdoor acceptance.

## Approved neutral palette

For a visual change, link the relevant screenshots or local presentation/artifact manifest from the PR's [current review handoff](../../docs/team-workflow.md#current-review-handoff), with the tested revision, approval status and static/runtime limitations. Select affected screens and states; this does not require a new capture of all 90 screens. The HTML, owning specification and design decisions remain authoritative; a local contact sheet is presentation material.

Compare Training, Work, Settings, the LOOP reader and Ghost Run against the previous gallery revision. The approved palette removes green undertones from backgrounds, surfaces, text, dividers and the illustrative map. It keeps one lime accent for action and current-state emphasis, while workout codes, wordmark and historical bars become neutral. Layout, labels, navigation and approved reader content are unchanged. The exact palette and neutral launcher background are recorded in design/decision.md; approved loop geometry is unchanged. Decorative dividers do not replace sufficiently contrasting functional control boundaries; see the specification.
