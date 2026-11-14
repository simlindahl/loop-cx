"""Exercise the exact Ghost archive DDL using a temporary, real SQLite database."""
from datetime import datetime, timezone
from pathlib import Path
from tempfile import TemporaryDirectory
import json
import sqlite3

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "storage/ghost/schema-1.sql"
GEOMETRY = json.dumps({"synthetic": True, "points": [[0, 0, 0], [0, 0.001, 111]]})
POLICY = json.dumps({"ruleVersion": "synthetic-v1", "productionDefault": False})
RAW = json.dumps([{"synthetic": True, "elapsedTicks": 0, "accuracyMetres": 1}])
PROGRESS = json.dumps([{"Elapsed": "00:00:00", "Metres": 0}, {"Elapsed": "00:00:10", "Metres": 111}])


def connect(path):
    db = sqlite3.connect(path)
    db.execute("PRAGMA foreign_keys = ON")
    assert db.execute("PRAGMA foreign_keys").fetchone() == (1,)
    return db


def reject(db, action):
    db.execute("SAVEPOINT rejected")
    try:
        action()
    except sqlite3.IntegrityError:
        db.execute("ROLLBACK TO rejected")
        db.execute("RELEASE rejected")
        return
    db.execute("ROLLBACK TO rejected")
    db.execute("RELEASE rejected")
    raise AssertionError("The archive accepted invalid or mutable evidence")


def route(db, version="v1", route_id="route-1"):
    db.execute("INSERT INTO ghost_routes VALUES (?, ?, ?, ?, ?, ?, ?)",
               (route_id, version, "source-route", "Årsrundan", "synthetic-v1", GEOMETRY, POLICY))


def attempt(db, attempt_id="a1", source="source-a1", version="v1", elapsed=100_000_000,
            quality="Verified", reason=None, year=2026, local="2026-01-01T00:00:00+01:00", rule="synthetic-v1", zone="Europe/Stockholm"):
    utc = datetime.fromisoformat(local).astimezone(timezone.utc)
    delta = utc - datetime(1, 1, 1, tzinfo=timezone.utc)
    utc_ticks = (delta.days * 86400 + delta.seconds) * 10_000_000 + delta.microseconds * 10
    db.execute("INSERT INTO ghost_attempts VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
               (attempt_id, source, "route-1", version, rule, local, year, zone,
                utc_ticks, elapsed, quality, reason, PROGRESS, RAW))


def start(db, race="race-1", version="v1", reference="a1", elapsed=100_000_000,
          quality="Verified", mode="YearBest"):
    db.execute("INSERT INTO ghost_race_starts VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
               (race, "route-1", version, "synthetic-v1", "2026-11-13T07:00:00+01:00", 2026,
                mode, reference, elapsed, quality, POLICY))


with TemporaryDirectory(prefix="loop-ghost-archive-") as temporary:
    path = Path(temporary) / "ghost.db"
    db = connect(path)
    db.executescript("BEGIN;\n" + SCHEMA.read_text(encoding="utf-8") + "\nCOMMIT;")
    assert db.execute("SELECT version FROM ghost_schema").fetchone() == (1,)
    # The exact native statement boundaries execute the same schema, including intact triggers.
    segmented = connect(Path(temporary) / "segmented.db")
    with segmented:
        blocks = SCHEMA.read_text(encoding="utf-8").split("-- archive-statement\n")
        assert len(blocks) > 1
        for statement in blocks[1:]:
            assert statement.strip()
            segmented.execute(statement)
    canonical_objects = db.execute("SELECT type,name,sql FROM sqlite_master WHERE name LIKE 'ghost_%' ORDER BY type,name").fetchall()
    assert segmented.execute("SELECT type,name,sql FROM sqlite_master WHERE name LIKE 'ghost_%' ORDER BY type,name").fetchall() == canonical_objects
    assert segmented.execute("PRAGMA user_version").fetchone() == (0,)
    segmented.close()

    route(db)
    route(db, version="v2")
    attempt(db)
    attempt(db, "old-year", "source-old", elapsed=90_000_000, year=2025,
            local="2025-12-31T23:30:00-05:00", zone="America/New_York")
    attempt(db, "interrupted", "source-gap", quality="Interrupted", reason="Process interrupted")
    start(db)
    start(db, race="no-ghost", reference=None, elapsed=None, quality=None)
    db.commit()

    reject(db, lambda: route(db))
    reject(db, lambda: route(db, route_id=""))
    reject(db, lambda: route(db, version=""))
    reject(db, lambda: attempt(db, "duplicate-source", "source-a1"))
    assert db.execute("SELECT COUNT(*) FROM ghost_attempts WHERE source_run_id='source-a1'").fetchone() == (1,)
    reject(db, lambda: attempt(db, "wrong-version", "wrong-version", version="missing"))
    reject(db, lambda: attempt(db, "wrong-rules", "wrong-rules", rule="changed-profile"))
    reject(db, lambda: attempt(db, "negative", "negative", elapsed=-1))
    reject(db, lambda: attempt(db, "fractional-ticks", "fractional-ticks", elapsed=1.5))
    reject(db, lambda: attempt(db, "invalid-ticks", "invalid-ticks", elapsed="not-a-timestamp"))
    reject(db, lambda: attempt(db, "wrong-year", "wrong-year", year=2025))
    reject(db, lambda: attempt(db, "missing-reason", "missing-reason", quality="Incomplete"))
    reject(db, lambda: attempt(db, "bad-quality", "bad-quality", quality="ProbablyVerified"))
    reject(db, lambda: start(db, race="different-route-version", version="v2"))
    reject(db, lambda: start(db, race="changed-record-time", elapsed=1))
    reject(db, lambda: start(db, race="unverified", reference="interrupted", quality="Interrupted"))
    reject(db, lambda: start(db, race="false-quality", reference="interrupted"))
    reject(db, lambda: start(db, race="partial-reference", elapsed=None))
    reject(db, lambda: start(db, race="partial-quality", quality=None))
    reject(db, lambda: start(db, race="unsupported-mode", mode="Pacer"))
    reject(db, lambda: db.execute("UPDATE ghost_routes SET geometry_json='[]' WHERE version_id='v1'"))
    reject(db, lambda: db.execute("UPDATE ghost_attempts SET elapsed_ticks=1 WHERE attempt_id='a1'"))
    reject(db, lambda: db.execute("UPDATE ghost_race_starts SET reference_attempt_id=NULL WHERE race_id='race-1'"))

    # A new faster result is a candidate for the next start, not a rewrite of this race's reference.
    attempt(db, "faster", "source-faster", elapsed=80_000_000)
    db.commit()
    assert db.execute("SELECT reference_attempt_id, reference_elapsed_ticks FROM ghost_race_starts WHERE race_id='race-1'").fetchone() == ("a1", 100_000_000)
    assert db.execute("SELECT local_start_year, started_local FROM ghost_attempts WHERE attempt_id='old-year'").fetchone() == (2025, "2025-12-31T23:30:00-05:00")

    # Roll back route and attempt together when finalization fails.
    try:
        db.execute("BEGIN")
        route(db, version="atomic")
        attempt(db, "atomic-failure", "source-atomic", version="atomic", rule="missing-rule")
        raise AssertionError("Finalization should have failed")
    except sqlite3.IntegrityError:
        db.rollback()
    assert db.execute("SELECT COUNT(*) FROM ghost_routes WHERE version_id='atomic'").fetchone() == (0,)
    assert db.execute("SELECT COUNT(*) FROM ghost_attempts WHERE attempt_id='atomic-failure'").fetchone() == (0,)
    assert db.execute("PRAGMA foreign_key_check").fetchall() == []
    assert db.execute("PRAGMA integrity_check").fetchone() == ("ok",)
    db.close()

    db = connect(path)
    assert db.execute("SELECT name, geometry_json, policy_json FROM ghost_routes WHERE version_id='v1'").fetchone() == ("Årsrundan", GEOMETRY, POLICY)
    assert db.execute("SELECT progress_json, raw_json FROM ghost_attempts WHERE attempt_id='a1'").fetchone() == (PROGRESS, RAW)
    assert db.execute("SELECT reference_attempt_id FROM ghost_race_starts WHERE race_id='race-1'").fetchone() == ("a1",)
    reject(db, lambda: db.execute("DELETE FROM ghost_attempts WHERE attempt_id='a1'"))
    db.close()

print("PASS: real SQLite archive, route/profile/source constraints, immutable evidence, local year and frozen nullable race reference.")
print("PASS: failed finalization rolls back atomically; reopening preserves UTF-8, raw/progress payloads and reference; integrity checks pass.")
