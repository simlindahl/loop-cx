---
type: Product Specification
title: LOOP knowledge feature
---
# LOOP knowledge feature

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

## LOOP knowledge — review proposal

An offline reading feature under **Settings → What is LOOP?**, aimed at experienced exercisers adapting training to a busy life. Retain Training, History and Settings as the only main destinations. The feature introduces the complete Core rotation, not just the app's running sessions. It must work without an account, app setup, WHOOP or another wearable.

The proposed landing page has a short introduction, a primary Get started action and optional workout and learning links. Get started is a four-part reading guide: understand the rotation; choose exercises and load; complete the first session; continue and adapt. It does not configure workouts, mark lessons complete or infer the next Core step from the running log. Bundle all explanatory content and references locally; opening an external research source is optional and requires internet.

Content baseline: [What is LOOP? draft](what-is-loop.md). Simon approved the guide and detailed screens at revision `4a4c97170c858c2206b507b0cd9296795b5ccc84` on 13 November 2026. See the [review brief](../docs/loop-knowledge-review.md) and [approval conditions](../design/decision.md#what-is-loop--content-and-layout-review). App implementation remains on hold pending a separate instruction.

Review acceptance:
- From Settings, open What is LOOP?, read Get started, then return to the introduction and Settings without a new main tab.
- Explain C1 → S1 + M → C2 → S2 + M, all four workout cards, safe technical failure, gym/home substitutions and continuing after interruptions.
- Keep two working sets to technical failure as the Core default; identify this as a design choice rather than a universal research requirement.
- Explanations remain usable offline. External source links are clearly identified. No private second-brain links or personal race data appear.
- Proposed views use Basalt, local Space Grotesk, visible navigation labels and wrapping text. Static review does not establish Android accessibility or screen-reader behavior.
