# LOOP source ownership migration

## Scope and status

Requested by Simon on 14 November 2026 under [#97](https://github.com/simlindahl/loop-cx/issues/97). This content change proposes the public LOOP repository as the single owner of the generic training texts. Final revised wording still needs review; the previous reader approval is preserved at `4a4c97170c858c2206b507b0cd9296795b5ccc84`. Neither this migration nor repository publication releases the reader runtime hold.

| Migrated resource | Owner and purpose |
|---|---|
| [LOOP system reference](../knowledge/training/system.md) | General training rules, assumptions, rationale and deliberate tradeoffs |
| [Health and recovery](../knowledge/training/health-recovery.md) | Supporting lifestyle reference; no new mandatory training steps |
| [Mobility](../knowledge/training/mobility.md) | Optional movement resource, not a daily Core requirement |
| [Generic 10K adaptation](../knowledge/training/plugins/10k.md) | Temporary event-reference material, not a personal plan or delivered app feature |

[What is LOOP?](../knowledge/what-is-loop.md) explains the system for an experienced trainee whose life interrupts training. It is a pedagogical projection, not a competing rule authority. App specifications own resolved/frozen plans, measurements, recording, execution, target contracts and acceptance; their numerical protocol tables are synchronized projections of approved training decisions. The roadmap owns delivery boundaries, not a separate training programme.

## Provenance and reconciliation

The four Swedish originals were inspected at source commit `b759bf28d09b76f27742ee2de92f526d93ba11ea`. The English documents are edited translations, not byte-identical copies or a claim that all new explanation was authored in the original text.

| Original resource | Source Git blob |
|---|---|
| System | `6a84530ffa86762e9a4e82e7062c30f76330d615` |
| Health and recovery | `bbbb6f7834fefd1e6b99df6a5514750329d3c3cc` |
| Mobility | `82d6ecfdadf31a5cc26d730c845d79bbf0cb1065` |
| 10K reference | `5a2e2520def23e167158aa288bb3e3c437a82df1` |

Later approved C1 variants/counts and 4/3 warm-up/cool-down replace the source's single 5×3/10-and-5 template; its four-versus-five wording inconsistency is resolved rather than copied. Approved C2 duration bounds/default replace the older generic duration wording. No historical recording changes. The 10K table remains a labelled optional reference, not a new Core dose or runtime implementation. The source's daily mobility suggestion is reconciled explicitly with Core's needs-based, optional mobility rule.

The owner requested removal of RPE from presentation, without converting it numerically to heart-rate zones or deleting historical/raw/export facts. The refreshed reader supplies no new official zone default. App-specific target/presentation decisions remain in [#96](https://github.com/simlindahl/loop-cx/issues/96), independently of this migration. Claims and rationale distinguish evidence from LOOP programming choices; the exact system and outdoor Ghost have not been validated as a complete intervention.

## Privacy and follow-up

The generic 10K document excludes the complete personal current-event section, personal dates/results/splits/goals/registration and associated organizer context. These belong in a private wrapper, not in public rule text. No private source URLs or source-folder paths are required by these references. The unrelated home-system material is excluded.

After this public change is reviewed and merged, prepare a separate private-repository PR replacing each former generic owner with a concise pointer to its public canonical counterpart. Preserve the personal event wrapper locally/private with links to the generic 10K reference. Do not duplicate future generic edits across repositories or rewrite historical commits. That private PR has not been made by this change; original private content remains untouched.

Content review must check fidelity, reconciled decisions, privacy and links before publication. Keep requested scope distinct from approval of final wording and reader implementation. No new reader screen, main navigation destination, training data, provider permission or app behavior is introduced here.
