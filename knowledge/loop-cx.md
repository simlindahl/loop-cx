---
type: Product Decision
title: LOOP Cx for running
---
<a id="loop-cx--officiell-löparapp-för-loops-konditionspass"></a>
# LOOP Cx · official running app for LOOP conditioning workouts
Product direction approved on 12 November 2026. The current visual baseline is [approved revision 5 with neutral Basalt](../design/decision.md#current-decision); earlier revision reviews remain historical in that decision record.
Training reference: [self-contained Core guide](what-is-loop.md), approved as an implementation reference on 13 November 2026. [Product specification](product.md) governs app behavior; optional racing plugins remain outside the first knowledge feature.

Core governs training content. The app executes C1/C2 when running is selected. A running-only log cannot determine the next step in the complete C1 → S1 + M → C2 → S2 + M rotation. Keep free start and personal favourites. Rowing and cycling are valid within Core but outside the running app's current support.
- C1 Core: deliver a source-labelled favourite with 10 min progressive warm-up, 5 × 3 min controlled hard effort, 2 min easy movement between efforts and 5 min easy finish. Exactly 38 min total. Core contains an inconsistent reference to four intervals; use the table's five in the template. Avoid low-heart-rate warnings that increase pace early in a work phase. Use RPE as the primary target. Template RPE values are suggested app guidelines, not exact Core rules.
- C2 Core: a source-labelled zone 2 favourite, normally 45–60 min. Simon approved revision 1 defaults on 13 November 2026: exactly one 45-minute work phase, target RPE 3 as a suggested app guideline, and no separate warm-up or cool-down. The 45 minutes is both main-set and total active time for this initial template; duration remains adjustable. Let zone/conversational effort govern; Zone 2 is target intent until actual heart-rate integration supplies measurements. Global 3/2 min remains the default for custom workouts; do not silently apply it to Core C2.
- Show a short purpose while editing and label workouts as C1/C2/custom in history and export. Save the template source and revision with the session. An edited personal favourite must be able to differ from official template doses.
- 10K plugin: optional. Most time intervals can be expressed. Weeks 9–10 include a 3 × 2 km option with 2–3 min jogging, which conflicts with the approved shared unit. The plugin's 3 × 8 min alternative can be expressed. Introduce separate phase units only after deciding to support the complete plugin. Its C1 specifies 10–15 min easy warm-up and cool-down jogging. Do not automate weekly quotas.
