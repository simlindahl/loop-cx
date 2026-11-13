---
type: Product Roadmap Specification
title: Future LOOP Core integration
---
# Future LOOP Core integration

## Scope and authorization

M2 follows [M1 · LOOP Cx](https://github.com/simlindahl/loop-cx/milestone/1). Simon requested planning and milestone metadata now, not future feature implementation. [M2 · LOOP](https://github.com/simlindahl/loop-cx/milestone/2) integrates the complete Core, including S1/S2 and M, after its initial rename gate. The existing no-server architecture remains; significant architecture, technology, cost and product choices stay with Simon.

## Rename at the start of M2

The first M2 work is to rename the app and GitHub repository to **LOOP**. Cx becomes the conditioning domain of LOOP rather than the product name. Through M1 the delivered app and repository remain LOOP Cx; this plan changes no current identity.

Before implementation, inventory the display name, launcher/branding, repository URL, README, internal/external links, Projects association, workflows, clone/remotes, APK naming, documentation and distribution instructions. Plan repository-link redirects and validate maintained links and automation after the rename; do not assume every external consumer follows redirects. The approved loop geometry and neutral palette are unchanged by this naming plan.

**Decision gate:** choose explicitly whether Android application ID, internal namespaces and data paths stay compatible or migrate. A product/display/repository rename does not itself authorize changing any of them. Assess installed APK upgrade compatibility, signing, local SQLite/files, Health Connect/external record IDs, export/session identities and recovery. No silent new package, uninstall, downgrade, data wipe or unsupported history conversion. Preserve existing data; agree and verify any migration before changing identity-bearing code.

Observable acceptance: an existing M1 installation updates through the approved path with the same recorded sessions/settings and a usable LOOP-branded app; required repository/document/workflow links resolve. Record actual signing/update/data evidence and remaining limitations. Do not claim this acceptance from a documentation rename.

## Complete Core and management boundaries

The [approved training guide](what-is-loop.md#get-started) owns Core content and safety guidance: C1 → S1 + M → C2 → S2 + M. S1 covers hinge, horizontal pull, single-leg squat and horizontal push; S2 covers single-leg squat, vertical pull, hinge and vertical push. M is the mixed finish after strength, not a fifth rotation step. Do not silently turn training examples into new mandatory app rules.

M management must be clearly **separate from Sx management**. Within Sx, select a **predefined M**. These are product constraints, not approval for standalone M execution, an automatic rotation engine or a custom exercise/workout library.

Observable acceptance examples for later detailed design:

- Managing M and managing Sx are distinct operations; a user editing Sx can select an existing predefined M without editing its definition as part of the Sx form.
- The complete Core supports the approved S1/S2 + M structure while retaining the current conditioning domain. Exact recording and execution behavior is specified and approved before implementation.
- Existing running history is preserved through the M2 transition; a running-only log does not claim to know completed strength/M steps.

## Product decisions still open

- What can M management create/edit/delete, who supplies predefined M, and what does “predefined” mean for ownership and lifecycle? No custom-library semantics are assumed.
- How are M selections/defaults attached to S1/S2, and what should happen to existing references/history when a definition changes or is removed?
- What Sx exercise/load/repetition and M execution/recording flows are required? Standalone M execution is not specified.
- Is rotation tracking wanted, and which activities count? Automatic advancement is not authorized by the milestone request.
- What detailed screens, accessibility and acceptance evidence are needed for these flows? New/material layouts require Simon's approval.

Resolve these with concrete proposals in the bounded M2 issues before implementation. Do not invent research, measured value, dates or ROI. The approved LOOP reader remains held for runtime implementation and Ghost detailed layouts remain preliminary; creating M2 does not lift either condition.

## Tracked delivery

- [#42 · M2 initial rename and transition gate](https://github.com/simlindahl/loop-cx/issues/42).
- [#43 · Complete Core/Sx integration](https://github.com/simlindahl/loop-cx/issues/43).
- [#44 · Separate M management and lifecycle decisions](https://github.com/simlindahl/loop-cx/issues/44).

Current documentation/milestone organization is tracked in [#41](https://github.com/simlindahl/loop-cx/issues/41). These links identify planned work, not implementation approval or acceptance.
