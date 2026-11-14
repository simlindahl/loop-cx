---
type: Product Roadmap Specification
title: Future LOOP Core integration
---
# Future LOOP Core integration

## Scope and authorization

M2 starts after acceptance of [M1 · LOOP Cx](https://github.com/simlindahl/loop-cx/milestone/1). This change records planning and milestone metadata for that future stage. [M2 · LOOP](https://github.com/simlindahl/loop-cx/milestone/2) integrates the complete Core, including S1/S2 and M, after its initial rename gate. The existing no-server architecture remains; significant architecture, technology, cost and product choices stay with Simon.

## Rename at the start of M2

The first M2 work is to rename the app and GitHub repository to **LOOP**. Cx becomes the conditioning domain of LOOP rather than the product name. Through M1 the delivered app and repository remain LOOP Cx; this plan changes no current identity.

Before implementation, inventory the display name, launcher/branding, repository URL, README, internal/external links, Projects association, workflows, clone/remotes, APK naming, documentation and distribution instructions. Plan repository-link redirects and validate maintained links and automation after the rename; do not assume every external consumer follows redirects. The approved loop geometry and neutral palette are unchanged by this naming plan.

**Owner decision, 14 November 2026:** rename technical identity as well as the visible app/repository name. The earlier recommendation to keep the Android package/internal code names was rejected. Simon confirmed that he is the only installed user and accepts a fresh installation without migrating local app history/settings. A data-preserving cross-package migration is therefore not a requirement: do not build a bridge, transfer endpoint or export/import process for this rename.

Inventory and consistently rename the application ID, namespaces/assembly/project references, activity/manifest bindings, scoped stylesheet references, workflow/script paths and package assumptions. Keep Cx where it denotes LOOP's conditioning domain. A new Android application ID creates a separate installation; describe the delivery as a fresh install, not an in-place update retaining old data. Exact target identifiers must be consistent and checked for collision on the intended device before delivery. Keep the existing signing material rather than creating a key merely for a naming change.

Observable acceptance: after M1 acceptance, the repository and human-facing identity are LOOP, maintained links/workflows/Projects association resolve, and the new technical identity builds, installs and starts successfully on the authorised phone. Simon permits removal/reinstallation of his old app for this transition; preservation or import of its local history is not an acceptance gate. Verify the actual final package/name/icon/startup and ordinary recording entry without claiming old-history continuity. This authorisation is specific to the M2 rename; normal M1 demo updates continue to use their data-retaining update workflow.

## Complete Core and management boundaries

The [approved training guide](what-is-loop.md#get-started) owns Core content and safety guidance: C1 → S1 + M → C2 → S2 + M. S1 covers hinge, horizontal pull, single-leg squat and horizontal push; S2 covers single-leg squat, vertical pull, hinge and vertical push. M is the mixed finish after strength, not a fifth rotation step. Do not silently turn training examples into new mandatory app rules.

M management must be clearly **separate from Sx management**. Within Sx, select a **predefined M**. These are product constraints, not approval for standalone M execution, an automatic rotation engine or a custom exercise/workout library.

Observable acceptance examples for later detailed design:

- Managing M and managing Sx are distinct operations; a user editing Sx can select an existing predefined M without editing its definition as part of the Sx form.
- The complete Core supports the approved S1/S2 + M structure while retaining the current conditioning domain. Exact recording and execution behavior is specified and approved before implementation.
- The fresh installation does not claim continuity of the old app's local history. Newly recorded running/Sx/M facts retain their correct ownership; a running-only log does not claim to know completed strength/M steps.

## Product decisions still open

- What can M management create/edit/delete, who supplies predefined M, and what does “predefined” mean for ownership and lifecycle? No custom-library semantics are assumed.
- How are M selections/defaults attached to S1/S2, and what should happen to existing references/history when a definition changes or is removed?
- What Sx exercise/load/repetition and M execution/recording flows are required? Standalone M execution is not specified.
- Is rotation tracking wanted, and which activities count? Automatic advancement is not authorized by the milestone request.
- What detailed screens, accessibility and acceptance evidence are needed for these flows? New/material layouts require Simon's approval.

Resolve these with concrete proposals in the bounded M2 issues before implementation. Do not invent research, measured value, dates or ROI. The approved LOOP reader remains held for runtime implementation; creating M2 does not lift that condition. Ghost's concrete detailed layout has its [separate recorded approval](../design/decision.md#ghost-layout-approval), without establishing runtime or physical acceptance.

## Tracked delivery

- [#42 · M2 initial rename and transition gate](https://github.com/simlindahl/loop-cx/issues/42).
- [#43 · Complete Core/Sx integration](https://github.com/simlindahl/loop-cx/issues/43).
- [#44 · Separate M management and lifecycle decisions](https://github.com/simlindahl/loop-cx/issues/44).

Current documentation/milestone organization is tracked in [#41](https://github.com/simlindahl/loop-cx/issues/41). These links identify planned work, not completed acceptance.
