# Approved Core-first design reference

Simon approved this complete Core-first layout and interaction direction, followed by explicit dose changes: SIT now has eight efforts and Threshold has three six-minute efforts. Those content deltas require no further dose/design approval. This is a standalone browser mockup, not app implementation or native acceptance evidence. The older arbitrary workout builder is historical and superseded.

## Approved protocols

| Protocol | Work / recovery | Fixed target RPE | Global preparation default | Main / total |
| --- | --- | --- | --- | --- |
| C1.1 — Sprint intervals (SIT) | 8 × 30 sec maximal/all-out / 7 × 3 min easy | 10 / 2 | 4 / 3 min | 25 / 32 min |
| C1.2 — Aerobic intervals (HIIT) | 5 × 3 min / 4 × 2 min | 7 / 3 | 4 / 3 min | 23 / 30 min |
| C1.3 — Sustained intervals (Threshold) | 3 × 6 min / 2 × 3 min | 6 / 3 | 4 / 3 min | 24 / 31 min |
| C2 — Zone 2 | 20–120 min, step 5; 45 default | 4 | None | Selected duration for both |

No recovery follows the final effort. SIT is maximal/all-out, not controlled HIIT and not an automatic progression for an inexperienced/returning runner. Programme intent and target RPE do not establish an individual's measured VO₂max, threshold or pulse zone. The owning running specification records research and transfer limits.

## Approved interaction

- Two Core cards. C1 initially selects C1.2, then retains the user's last selected official variant. Change opens three compact radio choices, with purpose, dose, RPE and main/total visible before start.
- Nearby RPE information opens short, variant-specific effort guidance. RPE remains plain read-only text. SIT help explicitly describes maximal effort; HIIT help remains controlled and repeatable.
- Optional additional target is Pace OR Heart-rate zone, with work and optional recovery in that chosen mode. Targets are intentions, not measured values.
- C1 Personalise adjusts only work repetition count and optional pace OR zone. Work/recovery lengths and RPE stay fixed. Main set is `N × work + (N−1) × recovery`, including recovery but excluding preparation, constrained to 10–60 min. Count limits are SIT 4–18 (default 8; 11–60 min main), HIIT 3–12 (default 5; 13–58 min) and Threshold 2–7 (default 3; 15–60 min). These are product guardrails, not validation of every allowed dose, particularly maximal SIT. No automatic progression or default increase occurs.
- Choosing an official C1 protocol uses its official count default. A saved shortcut retains its chosen count/target; global preparation is resolved at its next start, and previous plans remain frozen. Main/total and actual count are visible before start. Count buttons have 48px targets, a labelled integer input and validation alerts; numbers update without motion.
- C2 Change uses an accessible range input 20–120 minutes, step 5, initially 45, with visible/live accessible minutes. Fixed RPE 4 is read-only and establishes no measured HR zone. This supersedes the earlier 30/45/60 presets. A saved C2 shortcut retains the chosen duration and target.
- Shortcuts create neither new C1.x protocols nor revision numbers. C1.1/.2/.3 identify official protocols; source revisions and internal IDs remain separate implementation metadata.
- One global C1 preparation pair defaults to 4/3. Settings overrides apply at future starts, including existing shortcuts. C2 never inherits preparation. Previously started/completed runs retain their resolved frozen plans. Runtime +1 min and skip remain available without changing Settings.

The source of authority is [the design decision](../../decision.md) and [the owning workout specification](../../../knowledge/running-workouts.md). The initial selection is a convenience default, not a fitness recommendation. Last-selection persistence is represented only within this page's memory; production persistence is not demonstrated.

## Remaining limits

C1 repetition guardrails and C2 duration 20–120 / step 5 / default 45/RPE 4 are explicit owner decisions; no further question is required. Settings uses positive whole minutes to illustrate the form, without claiming approval of final zero/bounds validation. There is no generic unit builder, per-run/per-favourite preparation editor, automatic progression, History redesign, reader release or Ghost approval. Review framing and synthetic snapshots outside the phone are not app UI.

## Design and accessibility

Neutral Basalt: #111111 background, #1C1C1C surface, #F2F2F2 text, #ADADAD secondary text, #C4DA73 accent and decorative #505050. Space Grotesk loads from the repository's local font. Hit areas remain at least 48px; focus/control cues use higher contrast than decorative borders. Dialogs have accessible names, initial title focus, keyboard loop, Close/Escape and focus return.

Press 80/160ms and visual .98 preserve the hit area. Entrance 220ms/cubic-bezier(.2,0,0,1)/4px and system reduced motion follow the approved motion direction. All seven families remain applicable to genuine operations; no fake progress, animated sensor readout or outcome is introduced.

## Verification boundary

Browser checks cover current arithmetic, RPE dialogs/focus, shared Settings across C1, shortcut targets, immutable synthetic start snapshots, C1 count boundaries/neighbours/fractions and C2 slider bounds/step/default/RPE 4 and saved shortcut duration without preparation, narrow layouts and doubled text. The canonical copy must match the reviewed reference except its repository-relative font URL. Actual Android text scaling, TalkBack, physical touch, safe areas, notifications, background recording and persistence remain implementation QA. No user research, training efficacy result or native acceptance is claimed. No sensor, health data, export or real recording occurs; reload resets synthetic in-memory data.

Latest SIT recovery update: 3 minutes easy recovery is the explicit product choice, superseding 4 minutes. Main time is `0.5N + 3(N−1) = 3.5N−3`, so 4–18 work efforts satisfy the 10–60-minute main-set guard. The latest explicit default 8 gives 25 min main/32 min total. This does not alter a cited study’s original 4-minute recovery or claim that it directly validates the 3-minute product dose. Focused SIT math/guard/shortcut/freeze evidence replaces the old SIT-bound evidence; unchanged HIIT/Threshold/C2 and focus evidence is reused only for those unchanged inputs.
