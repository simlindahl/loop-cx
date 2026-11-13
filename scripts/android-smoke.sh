#!/usr/bin/env bash
set -euo pipefail
diagnostics() {
    adb shell cat /sdcard/loop-window.xml || true
    adb logcat -d -b crash || true
    adb logcat -d -s AndroidRuntime DOTNET chromium monodroid-assembly || true
}
trap diagnostics ERR
mapfile -t packages < <(find src/LoopCx/bin/Debug -name '*-Signed.apk')
[[ ${#packages[@]} == 1 ]]
adb install -r "${packages[0]}"
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard
# Resolve MAUI's generated activity name instead of relying on a random Monkey event.
loop_activity=$(adb shell cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -p se.loopcx.running | tr -d '\r' | tail -n 1)
[[ "$loop_activity" == se.loopcx.running/* ]]
adb logcat -c
adb shell am start -W -n "$loop_activity"
for attempt in {1..60}; do
    if adb shell uiautomator dump /sdcard/loop-window.xml >/dev/null &&
       adb shell cat /sdcard/loop-window.xml | grep -F 'Start a run' >/dev/null; then
        echo 'PASS: explicit Android launcher start and rendered Razor view'
        exit 0
    fi
    sleep 2
done
exit 1
