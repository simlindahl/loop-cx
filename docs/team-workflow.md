# Team workflow and plugin ownership

## Setup

Install and enable [LOOP Team](https://chatgpt.com/plugins/plugins_6af71e9449cc819192c5eb66934c2483) in the project chat. Version 0.2.0 introduced the separate system/software architect and retrospective workflow; use the current installed compatible release, not a pinned cache directory. The skill catalog must expose loop-team:team-lead, loop-team:fullstack-developer, loop-team:requirements-analyst, loop-team:qa-engineer, loop-team:ux-designer and loop-team:architect.

Follow [the AGENTS.md loader](../AGENTS.md#roles-and-decision-making) before substantive work. Read the current installed team agreement and retrospective reference in full and give each specialist its matching role skill and task context. Installing or saving a release does not spawn persistent agents or prove that an existing chat refreshed its skills. Verify the installed release and components; report a missing or stale prerequisite instead of silently continuing with the former four-specialist team. Host tools and concurrency limits determine how many agents can work simultaneously; keep role independence and schedule participation in turns as needed.

## Single owners

| Owner | Responsibility |
|---|---|
| Simon Dev Workflow | Global working agreement and reusable development conventions |
| LOOP Team plugin | Detailed team competencies, role skills, collaboration principles and retrospective procedure |
| [AGENTS.md](../AGENTS.md) | This project's delegation, merge authorization, status rules, language and publication restrictions |
| [Product specification owners](../knowledge/product.md) | Current product behavior, architecture, acceptance and future-stage boundaries |
| [Design decisions](../design/decision.md) and [HTML references](../design/mockups/README.md) | Actual design approvals, holds and visual references |
| [GitHub Projects](https://github.com/users/simlindahl/projects/2) | Current work, action owners, status, next steps and evidence |

The architect subagent supports Simon with analysis and recommendations. Significant architecture, technology, product, cost and design decisions remain Simon's. Specialist capabilities do not require cloud services, microservices, AI or other technology; [the approved local modular monolith](../knowledge/architecture.md) remains the baseline.

The team lead owns efficient collaboration and applies minimalism and Pareto across delegation, handoffs, review and verification. Retrospectives never relax independent requirements/QA review, exact-revision evidence, required checks, acceptance, security or accessibility. The [repository merge conditions](../AGENTS.md#work-tracking-and-status), existing implementation holds and physical-device gates remain in force. USB installation offers follow [the phone workflow](android.md#usb-phone-installation).

## Process changes

Use the installed plugin's references/retrospectives.md for triggers, direct participation by all five specialists, selection of concrete actions, implementation and follow-up. The team lead coordinates this work and reports material outcomes or decisions to Simon; routine feedback stays inside the team.

Update the owner of a process improvement. Reusable role or retrospective changes belong in LOOP Team's source and release workflow, followed by read-back and installed-version verification. Project-specific instructions and local skills change through the normal repository PR, independent review and required checks. Cross-link these owners instead of maintaining conflicting copies. Do not change Simon Dev Workflow's global agreement merely to duplicate this project's delegated model.

## Retrospective record

Record this project's completed retrospectives in this section through the normal documentation workflow. Each concise dated entry states participating roles, evidence, keep/change decisions, one to three concrete actions with owners, the owning file or workflow, state, expected observable benefit and the next relevant follow-up point. Link actions with remaining work to the existing Project rather than creating another board. At follow-up, record the observed effect and whether the team kept, adjusted or reverted the improvement; label unmeasured effects explicitly.

No retrospective is recorded yet. This document establishes the destination and does not claim that team feedback, consensus or productivity gains have already been collected.
