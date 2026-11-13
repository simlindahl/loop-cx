<a id="skydda-main"></a>
# Protect main

<a id="önskat-skydd"></a>
## Intended protection

The [ruleset configuration](../.github/main-ruleset.json) defines `Protect main` for `main`: PR requirement with 0 approvals, resolved review discussions, linear history and required GitHub Actions checks `specification`, `emulator` and `mockups`. The branch must be up to date with main. Deletion and force-push are blocked; the bypass list is empty. Use squash merge. Restrict updates, signed commits and merge queue are not required.

<<<<<<< HEAD
Zero approvals allows merging your own PRs. It does not replace the [repository approval rules](../AGENTS.md#work-tracking-and-status). Update board status directly in [GitHub Projects](https://github.com/users/simlindahl/projects/2); it does not affect main or CI.
=======
Zero approvals allows merging your own PRs. The lead developer may merge only after subagent review approval and passing required checks under the [repository merge rules](../AGENTS.md#work-tracking-and-status), without a separate merge approval from Simon. Subagent review evidence does not substitute for a GitHub approval required by active platform rules. Update board status directly in [GitHub Projects](https://github.com/users/simlindahl/projects/2); it does not affect main or CI.
>>>>>>> origin/main

<a id="aktivera-efter-verifiering"></a>
## Activate after verification

The configuration file expresses intended state and activates nothing automatically. On 13 November 2026, GitHub API returned 403: rulesets for this private repository require Pro. Protection is therefore not verified active. Keep the repository private until a separate publication decision follows a review of content and history.

Prerequisites: GitHub CLI authenticated with administrator permission, a plan that supports repository rulesets and the three checks available from a recent green PR. Run from the repository root. First check for overlapping existing rulesets:

```powershell
gh api repos/simlindahl/loop-cx/rulesets
```

After the workflow and board changes are merged and read back, create the ruleset once:

```powershell
gh api --method POST repos/simlindahl/loop-cx/rulesets --input .github/main-ruleset.json
```

If the ruleset already exists, read it and update its verified ID with PUT instead of creating a duplicate. Read back the ruleset using the ID returned by the API and check every rule, check, app source and the empty bypass list. Do not report protection as active before read-back confirms it.

Allow squash merge in repository merge settings. Linear history prevents merge commits; rebase is also compatible with that rule, but squash is the workflow here.

<a id="verifiera-pr-flödet"></a>
## Verify the PR workflow

All three workflows start on every PR without path filters. Android builds and runs the emulator test; the mockup check verifies layout and uploads screenshots. A green check means the test ran, including on documentation-only changes. Manual runs remain available.

Verify the Projects workflow with a synthetic card following the [board test instructions](../AGENTS.md#work-tracking-and-status). Projects status changes trigger no CI and need no test branch or PR. Verify repository CI and ruleset activation separately.

<a id="referenser"></a>
## References

[GitHub: available ruleset rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets) · [GitHub: required checks and skipped workflows](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks)
