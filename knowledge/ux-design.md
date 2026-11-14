---
type: Product Specification
title: Screens and design
---
# Screens and design

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

<a id="vyer-och-utformning"></a>
## Screens and layout
- **Start a workout.** Show two Core cards using the [approved Core-first reference](../design/mockups/core-variants/decision-notes.md): initial C1.2 and retained official selection, bounded counts/targets, a C2 duration slider and named saved shortcuts. Shortcuts retain count/duration/target, while current global warm-up/cool-down Settings resolve at each new C1 start. Start offline. The former arbitrary one-off builder is superseded; shortcut management beyond the approved flow remains separately tracked. Show GPS and heart-rate status without requiring an extra confirmation when everything is ready.
- **Training view.** At the top, show the phase, target RPE, optional pace or zone target, repetition number and remaining time/distance when the end is defined. Two clearly labelled groups show the main set and current split, each with time, distance, average pace and average heart rate. Show live pace and heart rate largest at the bottom. During warm-up, show that phase's own data. Reset main-set counters to zero at main-set start. Main-set results are final during cool-down.
- **History.** Most recent run first. Details show the main set, total including warm-up/cool-down, splits, time in heart-rate zones, a map of the complete GPS track, export status and Share workout data. Editing a favourite or zone boundary must never change a past result.
- **Settings.** WHOOP connection, manual zone boundaries, Health Connect status and default warm-up/cool-down durations. A favourite's audio settings must carry over when the workout is repeated.
- **Approved visual direction.** Minimalist Material Design with Space Grotesk and tabular numerals, a neutral background, high contrast and one accent for the active phase or primary action. Use Android text scaling, clear units and touch targets of at least 48 dp. Check reading order with TalkBack, large text and outdoor readability. Status needs a text label even when color is used.

<a id="uiux--designkontrakt-för-v1"></a>
## UI/UX – design contract for v1
Minimalist Material Design is this project's selection of Material Design conventions. Use a flat list, a primary filled button, secondary text buttons, subtle dividers and a navigation bar with three named destinations. One accent color; use elevation only to distinguish overlapping surfaces. Show focus, pressed, selected and disabled states.
- **Typography:** bundle Space Grotesk locally for app-owned screens, regular and bold; test å/ä/ö, system text scaling and stable numeral width.
- **Icons:** use Google's Material Icons, Filled, 24 dp by default. Use play_arrow for start, pause for pause and history/settings for navigation. Label actions with visible text or an accessible name. Hide decorative icons from screen readers. Standard notifications follow Android system typography and layout.
- **Design tests:** check Space Grotesk and icons in airplane mode, the selected tab with text and shape, and direct start from each Core template.

Minimalism is measured by the number of decisions the runner makes. After initial setup: one start action from Core or a saved shortcut, zero mandatory taps for ordinary phase transitions, automatic saving on completion. Separate primary actions from editing. The home screen shows the two Core cards and saved named shortcuts without a required menu. There is no generic Create first workout flow. Cancelling a Core editor, Settings or shortcut dialog preserves committed values.
- Navigation: Training, History and Settings. During a workout, prioritize phase and controls. Reopening from a notification shows the same run. The map appears in session details after completion.
- Visual hierarchy: phase, remaining time/distance and target at the top; main set and split with the same order and units in the middle; live pace/heart rate largest at the bottom. Use understandable empty values for missing data. Allow wrapping into two columns at larger text sizes.
- Typography scale: Space Grotesk with tabular numerals. Heading 28–32 sp, live measurement 40–48 sp, group heading 18 sp, body 16 sp, label 12–14 sp. Spacing in 4/8/16/24 dp, outer margins 20–24 dp. Dark default theme: Basalt, as specified in the color table below. No decorative chart in the training view.
- Actions: at least 48 × 48 dp touch targets. Phase-specific button names explain their effect. Distinguish manual pause from autopause with text. The app must work with TalkBack and large system text without losing values or actions.
- Acceptance: test first start, recurring favourite, standing recovery, warm-up +1 min, finish, disconnected heart-rate band and pending export. Check contrast (4.5:1 for normal text), reading order and text at 200%. Lock-screen examples prioritize content and actions; Android determines exact rendering.
- Review material: [static HTML mockups](../design/mockups/index.html) with [requirements mapping and reading guide](../design/mockups/README.md) are the ongoing visual reference for the whole app. Simon approved the layout direction on 12 November and chose Basalt on 13 November 2026. New screens and detailed layouts are preliminary. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history. HTML does not replace workout rules, acceptance tests or Android verification. Record decisions in design/decision.md before implementation.

<a id="basalt--beslutade-färger"></a>
### Basalt – approved colors

Simon approved this exact neutral palette and launcher background on 13 November 2026, superseding the earlier green-tinted Basalt base. This is the canonical color table for app-owned screens.

| Token / role | Color | Usage |
|---|---|---|
| `--cx-bg` / background | #111111 | App and launcher background |
| `--cx-surface` / surface | #1C1C1C | Groups and navigation bars |
| `--cx-text` / primary text | #F2F2F2 | Headings, timer and measurements |
| `--cx-muted` / secondary text | #ADADAD | Labels, units and neutral graphics |
| `--cx-accent` / accent | #C4DA73 | Primary actions, active phase, selected destination and focus |
| `--cx-on-accent` / on accent | #111111 | Text and icons on filled accent buttons |
| `--cx-line` / decorative divider | #505050 | Subtle separators, not required control/state cues |

Use a single lime accent for primary actions, the active phase, selected destination, focus and route/runner identification. Workout codes, the wordmark, start-screen plan diagrams and historical zone bars are neutral. Ghost map illustration surfaces and roads have no green tint. Retain text and shape for status and overlapping map markers; color never replaces labels. System-owned Android notifications follow system colors and layout. Distinguish disabled actions from active ones with state and accessibility information.

Acceptance: home and training views use this table. Primary/secondary text and accent text achieve at least 4.5:1 against background and surface. Calculated text contrast against surface: primary 15.22:1, secondary 7.59:1 and accent 11.06:1; on-accent text 12.25:1. Background contrast is higher. The #505050 divider has only 2.11:1 contrast against surface and 2.34:1 against background; reserve it for decoration. If an editable field boundary or another non-text cue is needed to identify a control or state, use an existing sufficiently contrasting role such as secondary text and verify at least 3:1 against adjacent colors ([WCAG non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html)). Static read-only field illustrations do not establish editable runtime accessibility. Verify actual Android text scaling, TalkBack, focus and outdoor readability during implementation.

The launcher uses #111111 behind the unchanged approved #C4DA73 continuous tilted loop and 108 × 108 geometry. Preserve package identity, signing compatibility and existing data during the authorized native integration and phone update. The approved LOOP reader content and its runtime hold, revision 5 layout, three tabs and preliminary Ghost layout gate remain in force.

## Approved motion and interaction system

Simon approved **all studies 01–07 and their proposed values** on 13 November 2026 in [#62](https://github.com/simlindahl/loop-cx/issues/62). This supersedes the earlier recommendation to start with only 01–02; the other patterns are approved implementation scope, not merely optional reference. [Design decision](../design/decision.md#motion-studies-0107--13-november-2026) records that approval. Apply each pattern to its real owning operation/view; a missing surface remains a delivery dependency, not permission to fabricate a screen or success state.

| Shared value | Approved value |
|---|---|
| Press / quick / standard / exit | 80 / 160 / 220 / 120 ms, where applicable |
| Easing | `cubic-bezier(.2,0,0,1)` |
| Group stagger | 24 ms across at most three groups |
| Movement | At most 4 px for state changes and 8 px for composed entrance |
| Press scale | Visual scale .98; the actual hit area never shrinks |

The approved family covers 01 press feedback, 02 phase/state changes, 03 navigation, 04 disclosure, 05 truthful determinate progress, 06 actual result feedback and 07 coordinated entrance. [The mapping](../design/mockups/README.md#motion-implementation-dependencies) identifies real-view dependencies. No extra general implementation-plan approval gate is required for this approved family; genuinely new material layout/product choices still follow the existing decision rules. Approval does not release the reader runtime hold or Ghost detailed-layout gate.

Respect system reduced motion with immediate equivalent content and function: no movement/animation, no loss of state, actions, focus or reading order. Commands and accessible values update immediately and never wait for an effect. Do not animate timer/sensor numbers, invent progress or saved results, run continuous decorative loops, or queue obsolete transitions. Preserve at least 48 dp targets, 200% text/scrolling, neutral Basalt, local Space Grotesk and icon geometry. Use small CSS transform/opacity effects first, without a new animation framework; actual Android WebView, TalkBack, touch, frame pacing and battery evidence remain implementation acceptance, not established by the browser studies.

### Core/Manage motion delivery boundary

The bounded Core slice applies press feedback to existing buttons through a visual backplate only; the button box, text, focus outline and at-least-48-dp hit area do not scale. Real service phase/pause labels receive state feedback only when their state key changes, not on timer/sensor refresh. Actual Training/Manage navigation and modal/target-field disclosure use the approved standard/exit timings without awaiting animation completion. Focus and saved values update through their existing paths. Reduced motion cancels active visual effects and leaves equivalent immediate content and operation.

Result feedback is a short outline on the existing updated region only after successful preferences persistence. It is not a toast, save queue or fabricated history/export result. Entrance choreography uses at most three existing groups with 24-ms stagger; dose/timer/sensor values never count up or receive an individual number animation. The C1 card keeps the short `C1 · Intervals` header with protocol detail in its dose section, reorder icons remain visually secondary with unchanged hit areas, and the C2 slider uses the approved accent.

All studies 01–07 remain required on their real surfaces. This slice provides only local Training/Manage navigation, not the future full Training/History/Settings destination system. Determinate progress (05) still requires a real approved progress surface with an honest numerator/denominator; no progress bar is added to demonstrate it. Other history/export/reader/Ghost applications retain their feature dependencies and holds. Native reduced-motion, focus, 200% text and touch verification is required; source code and browser checks do not establish TalkBack, frame pacing, outdoor or battery acceptance.


### Work values delivery boundary

The timed Core Work view reads its phase, repetition, RPE and phase-appropriate target from the frozen service snapshot. Main set and current kilometre split share time, measured distance, full-scope average pace and valid-time-weighted average HR rows, with explicit known/unknown coverage. During warm-up, total-so-far is labelled as warm-up rather than main. During cool-down, the final main and last recorded split remain unchanged; live values and total may continue. Missing evidence is unavailable, not a fabricated zero. Live pace is the separately versioned service-owned trusted-edge estimate described in [running workouts](running-workouts.md#live-pace-technical-pilot); it never replaces the strict aggregate average. Native rendering, text scaling, accessibility and actual GPS-to-journal acceptance must be verified for the delivery candidate. This does not add pairing, personal-zone settings, coaching, new navigation or an arbitrary workout editor.
