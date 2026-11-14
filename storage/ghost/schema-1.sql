-- Ghost Run archive schema 1. Enable foreign_keys on every connection.
-- Installed lazily by the native adapter; does not change app user_version.
-- archive-statement
CREATE TABLE ghost_schema (
    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
    version INTEGER NOT NULL CHECK (version >= 1)
);
-- archive-statement
INSERT INTO ghost_schema VALUES (1, 1);

-- archive-statement
CREATE TABLE ghost_routes (
    route_id TEXT NOT NULL CHECK (length(route_id) > 0),
    version_id TEXT NOT NULL CHECK (length(version_id) > 0),
    source_run_id TEXT NOT NULL CHECK (length(source_run_id) > 0),
    name TEXT NOT NULL CHECK (length(trim(name)) > 0),
    rule_version TEXT NOT NULL CHECK (length(rule_version) > 0),
    geometry_json TEXT NOT NULL CHECK (length(geometry_json) > 0),
    policy_json TEXT NOT NULL CHECK (length(policy_json) > 0),
    PRIMARY KEY (route_id, version_id),
    UNIQUE (route_id, version_id, rule_version)
);

-- archive-statement
CREATE TABLE ghost_attempts (
    attempt_id TEXT PRIMARY KEY NOT NULL CHECK (length(attempt_id) > 0),
    source_run_id TEXT NOT NULL CHECK (length(source_run_id) > 0),
    route_id TEXT NOT NULL CHECK (length(route_id) > 0),
    version_id TEXT NOT NULL CHECK (length(version_id) > 0),
    rule_version TEXT NOT NULL,
    started_local TEXT NOT NULL CHECK (length(started_local) >= 10),
    local_start_year INTEGER NOT NULL CHECK (local_start_year BETWEEN 1 AND 9999),
    time_zone_id TEXT NOT NULL CHECK (length(time_zone_id) > 0),
    started_utc_ticks INTEGER NOT NULL CHECK (typeof(started_utc_ticks) = 'integer' AND started_utc_ticks BETWEEN 0 AND 3155378975999999999),
    elapsed_ticks INTEGER NOT NULL CHECK (typeof(elapsed_ticks) = 'integer' AND elapsed_ticks > 0),
    quality TEXT NOT NULL CHECK (quality IN ('Verified', 'Incomplete', 'Interrupted', 'InsufficientEvidence')),
    quality_reason TEXT,
    progress_json TEXT NOT NULL CHECK (length(progress_json) > 0),
    raw_json TEXT NOT NULL CHECK (length(raw_json) > 0),
    CHECK (local_start_year = CAST(substr(started_local, 1, 4) AS INTEGER)),
    CHECK (quality = 'Verified' OR (quality_reason IS NOT NULL AND length(trim(quality_reason)) > 0)),
    UNIQUE (route_id, version_id, source_run_id),
    UNIQUE (attempt_id, route_id, version_id, rule_version, elapsed_ticks, quality),
    FOREIGN KEY (route_id, version_id, rule_version)
        REFERENCES ghost_routes (route_id, version_id, rule_version)
);
-- archive-statement
CREATE INDEX ghost_attempt_records ON ghost_attempts
    (route_id, version_id, rule_version, quality, elapsed_ticks, started_utc_ticks, attempt_id);

-- archive-statement
CREATE TABLE ghost_race_starts (
    race_id TEXT PRIMARY KEY NOT NULL CHECK (length(race_id) > 0),
    route_id TEXT NOT NULL CHECK (length(route_id) > 0),
    version_id TEXT NOT NULL CHECK (length(version_id) > 0),
    rule_version TEXT NOT NULL,
    started_local TEXT NOT NULL CHECK (length(started_local) >= 10),
    frozen_year INTEGER NOT NULL CHECK (frozen_year BETWEEN 1 AND 9999),
    mode TEXT NOT NULL CHECK (mode IN ('YearBest', 'PersonalBest')),
    reference_attempt_id TEXT,
    reference_elapsed_ticks INTEGER,
    reference_quality TEXT,
    frozen_profile_json TEXT NOT NULL CHECK (length(frozen_profile_json) > 0),
    CHECK (frozen_year = CAST(substr(started_local, 1, 4) AS INTEGER)),
    CHECK ((reference_attempt_id IS NULL AND reference_elapsed_ticks IS NULL AND reference_quality IS NULL)
        OR (reference_attempt_id IS NOT NULL AND reference_elapsed_ticks IS NOT NULL
            AND reference_quality IS NOT NULL AND reference_quality = 'Verified')),
    FOREIGN KEY (route_id, version_id, rule_version)
        REFERENCES ghost_routes (route_id, version_id, rule_version),
    FOREIGN KEY (reference_attempt_id, route_id, version_id, rule_version, reference_elapsed_ticks, reference_quality)
        REFERENCES ghost_attempts (attempt_id, route_id, version_id, rule_version, elapsed_ticks, quality)
);

-- Archived identities, evidence and race references are append-only.
-- The application must use INSERT, never INSERT OR REPLACE, for these tables.
-- archive-statement
CREATE TRIGGER ghost_route_frozen BEFORE UPDATE ON ghost_routes
BEGIN SELECT RAISE(ABORT, 'Archived route version is immutable'); END;
-- archive-statement
CREATE TRIGGER ghost_attempt_frozen BEFORE UPDATE ON ghost_attempts
BEGIN SELECT RAISE(ABORT, 'Archived attempt is immutable'); END;
-- archive-statement
CREATE TRIGGER ghost_race_start_frozen BEFORE UPDATE ON ghost_race_starts
BEGIN SELECT RAISE(ABORT, 'Race reference is frozen'); END;
