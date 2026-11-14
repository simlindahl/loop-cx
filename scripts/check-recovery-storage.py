"""Cheap, isolated schema-5 migration and marker-only transaction regression."""
from pathlib import Path
import re
import sqlite3
import tempfile

source = (Path(__file__).parents[1] / "src/LoopCx/Platforms/Android/RunJournal.cs").read_text(encoding="utf-8")
create = re.search(r'ExecSQL\("(CREATE TABLE runs [^"\n]+)"\)', source)[1]
migration = 'ALTER TABLE runs ADD COLUMN recovery_saved_utc TEXT'
assert migration in source
update = re.search(r'CompileStatement\("(UPDATE runs SET recovery_saved_utc[^"\n]+)"\)', source)[1]
temporary = tempfile.TemporaryDirectory(prefix="loop-recovery-")
database = Path(temporary.name) / "owned.db"
with sqlite3.connect(database) as db:
    # Historical schema 5 is explicit: no recovery column, existing HR and outbox fields.
    db.execute(create)
    db.execute("ALTER TABLE runs ADD COLUMN heart_rate_metadata TEXT")
    db.execute("CREATE TABLE export_queue (run_id TEXT PRIMARY KEY, status TEXT NOT NULL, attempt_json TEXT, last_error TEXT)")
    db.execute("CREATE TABLE locations (run_id TEXT, value TEXT)")
    db.execute("CREATE TABLE run_events (run_id TEXT, value TEXT)")
    db.execute("INSERT INTO locations VALUES ('owned', 'original raw point')")
    db.execute("INSERT INTO run_events VALUES ('owned', 'original event checkpoint')")
    db.execute("PRAGMA user_version=5")
    old_columns = [row[1] for row in db.execute("PRAGMA table_info(runs)")]
    assert old_columns == ["id", "started_utc", "observed_utc", "elapsed_ms", "status", "initial_plan", "checkpoint", "template_metadata", "heart_rate_metadata"]
    db.execute("INSERT INTO runs VALUES ('owned', 'start', 'last-observation', 1234, 'interrupted', 'original-plan', '{\"legacy\":true}', NULL, NULL)")
    before = db.execute("SELECT * FROM runs").fetchone()
    db.execute(migration)
    db.execute("PRAGMA user_version=6")
    assert db.execute("SELECT * FROM runs").fetchone() == before + (None,)
    db.commit()
    db.execute("CREATE TEMP TRIGGER fail_recovery BEFORE UPDATE OF recovery_saved_utc ON runs BEGIN SELECT RAISE(ABORT, 'owned failure'); END")
    try:
        with db:
            db.execute(update, ("first-ack", "owned"))
        raise AssertionError("Injected write did not fail")
    except sqlite3.IntegrityError:
        pass
    assert db.execute("SELECT * FROM runs").fetchone() == before + (None,)
    db.execute("DROP TRIGGER fail_recovery")
    with db:
        assert db.execute(update, ("first-ack", "owned")).rowcount == 1
    # Lost local return/repeated request cannot replace the first committed acknowledgement.
    with db:
        assert db.execute(update, ("later-ack", "owned")).rowcount == 0
    assert db.execute("SELECT * FROM runs").fetchone() == before + ("first-ack",)
    assert db.execute("SELECT COUNT(*) FROM export_queue").fetchone() == (0,)
    assert db.execute("PRAGMA integrity_check").fetchone() == ("ok",)
db.close()
with sqlite3.connect(database) as reopened:
    assert reopened.execute("SELECT * FROM runs").fetchone() == before + ("first-ack",)
    assert reopened.execute("PRAGMA user_version").fetchone() == (6,)
    assert reopened.execute("SELECT COUNT(*) FROM export_queue").fetchone() == (0,)
    assert reopened.execute("SELECT * FROM locations").fetchall() == [("owned", "original raw point")]
    assert reopened.execute("SELECT * FROM run_events").fetchall() == [("owned", "original event checkpoint")]
reopened.close()
temporary.cleanup()
print("PASS: genuine schema5-to6 shape/NULL, marker abort rollback, immutable prefix, stable ACK and no queue writes")
