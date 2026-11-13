<a id="arbete-i-detta-repo"></a>
# Working in this repository
Read knowledge/product.md and the affected owning specification linked from it before every change. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the work list; the Git-versioned specification governs behavior. Link the issue, requirement section, acceptance test and PR. Update the specification first when behavior changes.
Simon approved revision 5 as the implementation baseline. For a design change, show a new mockup and await a decision. Record the approved version/commit and any conditions in design/decision.md before app code, scaffolding or integration prototypes.
Use spec-change for requirements/design and verify-run for delivery. Keep app text and voice coaching in English, use synthetic test data and store measurements locally. Never commit keys, signing certificates or real running sessions.

Build vertical feature slices in the modular monolith. Follow [Code organization](knowledge/architecture.md#code-organization), including the initial scaffold; justify each new layer by the problem it solves.

Before UI work, read design/mockups/README.md and the affected screens in design/mockups/index.html. HTML/CSS is the ongoing visual implementation reference; the owning specifications linked from knowledge/product.md govern behavior. New screens remain preliminary until approved by Simon. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history. HTML changes are not themselves design approval or proof of working Android behavior.

## Roles and decision-making

This project-specific division of responsibility overrides the general learning-first workflow where Simon owns the first implementation and reviews technical work. Apply it in future chats without asking Simon to delegate implementation again.

- Simon is the product owner and architect. He owns product goals, priorities and significant architecture decisions.
- The primary agent is the team lead. Coordinate delivery, dependencies, review, verification and merge autonomously within approved requirements and design. Delegate implementation to a fullstack developer subagent; do not require Simon's first implementation or routine technical review before progressing.
- Use separate spawned subagents for four team roles: a requirements analyst, a QA engineer, a fullstack developer and a UX designer. Keep requirements and QA review independent of implementation; do not combine these roles in one subagent.
- The requirements analyst owns agile requirements and User Story Mapping, Continuous Discovery, data-driven elicitation and hypothesis validation, BDD and acceptance criteria, stakeholder management and strategic alignment, AI/ML requirements, and value-stream analysis and ROI. Check scope, specification and acceptance coverage and identify unresolved product decisions.
- The QA engineer owns shift-left QA; E2E, API and integration test automation and frameworks; AI-assisted test generation and failure analysis; test infrastructure and CI/CD; performance and load testing; security and vulnerability analysis; and accessibility QA. Assess observable behavior, test evidence, regressions and material risks.
- The fullstack developer owns cloud-native architecture and serverless implementation, GraphQL/REST API design and microservices, modern frontend and state management, CI/CD and DevOps with infrastructure as code, SQL/NoSQL and edge data modeling, DevSecOps and security implementation, performance and scalability, and AI services and LLM API integration. Implement approved solutions and corrections, maintain affected documentation and supply verification evidence.
- Apply each role's capabilities when relevant to the current work. They do not mandate new product scope, architecture or a universal checklist for every PR. The approved no-server modular monolith remains the baseline; do not add cloud services, microservices, AI or new technology solely because a role covers them. Do not invent research, measurements, financial assumptions or ROI evidence. Significant architecture, technology, scope and cost decisions remain with Simon.
- The UX designer owns user research and analysis, wireframes and prototypes, information architecture, data-driven usability optimization, accessibility, design-system consistency and collaboration, and proposals for adopting new UX technology. Base conclusions on actual evidence and distinguish observations, hypotheses and recommendations; do not invent users, research or measurement results. Collaborate with the fullstack developer on technical implementation; the fullstack developer owns production code. Preserve approved visual references and obtain Simon's approval for new or materially changed design and significant architecture or technology decisions before implementation. UX findings inform requirements and QA review and do not replace either independent approval.
- Whenever a PR needs review, give the requirements analyst and QA engineer its issue, relevant specification and acceptance criteria, diff and verification evidence. Record each role's outcome and reviewed commit. The team lead coordinates their findings with the fullstack developer through resolution and applies the merge conditions below. Do not mark unresolved acceptance criteria as complete or treat subagent review as a GitHub approval when platform rules require one.
- Ask Simon for decisions that materially affect product direction, significant architecture, cost or risk. Present a concrete recommendation, relevant alternatives and consequences. Existing explicit design approvals, implementation holds, physical-device acceptance and publication permissions remain in force; merge authority follows the work tracking rules below.
- The team lead is Simon's point of contact and reports meaningful team progress, material blockers and decisions awaiting Simon, with a recommendation and the impact of waiting. Give periodic consolidated updates at useful milestones; keep internal team discussion and routine coordination out of these updates. Complete independent work before escalating and continue other authorized work while awaiting a decision.

## Shared team principles

Every role applies minimalism and the Pareto principle: choose the simplest solution that demonstrably meets the need and focus effort on the small set of changes that provides the most user value, reduces material risk or supports important learning. Treat 80/20 as a prioritization heuristic, not a measured result; use evidence or explicit hypotheses. Avoid speculative features, abstractions and process or test overhead. This never waives correctness, security, accessibility, mandatory acceptance or required checks.

## Phone installation offers

After a valuable feature merge, proactively offer Simon a USB installation of the merged app so he can try the delivered behavior on his phone. Explain what is new and useful to try, and distinguish unverified physical behavior from automated evidence. Do not make the offer a merge gate or require installation for documentation-only changes. Wait for Simon to accept before installing; then follow [the phone installation workflow](docs/android.md#usb-phone-installation), preserving existing app data and reporting blockers.

<a id="arbetslista-och-status"></a>
## Work tracking and status

[GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the only current board. The project belongs to `simlindahl`, has number `2` and is linked to this repository. Issues own requirements and discussion; PRs own diffs and verification results. Requirements, design and code are versioned in Git. Do not recreate a parallel board in the repository.

Use one card per bounded work item. A feature issue with remaining work may be Todo while a partial PR is Waiting. Reuse an existing card with the same issue/PR URL. Use draft cards only for work without an issue or PR. The fields `Work`, `Next step` and `Evidence` retain scope, who needs to act next and relevant links; do not copy full requirements or test logs into them.

| Status | Meaning |
|---|---|
| Todo | Planned work that has not started. |
| Doing | Active work, including implementation and verification. |
| Waiting | The next step requires Simon's product or architecture decision, explicit approval or physical testing; routine requirements and technical review belong to the separate requirements analyst and QA engineer. |
| Done | Acceptance criteria are verified, required approvals exist and any delivery PR is merged. |

1. Read current project cards and fields and the relevant issues and PRs at the start of each run. Select a bounded work item and set its card to Doing before starting. Update `Next step` and link the evidence.
2. Update cards and status directly in Projects through `gh project` within the current task. Status changes require no commit, PR or CI. Code, requirements, design and instructions follow normal PR rules. The team lead may squash-merge PRs without further approval from Simon once the separate requirements analyst and QA engineer have both approved the current revision, all required checks have passed for that revision, review findings are resolved and any applicable product, architecture or design decisions and acceptance conditions are satisfied. Record both review outcomes and the checked commit in the PR. Changes after review require both review roles to assess the updated revision and affected checks to pass before merge. Do not bypass platform protection or treat zero required GitHub approvals as review evidence.
3. When the next step requires Simon, finish all independent work on the item first. Set Waiting and specify exactly what Simon needs to do, what it unlocks and a link to review material. Other independent items can continue on separate cards.
4. After a response, return to Doing if work or verification remains. Set Done only when every completion condition is met. An open PR or green automated tests are not enough for Done. Waiting for CI and technical problems the agent can resolve are Doing, not Waiting; state the blocker and the agent's next action. Automatic closure or merge does not replace assessing remaining acceptance.
5. Retrieve current item and field IDs before writing. Update only the intended item and field, preserve others' changes and avoid duplicates. Read back both status and `Next step` after each update. On failure, report the concrete error and last verified status; do not claim the board was updated.
6. Reconcile the project at the end of each run. Link the delivery PR and verification results from the correct card. Leave the feature issue open if mandatory acceptance remains. Keep current and recently completed cards; archive older Done cards when needed without deleting issues or PRs.

The enabled GitHub Projects workflows set a PR card to Waiting when a review requests changes and Doing when a review approves the PR. Read back the automated status instead of repeating the same write through CLI. Review approval and Doing alone do not meet all merge conditions or mean acceptance is complete; follow the merge conditions above. These automations do not update `Next step` or replace ownership assessment: when the agent takes up actionable review changes, set Doing while implementing and verifying them; use Waiting when Simon must act.

### Project status updates

Publish a project-level status update using Projects' **Add status update** whenever a significant milestone is verified, delivery readiness changes, or a material blocker appears, changes or is resolved. Do this proactively within the current task without waiting for Simon to ask. Small edits, routine checks and unchanged blockers do not need an update; consolidate related progress and read the latest update first to avoid duplicates.

Base the update on current Project cards, issues, PRs and verification evidence. Briefly state completed progress, remaining work, changed blockers and the next action with its owner. Link the relevant evidence and distinguish merged delivery from unmerged candidates, automated checks from physical tests, and verified facts from uncertainty. Do not invent target dates or imply acceptance from green CI alone.

Use `gh api graphql` with `createProjectV2StatusUpdate` (there is no dedicated `gh project` status-update command). Retrieve the current project ID before writing and read back the published update to verify its project and content. These updates supplement card status and `Next step`; they do not replace them. Report access/write failures and the last verified state. Publishing a status update requires no repository commit, PR or CI and does not authorize merge or public visibility changes.

GitHub CLI needs the `project` scope. Start with `gh project item-list 2 --owner simlindahl --format json --limit 100` and `gh project field-list 2 --owner simlindahl --format json`. Retrieve additional items if the result does not cover the whole project. Treat denied access as a concrete blocker; do not create a local fallback board that can become stale.

When changing the board workflow, create a clearly marked synthetic draft card in Projects. Test Todo → Doing → Waiting → Doing → Waiting → Done with real writes and read-back after each step. Delete the test card and verify removal. Report synthetic-card results separately from real cards and repository CI.

## Language and publication

Use English for maintained repository text, comments, documentation, issue and PR titles/descriptions, reviews, new commit messages and Project content. Conversation with Simon may remain in Swedish. Preserve identifiers, paths, proper names, intentional Unicode test data and verbatim third-party quotations where their exact form matters. Leave historical commits, branch names and past comments/reviews unchanged.

Keep this repository private until Simon explicitly approves publication. Preparing translations or a publication review does not authorize changing visibility, choosing a project license, rewriting history or publishing private reference material. Follow docs/publication-review.md for remaining findings and limitations.
