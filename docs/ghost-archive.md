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

<a id="kvarstående-integration"></a>
## Remaining integration
The native adapter installs this separate archive namespace lazily in the existing session database. It leaves the app user_version unchanged and uses the dedicated ghost_schema marker; there is no invented earlier app archive schema. Missing archive tables are created atomically from the exact embedded storage/ghost/schema-1.sql resource. An unknown archive marker or partial pre-existing namespace is rejected without rewriting it. Every journal connection enables foreign keys through SQLiteOpenHelper.OnConfigure.

Explicit archive-statement comments delimit complete DDL statements, including entire trigger bodies. Host SQLite tests and native migration consume that single canonical file; no semicolon splitter or duplicate handwritten schema is introduced. Route/version plus source attempt is one transaction. Readers expose the persisted route/version catalogue, attempts and existing domain objects; GhostReferences retains ranking ownership. Raw evidence, quality/reason and frozen start-zone/year are preserved; persistence never promotes an attempt to Verified or invents a local zone for an old run. The adapter accepts explicit typed attempt provenance; it does not automatically convert old session rows with unknown local-zone facts into dated Ghost references.

This adapter is not route-save UI or race-service orchestration. Those remain separate, alongside approved live UI, GPS during training pause and physical verification. No map SDK, billing, health export or permission is enabled. Android migration/rollback/retry/reopen evidence must be recorded against the pinned candidate; existing host tests alone do not establish it.


For the dedicated DEBUG ranchu emulator only, loop-ghost-archive-seed accepts a fresh synthetic GUID. The worker uses the production adapter and existing capture/domain rules to exercise atomic route/source-attempt writes, identical retry, route/source conflicts and rollback of a newly inserted version after a conflicting attempt. It saves a private ghost-archive-GUID.json report. Repeating the same seed after actual app/database reopening must return the same archived evidence. The test creates only its own Ghost identities, marks them unverified/interrupted and cannot create a workout, record, health export or user-facing Ghost screen. Production first-use wiring and physical route verification remain separate.
