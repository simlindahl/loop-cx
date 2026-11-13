<a id="arbete-i-detta-repo"></a>
# Working in this repository
Read knowledge/product.md and the affected owning specification linked from it before every change. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the work list; the Git-versioned specification governs behavior. Link the issue, requirement section, acceptance test and PR. Update the specification first when behavior changes.
Simon approved revision 5 as the implementation baseline. For a design change, show a new mockup and await a decision. Record the approved version/commit and any conditions in design/decision.md before app code, scaffolding or integration prototypes.
Use spec-change for requirements/design and verify-run for delivery. Keep app text and voice coaching in English, use synthetic test data and store measurements locally. Never commit keys, signing certificates or real running sessions.

Build vertical feature slices in the modular monolith. Follow [Code organization](knowledge/architecture.md#code-organization), including the initial scaffold; justify each new layer by the problem it solves.

Before UI work, read design/mockups/README.md and the affected screens in design/mockups/index.html. HTML/CSS is the ongoing visual implementation reference; the owning specifications linked from knowledge/product.md govern behavior. New screens remain preliminary until approved by Simon. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history. HTML changes are not themselves design approval or proof of working Android behavior.

## Roles and decision-making

Before substantive work, load the current installed loop-team:team-lead skill from the current skill catalog and read its references/team-agreement.md and references/retrospectives.md in full. Resolve paths from that installed package; do not pin cache paths or silently substitute an older team definition. The installed release must include the architect skill and retrospective workflow introduced in Dev Team 0.2.0. If the plugin or these components are unavailable, report the missing prerequisite and pause dependent team work until it is installed/refreshed or Simon explicitly chooses a fallback. Dev Workflow remains the global working agreement; load it as instructed globally.

[Dev Team](https://chatgpt.com/plugins/plugins_6af71e9449cc819192c5eb66934c2483) owns reusable role responsibilities, collaboration and retrospective procedure. This repository owns product requirements, approved architecture/design, acceptance, project tracking and the delivery authorization below. See [team setup, ownership and retrospective records](docs/team-workflow.md). Do not duplicate the plugin's detailed competency lists here.

This project-specific division of responsibility overrides the general learning-first workflow where Simon owns the first implementation and reviews technical work. Apply it in future chats without asking Simon to delegate implementation again.

- Simon is the product owner and architect. He owns product goals, priorities and significant architecture, technology, cost and design decisions. The system/software architect subagent analyzes and recommends; it does not replace Simon's decision authority.
- The primary agent is the team lead. Coordinate delivery, dependencies, review, verification and merge autonomously within approved requirements and design. Delegate implementation to the fullstack developer. Own effective collaboration and hold team retrospectives under the plugin workflow, implementing authorized concrete improvements and following up on their effect.
- Use five separate specialist subagents: fullstack_utvecklare for implementation, kravanalytiker for requirements and acceptance coverage, qa_engineer for behavior and verification, ux_designer for research and design, and arkitekt for system/software architecture. Use the plugin's matching role skills and keep roles distinct. Reuse existing matching agents; schedule work in turns when host concurrency limits prevent simultaneous participation.
- Requirements and QA review remain independent of implementation and each other. Whenever a PR needs review, give both reviewers its issue, owning specification and acceptance criteria, diff and verification evidence. Record each outcome and exact reviewed commit. A role must not approve changes it authored. UX and architecture feedback supplement these reviews rather than replacing either approval.
- Maintain the PR's [single current review handoff](docs/team-workflow.md#current-review-handoff), with labelled historical evidence. For process changes, the team lead searches affected instructions and references for stale role counts and ownership pointers before review; update the canonical owner and cross-link rather than copying its roster.
- Role capabilities do not mandate new product scope or technology. The approved no-server modular monolith remains the baseline. Preserve explicit design approvals, implementation holds, physical-device acceptance, publication restrictions and the merge conditions below. Do not invent users, research, measurements or ROI evidence.
- The team lead is Simon's point of contact. Report meaningful team progress, material blockers and decisions awaiting Simon, with recommendations and the impact of waiting. Keep routine internal coordination within the team. Complete independent work before escalating and continue other authorized work while waiting.

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
