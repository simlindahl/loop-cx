#!/usr/bin/env bash
set -euo pipefail
trap 'adb logcat -d -t 600' ERR
mapfile -t packages < <(find src/LoopCx/bin/Debug -name '*-Signed.apk')
[[ ${#packages[@]} == 1 ]]
adb install -r "${packages[0]}"
# Upgrade a synthetic v1 database without deleting previously recorded rows.
python3 - <<'PY'
import sqlite3
from pathlib import Path
path = Path('/tmp/loop-legacy.db')
path.unlink(missing_ok=True)
db = sqlite3.connect(path)
db.executescript("""
CREATE TABLE runs (id TEXT PRIMARY KEY, started_utc TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, status TEXT NOT NULL, initial_plan TEXT NOT NULL, checkpoint TEXT NOT NULL);
CREATE TABLE locations (run_id TEXT NOT NULL, utc_ms INTEGER NOT NULL, monotonic_ns INTEGER NOT NULL, latitude REAL NOT NULL, longitude REAL NOT NULL, accuracy REAL);
CREATE TABLE export_queue (run_id TEXT PRIMARY KEY, status TEXT NOT NULL);
PRAGMA user_version=1;
INSERT INTO runs VALUES ('legacy-run', '2026-01-01T00:00:00Z', '2026-01-01T00:00:02Z', 2000, 'recording', '[]', '{"legacy":true}');
INSERT INTO locations VALUES ('legacy-run', 2000, 2000000000, 59.33, 18.06, 5);
""")
db.commit()
db.close()
PY
adb shell am force-stop se.loopcx.running
adb push /tmp/loop-legacy.db /data/local/tmp/loop-legacy.db
adb shell run-as se.loopcx.running mkdir -p databases
adb shell run-as se.loopcx.running cp /data/local/tmp/loop-legacy.db databases/runs-debug.db
adb shell pm grant se.loopcx.running android.permission.ACCESS_COARSE_LOCATION
adb shell pm grant se.loopcx.running android.permission.ACCESS_FINE_LOCATION
adb shell pm grant se.loopcx.running android.permission.POST_NOTIFICATIONS
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard
adb logcat -c
dump_window() {
    local stage=$1
    local capture_status=1
    local evidence=verification/c1-ui/strong-smoke
    mkdir -p "$evidence"
    for capture_attempt in 1 2 3; do
        adb shell rm -f /sdcard/loop-window.xml
        if adb shell uiautomator dump /sdcard/loop-window.xml >"$evidence/$stage-$capture_attempt-dump.txt" 2>&1; then
            return 0
        else
            capture_status=$?
        fi
        printf 'capture exit=%s stage=%s attempt=%s\n' "$capture_status" "$stage" "$capture_attempt" >>"$evidence/capture-failures.txt"
        git rev-parse HEAD 'HEAD^{tree}' >"$evidence/source.txt"
        adb exec-out screencap -p >"$evidence/$stage-$capture_attempt-failed.png" || true
        adb logcat -d -t 200 >"$evidence/$stage-$capture_attempt-logcat.txt" || true
        sleep 1
    done
    return "$capture_status"
}

command() {
    adb shell am start -n se.loopcx.running/.MainActivity --es loop-command "$1"
}
snapshot() {
    adb logcat -d -s LoopRun:I '*:S' -v raw | sed 's/^[[:space:]]*//' | sed -n '/^{/p' | tail -n 1
}
command start
for attempt in {1..60}; do
    snapshot > /tmp/run-start.json
    if [[ -s /tmp/run-start.json ]]; then break; fi
    sleep 1
done
[[ -s /tmp/run-start.json ]]
sleep 3
snapshot > /tmp/run-start.json
command start
sleep 2
snapshot > /tmp/run-duplicate.json
adb shell input keyevent KEYCODE_HOME
adb shell input keyevent KEYCODE_SLEEP
for point in 1 2 3 4 5; do
    # This is a moving locked-screen scenario, not the separate standstill test.
    longitude=$(python3 -c "print(18.06 + $point * 0.00015)")
    adb emu geo fix "$longitude" 59.33
    sleep 2
done
snapshot > /tmp/run-locked.json
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard
command pause
sleep 2
snapshot > /tmp/run-pause.json
command pause
sleep 4
snapshot > /tmp/run-still-paused.json
command resume
command resume
sleep 4
snapshot > /tmp/run-resume.json
python3 - <<'PY'
import json
from pathlib import Path
def read(name):
    return json.loads(Path('/tmp/run-' + name + '.json').read_text())
start, duplicate, locked, pause, still, resume = map(read, [
    'start', 'duplicate', 'locked', 'pause', 'still-paused', 'resume'])
assert len({s['Id'] for s in [start, duplicate, locked, pause, still, resume]}) == 1
assert locked['ActiveSeconds'] >= duplicate['ActiveSeconds'] + 8
assert locked['Locations'] > duplicate['Locations'], 'GPS must arrive with screen off'

from decimal import Decimal
def seconds(value):
    hours, minutes, sec = value.split(':')
    return Decimal(hours)*3600 + Decimal(minutes)*60 + Decimal(sec)
stats = locked['Statistics']
assert stats['Version'] == 1 and not stats['HasSyntheticTime']
assert seconds(stats['Total']['KnownGpsActiveTime']) > 0, 'Actual GPS brackets must reach service statistics'
for scope in [stats['Total'], stats['Main']]:
    assert seconds(scope['KnownGpsActiveTime']) + seconds(scope['UnknownGpsActiveTime']) == seconds(scope['ActiveTime'])
    assert scope['AverageHeartRate'] is None and scope['KnownHeartRateActiveTime'] == '00:00:00'
    assert scope['UnknownHeartRateActiveTime'] == scope['ActiveTime']
assert int(seconds(stats['Total']['ActiveTime'])) == locked['ActiveSeconds']

assert pause['Paused'] and still['Paused']
assert still['ActiveSeconds'] == pause['ActiveSeconds']
assert not resume['Paused'] and resume['ActiveSeconds'] > still['ActiveSeconds']
print('PASS: one session, locked-screen time and GPS, pause, resume, reopened Razor UI')
PY

for attempt in {1..30}; do
    dump_window warmup
    if adb shell cat /sdcard/loop-window.xml | grep -F 'Warm-up'; then break; fi
    sleep 2
done
adb shell cat /sdcard/loop-window.xml | grep -F 'Warm-up'
command complete
for attempt in {1..30}; do
    dump_window finished
    if adb shell cat /sdcard/loop-window.xml | grep -F 'Start C1'; then break; fi
    sleep 1
done
adb shell cat /sdcard/loop-window.xml | grep -F 'Start C1'
command start
sleep 3
snapshot > /tmp/run-next.json
python3 - <<'PY'
import json
from pathlib import Path
previous = json.loads(Path('/tmp/run-resume.json').read_text())
current = json.loads(Path('/tmp/run-next.json').read_text())
assert current['Id'] != previous['Id'], 'A completed run must allow a new session'
assert current['Phase'] == 'Warmup' and not current['Paused']
print('PASS: finishing retires the service and enables a new workout without process restart')
PY

# Deliver measurements to the second session that the recovery test actually kills.
for point in 1 2 3; do
    adb emu geo fix 18.06 59.33
    sleep 2
done
snapshot > /tmp/run-next-gps.json
python3 - <<'PY'
import json
from pathlib import Path
assert json.loads(Path('/tmp/run-next-gps.json').read_text())['Locations'] > 0
PY
# Exercise effective/duplicate commands on the session used for recovery assertions.
command pause
command pause
sleep 4
command resume
command resume
# Record manual phase changes, and reject preparation commands during work.
command extend
command extend
command skip
command skip
command extend
# Force-stop commits no shutdown callback: only durable checkpoints may survive.
command pause
sleep 2
snapshot > /tmp/run-before-death.json
adb shell am force-stop se.loopcx.running
adb exec-out run-as se.loopcx.running cat databases/runs-debug.db > /tmp/before-death.db
sleep 3
adb logcat -c
command start
for attempt in {1..60}; do
    snapshot > /tmp/run-after-death.json
    if [[ -s /tmp/run-after-death.json ]]; then break; fi
    sleep 1
done
[[ -s /tmp/run-after-death.json ]]
command complete
for attempt in {1..30}; do
    if snapshot | grep -F '"Phase":"Finished"'; then break; fi
    sleep 1
done
snapshot | grep -F '"Phase":"Finished"'
adb shell am force-stop se.loopcx.running
adb exec-out run-as se.loopcx.running cat databases/runs-debug.db > /tmp/after-death.db
python3 - <<'PY'
import json, sqlite3
from pathlib import Path
before = sqlite3.connect('/tmp/before-death.db')
after = sqlite3.connect('/tmp/after-death.db')
old_id = json.loads(Path('/tmp/run-before-death.json').read_text())['Id']
new_id = json.loads(Path('/tmp/run-after-death.json').read_text())['Id']
assert old_id != new_id, 'Restart must not silently resume missing measurements'
fields = 'observed_utc, elapsed_ms, initial_plan, checkpoint'
saved = before.execute(f'SELECT {fields} FROM runs WHERE id=?', (old_id,)).fetchone()
recovered = after.execute(f'SELECT {fields} FROM runs WHERE id=?', (old_id,)).fetchone()
assert saved == recovered, 'Recovery must not invent time, GPS or phases'
assert after.execute('SELECT status FROM runs WHERE id=?', (old_id,)).fetchone()[0] == 'interrupted'
checkpoint = json.loads(saved[3])
assert checkpoint['Snapshot']['Paused']
assert len(json.loads(saved[2])) == 11, 'Freeze the entire Core C1 plan'
assert after.execute('SELECT COUNT(*) FROM locations WHERE run_id=?', (old_id,)).fetchone()[0] > 0
assert before.execute('SELECT COUNT(*) FROM locations WHERE run_id=?', (old_id,)).fetchone() == after.execute('SELECT COUNT(*) FROM locations WHERE run_id=?', (old_id,)).fetchone()
assert after.execute('SELECT status FROM runs WHERE id=?', (new_id,)).fetchone()[0] == 'finished'
assert after.execute('SELECT status FROM export_queue WHERE run_id=?', (new_id,)).fetchall() == [('disabled_synthetic',)]
assert after.execute('PRAGMA user_version').fetchone()[0] == 5
legacy = after.execute("SELECT elapsed_ms, status, initial_plan, checkpoint FROM runs WHERE id='legacy-run'").fetchone()
assert legacy == (2000, 'interrupted', '[]', '{"legacy":true}')
assert after.execute("SELECT template_metadata FROM runs WHERE id='legacy-run'").fetchone()[0] is None
assert after.execute("SELECT COUNT(*) FROM locations WHERE run_id='legacy-run'").fetchone()[0] == 1
assert after.execute("SELECT COUNT(*) FROM run_events WHERE run_id='legacy-run'").fetchone()[0] == 0, 'Never invent old events'
rows = after.execute('SELECT command, elapsed_ms, checkpoint FROM run_events WHERE run_id=? ORDER BY sequence', (old_id,)).fetchall()
assert [r[0] for r in rows] == ['start', 'pause', 'resume', 'extend', 'extend', 'skip', 'pause'], rows
assert [r[1] for r in rows] == sorted(r[1] for r in rows)
states = [json.loads(r[2]) for r in rows]
assert states[1]['Snapshot']['Paused'] and not states[2]['Snapshot']['Paused']
assert states[1]['TotalActiveTime'] == states[2]['TotalActiveTime'], 'Pause adds elapsed time but no active time'
assert rows[2][1] - rows[1][1] >= 4000
assert states[5]['Snapshot']['Phase'] == 'Work'
assert states[5]['Completed'][0]['Plan']['Duration'] == '00:06:00', 'Global four-minute warm-up retains both effective one-minute extensions'
assert before.execute('SELECT * FROM run_events WHERE run_id=? ORDER BY sequence', (old_id,)).fetchall() == after.execute('SELECT * FROM run_events WHERE run_id=? ORDER BY sequence', (old_id,)).fetchall()
print('PASS: SQLite survives process death, interrupted checkpoint unchanged, raw GPS retained, atomic finish and synthetic export disabled')
print('PASS: v1 migration preserves existing data; effective commands retain ordered timestamps and pause duration without duplicate events')
PY
