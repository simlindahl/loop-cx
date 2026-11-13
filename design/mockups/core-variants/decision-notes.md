# Approved Core-first design reference

Simon approved this complete Core-first layout and interaction direction, followed by explicit dose changes: SIT now has six efforts and Threshold has three six-minute efforts. Those content deltas require no further dose/design approval. This is a standalone browser mockup, not app implementation or native acceptance evidence. The older arbitrary workout builder is historical and superseded.

## Approved protocols

| Protocol | Work / recovery | Fixed target RPE | Global preparation default | Main / total |
| --- | --- | --- | --- | --- |
| C1.1 — Sprint intervals (SIT) | 6 × 30 sec maximal/all-out / 5 × 4 min easy | 10 / 2 | 4 / 3 min | 23 / 30 min |
| C1.2 — Aerobic intervals (HIIT) | 5 × 3 min / 4 × 2 min | 7 / 3 | 4 / 3 min | 23 / 30 min |
| C1.3 — Sustained intervals (Threshold) | 3 × 6 min / 2 × 3 min | 6 / 3 | 4 / 3 min | 24 / 31 min |
| C2 — Zone 2 | 30 / 45 / 60 min continuous; 45 default | 4 | None | Selected duration for both |

No recovery follows the final effort. SIT is maximal/all-out, not controlled HIIT and not an automatic progression for an inexperienced/returning runner. Programme intent and target RPE do not establish an individual's measured VO₂max, threshold or pulse zone. The owning running specification records research and transfer limits.

## Approved interaction

- Two Core cards. C1 initially selects C1.2, then retains the user's last selected official variant. Change opens three compact radio choices, with purpose, dose, RPE and main/total visible before start.
- Nearby RPE information opens short, variant-specific effort guidance. RPE remains plain read-only text. SIT help explicitly describes maximal effort; HIIT help remains controlled and repeatable.
- Optional additional target is Pace OR Heart-rate zone, with work and optional recovery in that chosen mode. Targets are intentions, not measured values.
- C2 Change uses the same compact radio pattern for exactly 30/45/60 minutes, initially 45. Its target RPE is 4, read-only, superseding the earlier 3; RPE does not establish a measured heart-rate zone. No arbitrary duration field is available.
- Named shortcuts capture official variant plus target; a C2 shortcut also retains its chosen preset duration. They create neither new C1.x protocols nor revision numbers. C1.1/.2/.3 identify official protocols; source revisions and internal IDs remain separate implementation metadata.
- One global C1 preparation pair defaults to 4/3. Settings overrides apply at future starts, including existing shortcuts. C2 never inherits preparation. Previously started/completed runs retain their resolved frozen plans. Runtime +1 min and skip remain available without changing Settings.

The source of authority is [the design decision](../../decision.md) and [the owning workout specification](../../../knowledge/running-workouts.md). The initial selection is a convenience default, not a fitness recommendation. Last-selection persistence is represented only within this page's memory; production persistence is not demonstrated.

## Remaining limits

C2 duration and RPE are explicitly decided: 30/45/60 presets, 45 default and RPE 4; no further question is required. Settings uses positive whole minutes to illustrate the form, without claiming approval of final zero/bounds validation. There is no generic unit builder, per-run/per-favourite preparation editor, automatic progression, History redesign, reader release or Ghost approval. Review framing and synthetic snapshots outside the phone are not app UI.

## Design and accessibility

Neutral Basalt: #111111 background, #1C1C1C surface, #F2F2F2 text, #ADADAD secondary text, #C4DA73 accent and decorative #505050. Space Grotesk loads from the repository's local font. Hit areas remain at least 48px; focus/control cues use higher contrast than decorative borders. Dialogs have accessible names, initial title focus, keyboard loop, Close/Escape and focus return.

Press 80/160ms and visual .98 preserve the hit area. Entrance 220ms/cubic-bezier(.2,0,0,1)/4px and system reduced motion follow the approved motion direction. All seven families remain applicable to genuine operations; no fake progress, animated sensor readout or outcome is introduced.

## Verification boundary

Browser checks cover current arithmetic, RPE dialogs/focus, shared Settings across C1, shortcut targets, immutable synthetic start snapshots, C2 presets/default/RPE 4 and saved shortcut duration without preparation, narrow layouts and doubled text. The canonical copy must match the reviewed reference except its repository-relative font URL. Actual Android text scaling, TalkBack, physical touch, safe areas, notifications, background recording and persistence remain implementation QA. No user research, training efficacy result or native acceptance is claimed. No sensor, health data, export or real recording occurs; reload resets synthetic in-memory data.
