#!/usr/bin/env bash
set -euo pipefail
trap 'adb logcat -d -t 600' ERR
mapfile -t packages < <(find src/LoopCx/bin/Debug -name '*-Signed.apk')
[[ ${#packages[@]} == 1 ]]
adb install -r "${packages[0]}"
adb shell pm grant se.loopcx.running android.permission.ACCESS_COARSE_LOCATION
adb shell pm grant se.loopcx.running android.permission.ACCESS_FINE_LOCATION
adb shell pm grant se.loopcx.running android.permission.POST_NOTIFICATIONS
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard
adb logcat -c
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
    adb emu geo fix 18.06 59.33
    sleep 2
done
snapshot > /tmp/run-locked.json
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard
command pause
sleep 2
snapshot > /tmp/run-pause.json
sleep 4
snapshot > /tmp/run-still-paused.json
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
assert pause['Paused'] and still['Paused']
assert still['ActiveSeconds'] == pause['ActiveSeconds']
assert not resume['Paused'] and resume['ActiveSeconds'] > still['ActiveSeconds']
print('PASS: one session, locked-screen time and GPS, pause, resume, reopened Razor UI')
PY

for attempt in {1..30}; do
    adb shell uiautomator dump /sdcard/loop-window.xml >/dev/null
    if adb shell cat /sdcard/loop-window.xml | grep -F 'Warmup'; then break; fi
    sleep 2
done
adb shell cat /sdcard/loop-window.xml | grep -F 'Warmup'
# Force-stop commits no shutdown callback: only durable checkpoints may survive.
command pause
sleep 2
snapshot > /tmp/run-before-death.json
adb shell am force-stop se.loopcx.running
adb exec-out run-as se.loopcx.running cat databases/runs-debug.db > /tmp/before-death.db
sleep 3
command start
sleep 5
snapshot > /tmp/run-after-death.json
command complete
sleep 3
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
print('PASS: SQLite survives process death, interrupted checkpoint unchanged, raw GPS retained, atomic finish and synthetic export disabled')
PY
