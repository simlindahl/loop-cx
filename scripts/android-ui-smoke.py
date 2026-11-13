"""Focused C1 UI evidence; run only after android-smoke.sh in a dedicated emulator."""
import json
import re
import subprocess
import sqlite3
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

PACKAGE = "se.loopcx.running"
OUT = Path("verification/c1-ui")
OUT.mkdir(parents=True, exist_ok=True)


def adb(*args, binary=False):
    return subprocess.check_output(["adb", *args], text=not binary, timeout=30).strip() if not binary else subprocess.check_output(["adb", *args], timeout=30)


assert adb("shell", "getprop", "ro.kernel.qemu") == "1", "Dedicated emulator required"
original_scale = adb("shell", "settings", "get", "system", "font_scale")
original_wifi = adb("shell", "settings", "get", "global", "wifi_on")
original_data = adb("shell", "settings", "get", "global", "mobile_data")
report = {"source": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
          "density": adb("shell", "wm", "density"), "size": adb("shell", "wm", "size"),
          "fontScaleBefore": original_scale, "states": [],
          "limits": "UIAutomator is not TalkBack. Screenshots require visual review; font asset presence does not prove font loading."}
density = int(re.findall(r"(\d+)", report["density"])[-1]) / 160


def open_app():
    adb("shell", "am", "start", "-W", "-n", PACKAGE + "/.MainActivity")


def capture(name, expected, heading_min_height=0):
    for _ in range(40):
        adb("shell", "uiautomator", "dump", "/data/local/tmp/loop-ui.xml")
        xml = adb("shell", "cat", "/data/local/tmp/loop-ui.xml")
        root = ET.fromstring(xml)
        if any(expected in (n.get("text", "") + n.get("content-desc", "")) for n in root.iter("node")) and heading_height(root, "Paused") >= heading_min_height:
            (OUT / (name + ".xml")).write_text(xml + "\n", encoding="utf-8")
            (OUT / (name + ".png")).write_bytes(adb("exec-out", "screencap", "-p", binary=True))
            report["states"].append(name)
            return root
        time.sleep(1)
    (OUT / (name + "-failed.xml")).write_text(xml + "\n", encoding="utf-8")
    (OUT / (name + "-failed.png")).write_bytes(adb("exec-out", "screencap", "-p", binary=True))
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
    adb("shell", "svc", "wifi", "disable")
    adb("shell", "svc", "data", "disable")
    adb("shell", "settings", "put", "system", "font_scale", "1.0")
    open_app()
    idle = capture("idle-offline", "Start C1 · Intervals")
    button(idle, "Start C1 · Intervals", tap=True)
    running = capture("running", "Pause")
    run_id = wait_snapshot(lambda s: s.get("TemplateId") == "C1" and not s["Paused"])["Id"]
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "start-c2")
    assert wait_snapshot(lambda s: s["Id"] == run_id)["TemplateId"] == "C1", "C2 duplicate must not replace C1"
    for label in ["Pause", "+1 min", "Start main run"]:
        button(running, label)
    button(running, "Pause", tap=True)
    paused = capture("paused", "Resume")
    normal_height = button(paused, "Resume")
    normal_heading_height = heading_height(paused, "Paused")
    assert normal_heading_height > 0, "Paused heading must have accessible bounds"
    wait_snapshot(lambda s: s["Id"] == run_id and s["Paused"])
    adb("shell", "settings", "put", "system", "font_scale", "2.0")
    open_app()
    large = capture("paused-large-text", "Resume", normal_heading_height * 1.4)
    large_height = button(large, "Resume")
    report["fontScaleRequested"] = "2.0"
    report["fontScaleReadback"] = adb("shell", "settings", "get", "system", "font_scale")
    report["resumeButtonHeightNormalPx"] = normal_height
    report["resumeButtonHeightLargePx"] = large_height
    report["pausedHeadingHeightNormalPx"] = normal_heading_height
    report["pausedHeadingHeightLargePx"] = heading_height(large, "Paused")
    report["actualTextScaling"] = "PASS: one-line Paused heading bounds grew at least 40% at system font scale 2.0; screenshots retain exact rendered evidence"
    assert snapshot()["Id"] == run_id and snapshot()["Paused"], "Reopen must retain the same paused session"
    button(large, "Resume", tap=True)
    resumed = capture("resumed-large-text", "Pause")
    for label in ["Pause", "+1 min", "Start main run"]:
        button(resumed, label)
    wait_snapshot(lambda s: s["Id"] == run_id and not s["Paused"])
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "complete")
    large_idle = capture("idle-large-text", "Start C1 · Intervals")
    button(large_idle, "Start C1 · Intervals")
    # A vertically scrollable large-text card is reachable, not clipped away.
    screen = re.findall(r"(\d+)x(\d+)", report["size"])[-1]
    screen_width, screen_height = map(int, screen)
    for attempt in range(5):
        c2_nodes = [n for n in large_idle.iter("node")
                    if n.get("clickable") == "true" and n.get("enabled") == "true"
                    and n.get("package", PACKAGE) == PACKAGE
                    and any(c.get("text") == "Start C2 · Zone 2" or c.get("content-desc") == "Start C2 · Zone 2"
                            for c in n.iter("node"))]
        visible = []
        for node in c2_nodes:
            x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
            if x2 > x1 and y2 > y1 and y2 < screen_height - 24 * density:
                visible.append(node)
        if visible:
            button(large_idle, "Start C2 · Zone 2")
            capture("c2-idle-large-text-reachable", "Start C2 · Zone 2")
            report["largeC2Reachability"] = {"enabled48dp": True, "swipes": attempt}
            break
        adb("shell", "input", "swipe", str(screen_width//2), str(int(screen_height*0.8)),
            str(screen_width//2), str(int(screen_height*0.3)), "400")
        large_idle = capture("idle-large-text-scrolled", "")
    else:
        raise AssertionError("Large-text C2 start not reachable after bounded vertical scrolling")
    # Return to normal scale before selecting the second Core card.
    adb("shell", "settings", "put", "system", "font_scale", "1.0")
    open_app()
    c2_idle = capture("c2-idle", "Start C2 · Zone 2")
    button(c2_idle, "Start C2 · Zone 2", tap=True)
    c2_running = capture("c2-running", "Pause")
    c2 = wait_snapshot(lambda s: s.get("TemplateId") == "C2" and not s["Paused"])
    assert c2["Phase"] == "Work" and 2680 <= c2["RemainingSeconds"] <= 2700
    assert not any(n.get("text") in ["+1 min", "Start main run"] for n in c2_running.iter("node"))
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "start")
    assert wait_snapshot(lambda s: s["Id"] == c2["Id"])["TemplateId"] == "C2", "Cross-template duplicate must not replace C2"
    button(c2_running, "Pause", tap=True)
    capture("c2-paused", "Resume")
    open_app()
    c2_reopened = capture("c2-reopened", "Resume")
    wait_snapshot(lambda s: s["Id"] == c2["Id"] and s["Paused"] and s["TemplateId"] == "C2")
    button(c2_reopened, "Resume", tap=True)
    capture("c2-resumed", "Pause")
    adb("shell", "am", "start", "-n", PACKAGE + "/.MainActivity", "--es", "loop-command", "complete")
    capture("c2-finished", "Start C2 · Zone 2")
    adb("shell", "am", "force-stop", PACKAGE)
    with tempfile.TemporaryDirectory() as temporary:
        db_path = Path(temporary) / "synthetic.db"
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with sqlite3.connect(db_path) as db:
            status, plan_json, metadata_json = db.execute("SELECT status, initial_plan, template_metadata FROM runs WHERE id=?", (c2["Id"],)).fetchone()
            assert status == "finished"
            plan, metadata = json.loads(plan_json), json.loads(metadata_json)
            assert len(plan) == 1 and plan[0]["TargetRpe"] == 3 and plan[0]["Duration"] == "00:45:00"
            assert metadata == {"TemplateId":"C2", "Source":"LOOP Core", "Category":"continuous", "TemplateRevision":1, "IntendedHeartRateZone":2}
            assert db.execute("SELECT COUNT(*) FROM export_queue WHERE run_id=?", (c2["Id"],)).fetchone()[0] == 1
            report["c2FrozenMetadata"] = metadata
            assert db.execute("SELECT COUNT(*) FROM run_events WHERE run_id=? AND command='start'", (c2["Id"],)).fetchone()[0] == 1
            c1_metadata = json.loads(db.execute("SELECT template_metadata FROM runs WHERE id=?", (run_id,)).fetchone()[0])
            assert c1_metadata["TemplateId"] == "C1" and c1_metadata["IntendedHeartRateZone"] is None
            assert db.execute("SELECT COUNT(*) FROM run_events WHERE run_id=? AND command='start'", (run_id,)).fetchone()[0] == 1
        backup = Path(temporary) / "ui-results.db"
        backup.write_bytes(db_path.read_bytes())
        fixture = Path(temporary) / "schema2.db"
        with sqlite3.connect(fixture) as db:
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
        capture("schema2-upgraded-idle", "Start C2 · Zone 2")
        adb("shell", "am", "force-stop", PACKAGE)
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with sqlite3.connect(db_path) as db:
            assert db.execute("PRAGMA user_version").fetchone()[0] == 3
            assert db.execute("SELECT initial_plan, checkpoint, template_metadata FROM runs WHERE id='v2'").fetchone() == ('[]', '{"legacy":true}', None)
            assert db.execute("SELECT checkpoint FROM run_events WHERE run_id='v2'").fetchall() == [('{"event":true}',)]
        adb("shell", "run-as", PACKAGE, "rm", "databases/runs-debug.db")
        open_app()
        capture("fresh-schema3-idle", "Start C2 · Zone 2")
        adb("shell", "am", "force-stop", PACKAGE)
        db_path.write_bytes(adb("exec-out", "run-as", PACKAGE, "cat", "databases/runs-debug.db", binary=True))
        with sqlite3.connect(db_path) as db:
            assert db.execute("PRAGMA user_version").fetchone()[0] == 3
            assert "template_metadata" in [row[1] for row in db.execute("PRAGMA table_info(runs)")]
            assert db.execute("SELECT COUNT(*) FROM runs").fetchone()[0] == 0
        adb("push", str(backup), "/data/local/tmp/loop-ui-results.db")
        adb("shell", "run-as", PACKAGE, "cp", "/data/local/tmp/loop-ui-results.db", "databases/runs-debug.db")
        report["schemaMigration"] = "PASS: fresh 3 and seeded 2-to-3, existing phase/checkpoint/event payload unchanged and legacy metadata NULL; 1-to-3 covered by stronger smoke"
    print("PASS: real C1/C2 Start/Pause/Resume, 48dp actions, actual large-text growth, same sessions and frozen C2 metadata")
finally:
    if original_scale == "null":
        adb("shell", "settings", "delete", "system", "font_scale")
    else:
        adb("shell", "settings", "put", "system", "font_scale", original_scale)
    adb("shell", "svc", "wifi", "enable" if original_wifi == "1" else "disable")
    adb("shell", "svc", "data", "enable" if original_data == "1" else "disable")
    (OUT / "manifest.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
