# Team workflow and plugin ownership

## Setup

Install and enable [Dev Team](https://chatgpt.com/plugins/plugins_6af747e58e648191939420739312d5bd) in the project chat. Use the canonical `dev-team` replacement package and current compatible release, not a pinned cache directory. Verify that the installed release contains the separate system/software architect and retrospective workflow. The skill catalog must expose dev-team:team-lead, dev-team:fullstack-developer, dev-team:requirements-analyst, dev-team:qa-engineer, dev-team:ux-designer and dev-team:architect.

Follow [the AGENTS.md loader](../AGENTS.md#roles-and-decision-making) before substantive work. Read the current installed team agreement and retrospective reference in full and give each specialist its matching role skill and task context. Installing or saving a release does not spawn persistent agents or prove that an existing chat refreshed its skills. Verify the installed release and components; report a missing or stale prerequisite instead of silently continuing with the former four-specialist team. Host tools and concurrency limits determine how many agents can work simultaneously; keep role independence and schedule participation in turns as needed.

The canonical replacements are [Dev Workflow](https://chatgpt.com/plugins/plugins_6af747dbe37881918bde3e074e2c6c77), package `dev-workflow`, and [Dev Team](https://chatgpt.com/plugins/plugins_6af747e58e648191939420739312d5bd), package `dev-team`. These are new backend identities, not a rename or alias of the old packages. Install and enable the replacements, then verify their current catalog before relying on them. In Dev Workflow use `dev-workflow:working-mode`, `dev-workflow:setup-plugin` and `dev-workflow:maintain-plugin`; use `dev-team:*` for the team role skills listed above.

The old `simon-dev-workflow:*` and `loop-team:*` packages may remain installed for rollback, but they are not canonical loaders or aliases for the replacements. The old `simon-working-mode`, `setup-simon-plugin` and `maintain-simon-plugin` names are historical identifiers, not aliases for the new skill names. If a chat still exposes the old catalog, refresh it and verify the replacements before dependent work; report an unavailable prerequisite rather than silently substituting a legacy package. Retention is not fallback authorization. Any exception requires an explicit project-scoped decision under AGENTS.md. This migration does not change the LOOP Cx app identity, grant cross-project authority or change publication permissions.

## Single owners

| Owner | Responsibility |
|---|---|
| Dev Workflow | Global working agreement and reusable development conventions |
| Dev Team plugin | Detailed team competencies, role skills, collaboration principles and retrospective procedure |
| [AGENTS.md](../AGENTS.md) | This project's delegation, merge authorization, status rules, language and publication restrictions |
| [Product specification owners](../knowledge/product.md) | Current product behavior, architecture, acceptance and future-stage boundaries |
| [Design decisions](../design/decision.md) and [HTML references](../design/mockups/README.md) | Actual design approvals, holds and visual references |
| [GitHub Projects](https://github.com/users/simlindahl/projects/2) | Current work, action owners, status, next steps and evidence |

The architect subagent supports Simon with analysis and recommendations. Significant architecture, technology, product, cost and design decisions remain Simon's. Specialist capabilities do not require cloud services, microservices, AI or other technology; [the approved local modular monolith](../knowledge/architecture.md) remains the baseline.

The team lead owns efficient collaboration and applies minimalism and Pareto across delegation, handoffs, review and verification. Retrospectives never relax independent requirements/QA review, exact-revision evidence, required checks, acceptance, security or accessibility. The [repository merge conditions](../AGENTS.md#work-tracking-and-status), existing implementation holds and physical-device gates remain in force. USB installation offers follow [the phone workflow](android.md#usb-phone-installation).

## Process changes

Use the installed plugin's references/retrospectives.md for triggers, direct participation by all five specialists, selection of concrete actions, implementation and follow-up. The team lead coordinates this work and reports material outcomes or decisions to Simon; routine feedback stays inside the team.

Update the owner of a process improvement. Reusable role or retrospective changes belong in Dev Team's source and release workflow, followed by read-back and installed-version verification. Project-specific instructions and local skills change through the normal repository PR, independent review and required checks. Cross-link these owners instead of maintaining conflicting copies. Do not change Dev Workflow's global agreement merely to duplicate this project's delegated model.

## Current review handoff

The team lead maintains one clearly labelled current handoff in the existing PR description. Use the [PR template](../.github/pull_request_template.md): exact head and base revisions, bounded diff, owning requirements and acceptance, design status, applicable verification/artifact manifest and remaining gates. Distinguish approved from preliminary design, static references from runtime behavior, and automated evidence from physical acceptance. Select visual evidence relevant to the changed screens; a full-gallery capture is not a universal requirement.

When the head or base changes, update that handoff and reassess affected evidence under the existing merge rules. Keep earlier results as labelled historical evidence rather than presenting them as current. After merge, reconcile the live issue and Project Next step with delivered scope and remaining acceptance; do not let an old candidate description remain the current status.

## Retrospective record

Record this project's completed retrospectives in this section through the normal documentation workflow. Each concise dated entry states participating roles, evidence, keep/change decisions, one to three concrete actions with owners, the owning file or workflow, state, expected observable benefit and the next relevant follow-up point. Link actions with remaining work to the existing Project rather than creating another board. At follow-up, record the observed effect and whether the team kept, adjusted or reverted the improvement; label unmeasured effects explicitly.

### 2026-11-13 · First delivery retrospective

Participants: team lead, requirements analyst, QA engineer, fullstack developer, UX designer and system/software architect, each providing direct input. All six agreed the three actions below before implementation. Evidence: the dependent Android/Ghost delivery [#23](https://github.com/simlindahl/loop-cx/pull/23), [#24](https://github.com/simlindahl/loop-cx/pull/24) and [#25](https://github.com/simlindahl/loop-cx/pull/25), approved brand and data-preserving phone update [#40](https://github.com/simlindahl/loop-cx/pull/40), specification split [#45](https://github.com/simlindahl/loop-cx/pull/45) and team-plugin ownership [#47](https://github.com/simlindahl/loop-cx/pull/47). Improvements are tracked in [#48](https://github.com/simlindahl/loop-cx/issues/48).

Keep bounded changes, separate requirements/QA review of the exact revision and explicit design/physical gates. The lead observed that these preserved independent scope and revision evidence. Requirements identified stale issue/base descriptions; QA identified head transitions and signing-proof friction. UX distinguished static review and local presentation material from runtime approval. The architect, who joined recently, found a duplicated four-role roster. Fullstack observed that a probe against an uncomposed stale parent produced avoidable conflicts, and that an assumed key path and stale signed output required correction before installation. Actual installed certificate comparison prevented an incompatible update; database and journal hashes were unchanged after the successful update. No productivity gains have been measured.

| Action | Owner and owning files/workflow | State and expected benefit | Follow-up |
|---|---|---|---|
| A1 · One current review handoff, with labelled history and merge-time issue/Next step reconciliation | Team lead; this section, PR template and visual-evidence pointer in the mockup README; live issues #1/#18 and Projects remain the status owners | Documentation implemented; lead verified live issue #1/#18 status reconciliation and labelled superseded merge instructions as history. Expected to prevent stale head/base, scope and design/evidence assumptions | Next app PR: assess whether reviewers can identify the current revision, relevant evidence and remaining gates without reconstructing history |
| A2 · Verify installed certificate → existing compatible key → final APK certificate/hash and source | Fullstack; [USB workflow](android.md#usb-phone-installation), existing local artifact manifest; QA reviews the proof | Documentation implemented in this candidate. Expected to avoid repeating the wrong-key/stale-output signing path; no private key material or credentials in Git | Next accepted USB update: record whether the final package matches the installed certificate before installation |
| A3 · Cross-link canonical role ownership and check stale references for process changes | Fullstack; [delivery governance](../knowledge/delivery-acceptance.md#project-governance). Team lead; [repository instructions](../AGENTS.md#roles-and-decision-making) and scoped process review | Documentation implemented in this candidate. Expected to prevent competing role rosters | Next process PR: inspect affected references for obsolete role counts and pointers; keep, adjust or revert based on the result |

The actions are documented in the delivery PR linked from [#48](https://github.com/simlindahl/loop-cx/issues/48); use that PR for current review and merge state. Their effects remain unmeasured; follow-up has not occurred and is due at the relevant checkpoints above. At that retrospective checkpoint C2 product definition and early sensor feasibility were outstanding. Subsequently PR #50 delivered the approved initial C2 slice and PR #64 delivered bounded early native feasibility; remaining full-product/physical acceptance stays with its owning issues.

## Planned baseline review · issue #61

Review cutoff: main `9334e94d19f06143dd6c691a913a5f879f92f463` and the frozen nine open issues #2/#3/#4/#18/#42/#43/#44/#61/#62. All five specialists supplied their own baseline inputs and the lead supplied delivery assessment. They reviewed all eleven owning Git documents reached through [the product index](../knowledge/product.md#specification-ownership), including architecture, delivery, the approved training guide and reader hold. UX also checked the 90-screen gallery/mapping with no broken anchors reported. These are qualified **planning-baseline** outcomes, not feature acceptance or approval of an unreviewed documentation candidate. The [#61 current review handoff](https://github.com/simlindahl/loop-cx/issues/61) owns exact candidate confirmations, independent requirements/QA reviews and required CI; no whole-team completion is claimed before those confirmations.

| Item / specification owner | Consolidated baseline direction and remaining condition |
|---|---|
| #2 · [Running workouts](../knowledge/running-workouts.md) | Qualified planning approval. Timed rules exist; distance, movement/autopause, statistics and unknown-data tests remain. Proposed timing/coaching interpretations remain explicit product decisions, not silently approved rules. Core 38-minute and generic 28-minute protocols at this cutoff are different fixtures. |
| #3 · [Screens/design](../knowledge/ux-design.md), [Core decision](../knowledge/loop-cx.md) | Qualified planning approval. Initial C1/C2 start/readability is delivered; favourites CRUD/cancel, frozen history, full statistics, offline/a11y and physical evidence remain. Detailed preliminary views do not become approved by this review. |
| #4 · [Integrations/data](../knowledge/integrations-data.md), [delivery](../knowledge/delivery-acceptance.md) | Qualified planning approval. Durable journal and isolated own-record HC probe are foundations; production idempotent exporter/retries, independent JSON parsing and real WHOOP import/credit remain. |
| #18 · [Ghost](../knowledge/ghost-run.md), [architecture](../knowledge/architecture.md) | Qualified direction, conditional delivery: additive native archive writer/migration/rollback/retry/reopen, then service-owned race evidence/reference and physical tests. Never infer Verified automatically. Race clock/GPS continue during training pause; detailed UI remains gated. |
| #42 · [M2 rename gate](../knowledge/loop-core-roadmap.md#rename-at-the-start-of-m2) | Qualified planning approval. M2 follows M1; explicit identity/signing/data/link decision and real compatible-upgrade proof are required. Retaining package identity is an architect recommendation, not an owner decision. |
| #43 · [Complete Core](../knowledge/loop-core-roadmap.md#complete-core-and-management-boundaries), [guide](../knowledge/what-is-loop.md) | Qualified boundary approval, not implementation-ready. Sx execution/recording and historical preservation need concrete decisions/layouts. Running history cannot establish full Core rotation. |
| #44 · [M lifecycle](../knowledge/loop-core-roadmap.md#product-decisions-still-open) | Qualified boundary approval, not implementation-ready. Separate management/predefined selection is the constraint; supplier, operations, defaults, version/deletion/history behavior remain open. |
| #61 · [Product ownership](../knowledge/product.md), this review | Support the one-time review with routine canonical corrections and exact-head confirmation. Preserve minimalism and existing approval/physical gates; do not create a perpetual six-role ceremony or parallel board. |
| #62 · [Motion contract](../knowledge/ux-design.md#approved-motion-and-interaction-system) | All 01–07 and exact values are owner-approved implementation scope. Real-surface dependencies remain, reduced-motion/direct-command/hit-area/truthful-progress acceptance is required; no extra general approval gate and no held-feature release. |

| Actual role input at the cutoff | Qualified outcome / evidence boundary |
|---|---|
| Requirements analyst | Supports the planning baseline after routine corrections; preserves proposed choices, M2 open decisions and Ghost/reader gates. |
| Fullstack developer | Supports feasibility of all nine bounded directions in the existing local modular monolith; absent production features remain absent and dependencies explicit. |
| QA engineer | Supports the corrected planning baseline with observable rule, persistence, idempotency, migration, accessibility and physical acceptance; no test pass is invented for future work. |
| UX designer | Supports the scope with approved shared Training/Work direction and six reader references; other detailed views remain preliminary, reader runtime held, production HC needs its own privacy/UI design. |
| Architect | Supports bounded local implementation, additive archive storage and race-clock ownership; no new server, automatic Verified status or unapproved identity migration. |
| Team lead | Supports measurement/rules before dependent #3/#4 integration, isolated archive work before held Ghost UI, M2 after M1/#42, all motion patterns against real surfaces, and #58/#63 as discovery only. |

Routine corrections in this delivery reconcile motion approval/dependencies, repository visibility versus reader hold, delivered Windows/C2/native/phone evidence, ordinary-map versus Ghost-live exception, API34 framework availability versus actual API35 probe and current revision 5 design pointers. The lead separately reconciled live #42–44 roadmap metadata, #4 production-versus-probe scope and #3's stale next action; live issue status remains in Projects. No app feature is implemented by this documentation change.

Later decisions/items need affected reassessment rather than inheriting this cutoff's approval. #58 self-comparison and #63 enrichment remain discovery links in their owning specifications. Simon's subsequent decision to use app C1 preparation defaults 3/2 minutes is authorized for [separate feature delivery #65](https://github.com/simlindahl/loop-cx/issues/65) with canonical specs/decision first and preservation of active/saved original plans; it is not implemented here and does not silently rewrite the guide's historical/source 10/5 dose. The proposed timing/coaching interpretations and M2 lifecycle/recording/identity choices remain unresolved until their relevant delivery.
