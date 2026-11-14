"""Focused C1 UI evidence; run only after android-smoke.sh in a dedicated emulator."""
import json
import math
import os
import re
import subprocess
import sys
import zipfile
import sqlite3
from contextlib import closing
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

def check_package():
    packages = list(Path("src/LoopCx/bin/Debug").rglob("*-Signed.apk"))
    assert len(packages) == 1, "Expected one built APK for package verification"
    with zipfile.ZipFile(packages[0]) as package:
        names = package.namelist()
        assert "assets/wwwroot/LoopCx.styles.css" in names, "Missing served Razor scoped stylesheet"
        bundle = package.read("assets/wwwroot/LoopCx.styles.css")
        assert bundle.strip() and b".countdown[b-" in bundle, "Missing component-scoped countdown styling"
        assert "assets/wwwroot/fonts/space-grotesk.ttf" in names
        assert "LoopCx.styles.css" in package.read("assets/wwwroot/index.html").decode("utf-8")
        assert "assets/wwwroot/fonts/FONT-LICENSE.txt" in names
    print("PASS: APK contains linked Razor scoped stylesheet and bundled font license")


check_package()
if "--check-package" in sys.argv:
    raise SystemExit(0)


PACKAGE = "se.loopcx.running"
OUT = Path("verification/c1-ui")
OUT.mkdir(parents=True, exist_ok=True)


def adb(*args, binary=False):
    return subprocess.check_output(["adb", *args], text=not binary, timeout=30, stderr=subprocess.STDOUT).strip() if not binary else subprocess.check_output(["adb", *args], timeout=30)


assert adb("shell", "getprop", "ro.kernel.qemu") == "1", "Dedicated emulator required"
original_scale = adb("shell", "settings", "get", "system", "font_scale")
original_wifi = adb("shell", "settings", "get", "global", "wifi_on")
original_data = adb("shell", "settings", "get", "global", "mobile_data")
original_location = adb("shell", "cmd", "location", "is-location-enabled")
report = {"source": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
          "density": adb("shell", "wm", "density"), "size": adb("shell", "wm", "size"),
          "fontScaleBefore": original_scale, "states": [],
          "limits": "UIAutomator is not TalkBack. Screenshots require visual review; font asset presence does not prove font loading."}
event_path = os.environ.get("GITHUB_EVENT_PATH")
event = json.loads(Path(event_path).read_text(encoding="utf-8")) if event_path else {}
report["pullRequestHead"] = event.get("pull_request", {}).get("head", {}).get("sha")
report["pullRequestBase"] = event.get("pull_request", {}).get("base", {}).get("sha")
report["sourceTree"] = subprocess.check_output(["git", "rev-parse", "HEAD^{tree}"], text=True).strip()
report["captureFailures"] = []
density = int(re.findall(r"(\d+)", report["density"])[-1]) / 160


def open_app():
    adb("shell", "am", "start", "-W", "-n", PACKAGE + "/.MainActivity")


def capture_diagnostics(name, reason):
    report["captureFailures"].append({"state": name, "reason": str(reason)})
    (OUT / (name + "-capture-error.txt")).write_text(str(reason) + "\n", encoding="utf-8")
    for suffix, args, binary in [
        ("-capture-error.png", ("exec-out", "screencap", "-p"), True),
        ("-crash-log.txt", ("logcat", "-d", "-b", "crash", "-t", "100"), False),
    ]:
        try:
            output = adb(*args, binary=binary)
            if binary:
                (OUT / (name + suffix)).write_bytes(output)
            else:
                (OUT / (name + suffix)).write_text(output + "\n", encoding="utf-8")
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as diagnostic_error:
            report["captureFailures"].append({"state": name, "diagnosticError": str(diagnostic_error)})



def location_notice_close(root):
    nodes = list(root.iter("node"))
    package = "com.google.android.gms"
    title = any(node.get("package") == package and node.get("resource-id") == package + ":id/alertTitle"
                and node.get("text") == "No location access" for node in nodes)
    if not title:
        return None
    return next((node for node in nodes if node.get("package") == package
                 and node.get("resource-id") == "android:id/button2" and node.get("text") == "Close"
                 and node.get("clickable") == "true" and node.get("enabled") == "true"), None)


def dismiss_location_notice(root, name, xml):
    close = location_notice_close(root)
    if close is None:
        return False
    assert not report.get("systemDialogs"), "Repeated location-disabled notice; refusing a general dialog-dismiss loop"
    evidence = name + "-location-disabled-notice"
    (OUT / (evidence + ".xml")).write_text(xml + "\n", encoding="utf-8")
    (OUT / (evidence + ".png")).write_bytes(adb("exec-out", "screencap", "-p", binary=True))
    x1, y1, x2, y2 = map(int, re.findall(r"\d+", close.get("bounds")))
    assert x2 > x1 and y2 > y1, "The known system Close action is not visible"
    adb("shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2))
    location_after = adb("shell", "cmd", "location", "is-location-enabled")
    assert location_after == "false", "Closing the known notice must not enable location"
    report["systemDialogs"] = [{"package": "com.google.android.gms", "title": "No location access",
                                "action": "Close", "locationEnabledAfter": location_after, "evidence": evidence}]
    return True


def capture(name, expected, heading_min_height=0):
    xml = ""
    capture_errors = 0
    for _ in range(40):
        try:
            adb("shell", "uiautomator", "dump", "/data/local/tmp/loop-ui.xml")
            xml = adb("shell", "cat", "/data/local/tmp/loop-ui.xml")
            root = ET.fromstring(xml)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired, ET.ParseError) as error:
            capture_errors += 1
            detail = str(error) + "\n" + str(getattr(error, "output", ""))
            capture_diagnostics(name + "-attempt-" + str(capture_errors), detail)
            if capture_errors >= 3:
                raise AssertionError("UI capture failed three times: " + name) from error
            time.sleep(1)
            continue
        if dismiss_location_notice(root, name, xml):
            continue
        if any(expected in (n.get("text", "") + n.get("content-desc", "")) for n in root.iter("node")) and heading_height(root, "Manually paused") >= heading_min_height:
            (OUT / (name + ".xml")).write_text(xml + "\n", encoding="utf-8")
            (OUT / (name + ".png")).write_bytes(adb("exec-out", "screencap", "-p", binary=True))
            report["states"].append(name)
            return root
        time.sleep(1)
    (OUT / (name + "-failed.xml")).write_text(xml + "\n", encoding="utf-8")
    capture_diagnostics(name + "-failed", "Missing rendered UI: " + expected)
    raise AssertionError("Missing rendered UI: " + expected)


def button(root, label, tap=False):
    candidates = [n for n in root.iter("node") if n.get("clickable") == "true" and n.get("enabled") == "true" and n.get("package", PACKAGE) == PACKAGE and
                  any(label == c.get("text") or label == c.get("content-desc") for c in n.iter("node"))]
    assert candidates, "No accessible clickable button: " + label
    node = min(candidates, key=lambda n: area(n))
    x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
    assert (x2 - x1) / density >= 48 and (y2 - y1) / density >= 48, "Touch target below 48dp: " + label
    if tap:
        adb("shell", "input", "tap", str((x1+x2)//2), str((y1+y2)//2))
    return y2-y1


def scroll_to_top():
    screen_width, screen_height = map(int, re.findall(r"(\d+)x(\d+)", report["size"])[-1])
    for _ in range(3):
        adb("shell", "input", "swipe", str(screen_width//2), str(int(screen_height*0.3)),
            str(screen_width//2), str(int(screen_height*0.8)), "400")


def reachable_button(name, label):
    screen_width, screen_height = map(int, re.findall(r"(\d+)x(\d+)", report["size"])[-1])
    last_error = None
    # At 200% text the two Core cards require more than five viewport-half swipes.
    # Keep the search bounded while preserving fresh visible-bounds and 48dp checks.
    attempts = 12
    for attempt in range(attempts):
        root = capture(name + "-scroll-" + str(attempt), "")
        try:
            button(root, label)
            report.setdefault("buttonReachability", []).append({"label": label, "state": name, "scanAttempt": attempt, "resetToTop": attempt > 0})
            return root
        except AssertionError as error:
            last_error = error
        if attempt == 0:
            scroll_to_top()
        elif attempt < attempts - 1:
            adb("shell", "input", "swipe", str(screen_width//2), str(int(screen_height*0.8)),
                str(screen_width//2), str(int(screen_height*0.3)), "400")
    raise AssertionError("Button not reachable at 48dp after bounded scrolling: " + label) from last_error


def heading_height(root, text):
    heights = []
    for node in root.iter("node"):
        if node.get("text") == text:
            x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
            if y2 > y1:
                heights.append(y2-y1)
    return min(heights, default=0)


def area(node):
    x1,y1,x2,y2 = map(int,re.findall(r"\d+",node.get("bounds")))
    return (x2-x1)*(y2-y1)


def snapshot():
    lines = adb("logcat", "-d", "-s", "LoopRun:I", "*:S", "-v", "raw").splitlines()
    return json.loads(next(line for line in reversed(lines) if line.strip().startswith("{")))


def wait_snapshot(predicate):
    for _ in range(20):
        state = snapshot()
        if predicate(state):
            return state
        time.sleep(0.5)
    raise AssertionError("Expected service snapshot not observed")


try:
    # Manual UI controls are tested under Unknown GPS; dedicated native motion tests own autopause.
    adb("shell", "cmd", "location", "set-location-enabled", "false")
    report["movementScenario"] = "Unknown GPS: manual UI evidence only, not autopause acceptance"
    adb("shell", "svc", "wifi", "disable")
    adb("shell", "svc", "data", "disable")
    adb("shell", "settings", "put", "system", "font_scale", "1.0")
    open_app()
    idle = capture("idle-offline", "Start C1.2")
    button(reachable_button("idle-start-c1", "Start C1.2"), "Start C1.2", tap=True)
    running = capture("running", "Pause")
    c1_start = wait_snapshot(lambda s: s.get("TemplateId") == "C1" and not s["Paused"])
    assert c1_start["Phase"] == "Warmup" and c1_start["ActiveSeconds"] + c1_start["RemainingSeconds"] == 240
    run_id = c1_start["Id"]
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "start-c2")
    assert wait_snapshot(lambda s: s["Id"] == run_id)["TemplateId"] == "C1", "C2 duplicate must not replace C1"
    for label in ["Pause", "+1 min", "Start main run"]:
        reachable_button("running-" + label.replace(" ", "-"), label)
    button(reachable_button("running-pause", "Pause"), "Pause", tap=True)
    scroll_to_top()
    paused = capture("paused", "Resume")
    normal_heading_height = heading_height(paused, "Manually paused")
    assert normal_heading_height > 0, "Manually paused heading must have accessible bounds"
    normal_height = button(reachable_button("paused-resume", "Resume"), "Resume")
    wait_snapshot(lambda s: s["Id"] == run_id and s["Paused"])
    adb("shell", "settings", "put", "system", "font_scale", "2.0")
    open_app()
    scroll_to_top()
    large = capture("paused-large-text", "Resume", normal_heading_height * 1.4)
    large_resume = reachable_button("paused-large-resume", "Resume")
    large_height = button(large_resume, "Resume")
    report["fontScaleRequested"] = "2.0"
    report["fontScaleReadback"] = adb("shell", "settings", "get", "system", "font_scale")
    report["resumeButtonHeightNormalPx"] = normal_height
    report["resumeButtonHeightLargePx"] = large_height
    report["pausedHeadingHeightNormalPx"] = normal_heading_height
    report["pausedHeadingHeightLargePx"] = heading_height(large, "Manually paused")
    report["actualTextScaling"] = "PASS: Manually paused heading rendered bounds grew at least 40% at system font scale 2.0; screenshots retain exact rendered evidence"
    assert snapshot()["Id"] == run_id and snapshot()["Paused"], "Reopen must retain the same paused session"
    button(reachable_button("large-resume-tap", "Resume"), "Resume", tap=True)
    resumed = capture("resumed-large-text", "Pause")
    for label in ["Pause", "+1 min", "Start main run"]:
        reachable_button("resumed-large-" + label.replace(" ", "-"), label)
    wait_snapshot(lambda s: s["Id"] == run_id and not s["Paused"])
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "complete")
    capture("c1-saved", "Run saved locally.")
    button(reachable_button("c1-saved-back", "Back to training"), "Back to training", tap=True)
    large_idle = capture("idle-large-text", "Start C1.2")
    reachable_button("idle-large-c1", "Start C1.2")
    reachable_button("idle-large-c2", "Start C2")
    # Return to normal scale before selecting the second Core card.
    adb("shell", "settings", "put", "system", "font_scale", "1.0")
    open_app()
    c2_idle = capture("c2-idle", "Start C2")
    c2_start = reachable_button("idle-start-c2", "Start C2")
    c2_tapped_at = time.monotonic()
    button(c2_start, "Start C2", tap=True)
    c2_running = capture("c2-running", "Pause")
    c2 = wait_snapshot(lambda s: s.get("TemplateId") == "C2" and not s["Paused"])
    capture_elapsed = math.ceil(time.monotonic() - c2_tapped_at)
    assert c2["Phase"] == "Work" and max(0, 2700 - capture_elapsed - 2) <= c2["RemainingSeconds"] <= 2700
    assert c2["ActiveSeconds"] + c2["RemainingSeconds"] == 2700, "C2 has exactly 45 active minutes"
    report["c2CaptureElapsedSeconds"] = capture_elapsed
    assert not any(n.get("text") in ["+1 min", "Start main run"] for n in c2_running.iter("node"))
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "start")
    assert wait_snapshot(lambda s: s["Id"] == c2["Id"])["TemplateId"] == "C2", "Cross-template duplicate must not replace C2"
    button(reachable_button("c2-running-pause", "Pause"), "Pause", tap=True)
    capture("c2-paused", "Resume")
    open_app()
    c2_reopened = capture("c2-reopened", "Resume")
    wait_snapshot(lambda s: s["Id"] == c2["Id"] and s["Paused"] and s["TemplateId"] == "C2")
    button(reachable_button("c2-reopened-resume", "Resume"), "Resume", tap=True)
    capture("c2-resumed", "Pause")
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "complete")
    capture("c2-saved", "Run saved locally.")
    button(reachable_button("c2-saved-back", "Back to training"), "Back to training", tap=True)
    capture("c2-finished", "Start C2")
    adb("shell", "am", "force-stop", PACKAGE)
    with tempfile.TemporaryDirectory() as temporary:
        db_path = Path(temporary) / "synthetic.db"
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with closing(sqlite3.connect(db_path)) as db, db:
            status, plan_json, metadata_json = db.execute("SELECT status, initial_plan, template_metadata FROM runs WHERE id=?", (c2["Id"],)).fetchone()
            assert status == "finished"
            plan, metadata = json.loads(plan_json), json.loads(metadata_json)
            assert len(plan) == 1 and plan[0]["TargetRpe"] == 4 and plan[0]["Duration"] == "00:45:00"
            assert metadata["TemplateId"] == "C2" and metadata["TemplateRevision"] == 2 and metadata["ProtocolId"] == "C2" and metadata["IntendedHeartRateZone"] == 2
            assert metadata["Targets"]["Mode"] == 0 and metadata["IsPersonalised"] is False
            assert db.execute("SELECT COUNT(*) FROM export_queue WHERE run_id=?", (c2["Id"],)).fetchone()[0] == 1
            report["c2FrozenMetadata"] = metadata
            assert db.execute("SELECT COUNT(*) FROM run_events WHERE run_id=? AND command='start'", (c2["Id"],)).fetchone()[0] == 1
            for checked_id in [run_id, c2["Id"]]:
                checkpoint = json.loads(db.execute("SELECT checkpoint FROM runs WHERE id=?", (checked_id,)).fetchone()[0])
                statistics = checkpoint["Snapshot"]["Statistics"]
                assert statistics["Version"] == 1 and statistics["HasSyntheticTime"]
                assert statistics["Total"]["ActiveTime"] == checkpoint["TotalActiveTime"]
                assert statistics["Main"]["ActiveTime"] == checkpoint["MainActiveTime"]
                assert statistics["CurrentSplit"] is None
                for scope in [statistics["Total"], statistics["Main"], *statistics["CompletedSplits"]]:
                    assert scope["KnownGpsActiveTime"] == "00:00:00"
                    assert scope["UnknownGpsActiveTime"] == scope["ActiveTime"]
                    assert scope["AveragePaceSecondsPerKilometre"] is None
                    assert scope["AverageHeartRate"] is None and scope["KnownHeartRateActiveTime"] == "00:00:00"
                    assert scope["UnknownHeartRateActiveTime"] == scope["ActiveTime"]
            report["persistedStatistics"] = "PASS: actual service/checkpoint active scopes conserved, Unknown GPS/HR remain unknown, final split not duplicated; accelerated debug completion labelled synthetic"
            c1_metadata = json.loads(db.execute("SELECT template_metadata FROM runs WHERE id=?", (run_id,)).fetchone()[0])
            assert c1_metadata["TemplateId"] == "C1" and c1_metadata["IntendedHeartRateZone"] is None
            assert c1_metadata["TemplateRevision"] == 3 and c1_metadata["ProtocolId"] == "C1.2"
            c1_plan = json.loads(db.execute("SELECT initial_plan FROM runs WHERE id=?", (run_id,)).fetchone()[0])
            assert len(c1_plan) == 11 and c1_plan[0]["Duration"] == "00:04:00" and c1_plan[-1]["Duration"] == "00:03:00"
            assert db.execute("SELECT COUNT(*) FROM run_events WHERE run_id=? AND command='start'", (run_id,)).fetchone()[0] == 1
        backup = Path(temporary) / "ui-results.db"
        backup.write_bytes(db_path.read_bytes())
        fixture = Path(temporary) / "schema2.db"
        with closing(sqlite3.connect(fixture)) as db, db:
            db.executescript("""
                CREATE TABLE runs (id TEXT PRIMARY KEY, started_utc TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, status TEXT NOT NULL, initial_plan TEXT NOT NULL, checkpoint TEXT NOT NULL);
                CREATE TABLE locations (run_id TEXT NOT NULL, utc_ms INTEGER NOT NULL, monotonic_ns INTEGER NOT NULL, latitude REAL NOT NULL, longitude REAL NOT NULL, accuracy REAL);
                CREATE TABLE export_queue (run_id TEXT PRIMARY KEY, status TEXT NOT NULL);
                CREATE TABLE run_events (sequence INTEGER PRIMARY KEY, run_id TEXT NOT NULL, command TEXT NOT NULL, observed_utc TEXT NOT NULL, elapsed_ms INTEGER NOT NULL, checkpoint TEXT NOT NULL);
                INSERT INTO runs VALUES ('v2', 'synthetic-start', 'synthetic-end', 1000, 'finished', '[]', '{"legacy":true}');
                INSERT INTO run_events VALUES (1, 'v2', 'start', 'synthetic-start', 0, '{"event":true}');
                PRAGMA user_version=2;
            """)
        adb("push", str(fixture), "/data/local/tmp/loop-schema2.db")
        adb("shell", "run-as", PACKAGE, "cp", "/data/local/tmp/loop-schema2.db", "databases/runs-debug.db")
        open_app()
        capture("schema2-upgraded-idle", "Start C2")
        adb("shell", "am", "force-stop", PACKAGE)
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with closing(sqlite3.connect(db_path)) as db, db:
            assert db.execute("PRAGMA user_version").fetchone()[0] == 5
            assert db.execute("SELECT initial_plan, checkpoint, template_metadata FROM runs WHERE id='v2'").fetchone() == ('[]', '{"legacy":true}', None)
            assert db.execute("SELECT checkpoint FROM run_events WHERE run_id='v2'").fetchall() == [('{"event":true}',)]
            assert db.execute("SELECT heart_rate_metadata FROM runs WHERE id='v2'").fetchone() == (None,)
            assert db.execute("SELECT COUNT(*) FROM heart_rate").fetchone()[0] == 0
        adb("shell", "run-as", PACKAGE, "rm", "databases/runs-debug.db")
        open_app()
        capture("fresh-schema5-idle", "Start C2")
        adb("shell", "am", "force-stop", PACKAGE)
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with closing(sqlite3.connect(db_path)) as db, db:
            assert db.execute("PRAGMA user_version").fetchone()[0] == 5
            assert "heart_rate_metadata" in [row[1] for row in db.execute("PRAGMA table_info(runs)")]
            assert db.execute("SELECT COUNT(*) FROM runs").fetchone()[0] == 0
            assert db.execute("SELECT COUNT(*) FROM heart_rate").fetchone()[0] == 0
        # Real schema-3 legacy rows: opening the journal interrupts recording only;
        # it does not rehydrate or replace frozen plans with the current factory.
        legacy_plan = [dict(phase) for phase in c1_plan]
        legacy_plan[0]["Duration"], legacy_plan[-1]["Duration"] = "00:10:00", "00:05:00"
        legacy_metadata = {"TemplateId":"C1", "Source":"LOOP Core", "Category":"intervals", "TemplateRevision":1, "IntendedHeartRateZone":None}
        for phase in legacy_plan:
            if phase["Kind"] in [0, 2, 3]: phase["TargetRpe"] = 2
        legacy_plan_json = json.dumps(legacy_plan, separators=(",", ":"))
        legacy_metadata_json = json.dumps(legacy_metadata, separators=(",", ":"))
        old_rows = []
        for suffix, status, metadata in [("recording", "recording", legacy_metadata_json),
                                          ("finished", "finished", legacy_metadata_json),
                                          ("unknown", "finished", None)]:
            finished = status == "finished"
            snapshot_data = {"Phase": "Finished" if finished else "Warmup", "Paused": False,
                             "ActiveSeconds": 2280 if finished else 120,
                             "RemainingSeconds": 0 if finished else 480}
            if metadata is not None:
                snapshot_data["TemplateId"] = "C1"
            checkpoint = json.dumps({"SchemaVersion": 1, "Plan": legacy_plan,
                "Completed": [{"Plan": phase, "ActiveTime": phase["Duration"]} for phase in legacy_plan] if finished else [],
                "PhaseActiveTime": "00:00:00" if finished else "00:02:00",
                "TotalActiveTime": "00:38:00" if finished else "00:02:00",
                "MainActiveTime": "00:23:00" if finished else "00:00:00",
                "Snapshot": snapshot_data}, separators=(",", ":"))
            old_rows.append(("legacy-c1-" + suffix, "2026-11-13T08:00:00Z",
                             "2026-11-13T08:38:00Z" if finished else "2026-11-13T08:02:00Z",
                             2280000 if finished else 120000, status, legacy_plan_json, checkpoint, metadata))
        with closing(sqlite3.connect(db_path)) as db, db:
            # Turn the fresh DB into the actual previous schema before journal migration.
            db.execute("ALTER TABLE runs DROP COLUMN heart_rate_metadata")
            db.execute("DROP TABLE heart_rate")
            db.execute("ALTER TABLE export_queue DROP COLUMN attempt_json")
            db.execute("ALTER TABLE export_queue DROP COLUMN last_error")
            assert [row[1] for row in db.execute("PRAGMA table_info(export_queue)")] == ["run_id", "status"]
            db.execute("PRAGMA user_version=3")
            db.executemany("INSERT INTO runs VALUES (?,?,?,?,?,?,?,?)", old_rows)
            db.executemany("INSERT INTO export_queue (run_id, status) VALUES (?, 'disabled_synthetic')",
                           [(row[0],) for row in old_rows if row[4] == "finished"])
        adb("push", str(db_path), "/data/local/tmp/loop-legacy-c1.db")
        adb("shell", "run-as", PACKAGE, "cp", "/data/local/tmp/loop-legacy-c1.db", "databases/runs-debug.db")
        open_app()
        capture("legacy-c1-opened-idle", "Start C1.2")
        adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "start")
        new_c1 = wait_snapshot(lambda state: state.get("TemplateId") == "C1" and state["Id"] != run_id)
        assert new_c1["ActiveSeconds"] + new_c1["RemainingSeconds"] == 240
        adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "complete")
        wait_snapshot(lambda state: state["Id"] == new_c1["Id"] and state["Phase"] == "Finished")
        adb("shell", "am", "force-stop", PACKAGE)
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with closing(sqlite3.connect(db_path)) as db, db:
            assert db.execute("PRAGMA user_version").fetchone()[0] == 5
            assert [row[1] for row in db.execute("PRAGMA table_info(export_queue)")] == ["run_id", "status", "attempt_json", "last_error"]
            for row in old_rows:
                if row[4] == "finished":
                    assert db.execute("SELECT status, attempt_json, last_error FROM export_queue WHERE run_id=?", (row[0],)).fetchone() == ("disabled_synthetic", None, None)
            for row in old_rows:
                expected = list(row) + [None]
                if expected[4] == "recording":
                    expected[4] = "interrupted"
                assert db.execute("SELECT * FROM runs WHERE id=?", (row[0],)).fetchone() == tuple(expected)
            assert db.execute("SELECT COUNT(*) FROM runs").fetchone()[0] == 4
            new_plan, new_metadata = db.execute("SELECT initial_plan, template_metadata FROM runs WHERE id=?", (new_c1["Id"],)).fetchone()
            assert json.loads(new_plan) == c1_plan and json.loads(new_metadata)["TemplateRevision"] == 3
            assert db.execute("SELECT COUNT(*) FROM export_queue WHERE status!='disabled_synthetic'").fetchone()[0] == 0
        report["legacyC1"] = "PASS: journal reopen only interrupts old recording; frozen 10/5 plan/checkpoint/v1 or NULL metadata unchanged; separate new start uses 4/3 revision 3"
        adb("push", str(backup), "/data/local/tmp/loop-ui-results.db")
        adb("shell", "run-as", PACKAGE, "cp", "/data/local/tmp/loop-ui-results.db", "databases/runs-debug.db")
        report["schemaMigration"] = "PASS: fresh 5 and seeded 2-to-5/3-to-5, existing phase/checkpoint/event payload unchanged, legacy HR metadata and new attempt fields NULL; 1-to-5 covered by stronger smoke"
    print("PASS: real C1/C2 Start/Pause/Resume, 48dp actions, actual large-text growth, same sessions and frozen C2 metadata")
finally:
    adb("shell", "cmd", "location", "set-location-enabled", original_location)
    if original_scale == "null":
        adb("shell", "settings", "delete", "system", "font_scale")
    else:
        adb("shell", "settings", "put", "system", "font_scale", original_scale)
    adb("shell", "svc", "wifi", "enable" if original_wifi == "1" else "disable")
    adb("shell", "svc", "data", "enable" if original_data == "1" else "disable")
    (OUT / "manifest.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
