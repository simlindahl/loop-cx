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

Every database connection must enable `PRAGMA foreign_keys = ON`. Use INSERT, never INSERT OR REPLACE, for archive rows. On retry, the future command layer must read the existing identity: identical evidence returns the same result; conflicting evidence is rejected. Schema uniqueness prevents a duplicate but does not replace the application handling that remains unwritten.

Write route, source attempt and final result in an explicit transaction. Record selection may read the new result only after a successful commit. Foreign keys and update triggers protect frozen references and archived evidence. Rows used by a historical race reference cannot be deleted without separate, intentional handling of that history.

Store JSON payloads as UTF-8 text without depending on SQLite JSON extensions. The future writer must validate typed data and JSON before INSERT, use canonical stable IDs and preserve raw data. The database's Verified label is not itself evidence of a complete route or a physically approved filter.

<a id="faktiskt-verifierat"></a>
## Actually verified
Run `python3 tests/GhostStorageChecks/check.py`. The test uses the exact DDL in a temporary SQLite file. It checks version/profile, source uniqueness, a local-year snapshot, incomplete references, an unverified opponent, frozen rows and reference preservation after a new faster result. Failure during finalization rolls back both route and attempt. Closing/reopening preserves raw/playback payloads, Unicode and reference. foreign_key_check and integrity_check must pass.

<a id="kvarstående-integration"></a>
## Remaining integration
This is a verified data model with real storage tests, not the app's finished route archive. No production connection, migration or Android binding runs the file yet. A future native migration must run atomically and process complete DDL statements including trigger bodies; naive splitting on semicolons is wrong. The session database, retry command, service GPS during pause and record finalization must be connected later under the approval rules. Android's SQLite version and physical Pixel/WHOOP/Spotify have not been tested here. No UI/integration prototype or health export is enabled.
