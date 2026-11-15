---
type: Product Specification
title: LOOP knowledge feature
---
# LOOP knowledge feature

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

## LOOP knowledge — review proposal

An offline reading feature under **Settings → What is LOOP?**, aimed at experienced exercisers adapting training to a busy life. Retain Training, History and Settings as the only main destinations. The feature introduces the complete Core rotation, not just the app's running sessions. It must work without an account, app setup, WHOOP or another wearable.

The proposed landing page has a short introduction, a primary Get started action and optional workout and learning links. Get started is a four-part reading guide: understand the rotation; choose exercises and load; complete the first session; continue and adapt. It does not configure workouts, mark lessons complete or infer the next Core step from the running log. Bundle all explanatory content and references locally; opening an external research source is optional and requires internet.

Historical approved baseline: the guide and detailed screens at revision `4a4c97170c858c2206b507b0cd9296795b5ccc84`, approved on 13 November 2026. On 14 November Simon requested the [revised guide](what-is-loop.md) to explain assumptions, why choices were made and deliberate tradeoffs, and requested [canonical system ownership](training/system.md) in this repository under [#97](https://github.com/simlindahl/loop-cx/issues/97). The guide is the pedagogical projection, not another rule owner. The request is not approval of each revised sentence or a new reader layout. See the [review brief](../docs/loop-knowledge-review.md) and [recorded refresh scope](../design/decision.md#loop-content-ownership-refresh). App implementation remains on hold pending a separate instruction; existing offline/four-part/three-destination constraints remain.

Keep Get started actionable with brief rationale at the relevant choice; fuller assumptions, evidence limits and optional self-competition belong in Learn more or the bundled system reference. Keep critical safety instructions visible in the entry flow. The owner requested no visible RPE in the reader proposal; do not infer heart-rate-zone defaults from it. The separate [#96](https://github.com/simlindahl/loop-cx/issues/96) app presentation/target decision does not rewrite archived raw/export values or implement itself through this content change.

The current #97 genre is an independent, evidence-based [training reference](training/system.md) for informed athletes, coaches and practitioners. It explains physiology through practical programming and coaching consequences, with useful prescription tables, source qualifications and a complete reference list. The guide links to it one-way; product behavior, Ghost feature rules and provenance are excluded from the reference. Simon approved baseline `6184542` on 15 November and subsequently directed the [ESSENS foundation](../design/decision.md#essens-foundation-for-loop): simplicity, meaningful effort and enjoyment without migrating or renaming LOOP. This introduces no mandatory reflection flow, programme change or reader-runtime authorization. Later implementation/content changes retain normal independent review and existing design boundaries.

Review acceptance:
- From Settings, open What is LOOP?, read Get started, then return to the introduction and Settings without a new main tab.
- Explain C1 → S1 + M → C2 → S2 + M, all four workout cards, safe technical failure, gym/home substitutions and continuing after interruptions.
- Keep two working sets to technical failure as the Core default; identify this as a design choice rather than a universal research requirement.
- Explanations remain usable offline. External source links are clearly identified. No private second-brain links or personal race data appear.
- Proposed views use Basalt, local Space Grotesk, visible navigation labels and wrapping text. Static review does not establish Android accessibility or screen-reader behavior.
