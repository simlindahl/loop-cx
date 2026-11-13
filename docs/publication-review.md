# Publication review

Updated on 13 November 2026. The repository remains **private**. Simon approved the identity/history, material-rights, private-reference and license decisions below. Final public visibility has **not** been approved. License preparation and this review do not change visibility, publish the private source repository or authorize a history rewrite.

## Current publication preparation

- The approved [license scope map](../LICENSE.md) applies PolyForm Noncommercial 1.0.0 to owned software and CC BY-NC 4.0 to original text/design. Official full texts are included under `LICENSES/`; existing third-party notices remain unchanged.
- The successful pre-PR50-merge history follow-up scanned **134 reachable commits**, approximately **2.27 MB**, including candidate `f60c023fb25181fcae94e5de0eb4e56f8abfc9cf`, with no Gitleaks findings. The earlier incomplete 104-commit attempt was a failed diagnostic, not acceptance. This evidence does not claim that later commit metadata or GitHub exposure has already been reviewed.
- The GitHub refresh inventory snapshot contains **498 Actions runs and 229 artifacts**. Downloads and review are still ongoing; these are inventory totals, not completed scan counts. The older 108-log/35-artifact inventory below remains historical evidence and does not cover later material.
- **Required before visibility approval:** the team lead must record the final reviewed revision/cutoff, actual downloaded/scanned/unavailable counts, findings and limitations, and the post-snapshot delta outcome in [live issue #51 and its current review handoff](https://github.com/simlindahl/loop-cx/issues/51). That delta includes relevant new commits, Actions logs/artifacts and other GitHub-exposed material produced after the snapshot, including this delivery's CI. A pending or failed exposure review does not satisfy the publication gate.

This candidate is based on merged PR50 main `1b5caa1c100929810abe314bbd918cd4d9d96739`. Complete its independent review and required checks, then use the live handoff above for current audit readiness before requesting the final public-visibility decision. Exposure freshness is established by that delta review, not by a no-op commit or CI rerun solely to create newer logs. This document does not claim that the refresh, visibility change or active protection is complete.

## Historical baseline evidence

- Baseline: `main` at `e9759249e971de61a84419453cd280b5c3046ee3`, fetched repository history and all 11 then-open PR branches. Gitleaks 8.30.1 was downloaded from its official GitHub release and its archive checksum was verified against the release checksum file. The history scan covered 71 commits and approximately 728 KB, with no secret findings. A separate complete reachable-commit inventory was retained locally for preservation checks.
- Current and historical paths were checked for environment/credential files, signing key stores and real run exports. No matching key stores, GPX/FIT/TCX exports or environment files were found. This is a filename and content review, not proof that sensitive data cannot exist.
- All 108 available Actions run logs were downloaded and scanned without secret findings. All 35 available artifacts were inventoried; textual evidence was scanned without findings. APK archives were inspected for sensitive filenames, with no matches. APKs were not reverse-engineered or assessed as production releases. Artifacts are emulator APKs, static mockup evidence and combined-candidate evidence.
- Repository issue/PR titles and descriptions, five issue comments, Project items/fields and run metadata were captured privately and scanned without secret findings. No review comments or uploaded-content links appeared in those snapshots. Historical comments and reviews are preserved rather than translated.
- Six historical PDF blob versions were text-extracted and scanned without secret findings; no PDF attachments were present. The four current PNG design references were visually inspected and their available metadata checked. They show illustrative workout screens, not identifiable real run tracks. Two PNGs contain historical Swedish labels and are retained as binary historical references.
- Existing Space Grotesk and Material Icons license notices are present: SIL Open Font License and Apache 2.0 respectively. Their license text is preserved verbatim. No repository-wide project license was present at that historical baseline; the approved license selection above supersedes that status.
- Repository workflow defaults are read-only and cannot approve PR reviews. Current workflows declare read-only content access; no `pull_request_target` trigger or `secrets.*` reference was found in current workflows. Fork-PR approval settings could not be read while private: GitHub returned 422 because this policy is unavailable for private repositories. Recheck the policy before enabling public contribution workflows.
- No releases, GitHub Pages site or wiki were enabled at review time. The repository and linked Projects board were kept private.

A broad initial directory scan also matched two deliberate example secrets in the scanner's own bundled README. Those were diagnostic false positives outside the reviewed repository/GitHub data; the separately scoped scans above had no findings. Raw snapshots, logs, scanner reports and extracted PDF text stay in a local private review folder, not in Git.

## Owner decisions and remaining gate

Simon explicitly approved the following on 13 November 2026:

1. **Author identity and preserved history.** Retain Git history, including personal and work email addresses in commit identities. These become visible if publication is approved. No history rewrite is authorized.
2. **Private source references.** Historical private `second-brain` links and fitness-related source paths may remain visible. The source repository stays private; this task does not publish or fetch its private contents. A visible link is not a license to those external contents.
3. **Material rights.** Simon confirmed rights to the training and design material, including historical PNG/PDF references, for the approved licensing/publication preparation. This owner confirmation is distinct from the earlier visual/secret review and does not replace third-party notices or grant rights Simon does not own.
4. **Project licenses.** Owned software uses PolyForm Noncommercial 1.0.0; original text/design uses CC BY-NC 4.0 under [LICENSE.md](../LICENSE.md). Commercial enquiries go to simon.a.lindahl@gmail.com. The scope distinguishes protected material/code from underlying training ideas and preserves the standard terms' permissions and statutory exceptions.

**Still required:** approve the final repository visibility change separately after the fresh exposure review. If approved, activate and read back `Protect main` under [main protection instructions](main-protection.md); a stored JSON configuration is not active protection. After approved publication, configure and read back the intended `all_external_contributors` fork-workflow approval policy while retaining read-only tokens/no automatic PR approvals, and confirm Projects visibility separately. This license task does not approve any of those external changes.

## Language and history boundaries

Maintained text is translated into English, including repository instructions, templates, technical documentation, mockup requirement captions, active PR-branch documentation and GitHub titles/descriptions/Project content. Intentional Unicode test names such as `Årsrundan`, font coverage characters, identifiers, paths, proper names and exact third-party license quotations are preserved.

Existing commits and branch names are unchanged. Past discussion/review comments, generated assets and binary design references are not translated. English translation commits are appended on active branches; they do not merge feature PRs, retarget branches or change product behavior. Old Swedish text and historical evidence remain visible in preserved history by design. Previously recorded verification results remain attributed to their original commit IDs; new CI results must identify their own translated heads.

## Limits and publication gate

No-secret findings are evidence from the stated tools and scope, not a guarantee. Binary payloads, compiled application behavior, external/private source rights and every possible personal datum have not been exhaustively certified. New commits, logs, artifacts or GitHub edits after this snapshot require a proportionate follow-up review before publication. Do not delete historical evidence, rotate credentials, rewrite history or change visibility without the relevant authorization; no credential requiring rotation was identified in these scans.

The next publication request should present the current refresh findings and the recorded owner decisions, request explicit final public visibility approval and confirm follow-up protection verification. Translation/review readiness is distinct from publication approval and from feature acceptance.

## Sources

[GitHub: changing repository visibility](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility) explains that public visibility exposes code and Actions history/logs and permits forks. [GitHub: licensing a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository) describes project licensing and the default position without a license. These references inform the publication decision; this report is not a legal clearance.
