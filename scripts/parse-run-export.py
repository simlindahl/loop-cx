#!/usr/bin/env python3
"""Independent consumer of the documented v1/v2 JSON; no app assembly or projector import."""
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


def split_policy(snapshot):
    distance = snapshot.get("Distance")
    if distance is None or "SplitPolicy" not in distance:
        return None
    policy = distance["SplitPolicy"]
    if not isinstance(policy, dict) or type(policy.get("Version")) is not int or policy["Version"] != 1:
        raise ValueError("Unsupported split policy version")
    basis, interval = policy.get("Basis"), policy.get("Interval")
    if type(interval) is not int or not (
        (basis == "Distance" and 500 <= interval <= 5000 and interval % 500 == 0)
        or (basis == "Time" and 1 <= interval <= 30)
    ):
        raise ValueError("Invalid frozen split basis or interval")
    return policy


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
    policy = split_policy(snapshot)
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
        if policy is not None:
            by_time = policy["Basis"] == "Time"
            key = "activeTicks" if by_time else "measuredMetres"
            limit = policy["Interval"] * 60 * TICKS if by_time else policy["Interval"]
            tolerance = 0 if by_time else 0.001
            for index, piece in enumerate(pieces):
                value = piece[key]
                is_complete = index < len(pieces) - 1 or (pending is None and current_kind in ("Work", "Recovery"))
                if value > limit + tolerance or (is_complete and abs(value - limit) > tolerance):
                    raise ValueError("Stored split disagrees with frozen interval")
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


def validate_points(version, run):
    seen = {}
    latest = 0
    chain = None
    for point in run["locations"]:
        if version == 1:
            if "provenance" in point:
                raise ValueError("Version 1 has no point provenance field")
            continue
        fact = point.get("provenance")
        if fact is None:
            chain = None
            continue
        expected = {"version", "runId", "pointId", "outcome", "drawablePredecessor", "breakReason"}
        if set(fact) != expected or type(fact["version"]) is not int or fact["version"] != 1 or uuid.UUID(fact["runId"]) != uuid.UUID(run["id"]):
            raise ValueError("Unsupported point provenance")
        identity = fact["pointId"]
        outcome = fact["outcome"]
        predecessor = fact["drawablePredecessor"]
        if type(identity) is not int or identity <= latest or outcome not in ("valid", "rejected", "ignored", "not-assessed"):
            raise ValueError("Invalid point identity or outcome")
        if predecessor is None:
            if not isinstance(fact["breakReason"], str) or not fact["breakReason"].strip():
                raise ValueError("Unlinked point requires explicit break provenance")
        elif (type(predecessor) is not int or outcome != "valid" or predecessor != chain
              or predecessor not in seen or seen[predecessor]["provenance"]["outcome"] != "valid"
              or fact["breakReason"] is not None
              or seen[predecessor]["bootMonotonicNanoseconds"] >= point["bootMonotonicNanoseconds"]):
            raise ValueError("Invalid drawable predecessor")
        seen[identity] = point
        latest = identity
        if outcome == "valid":
            chain = identity
        elif outcome != "ignored":
            chain = None


def utc_ticks(value):
    parsed = datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("UTC/civil provenance requires a recorded offset")
    fraction = re.search(r"\.(\d{1,7})(?:Z|[+-]\d{2}:\d{2})$", value)
    remainder = int(fraction[1].ljust(7, "0")[-1]) if fraction else 0
    delta = parsed.astimezone(datetime.timezone.utc) - datetime.datetime(1970, 1, 1, tzinfo=datetime.timezone.utc)
    return ((delta.days * 86400 + delta.seconds) * TICKS + delta.microseconds * 10 + remainder)


def ghost_checkpoint(cp):
    def require(ok, message):
        if not ok:
            raise ValueError(message)
    def number(value):
        return type(value) in (int, float) and math.isfinite(value)
    def identity(value):
        parsed = uuid.UUID(value)
        require(parsed.int != 0, "Empty Ghost identity")
        return parsed
    require(cp["ActivityKind"] == "ghost" and type(cp["GhostCheckpointVersion"]) is int and cp["GhostCheckpointVersion"] == 1,
            "Unsupported Ghost checkpoint")
    origin = cp.get("Origin")
    require(origin is None or (type(origin) is dict and set(origin) == {"Version", "Kind"}
            and type(origin.get("Version")) is int and origin["Version"] == 1
            and type(origin.get("Kind")) is int and origin["Kind"] in (1, 2, 3)), "Unsupported Ghost creation origin")
    route, view, profile = cp["Route"], cp["Snapshot"], cp["Profile"]
    key = lambda k: (identity(k["Id"]), identity(k["Version"]))
    require(identity(cp["Id"]) == identity(view["Id"]) and key(route["Key"]) == key(view["Route"]), "Ghost snapshot identity mismatch")
    identity(route["SourceRunId"])
    require(type(cp["Mode"]) is int and cp["Mode"] in (0, 1) and cp["Mode"] == view["Mode"], "Invalid Ghost mode")
    require(type(cp["SaveRevision"]) is int and cp["SaveRevision"] > 0, "Invalid Ghost revision")
    require(utc_ticks(cp["StartedLocal"]) == utc_ticks(cp["StartedUtc"]) and cp["TimeZoneId"], "Ghost frozen civil start mismatch")
    require(view["FrozenYear"] == datetime.datetime.fromisoformat(cp["StartedLocal"]).year, "Frozen year mismatch")
    vertices = route["Geometry"]
    require(len(vertices) >= 2, "Missing ordered route geometry")
    previous = -1
    for index, vertex in enumerate(vertices):
        require(number(vertex["Latitude"]) and -90 <= vertex["Latitude"] <= 90 and number(vertex["Longitude"]) and -180 <= vertex["Longitude"] <= 180,
                "Invalid frozen geometry")
        require(number(vertex["Metres"]) and (vertex["Metres"] == 0 if index == 0 else vertex["Metres"] > previous), "Invalid route chainage")
        previous = vertex["Metres"]
    length = route["LengthMetres"]
    require(number(length) and length == previous and length > 0, "Frozen route length mismatch")
    require(profile["Version"] == "ghost-service-pilot-v1" and profile["Matching"]["RuleVersion"] == route["RuleVersion"], "Unsupported frozen Ghost policy")
    matching = profile["Matching"]
    require(all(number(matching[name]) and matching[name] > 0 for name in ["CorridorMetres","GateMetres","MaximumAccuracyMetres","MaximumSpeedMetresPerSecond","MaximumStepMetres","ProjectionExtentMetres"])
            and duration_ticks(matching["MaximumGap"]) > 0 and matching["MaximumAccuracyMetres"] <= min(matching["CorridorMetres"],matching["GateMetres"]), "Invalid frozen matcher policy")
    require(duration_ticks(profile["MaximumStartAge"]) > 0 and duration_ticks(profile["MaximumInterpolationGap"]) > 0, "Invalid support policy")
    clock = view["Clock"]
    require(type(clock["State"]) is int and clock["State"] in range(4), "Invalid race state")
    race, active = duration_ticks(clock["RaceElapsed"]), duration_ticks(clock["TrainingActive"])
    start, observed = cp["StartedBootMilliseconds"], cp["ObservedBootMilliseconds"]
    require(type(start) is int and type(observed) is int and 0 <= start <= observed and 0 <= active <= race <= (observed-start)*10000,
            "Unsupported race clock domain")
    end, through, paused = start*10000 + race, start*10000, 0
    intervals = []
    for pause in cp["Pauses"]:
        a = duration_ticks(pause["StartedAt"])
        b = None if pause["EndedAt"] is None else duration_ticks(pause["EndedAt"])
        require(start*10000 <= a <= observed*10000 and (b is None or a <= b <= observed*10000), "Invalid boot pause interval")
        intervals.append((a, b))
    for a, b in sorted(intervals, key=lambda item:item[0]):
        a, b = max(a, through), min(end, b if b is not None else end)
        if b > a:
            paused += b-a
            through = b
    require(active == race-paused and type(clock["TrainingPaused"]) is bool and clock["TrainingPaused"] == any(a <= end and (b is None or b > end) for a,b in intervals),
            "Training clock disagrees with recorded pause union")
    expected = ("not-terminal", None) if clock["State"] == 0 else (("zero-elapsed-incomplete", "explicit-end") if clock["State"] == 2 and race == 0 else
               ("attempt", {1:"matched-finish",2:"explicit-end",3:"interrupted"}[clock["State"]]))
    require((cp["ArchiveDisposition"],cp["TerminalKind"]) == expected, "Ghost disposition mismatch")
    match = view["Match"]
    require(key(match["Route"]) == key(route["Key"]) and match["RuleVersion"] == route["RuleVersion"], "Matcher identity mismatch")
    if clock["State"] == 1:
        require(race > 0 and match["State"] == 3 and duration_ticks(match["FinishedAt"]) == race, "Finished requires actual matched finish")
    def progress(points, elapsed):
        previous = -1
        require(bool(points), "Missing frozen progress")
        for point in points:
            at = duration_ticks(point["Elapsed"])
            require(previous < at <= elapsed and number(point["Metres"]) and 0 <= point["Metres"] <= length and type(point["Known"]) is bool,
                    "Invalid frozen progress order/support")
            previous = at
    progress(cp["Progress"], race)
    reference = cp["Reference"]
    if reference is None:
        require(view["ReferenceId"] is None and view["ReferenceElapsed"] is None, "Invented reference")
    else:
        identity(reference["SourceRunId"])
        elapsed = duration_ticks(reference["Elapsed"])
        require(identity(reference["Id"]) == identity(view["ReferenceId"]) and key(reference["Route"]) == key(route["Key"])
                and elapsed > 0 and elapsed == duration_ticks(view["ReferenceElapsed"]) and reference["RuleVersion"] == route["RuleVersion"]
                and type(reference["Quality"]) is int and reference["Quality"] == 0 and reference["TimeZoneId"], "Frozen reference mismatch")
        utc_ticks(reference["StartedLocal"])
        require(reference["SavedLocalYear"] == datetime.datetime.fromisoformat(reference["StartedLocal"]).year, "Reference civil year mismatch")
        progress(reference["Points"], elapsed)
        first,last = reference["Points"][0],reference["Points"][-1]
        require(first["Known"] and first["Metres"] == 0 and duration_ticks(first["Elapsed"]) == 0 and last["Known"]
                and last["Metres"] == length and duration_ticks(last["Elapsed"]) == elapsed, "Reference lacks recorded complete endpoints")
        # Unknown internal comparison intervals remain unknown; no blanket all-Known gate.
    measured = cp.get("MeasuredTrainingDistance")
    if measured is not None:
        require(measured["Policy"] == profile["TrainingDistance"] and number(measured["TotalMeasuredMetres"])
                and number(measured["MainMeasuredMetres"]) and 0 <= measured["MainMeasuredMetres"] <= measured["TotalMeasuredMetres"], "Frozen measured distance mismatch")
        require(type(measured["MainHasGaps"]) is bool, "Missing measured-distance coverage")
        for split in measured["Splits"]:
            require(number(split["MeasuredMetres"]) and split["MeasuredMetres"] >= 0 and type(split["HasGaps"]) is bool, "Invalid stored distance split")
            duration_ticks(split["ActiveTime"])
    terminal = cp.get("TerminalContext")
    if terminal is not None:
        require(clock["State"] != 0 and type(terminal["Version"]) is int and terminal["Version"] == 1
                and terminal["Provenance"] == "terminal-observed-zone-rules-v1" and terminal["ObservedBootMilliseconds"] == observed
                and terminal["ObservedZoneId"] and utc_ticks(terminal["StartDerivedEffectiveUtc"]) == utc_ticks(cp["StartedUtc"])+race,
                "Terminal provenance mismatch")
        for field in ["ObservedUtcOffsetSeconds","EffectiveUtcOffsetSeconds"]:
            require(type(terminal[field]) is int and -50400 <= terminal[field] <= 50400 and terminal[field] % 60 == 0, "Invalid frozen terminal offset")
    for raw in cp["Raw"]:
        at = raw["Elapsed"]
        duration_ticks(at[1:] if at.startswith("-") else at)
        utc_ticks(raw["Utc"])
        require(number(raw["Latitude"]) and -90 <= raw["Latitude"] <= 90 and number(raw["Longitude"]) and -180 <= raw["Longitude"] <= 180
                and type(raw["GapBefore"]) is bool, "Malformed original Ghost callback")
    return race, active


def read_ghost_export(document):
    units = dict(UNITS, ghostBootMonotonic="milliseconds",pauseBootMonotonic="TimeSpan-constant-format",raceProgressElapsed="TimeSpan-constant-format")
    if document.get("activityKind") != "ghost" or document.get("units") != units:
        raise ValueError("Unsupported Ghost identity/units")
    run = document["run"];cp = run["checkpoint"]
    race,active = ghost_checkpoint(cp)
    for field in ["startedUtc","lastObservedUtc"]:
        if datetime.datetime.fromisoformat(run[field].replace("Z","+00:00")).utcoffset() != datetime.timedelta(0):
            raise ValueError("Ghost UTC receipt requires UTC offset")
        utc_ticks(run[field])
    if uuid.UUID(run["id"]) != uuid.UUID(cp["Id"]) or utc_ticks(run["startedUtc"]) != utc_ticks(cp["StartedUtc"]) or run["originalPlan"] != []:
        raise ValueError("Ghost row identity/plan mismatch")
    if type(run["observedElapsedMilliseconds"]) is not int or run["observedElapsedMilliseconds"] != cp["ObservedBootMilliseconds"]-cp["StartedBootMilliseconds"]:
        raise ValueError("Ghost row observation clock mismatch")
    marker = run["localSavedPartUtc"]
    state = cp["Snapshot"]["Clock"]["State"]
    if run["status"] == "interrupted":
        if state not in (0,3) or marker is None:
            raise ValueError("Interrupted prefix lacks explicit saved-part confirmation")
        utc_ticks(marker)
    elif run["status"] not in ("finished","incomplete") or state != {"finished":1,"incomplete":2}[run["status"]] or marker is not None or race <= 0:
        raise ValueError("Unconfirmed/invalid Ghost terminal source")
    if cp.get("TerminalContext") is not None and utc_ticks(run["lastObservedUtc"]) != utc_ticks(cp["TerminalContext"]["ObservedUtc"]):
        raise ValueError("Terminal receipt differs from row")
    validate_points(2,run)
    for key, order in [("events","sequence"),("heartRate","sequence"),("locations","snapshotOrder") ]:
        sequences=[row[order] for row in run[key]]
        if any(type(n) is not int or n < 0 for n in sequences) or any(a>=b for a,b in zip(sequences,sequences[1:])):
            raise ValueError("Original receipt/event order changed")
    for event in run["events"]:
        prior=event["checkpoint"];ghost_checkpoint(prior)
        if datetime.datetime.fromisoformat(event["observedUtc"].replace("Z","+00:00")).utcoffset() != datetime.timedelta(0):
            raise ValueError("Event receipt requires UTC offset")
        if prior.get("TerminalContext") is not None and utc_ticks(prior["TerminalContext"]["ObservedUtc"]) != utc_ticks(event["observedUtc"]):
            raise ValueError("Event terminal receipt mismatch")
        if uuid.UUID(prior["Id"]) != uuid.UUID(cp["Id"]) or prior["StartedBootMilliseconds"] != cp["StartedBootMilliseconds"] or prior["ObservedBootMilliseconds"]-prior["StartedBootMilliseconds"] != event["observedElapsedMilliseconds"] or prior["SaveRevision"] > cp["SaveRevision"]:
            raise ValueError("Historical event identity/clock mismatch")
        if any(prior[name] != cp[name] for name in ["Route","Reference","Profile","StartedLocal","TimeZoneId","Mode","HealthConnectConsentRevision"]) or (("Origin" in prior) != ("Origin" in cp)) or prior.get("Origin") != cp.get("Origin"):
            raise ValueError("Historical frozen reference/policy changed")
    return {"schemaVersion":3,"activityKind":"ghost","id":run["id"],"status":run["status"],"raceElapsedTicks":race,
            "trainingActiveTicks":active,"localSavedPartUtc":marker,"checkpoint":cp,"frozenReference":cp["Reference"],
            "measuredTrainingDistance":cp.get("MeasuredTrainingDistance"),"terminalContext":cp.get("TerminalContext"),
            "heartRateAggregates":None,"locations":run["locations"],"heartRate":run["heartRate"],"events":run["events"]}

def read_export(path):
    def reject_constant(value):
        raise ValueError("Nonfinite JSON number: " + value)

    document = json.loads(Path(path).read_text(encoding="utf-8"), parse_constant=reject_constant)
    if type(document.get("schemaVersion")) is int and document["schemaVersion"] == 3:
        return read_ghost_export(document)
    if type(document.get("schemaVersion")) is not int or document["schemaVersion"] not in (1, 2) or document.get("units") != UNITS:
        raise ValueError("Unsupported export version or units")
    run = document["run"]
    uuid.UUID(run["id"])
    for key in ("startedUtc", "lastObservedUtc"):
        value = datetime.datetime.fromisoformat(run[key].replace("Z", "+00:00"))
        if value.utcoffset() != datetime.timedelta(0):
            raise ValueError("Run UTC timestamp has a non-UTC offset")
    if run["status"] not in ("recording", "finished", "interrupted") or run["observedElapsedMilliseconds"] < 0:
        raise ValueError("Invalid observed run state")
    validate_points(document["schemaVersion"], run)
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
        if split_policy(event_state["snapshot"] or {}) != split_policy(decoded["snapshot"] or {}):
            raise ValueError("Historical frozen split policy changed")
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
