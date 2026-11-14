# Live measurements · approved visual direction

On 14 November 2026, Simon approved the final interactive study with “Godkänn den reviderade riktningen”. This closes the visual-direction decision; it is not app delivery, algorithm calibration or native/physical acceptance. The [canonical decision](../../decision.md#live-measurement-motion-approval) owns the approval; [UX](../../../knowledge/ux-design.md#live-measurement-motion) owns presentation and [running workouts](../../../knowledge/running-workouts.md#pace-trend-direction) owns comparison semantics.

## Version actually reviewed

Original study SHA-256 values:

| Input | SHA-256 |
|---|---|
| index.html | d77580cb31199580d198f7a7771d8fe93a60e4a3221c5ce3823031bd45df74f8 |
| style.css | a3474ea3580fcc000ce0aedba2c9ca0e0c2bd015daa2a5c4dd54f40e300c910f |
| prototype.js | e19dee6aab23b15b1a6338f7e7f3840f18adabf0ee5276e71b2a6d684629e0c4 |

The original final revision has sixteen focused browser checks and three visually inspected previews. Earlier halo, unit and state-change-only arrow studies remain historical evidence, not the selected direction. Historical HTML source hashes are `cd8ac857ed63344358b1ae71094f8fd3f71dda75b2f0f8bf99ecfbdef6cd55ad` (halo), `7c6a8448b88c9b2659e9848165687fd28dc8b011cc03d5d08cbf1dff535d9452` (selected heart before trend), and `c384ab176e6a2e2e37fc212d197516bf06e49230f69ccb0215df69f42cfb50a7` (state-change-only trend). These identify historical inputs, not additional final approvals. This maintained transfer preserves the exact phone markup, stylesheet and script behavior; only outside-phone review headings, explanatory text and controls are normalized to English. The repository font path is already relative and reused. Screenshots, local bindings and historical artifacts are not maintained reference assets.

## Approval boundary

Stable BPM with the neutral right-hand Material Filled favorite symbol is selected. The heart's symbolic cycle is 60000/BPM, never actual beat timing. Repeating bounded Faster/Slower motion follows current running speed; Steady and reduced motion remain static. Recent supported comparable pace is the reference, not split mean. Truthful invalid, paused, acquiring and hidden states remain required.

Two 10-second windows, a 5 sec/km deadband and clamp(sec/km × 4, 900, 1800) ms are uncalibrated test proposals, not owner-locked production parameters. The study demonstrates them to expose behavior, not to establish field accuracy, measured steps or physiological cadence. Implementation still needs normal independent review and applicable runtime QA. No new source, permission, backend, animation framework or reader release is implied.

Run `node design/mockups/verify-live-metrics.cjs` from the repository root with the existing Playwright module available (`PLAYWRIGHT_MODULE` when not resolved normally). Optional `APPROVED_STUDY` identifies the original local HTML for transfer parity only; it is not a runtime dependency. Checks exercise synthetic states, not native acceptance.
