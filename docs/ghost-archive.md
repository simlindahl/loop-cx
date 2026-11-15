<a id="ghost-run-sqlite-arkivets-datamodell"></a>
# Ghost Run: SQLite archive data model
Requirements: [Ghost Run](../knowledge/ghost-run.md#ghost-run), Save and select a route, Opponent and year selection, and Validity and uncertainty. Part of #18, on top of #23.

`storage/ghost/schema-1.sql` defines a separate SQLite namespace:
- `ghost_routes`: frozen version, source session, name, calculation revision, geometry and quality policy.
- `ghost_attempts`: stable attempt/source ID, exact route/rule version, local start with time zone and stored year, UTC start in .NET ticks, elapsed time, quality/reason, raw and playback payloads.
- `ghost_race_starts`: frozen route version, calendar year, mode, reference and comparison profile. A first time without a ghost has null in every reference field.
- `ghost_schema`: archive revision marker. This file does not change the app's `PRAGMA user_version`.

<a id="skrivkontrakt"></a>
## Write contract
A source recording may occur at most once per route version. Reference ID, route version, calculation revision, record time and Verified status must match the same archived attempt. Equal distance or a record from another version is insufficient. Unverified attempts need a concrete reason. Archived rows and race references cannot be updated. A new faster time creates a new candidate for the next start; it never replaces an ongoing reference.

C# `GhostReferences` still owns PB/year-best ranking. SQL does not introduce a second production record-selection implementation. Check the stored local start year against the local date, never a later system time zone or UTC year. Ticks are 100 ns under .NET's UTC/TimeSpan contract.

Every database connection must enable `PRAGMA foreign_keys = ON`. Use INSERT, never INSERT OR REPLACE, for archive rows. On retry, the native adapter reads the existing identity: identical frozen evidence returns AlreadyStored; conflicting evidence is rejected and the entire write rolls back. Uniqueness remains a storage guard rather than a substitute for this comparison.

Write route, source attempt and final result in an explicit transaction. Record selection may read the new result only after a successful commit. Foreign keys and update triggers protect frozen references and archived evidence. Rows used by a historical race reference cannot be deleted without separate, intentional handling of that history.

Store JSON payloads as UTF-8 text without depending on SQLite JSON extensions. The native writer validates typed data and JSON before INSERT, use canonical stable IDs and preserve raw data. The database's Verified label is not itself evidence of a complete route or a physically approved filter.

<a id="faktiskt-verifierat"></a>
## Actually verified
Run `python3 tests/GhostStorageChecks/check.py`. The test uses the exact DDL in a temporary SQLite file. It checks version/profile, source uniqueness, a local-year snapshot, incomplete references, an unverified opponent, frozen rows and reference preservation after a new faster result. Failure during finalization rolls back both route and attempt. Closing/reopening preserves raw/playback payloads, Unicode and reference. foreign_key_check and integrity_check must pass.

## Stored-source bridge compatibility · planned integrated version2

Keep the canonical archive namespace/schema1 and existing rows unless an actual new column contract requires a migration. Legacy SaveCapturedRoute still requires sourceAttempt.Elapsed == captured.ObservedSpan and its exact progress/raw serialization. Do not add nullable properties to RecordedRouteFix or change old serializer options: InsertFrozen compares persisted strings, so readable JSON is not sufficient retry compatibility.

The new stored-source writer uses a separate, explicitly versioned raw_json object envelope, while legacy raw_json remains its unchanged RecordedRouteFix array. The envelope freezes source run/plan/checkpoint identity, effective Main start/end and clock/local-zone derivation, every original raw GPS callback/outcome in order, the frozen capture policy, and the distinction between raw evidence and derived geometry/progress. Version-aware readers distinguish array-v1 from object-v2 and reject unsupported/malformed envelopes without rewriting either. Existing geometry and attempt progress columns retain their existing units; v2 attempt elapsed/progress explicitly use full boundary Main time rather than ObservedSpan. No new AI-file/checkpoint/HC version is implied.

Route/version and source attempt/envelope are one existing archive transaction. Stable identities and exact evidence survive failed writes, an uncertain local commit return, retry and cold reopen; conflicts roll back rather than overwrite. The source journal/raw/outbox is read-only. An incomplete endpoint is stored as unknown, not promoted into the first-known-zero/last-known-fullElapsed evidence required by GhostReferences. Source quality and reason come from the explicit frozen evaluation, never successful insertion. Unknown source local-zone facts remain unavailable.

Observable acceptance covers full Main elapsed differing from first/last GPS span, pauses and unknown prefix/suffix; preserved ignored raw callbacks versus their documented derivation exclusion; actual breaks/rejected/not-assessed evidence; supported positive complete eligibility and insufficient evidence with reasons; legacy v1 byte-identical roundtrip/retry and mixed readers; atomic route/source write failure, exact retry/reopen and unchanged source cells. The integrated Saved→Save→catalogue/detail/start path and service race ownership require independent source/native acceptance. No package, provider, map SDK, account/key/billing or device action is authorized by this specification checkpoint.

<a id="kvarstående-integration"></a>
## Remaining integration
The native adapter installs this separate archive namespace lazily in the existing session database. It leaves the app user_version unchanged and uses the dedicated ghost_schema marker; there is no invented earlier app archive schema. Missing archive tables are created atomically from the exact embedded storage/ghost/schema-1.sql resource. An unknown archive marker or partial pre-existing namespace is rejected without rewriting it. Every journal connection enables foreign keys through SQLiteOpenHelper.OnConfigure.

Explicit archive-statement comments delimit complete DDL statements, including entire trigger bodies. Host SQLite tests and native migration consume that single canonical file; no semicolon splitter or duplicate handwritten schema is introduced. Route/version plus source attempt is one transaction. Readers expose the persisted route/version catalogue, attempts and existing domain objects; GhostReferences retains ranking ownership. Raw evidence, quality/reason and frozen start-zone/year are preserved; persistence never promotes an attempt to Verified or invents a local zone for an old run. The adapter accepts explicit typed attempt provenance; it does not automatically convert old session rows with unknown local-zone facts into dated Ghost references.

This adapter is not route-save UI or race-service orchestration. Those remain separate, alongside approved live UI, GPS during training pause and physical verification. No map SDK, billing, health export or permission is enabled. Android migration/rollback/retry/reopen evidence must be recorded against the pinned candidate; existing host tests alone do not establish it.


For the dedicated DEBUG ranchu emulator only, loop-ghost-archive-seed accepts a fresh synthetic GUID. The worker uses the production adapter and existing capture/domain rules to exercise atomic route/source-attempt writes, identical retry, route/source conflicts and rollback of a newly inserted version after a conflicting attempt. It saves a private ghost-archive-GUID.json report. Repeating the same seed after actual app/database reopening must return the same archived evidence. The test creates only its own Ghost identities, marks them unverified/interrupted and cannot create a workout, record, health export or user-facing Ghost screen. Production first-use wiring and physical route verification remain separate.

## Integrated race transaction ownership · planned checkpoint 1

The journal owns the transaction for a service race. An internal archive entry may validate/write on that already-owned connection but cannot begin/end an independent transaction: the first journal checkpoint and ghost_race_starts are one commit, and the terminal journal/raw/event prefix and archive attempt are one commit. Existing public standalone archive methods retain their transaction behavior. Persist immutable starts/reference/profile and exact attempt identities; a returned error after actual commit is resolved through the existing revision/evidence comparison, not a second start or mutated result. Existing source-v1 raw arrays and stored-source-v2 objects retain their separate codecs and exact retry semantics.

Run checkpoint activity kind/version routes reads before Core parsers; no schema bump is implied by a new JSON discriminator in existing columns. Ghost terminal outcomes remain finished, incomplete or interrupted as actually observed, with no fabricated Core plan or Core export queue. A local result is not an HC ACK. Ghost AI-v3 sharing and actual HC mapping remain explicit follow-up work, rather than forcing Ghost through Core AI-v2/HC-v1. Normal Core formats and frozen attempts are unchanged.

The actual Ghost start transaction also freezes the current prospective HealthConnectConsentRevision in the typed checkpoint. A non-null start intent is retained while Ghost mapping remains unsupported; OFF at start stays null without later backfill. Route-card/preflight reads do not freeze it. No Core queue/dispatch/ACK is substituted for this separate mapping disposition.

The only zero-duration exception is an explicit End resulting in Incomplete with race elapsed exactly zero in the recorded monotonic clock. Freeze the typed `ArchiveDisposition = "zero-elapsed-incomplete"`, exact zero and reason in the terminal checkpoint; commit that journal/raw/event prefix and its frozen race-start row together, without a GhostAttempt or record. Do not fabricate one millisecond or mutate archive schema1, whose attempt contract requires positive elapsed. All positive-duration terminals retain the ordinary same-transaction archive-attempt requirement. A zero-duration Finished result or any other missing-attempt disposition is invalid. Retry/rollback/idempotence retain the exact zero checkpoint/start, and cold History reads the incomplete local result directly without Core fallback or an invented archive attempt; PB/year-best remains unchanged.
