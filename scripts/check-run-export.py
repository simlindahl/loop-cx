#!/usr/bin/env python3
"""Check the schema and independent consumer against synthetic and projected documents."""
import copy
import importlib.util
import json
import sys
import tempfile
from pathlib import Path
from jsonschema import Draft202012Validator, FormatChecker

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("consumer", root / "scripts/parse-run-export.py")
consumer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(consumer)
schema = json.loads((root / "docs/export/run-v1.schema.json").read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
validator = Draft202012Validator(schema, format_checker=FormatChecker())
paths = [root / "docs/export/sample-v1.json", root / "docs/export/sample-current-v1.json"] + [Path(value) for value in sys.argv[1:]]
for path in paths:
    document = json.loads(path.read_text(encoding="utf-8"))
    validator.validate(document)
    reconstructed = consumer.read_export(path)
    run = document["run"]
    assert reconstructed["originalPlan"] == run["originalPlan"]
    assert reconstructed["terminalContext"] == run["checkpoint"].get("TerminalContext")
    known_checkpoint = "SchemaVersion" in run["checkpoint"]
    if known_checkpoint:
        assert reconstructed["snapshot"] == run["checkpoint"]["Snapshot"]
        assert len(reconstructed["resolvedPlan"]) == len(run["checkpoint"]["Plan"])
        assert reconstructed["activeTicks"] >= reconstructed["mainActiveTicks"]
    else:
        for key in ("snapshot", "resolvedPlan", "completedPhases", "statistics", "activeTicks", "mainActiveTicks", "phaseBoundaries", "startContext", "terminalContext"):
            assert reconstructed[key] is None
    assert reconstructed["locations"] == run["locations"]
    assert reconstructed["heartRate"] == run["heartRate"]
    invalid_cases = [lambda value: value.update(schemaVersion=2),
                     lambda value: value["units"].update(distance="miles"),
                     lambda value: value["run"]["originalPlan"][0].update(Duration="bananas")]
    if known_checkpoint:
        invalid_cases.extend([lambda value: value["run"]["checkpoint"].pop("Plan"),
                              lambda value: value["run"]["checkpoint"].pop("Completed")])
    for mutate in invalid_cases:
        bad = copy.deepcopy(document)
        mutate(bad)
        assert not validator.is_valid(bad)
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "bad.json"
            target.write_text(json.dumps(bad), encoding="utf-8", newline="\n")
            try:
                consumer.read_export(target)
            except (ValueError, KeyError, TypeError):
                pass
            else:
                raise AssertionError("Consumer accepted malformed version, units or workout facts")
    if known_checkpoint and run["checkpoint"]["Snapshot"].get("Statistics") is not None:
        for kind in ("missing", "inconsistent"):
            bad = copy.deepcopy(document)
            total = bad["run"]["checkpoint"]["Snapshot"]["Statistics"]["Total"]
            if kind == "missing":
                total.pop("ActiveTime")
                assert not validator.is_valid(bad)
            else:
                total["UnknownGpsActiveTime"] = "00:00:00"
            with tempfile.TemporaryDirectory() as temporary:
                target = Path(temporary) / "bad-statistics.json"
                target.write_text(json.dumps(bad), encoding="utf-8", newline="\n")
                try:
                    consumer.read_export(target)
                except (ValueError, KeyError, TypeError):
                    pass
                else:
                    raise AssertionError("Consumer accepted missing or nonconserving statistics")
    print("PASS schema + independent reconstruction:", path.name)

current = json.loads((root / "docs/export/sample-current-v1.json").read_text(encoding="utf-8"))
terminal = {"TerminalPreviousPhaseIndex": 2, "ObservedTimeZoneId": "Europe/Stockholm",
            "ObservedUtcOffsetSeconds": 3600, "EffectiveUtcOffsetSeconds": 7200,
            "Provenance": "terminal-observed-zone-rules-v1"}
current["run"]["checkpoint"]["TerminalContext"] = terminal
validator.validate(current)
with tempfile.TemporaryDirectory() as temporary:
    target = Path(temporary) / "terminal-context.json"
    target.write_text(json.dumps(current), encoding="utf-8", newline="\n")
    assert consumer.read_export(target)["terminalContext"] == terminal
print("PASS additive terminal-zone provenance reconstructed without current-zone fallback")

legacy = json.loads((root / "docs/export/sample-v1.json").read_text(encoding="utf-8"))
legacy["run"]["checkpoint"] = {"legacy": "retained exactly"}
validator.validate(legacy)
with tempfile.TemporaryDirectory() as temporary:
    target = Path(temporary) / "legacy.json"
    target.write_text(json.dumps(legacy), encoding="utf-8", newline="\n")
    reconstructed = consumer.read_export(target)
    assert reconstructed["resolvedPlan"] is None and reconstructed["statistics"] is None
    assert reconstructed["phaseBoundaries"] is None and reconstructed["startContext"] is None
    assert reconstructed["terminalContext"] is None
print("PASS explicit unknown legacy checkpoint without fabricated current facts")
