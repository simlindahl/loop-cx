# Publication review

Reviewed on 13 November 2026. The repository remains **private**. This review and the English translation do not authorize publication, a project license, a history rewrite or publication of private reference material. Publication needs a separate explicit owner approval after reviewing the findings below.

## Reviewed scope and evidence

- Baseline: `main` at `e9759249e971de61a84419453cd280b5c3046ee3`, fetched repository history and all 11 then-open PR branches. Gitleaks 8.30.1 was downloaded from its official GitHub release and its archive checksum was verified against the release checksum file. The history scan covered 71 commits and approximately 728 KB, with no secret findings. A separate complete reachable-commit inventory was retained locally for preservation checks.
- Current and historical paths were checked for environment/credential files, signing key stores and real run exports. No matching key stores, GPX/FIT/TCX exports or environment files were found. This is a filename and content review, not proof that sensitive data cannot exist.
- All 108 available Actions run logs were downloaded and scanned without secret findings. All 35 available artifacts were inventoried; textual evidence was scanned without findings. APK archives were inspected for sensitive filenames, with no matches. APKs were not reverse-engineered or assessed as production releases. Artifacts are emulator APKs, static mockup evidence and combined-candidate evidence.
- Repository issue/PR titles and descriptions, five issue comments, Project items/fields and run metadata were captured privately and scanned without secret findings. No review comments or uploaded-content links appeared in those snapshots. Historical comments and reviews are preserved rather than translated.
- Six historical PDF blob versions were text-extracted and scanned without secret findings; no PDF attachments were present. The four current PNG design references were visually inspected and their available metadata checked. They show illustrative workout screens, not identifiable real run tracks. Two PNGs contain historical Swedish labels and are retained as binary historical references.
- Existing Space Grotesk and Material Icons license notices are present: SIL Open Font License and Apache 2.0 respectively. Their license text is preserved verbatim. No repository-wide project license is present or added.
- Repository workflow defaults are read-only and cannot approve PR reviews. Current workflows declare read-only content access; no `pull_request_target` trigger or `secrets.*` reference was found in current workflows. Fork-PR approval settings could not be read while private: GitHub returned 422 because this policy is unavailable for private repositories. Recheck the policy before enabling public contribution workflows.
- No releases, GitHub Pages site or wiki were enabled at review time. The repository and linked Projects board were kept private.

A broad initial directory scan also matched two deliberate example secrets in the scanner's own bundled README. Those were diagnostic false positives outside the reviewed repository/GitHub data; the separately scoped scans above had no findings. Raw snapshots, logs, scanner reports and extracted PDF text stay in a local private review folder, not in Git.

## Decisions still required before publication

1. **Author identity privacy.** Unchanged Git history includes personal and work email addresses in commit identities, as well as bot identities. Publication exposes that history. No addresses are reproduced in this report. Accept that exposure explicitly, or revisit the no-history-rewrite boundary before publication. New English commits do not hide old identities.
2. **Private source references.** knowledge/loop-cx.md links to the owner's private `second-brain` repository and reveals fitness-related source paths. The private repository was verified private; none of its contents was fetched or copied by this task. Decide whether these links and path disclosures are suitable for public readers and whether the derived training material may be redistributed. Do not make the source repository public as part of this task.
3. **Third-party and image rights.** Font/icon license notices are retained. Provenance and redistribution rights for historical PNG/PDF design material and any externally derived training content still need owner confirmation. A visual review cannot establish ownership or licensing. Historical files remain retrievable even when absent from main.
4. **Project license.** Choose a license separately, or explicitly choose publication without a project license after considering reuse expectations. Do not imply that the font/icon licenses license the application source. No license choice has been made here.
5. **Publication and protection.** Approve visibility change separately. Afterwards, activate and read back `Protect main` using [main protection instructions](main-protection.md); do not describe a stored JSON configuration as active branch protection. Review public-fork Actions approval policy and confirm the intended Projects visibility separately.

## Language and history boundaries

Maintained text is translated into English, including repository instructions, templates, technical documentation, mockup requirement captions, active PR-branch documentation and GitHub titles/descriptions/Project content. Intentional Unicode test names such as `Årsrundan`, font coverage characters, identifiers, paths, proper names and exact third-party license quotations are preserved.

Existing commits and branch names are unchanged. Past discussion/review comments, generated assets and binary design references are not translated. English translation commits are appended on active branches; they do not merge feature PRs, retarget branches or change product behavior. Old Swedish text and historical evidence remain visible in preserved history by design. Previously recorded verification results remain attributed to their original commit IDs; new CI results must identify their own translated heads.

## Limits and publication gate

No-secret findings are evidence from the stated tools and scope, not a guarantee. Binary payloads, compiled application behavior, external/private source rights and every possible personal datum have not been exhaustively certified. New commits, logs, artifacts or GitHub edits after this snapshot require a proportionate follow-up review before publication. Do not delete historical evidence, rotate credentials, rewrite history or change visibility without the relevant authorization; no credential requiring rotation was identified in these scans.

The next publication request should explicitly resolve the identity, private-reference, rights and license decisions, approve public visibility and confirm follow-up protection verification. Translation/review readiness is distinct from publication approval and from feature acceptance.

## Sources

[GitHub: changing repository visibility](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility) explains that public visibility exposes code and Actions history/logs and permits forks. [GitHub: licensing a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository) describes project licensing and the default position without a license. These references inform the publication decision; this report is not a legal clearance.
