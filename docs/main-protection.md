<a id="skydda-main"></a>
# Protect main

<a id="önskat-skydd"></a>
## Intended protection

The [ruleset configuration](../.github/main-ruleset.json) defines `Protect main` for `main`: PR requirement with 0 approvals, resolved review discussions, linear history and required GitHub Actions checks `specification`, `emulator`, `mockups`, `deterministic` and `candidate`. The branch must be up to date with main. Deletion and force-push are blocked; the bypass list is empty. Use squash merge. Restrict updates, signed commits and merge queue are not required.

Zero approvals allows merging your own PRs. The team lead may merge only after both separate requirements and test review approvals and passing required checks under the [repository merge rules](../AGENTS.md#work-tracking-and-status), without a separate merge approval from Simon. Subagent review evidence does not substitute for a GitHub approval required by active platform rules. Update board status directly in [GitHub Projects](https://github.com/users/simlindahl/projects/2); it does not affect main or CI.

<a id="aktivera-efter-verifiering"></a>
## Activate after verification

The configuration file expresses intended state and activates nothing automatically. On 13 November 2026, GitHub API returned 403: rulesets for this private repository require Pro. Protection is therefore not verified active. Keep the repository private until a separate publication decision follows a review of content and history.

Prerequisites: GitHub CLI authenticated with administrator permission, a plan that supports repository rulesets and the five required checks available from a recent green PR. Run from the repository root. First check for overlapping existing rulesets:

```powershell
gh api repos/simlindahl/loop-cx/rulesets
```

After the workflow and board changes are merged and read back, create the ruleset once:

```powershell
gh api --method POST repos/simlindahl/loop-cx/rulesets --input .github/main-ruleset.json
```

If the ruleset already exists, read it and update its verified ID with PUT instead of creating a duplicate. Read back the ruleset using the ID returned by the API and check every rule, check, app source and the empty bypass list. Do not report protection as active before read-back confirms it.

Allow squash merge in repository merge settings. Linear history prevents merge commits; rebase is also compatible with that rule, but squash is the workflow here.

## Public-fork workflow approval

Only after an explicit public-visibility approval and the visibility change, read the repository's contributor-approval policy, set `all_external_contributors` and read it back. This requires maintainer approval for external contributors' fork PR workflows; it does not approve a workflow or grant it a write token. While private, an unavailable policy is a blocker to verification, not evidence of an active public-fork setting.

```powershell
gh api repos/simlindahl/loop-cx/actions/permissions/fork-pr-contributor-approval
gh api --method PUT repos/simlindahl/loop-cx/actions/permissions/fork-pr-contributor-approval -f approval_policy=all_external_contributors
gh api repos/simlindahl/loop-cx/actions/permissions/fork-pr-contributor-approval
gh api repos/simlindahl/loop-cx/actions/permissions/workflow
```

Verify `approval_policy` is `all_external_contributors`; preserve read-only default workflow permissions and `can_approve_pull_request_reviews=false`. Existing workflow `contents: read` declarations remain unchanged. Do not enable write tokens, automatic PR approvals or a `pull_request_target` path as part of this preparation. Record actual settings and limitations after read-back, separately from the stored ruleset and CI evidence. [GitHub Actions permissions API](https://docs.github.com/en/rest/actions/permissions) defines these settings.

<a id="verifiera-pr-flödet"></a>
## Verify the PR workflow

The five required contexts come from workflows that start on every PR without path filters, including documentation-only changes:

| Required context | Workflow |
|---|---|
| `specification` | Repository checks |
| `emulator` | Android |
| `mockups` | Static mockup review |
| `deterministic` | Session rules |
| `candidate` | Combined candidate verification |

Android builds and runs the emulator test; the mockup check verifies layout and uploads screenshots. The combined candidate verifies the shared app sources, Ghost rules/storage and Android regression. The separate path-filtered Ghost workflow exposes `ghost-deterministic`, not the required Session `deterministic` context; its optional check cannot ambiguously satisfy the Session requirement. The combined required check still covers Ghost checks. A green required check means that test ran. Manual runs remain available.

Verify the Projects workflow with a synthetic card following the [board test instructions](../AGENTS.md#work-tracking-and-status). Projects status changes trigger no CI and need no test branch or PR. Verify repository CI and ruleset activation separately.

<a id="referenser"></a>
## References

[GitHub: available ruleset rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets) · [GitHub: required checks and skipped workflows](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks)
