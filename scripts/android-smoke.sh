#!/usr/bin/env bash
set -euo pipefail
mapfile -t packages < <(find src/LoopCx/bin/Debug -name '*-Signed.apk')
[[ ${#packages[@]} == 1 ]]
adb install -r "${packages[0]}"
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard
adb shell monkey -p se.loopcx.running -c android.intent.category.LAUNCHER 1
for attempt in {1..20}; do
    if adb shell uiautomator dump /sdcard/loop-window.xml >/dev/null &&
       adb shell cat /sdcard/loop-window.xml | grep -F 'Start a run' >/dev/null; then
        exit 0
    fi
    sleep 2
done
adb logcat -d -t 300
exit 1
