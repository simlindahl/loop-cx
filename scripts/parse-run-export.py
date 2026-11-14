#!/usr/bin/env python3
"""Independent consumer of the documented v1 JSON; no app assembly or projector import."""
import argparse
import datetime
import json
import math
import re
import uuid
from pathlib import Path

UNITS = {'distance': 'metres', 'coordinates': 'decimal-degrees', 'heartRate': 'beats-per-minute', 'utc': 'ISO-8601', 'gpsUtc': 'unix-milliseconds', 'observedElapsed': 'milliseconds', 'gpsBootMonotonic': 'nanoseconds', 'hrBootReceiptMonotonic': '100-nanosecond-ticks', 'checkpointDuration': 'TimeSpan-constant-format', 'pace': 'seconds-per-kilometre'}


PHASES = ("Warmup", "Work", "Recovery", "Cooldown")
TICKS = 10_000_000


def duration_ticks(value):
    match = re.fullmatch(r"(?:(\d+)\.)?(\d{2}):([0-5]\d):([0-5]\d)(?:\.(\d{1,7}))?", value or "")
    if not match or int(match[2]) > 23:
        raise ValueError("Invalid stored TimeSpan duration")
    days, hours, minutes, seconds, fraction = match.groups()
    return ((int(days or 0) * 24 + int(hours)) * 3600 + int(minutes) * 60 + int(seconds)) * TICKS + int((fraction or "").ljust(7, "0") or 0)


def phase(value):
    kind = value["Kind"]
    ticks = duration_ticks(value["Duration"])
    rpe = value["TargetRpe"]
    if type(kind) is not int or kind not in range(4) or ticks <= 0 or type(rpe) is not int or rpe not in range(1, 11):
        raise ValueError("Invalid frozen phase")
    return {"kind": PHASES[kind], "durationTicks": ticks, "targetRpe": rpe}


def statistics_scope(value):
    active = duration_ticks(value["ActiveTime"])
    known = duration_ticks(value["KnownGpsActiveTime"])
    unknown = duration_ticks(value["UnknownGpsActiveTime"])
    metres = value["MeasuredMetres"]
    if active != known + unknown or not math.isfinite(metres) or metres < 0:
        raise ValueError("GPS scope does not conserve active time")
    pace = value["AveragePaceSecondsPerKilometre"]
    if pace is not None and (unknown > 0 or metres <= 0 or not math.isfinite(pace) or pace <= 0):
        raise ValueError("Unsupported full-scope average pace")
    hr_known = duration_ticks(value["KnownHeartRateActiveTime"]) if "KnownHeartRateActiveTime" in value else None
    hr_unknown = duration_ticks(value["UnknownHeartRateActiveTime"]) if "UnknownHeartRateActiveTime" in value else active
    mean = value.get("AverageHeartRate")
    if hr_known is not None and active != hr_known + hr_unknown:
        raise ValueError("HR scope does not conserve active time")
    if mean is not None and (not hr_known or not math.isfinite(mean) or mean <= 0):
        raise ValueError("HR mean requires valid supported time")
    zones = value.get("HeartRateZones")
    if zones is not None:
        indices = [zone["Zone"] for zone in zones]
        if len(set(indices)) != len(indices) or any(index not in range(1, 6) for index in indices):
            raise ValueError("Invalid stored zone identity")
        if sum(duration_ticks(zone["ActiveTime"]) for zone in zones) + duration_ticks(value["UnclassifiedHeartRateActiveTime"]) != hr_known:
            raise ValueError("Zone and unclassified time do not conserve valid HR time")
    return {"activeTicks": active, "measuredMetres": metres, "knownGpsTicks": known, "unknownGpsTicks": unknown,
            "averagePaceSecondsPerKilometre": pace, "averageHeartRate": mean, "knownHrTicks": hr_known,
            "unknownHrTicks": hr_unknown, "zones": zones, "unclassifiedHrTicks": duration_ticks(value["UnclassifiedHeartRateActiveTime"]) if "UnclassifiedHeartRateActiveTime" in value else None}


def reconstruct_checkpoint(checkpoint):
    if "SchemaVersion" not in checkpoint:
        return {"format": "legacy-unknown", "resolvedPlan": None, "completedPhases": None, "statistics": None,
                "snapshot": None, "activeTicks": None, "mainActiveTicks": None}
    if checkpoint["SchemaVersion"] != 1:
        raise ValueError("Unsupported checkpoint version")
    plan = [phase(value) for value in checkpoint["Plan"]]
    completed = [{"plan": phase(value["Plan"]), "activeTicks": duration_ticks(value["ActiveTime"])} for value in checkpoint["Completed"]]
    total = duration_ticks(checkpoint["TotalActiveTime"])
    main = duration_ticks(checkpoint["MainActiveTime"])
    current = duration_ticks(checkpoint["PhaseActiveTime"])
    snapshot = checkpoint["Snapshot"]
    if not plan or len(completed) > len(plan) or sum(value["activeTicks"] for value in completed) + current != total:
        raise ValueError("Actual phases do not conserve total active time")
    current_kind = plan[len(completed)]["kind"] if len(completed) < len(plan) else "Finished"
    if snapshot["Phase"] != current_kind or (current_kind == "Finished" and current != 0):
        raise ValueError("Current phase disagrees with stored completed instances")
    derived_main = sum(value["activeTicks"] for value in completed if value["plan"]["kind"] in ("Work", "Recovery"))
    if current_kind in ("Work", "Recovery"):
        derived_main += current
    if derived_main != main:
        raise ValueError("Actual phase accounting disagrees with stored main time")
    statistics = snapshot.get("Statistics")
    decoded = None
    if statistics is not None:
        if statistics["Version"] != 1:
            raise ValueError("Unsupported statistics version")
        total_scope, main_scope = statistics_scope(statistics["Total"]), statistics_scope(statistics["Main"])
        splits = [statistics_scope(value) for value in statistics["CompletedSplits"]]
        pending = statistics_scope(statistics["CurrentSplit"]) if statistics["CurrentSplit"] is not None else None
        pieces = splits + ([pending] if pending else [])
        if total_scope["activeTicks"] != total or main_scope["activeTicks"] != main:
            raise ValueError("Statistics and session clocks disagree")
        for key in ("activeTicks", "knownGpsTicks", "unknownGpsTicks"):
            if sum(piece[key] for piece in pieces) != main_scope[key]:
                raise ValueError("Split coverage does not conserve main")
        for key in ("knownHrTicks", "unknownHrTicks"):
            if main_scope[key] is not None and all(piece[key] is not None for piece in pieces) and sum(piece[key] for piece in pieces) != main_scope[key]:
                raise ValueError("Split HR coverage does not conserve main")
        if abs(sum(piece["measuredMetres"] for piece in pieces) - main_scope["measuredMetres"]) > 0.001:
            raise ValueError("Split distance does not conserve main")
        decoded = {"total": total_scope, "main": main_scope, "completedSplits": splits, "currentSplit": pending}
    previous = -1
    for boundary in checkpoint.get("PhaseBoundaries") or []:
        index = boundary["PreviousPhaseIndex"]
        if index <= previous or index >= len(plan) or PHASES[boundary["PreviousPhase"]] != plan[index]["kind"]:
            raise ValueError("Boundary phase-instance order is invalid")
        following = boundary["NextPhaseIndex"]
        if following != (index + 1 if index + 1 < len(plan) else None):
            raise ValueError("Boundary next instance is invalid")
        next_kind = plan[following]["kind"] if following is not None else None
        if (PHASES[boundary["NextPhase"]] if boundary["NextPhase"] is not None else None) != next_kind:
            raise ValueError("Boundary next phase disagrees with its instance")
        if boundary["EffectiveElapsedMilliseconds"] < 0 or boundary["ObservedElapsedMilliseconds"] < 0:
            raise ValueError("Invalid boundary clock")
        previous = index
    return {"format": "stored-v1", "resolvedPlan": plan, "completedPhases": completed, "statistics": decoded,
            "snapshot": snapshot, "activeTicks": total, "mainActiveTicks": main}



def interpret_template(value):
    if value is None or "TemplateRevision" not in value:
        return None
    if value["TemplateRevision"] < 1:
        raise ValueError("Invalid frozen template revision")
    targets = value.get("Targets")
    interpreted = None
    if targets is not None:
        mode = targets["Mode"]
        if type(mode) is not int or mode not in (0, 1, 2):
            raise ValueError("Unsupported frozen target mode")
        work, recovery = targets.get("Work"), targets.get("Recovery")
        if mode == 0 and (work is not None or recovery is not None):
            raise ValueError("Unexpected target under None")
        if mode != 0 and (type(work) is not int or work <= 0 or (recovery is not None and (type(recovery) is not int or recovery <= 0))):
            raise ValueError("Missing or invalid frozen target")
        if mode == 2 and (work not in range(1, 6) or (recovery is not None and recovery not in range(1, 6))):
            raise ValueError("Invalid intended zone")
        interpreted = {"mode": ("none", "pace-seconds-per-kilometre", "intended-heart-rate-zone")[mode], "work": work, "recovery": recovery}
    return {"templateId": value["TemplateId"], "source": value["Source"], "revision": value["TemplateRevision"], "targets": interpreted}


def interpret_hr_setup(value):
    if value is None:
        return None
    config = value["Configuration"]
    policy = config["Policy"]
    gap = duration_ticks(policy["MaximumGap"])
    if config["Version"] != 1 or policy["Version"] != 1 or gap <= 0:
        raise ValueError("Unsupported HR quality policy")
    zones = config["Zones"]
    if len(zones) not in (0, 5):
        raise ValueError("Incomplete frozen personal zones")
    previous = None
    for index, zone in enumerate(zones):
        if zone["Number"] != index + 1 or zone["MinimumBpm"] <= 0 or zone["MaximumBpm"] < zone["MinimumBpm"] or (previous is not None and zone["MinimumBpm"] != previous + 1):
            raise ValueError("Invalid frozen personal zone boundaries")
        previous = zone["MaximumBpm"]
    return {"deviceAddress": config["DeviceAddress"], "maximumGapTicks": gap, "zones": zones, "configurationError": value["ConfigurationError"]}


def read_export(path):
    def reject_constant(value):
        raise ValueError("Nonfinite JSON number: " + value)

    document = json.loads(Path(path).read_text(encoding="utf-8"), parse_constant=reject_constant)
    if document.get("schemaVersion") != 1 or document.get("units") != UNITS:
        raise ValueError("Unsupported export version or units")
    run = document["run"]
    uuid.UUID(run["id"])
    for key in ("startedUtc", "lastObservedUtc"):
        value = datetime.datetime.fromisoformat(run[key].replace("Z", "+00:00"))
        if value.utcoffset() != datetime.timedelta(0):
            raise ValueError("Run UTC timestamp has a non-UTC offset")
    if run["status"] not in ("recording", "finished", "interrupted") or run["observedElapsedMilliseconds"] < 0:
        raise ValueError("Invalid observed run state")
    original_plan = [phase(value) for value in run["originalPlan"]]
    checkpoint = run["checkpoint"]
    decoded = reconstruct_checkpoint(checkpoint)
    for rows, key in ((run["events"], "sequence"), (run["locations"], "snapshotOrder"), (run["heartRate"], "sequence")):
        values = [row[key] for row in rows]
        if any(not isinstance(value, int) or value < 0 for value in values) or values != sorted(set(values)):
            raise ValueError("Duplicate or unordered raw/event identities")
    if not isinstance(run["originalPlan"], list):
        raise ValueError("Missing frozen original plan")
    for row in run["heartRate"]:
        if row["bpm"] is not None and (not isinstance(row["bpm"], int) or row["bpm"] <= 0):
            raise ValueError("Invalid BPM; unknown must be null")
    interpreted_events = []
    for event in run["events"]:
        event_state = reconstruct_checkpoint(event["checkpoint"])
        interpreted_events.append({"sequence": event["sequence"], "command": event["command"],
            "observedElapsedMilliseconds": event["observedElapsedMilliseconds"], "observedUtc": event["observedUtc"],
            "paused": event_state["snapshot"].get("Paused") if event_state["snapshot"] else None,
            "automaticEvent": event["checkpoint"].get("AutomaticEvent"), "activeTicks": event_state["activeTicks"]})
    # Return exact stored facts plus independently interpreted quantities; never recompute main from later raw.
    return {"id": run["id"], "status": run["status"], "originalPlan": run["originalPlan"], "interpretedOriginalPlan": original_plan,
            "resolvedPlan": decoded["resolvedPlan"], "completedPhases": decoded["completedPhases"],
            "statistics": decoded["statistics"], "activeTicks": decoded["activeTicks"], "mainActiveTicks": decoded["mainActiveTicks"],
            "snapshot": decoded["snapshot"], "startContext": checkpoint.get("StartContext"),
            "terminalContext": checkpoint.get("TerminalContext"),
            "phaseBoundaries": checkpoint.get("PhaseBoundaries"), "template": run["template"],
            "interpretedTemplate": interpret_template(run["template"]), "interpretedHeartRateSetup": interpret_hr_setup(run["heartRateSetup"]),
            "heartRateSetup": run["heartRateSetup"], "events": run["events"], "interpretedEvents": interpreted_events,
            "locations": run["locations"], "heartRate": run["heartRate"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("file")
    arguments = parser.parse_args()
    print(json.dumps(read_export(arguments.file), indent=2, allow_nan=False))
