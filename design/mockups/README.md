<a id="loop-cx--statiska-mockuper"></a>
# LOOP Cx · static mockups

Open `index.html` directly in a browser. No build, server or network is required. From the repository root on Windows: `Start-Process .\design\mockups\index.html` in PowerShell. GitHub's file view shows the HTML source; it does not run the gallery.

One HTML file with shared inline CSS, embedded Space Grotesk and Google Material Icons. This supports offline viewing and transferring dimensions, color roles, component structure and states to Blazor. The large first CSS rule contains font data; jump to `:root` for design tokens and to the desired screen ID for markup.

<a id="vad-som-gäller"></a>
## What applies

Basalt, English, Space Grotesk, Material Icons and revision 5's direction are approved. New screens and detailed layouts are **preliminary** and await Simon's review. Read the [design decisions](../decision.md) and [product specification](../../knowledge/product.md). Requesting mockups does not approve their design. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history.

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

| Screen | Screen ID | Requirement in product.md |
|---|---|---|
| [First launch](index.html#welcome) | `welcome` | Product and delivery; Android |
| [Permissions](index.html#permissions) | `permissions` | Integration, storage and errors – Android |

### Training

| Screen | Screen ID | Requirement in product.md |
|---|---|---|
| [Training](index.html#training) | `training` | Screens and layout; LOOP Core |
| [No personal favourites](index.html#empty-training) | `empty-training` | Screens and layout – Start a workout |
| [Manage favourites](index.html#manage) | `manage` | Screens and layout – Start a workout |
| [Reordered favourites](index.html#manage-ordered) | `manage-ordered` | Screens and layout – reorder favourites |
| [Delete confirmation](index.html#delete-workout) | `delete-workout` | Screens and layout – delete a favourite |

### Editor

| Screen | Screen ID | Requirement in product.md |
|---|---|---|
| [Choose workout type](index.html#workout-type) | `workout-type` | Workouts and statistics – continuous/interval |
| [Interval favourite](index.html#edit-intervals) | `edit-intervals` | Workouts and statistics 2–4; Feedback |
| [Distance intervals](index.html#edit-distance) | `edit-distance` | Workouts and statistics 2 |
| [Continuous favourite](index.html#edit-easy) | `edit-easy` | Workouts and statistics 1, 3, 4 |
| [Finish condition](index.html#edit-open) | `edit-open` | Workouts and statistics 1; Feedback – open finish |
| [Intensity target](index.html#edit-target) | `edit-target` | Workouts and statistics 3; Feedback |
| [One-off workout](index.html#run-once) | `run-once` | Screens and layout – one-off workout |

### Run

| Screen | Screen ID | Requirement in product.md |
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

| Screen | Screen ID | Requirement in product.md |
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

| Screen | Screen ID | Requirement in product.md |
|---|---|---|
| [Share sheet boundary](index.html#share-sheet) | `share-sheet` | AI export |
| [Lock screen · warm-up](index.html#notification-warm) | `notification-warm` | Feedback – mini-UI |
| [Lock screen · work](index.html#notification-work) | `notification-work` | Feedback – mini-UI |
| [Lock screen · recovery](index.html#notification-recovery) | `notification-recovery` | Feedback – mini-UI |
| [Lock screen · paused](index.html#notification-paused) | `notification-paused` | Feedback – mini-UI |
| [Lock screen · cool-down](index.html#notification-cool) | `notification-cool` | Feedback – mini-UI |
| [Notification · open finish](index.html#notification-open) | `notification-open` | Feedback – mini-UI |

### Settings

| Screen | Screen ID | Requirement in product.md |
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

| Screen | Screen ID | Requirement in product.md |
|---|---|---|
| [Export pending](index.html#export-pending) | `export-pending` | Health Connect – safe retries |
| [Export needs permission](index.html#export-action) | `export-action` | Health Connect – changed permissions |
| [Export complete](index.html#exported) | `exported` | Health Connect; Health Connect → WHOOP |

### Errors

| Screen | Screen ID | Requirement in product.md |
|---|---|---|
| [Permission denied](index.html#permission-denied) | `permission-denied` | Integration, storage and errors – Android |
| [Distance requires GPS](index.html#distance-blocked) | `distance-blocked` | Android – distance required |
| [Sensor loss in a run](index.html#sensor-loss) | `sensor-loss` | Workouts and statistics 7; WHOOP |
| [Distance phase with GPS loss](index.html#distance-wait) | `distance-wait` | Workouts and statistics 7 |

<a id="granskningskontroll"></a>
## Review checks

[Static mockup review](../../.github/workflows/mockups.yml) uses Chromium on GitHub Actions. The check opens the file offline and checks broken anchors, duplicate IDs, the embedded font, scripts and external requests. It measures horizontal overflow at 360, 390, 700, 1024 and 1440 px, follows Start C1 to warm-up and saves representative screenshots and `report.json` as the `mockup-review` Actions artifact. An approximation of 200% text scaling is reported separately; it does not replace Android system text scaling or TalkBack.

Local equivalent with Node and Playwright installed: `node design/mockups/verify.cjs`. `PLAYWRIGHT_MODULE` can select a separate installation. Screenshots and the report are written to `design/mockups/qa/` and must not be committed. This working environment could not start local Chromium, so browser checks run in Actions. Record actual results in the PR after the run.

<a id="licenser"></a>
## Licenses

Space Grotesk: [SIL Open Font License](FONT-LICENSE.txt), the full variable font from `google/fonts/ofl/spacegrotesk/SpaceGrotesk[wght].ttf`. Material Icons: [Apache 2.0](ICON-LICENSE.txt), the same icon shapes as `design/icons/`. Do not change icon shapes to create a custom icon library.
