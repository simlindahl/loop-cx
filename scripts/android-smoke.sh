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
command complete
for attempt in {1..30}; do
    adb shell uiautomator dump /sdcard/loop-window.xml >/dev/null
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
