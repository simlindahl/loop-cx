<a id="arbete-i-detta-repo"></a>
# Working in this repository
Read knowledge/product.md before every change. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the work list; the Git-versioned specification governs behavior. Link the issue, requirement section, acceptance test and PR. Update the specification first when behavior changes.
Simon approved revision 5 as the implementation baseline. For a design change, show a new mockup and await a decision. Record the approved version/commit and any conditions in design/decision.md before app code, scaffolding or integration prototypes.
Use spec-change for requirements/design and verify-run for delivery. Keep app text and voice coaching in English, use synthetic test data and store measurements locally. Never commit keys, signing certificates or real running sessions.

Build vertical feature slices in the modular monolith. Follow [Code organization](knowledge/product.md#code-organization), including the initial scaffold; justify each new layer by the problem it solves.

Before UI work, read design/mockups/README.md and the affected screens in design/mockups/index.html. HTML/CSS is the ongoing visual implementation reference; knowledge/product.md governs behavior. New screens remain preliminary until approved by Simon. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history. HTML changes are not themselves design approval or proof of working Android behavior.

## Work tracking and status

[GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) is the only current board. The project belongs to `simlindahl`, has number `2` and is linked to this repository. Issues own requirements and discussion; PRs own diffs and verification results. Requirements, design and code are versioned in Git. Do not recreate a parallel board in the repository.

Use one card per bounded work item. A feature issue with remaining work may be Todo while a partial PR is Waiting. Reuse an existing card with the same issue/PR URL. Use draft cards only for work without an issue or PR. The fields `Work`, `Next step` and `Evidence` retain scope, who needs to act next and relevant links; do not copy full requirements or test logs into them.

| Status | Meaning |
|---|---|
| Todo | Planned work that has not started. |
| Doing | Active work, including implementation and verification. |
| Waiting | The next step requires Simon's input, decision, review, approval or physical testing. |
| Done | Acceptance criteria are verified, required approvals exist and any delivery PR is merged. |

1. Read current project cards and fields and the relevant issues and PRs at the start of each run. Select a bounded work item and set its card to Doing before starting. Update `Next step` and link the evidence.
2. Update cards and status directly in Projects through `gh project` within the current task. Status changes require no commit, PR or CI. Code, requirements, design and instructions follow normal PR rules. Simon approves merge unless merge of that specific PR has been explicitly delegated. Zero required GitHub approvals do not replace this approval.
3. When the next step requires Simon, finish all independent work on the item first. Set Waiting and specify exactly what Simon needs to do, what it unlocks and a link to review material. Other independent items can continue on separate cards.
4. After a response, return to Doing if work or verification remains. Set Done only when every completion condition is met. An open PR or green automated tests are not enough for Done. Waiting for CI and technical problems the agent can resolve are Doing, not Waiting; state the blocker and the agent's next action. Automatic closure or merge does not replace assessing remaining acceptance.
5. Retrieve current item and field IDs before writing. Update only the intended item and field, preserve others' changes and avoid duplicates. Read back both status and `Next step` after each update. On failure, report the concrete error and last verified status; do not claim the board was updated.
6. Reconcile the project at the end of each run. Link the delivery PR and verification results from the correct card. Leave the feature issue open if mandatory acceptance remains. Keep current and recently completed cards; archive older Done cards when needed without deleting issues or PRs.

GitHub CLI needs the `project` scope. Start with `gh project item-list 2 --owner simlindahl --format json --limit 100` and `gh project field-list 2 --owner simlindahl --format json`. Retrieve additional items if the result does not cover the whole project. Treat denied access as a concrete blocker; do not create a local fallback board that can become stale.

When changing the board workflow, create a clearly marked synthetic draft card in Projects. Test Todo → Doing → Waiting → Doing → Waiting → Done with real writes and read-back after each step. Delete the test card and verify removal. Report synthetic-card results separately from real cards and repository CI.

## Language and publication

Use English for maintained repository text, comments, documentation, issue and PR titles/descriptions, reviews, new commit messages and Project content. Conversation with Simon may remain in Swedish. Preserve identifiers, paths, proper names, intentional Unicode test data and verbatim third-party quotations where their exact form matters. Leave historical commits, branch names and past comments/reviews unchanged.

Keep this repository private until Simon explicitly approves publication. Preparing translations or a publication review does not authorize changing visibility, choosing a project license, rewriting history or publishing private reference material. Follow docs/publication-review.md for remaining findings and limitations.
