"""Explicit isolated Health Connect diagnostic. Never target a physical device.

Run only after manually exercising the OS permission allow/deny flow. This script
creates no permission grant and does not start a workout or enable normal export.
"""
from contextlib import closing
import argparse
import json
import re
import sqlite3
import tempfile
import subprocess
import time
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--mode", choices=["denied", "roundtrip"], required=True)
parser.add_argument("--adb", default="adb")
parser.add_argument("--out", type=Path, required=True)
args = parser.parse_args()
SERIAL = "emulator-5580"
AVD = "LOOP_Cx_QA_API35_193fb70"
PACKAGE = "se.loopcx.running"
TOKEN = "emulator-5580|LOOP_Cx_QA_API35_193fb70|accounts=0|whoop=absent"


def adb(*command, data=None):
    return subprocess.check_output([args.adb, "-s", SERIAL, *command], input=data,
                                   text=True, stderr=subprocess.STDOUT, timeout=30).strip()



def assert_synthetic_queue_disabled():
    # Copy only this account-free emulator's synthetic DB to a temporary host file.
    raw = subprocess.check_output([args.adb, "-s", SERIAL, "exec-out", "run-as", PACKAGE,
                                   "cat", "databases/runs-debug.db"], timeout=30)
    with tempfile.TemporaryDirectory(prefix="loop-health-probe-") as directory:
        database = Path(directory) / "runs-debug.db"
        database.write_bytes(raw)
        with closing(sqlite3.connect(database)) as connection:
            unexpected = connection.execute("SELECT COUNT(*) FROM export_queue WHERE status != 'disabled_synthetic'").fetchone()[0]
            assert unexpected == 0, "Normal synthetic export must remain disabled"

args.out.mkdir(parents=True, exist_ok=True)
report = {"serial": SERIAL, "mode": args.mode, "outcome": "PENDING", "normalExport": "unchanged/disabled"}
try:
    assert AVD in adb("emu", "avd", "name").splitlines(), "Unexpected AVD"
    properties = {key: adb("shell", "getprop", key) for key in
                  ["ro.kernel.qemu", "ro.hardware", "ro.product.name", "ro.product.model", "ro.product.cpu.abi", "ro.build.version.sdk"]}
    assert properties == {"ro.kernel.qemu": "1", "ro.hardware": "ranchu",
                          "ro.product.name": "sdk_gphone64_x86_64", "ro.product.model": "sdk_gphone64_x86_64",
                          "ro.product.cpu.abi": "x86_64", "ro.build.version.sdk": "35"}, properties
    accounts = adb("shell", "dumpsys", "account")
    assert re.search(r"Accounts:\s*0\b", accounts), "Account-free AVD required"
    assert not adb("shell", "pm", "list", "packages", "com.whoop"), "WHOOP must be absent"
    assert "package:" in adb("shell", "pm", "path", "com.google.android.healthconnect.controller")
    report["guardProperties"] = properties
    assert_synthetic_queue_disabled()
    before = adb("logcat", "-d", "-v", "brief", "-s", "LoopHealthProbe:I", "*:S")
    adb("shell", "run-as", PACKAGE, "tee", "files/hc-isolated-probe.optin", data=TOKEN)
    adb("shell", "am", "start", "-W", "-n", PACKAGE + "/.MainActivity", "--es", "loop-native-probe", "health-connect")
    deadline = time.monotonic() + 90
    observed = ""
    while time.monotonic() < deadline:
        current = adb("logcat", "-d", "-v", "brief", "-s", "LoopHealthProbe:I", "*:S")
        observed = current[len(before):] if current.startswith(before) else current
        if "CLEANUP-FAILED" in observed or "failed:" in observed or "blocked-" in observed:
            raise AssertionError(observed)
        if args.mode == "denied" and "permission-denied" in observed:
            assert "own-insert-read-pass" not in observed
            report["outcome"] = "PASS permission denied; no insert"
            break
        if args.mode == "roundtrip" and "own-delete-absence-pass" in observed:
            assert "own-insert-read-pass" in observed
            report["outcome"] = "PASS own insert/read/delete/absence"
            break
        time.sleep(1)
    else:
        raise TimeoutError("Native callback pending: cleanup NOT verified; retain isolated AVD for own-ID reconciliation")
    assert_synthetic_queue_disabled()
    report["probeEvents"] = observed
    exists = subprocess.run([args.adb, "-s", SERIAL, "shell", "run-as", PACKAGE, "test", "-e", "files/hc-isolated-probe.optin"], timeout=30)
    assert exists.returncode == 1, "Consumed opt-in expected"
except BaseException as error:
    report["outcome"] = "FAIL/PENDING"
    report["error"] = str(error)
    raise
finally:
    (args.out / "health-probe.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(report["outcome"])
