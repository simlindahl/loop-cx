Closes #

<a id="ändring"></a>
## Change
Describe the behavior and link its requirement section. For a design change, link the design decision.

## Current review handoff

- Exact head and base revisions; bounded diff and linked issue/owning specification/acceptance criteria.
- Design status: approved or preliminary; static reference or runtime change; applicable decision and holds.
- Applicable verification and artifact manifest links, tested revision/environment, evidence limits and pending gates.

Maintain this as the single current handoff under [the team workflow](../docs/team-workflow.md#current-review-handoff). Label superseded results as history when revisions change.

<a id="verifiering"></a>
## Verification
State the test environment and observed result. Distinguish emulator tests from physical tests and report remaining checks.

## Requirements and test review
Record the separate requirements analyst and QA engineer subagents' outcomes and reviewed commits, requirement and acceptance coverage, and the fullstack developer's resolution of findings. For UX-impacting changes, include the UX designer's assessment and the applicable design decision. Link required check results for the current revision and identify outstanding decisions or acceptance conditions. Follow [AGENTS.md](../AGENTS.md#work-tracking-and-status) before merge.
