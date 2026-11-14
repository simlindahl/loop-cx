using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.HealthConnectExport;

internal static class HCWritePlanChecks
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run(string[] arguments)
    {
        var started = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);
        var runId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var plan = new[] { new TimedPhase(RunPhase.Warmup, TimeSpan.FromSeconds(10), 2), new TimedPhase(RunPhase.Work, TimeSpan.FromSeconds(20), 7), new TimedPhase(RunPhase.Cooldown, TimeSpan.FromSeconds(10), 2) };
        ExportJournalSnapshot Source(bool synthetic = false, bool context = true, bool end = true)
        {
            var boundaries = end ? [new PhaseBoundaryFact(2, null, RunPhase.Cooldown, null, 45000, 45010, started.AddSeconds(45.01), started.AddSeconds(45), synthetic)] : Array.Empty<PhaseBoundaryFact>();
            var checkpoint = JsonSerializer.SerializeToElement(new
            {
                SchemaVersion = 1,
                Snapshot = new { Phase = "Finished", Statistics = new { Version = 1, HasSyntheticTime = synthetic, Total = new { MeasuredMetres = 123.5, KnownGpsActiveTime = TimeSpan.FromSeconds(35), UnknownGpsActiveTime = TimeSpan.FromSeconds(5) } } },
                Plan = plan,
                Completed = plan.Select(phase => new CompletedPhase(phase, phase.Duration)).ToArray(),
                TotalActiveTime = TimeSpan.FromSeconds(40),
                PhaseBoundaries = boundaries,
                StartContext = context ? new RunStartContext("Europe/Stockholm", 3600, 1000) : null
            });
            var eventCheckpoint = JsonSerializer.SerializeToElement(new { Snapshot = new { Paused = true } });
            var events = new[] { new ExportEvent(1, "pause", started.AddSeconds(12).ToString("O", CultureInfo.InvariantCulture), 12000, eventCheckpoint),
                new ExportEvent(2, "resume", started.AddSeconds(17).ToString("O", CultureInfo.InvariantCulture), 17000, eventCheckpoint) };
            return new(runId, started.ToString("O", CultureInfo.InvariantCulture), started.AddSeconds(45.2).ToString("O", CultureInfo.InvariantCulture),
                45200, "finished", JsonSerializer.SerializeToElement(plan), null, null, checkpoint, "pending", events, [],
                [new(1, started.AddSeconds(2).ToString("O", CultureInfo.InvariantCulture), 30000000, 100, "valid"),
                 new(2, started.AddSeconds(20).ToString("O", CultureInfo.InvariantCulture), 210000000, null, "contact-or-payload-invalid"),
                 new(3, started.AddSeconds(35).ToString("O", CultureInfo.InvariantCulture), 360000000, 140, "valid")]);
        }

        var source = Source();
        var result = HCWritePlanner.Create(source, false);
        Require(result.State == "ready" && result.Plan is not null, "A real-like finalized snapshot produces a plan without provider calls.");
        var frozen = result.Plan!;
        var session = frozen.Records.Single(record => record.Kind == HCRecordKind.RunningSession);
        Require(session.StartUtc == started && session.EndUtc == started.AddSeconds(45) && session.Pauses.Single() == new HCPause(started.AddSeconds(12), started.AddSeconds(17)),
            "Whole warm-up/main/cool-down elapsed window retains explicit pause rather than shrinking to active time.");
        Require(frozen.Records.Single(record => record.Kind == HCRecordKind.Distance).AcceptedMetres == 123.5
            && frozen.Records.Single(record => record.Kind == HCRecordKind.HeartRate).HeartRateSamples.Select(sample => sample.Bpm).SequenceEqual([100, 140]),
            "Only accepted partial distance and valid raw receipts are mapped without extrapolation or missing HR zeros.");
        Require(HCWritePlanner.Create(source, false).Plan!.PayloadHash == frozen.PayloadHash && frozen.HasValidHash(),
            "The same detached input has stable record IDs/version and frozen payload hash.");
        Require(HCWritePlanner.Create(Source(context: false), false).State == "ready", "Missing local zone alone does not invalidate sound UTC.");
        Require(HCWritePlanner.Create(Source(end: false), false).State == "action_required"
            && HCWritePlanner.Create(source with { Status = "interrupted" }, false).Plan is null
            && HCWritePlanner.Create(Source(synthetic: true), false).Plan is null
            && HCWritePlanner.Create(source, true).Plan is null,
            "Unknown required finish, interrupted, synthetic and debug sources cannot construct a ready plan.");
        var noHr = HCWritePlanner.Create(source with { HeartRate = [] }, false).Plan!;
        Require(noHr.Records.All(record => record.Kind != HCRecordKind.HeartRate), "Absent HR omits the child record; it never fabricates samples.");
        var noMeasurements = HCWritePlanner.Create(source with
        {
            HeartRate = [],
            Checkpoint = JsonSerializer.SerializeToElement(new
            {
                SchemaVersion = 1,
                Snapshot = new { Phase = "Finished", Statistics = (RunStatisticsState?)null },
                Plan = plan,
                Completed = plan.Select(phase => new CompletedPhase(phase, phase.Duration)).ToArray(),
                TotalActiveTime = TimeSpan.FromSeconds(40),
                PhaseBoundaries = source.Checkpoint.GetProperty("PhaseBoundaries"),
                StartContext = (RunStartContext?)null
            })
        }, false);
        Require(noMeasurements.Plan is not null && noMeasurements.Plan.Records.Count == 1,
            "A trustworthy UTC session remains valid with absent optional HR, distance and local zone.");
        var duplicate = source.HeartRate.Append(source.HeartRate[0] with { Sequence = 4 }).ToArray();
        Require(HCWritePlanner.Create(source with { HeartRate = duplicate }, false).Plan!.Records.Single(record => record.Kind == HCRecordKind.HeartRate).HeartRateSamples.Count == 2,
            "Identical same-instant readings are deduplicated deterministically before a platform could choose.");
        Require(HCWritePlanner.Create(source with { HeartRate = [.. duplicate.Select(row => row.Sequence == 4 ? row with { Bpm = 101 } : row)] }, false).State == "action_required",
            "Conflicting BPM at one receipt instant is not silently chosen.");

        JsonElement ChangedCheckpoint(Action<System.Text.Json.Nodes.JsonObject> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(source.Checkpoint.GetRawText())!.AsObject();
            change(node);

            return JsonSerializer.SerializeToElement(node);
        }
        var nullBoundary = HCWritePlanner.Create(source with
        {
            Checkpoint = ChangedCheckpoint(node => node["PhaseBoundaries"]!.AsArray().Add(null))
        }, false);
        Require(nullBoundary.State == "action_required" && nullBoundary.Plan is null,
            "A null stored boundary must fail closed without leaking an exception or a ready plan.");
        var noPauseCheckpoint = ChangedCheckpoint(node =>
        {
            node["TotalActiveTime"] = "00:00:45";
            node["Snapshot"]!["Statistics"]!["Total"]!["UnknownGpsActiveTime"] = "00:00:10";
        });
        var reversedReceipts = HCWritePlanner.Create(source with
        {
            Checkpoint = noPauseCheckpoint,
            Events = [source.Events[0] with { ObservedElapsedMilliseconds = 5000 }, source.Events[1] with { ObservedElapsedMilliseconds = 3000 }]
        }, false);
        Require(reversedReceipts.State == "action_required" && reversedReceipts.Plan is null,
            "Backward pause/resume receipts must reject even when silently dropping the interval would conserve active time.");
        Require(HCWritePlanner.Create(source with
        {
            Checkpoint = noPauseCheckpoint,
            Events = [source.Events[0] with { ObservedElapsedMilliseconds = 5000 }, source.Events[1] with { ObservedElapsedMilliseconds = 5000 }]
        }, false).State == "ready", "Equal receipt instants retain a valid zero-duration manual pause.");
        Require(HCWritePlanner.Create(source with
        {
            Checkpoint = ChangedCheckpoint(node =>
        {
            node["Snapshot"]!["Statistics"]!["Total"]!["KnownGpsActiveTime"] = "00:00:00";
            node["Snapshot"]!["Statistics"]!["Total"]!["UnknownGpsActiveTime"] = "00:00:40";
        })
        }, false).State == "action_required", "Positive distance without supported GPS time is contradictory, not an ordinary omitted distance.");
        Require(HCWritePlanner.Create(source with
        {
            Checkpoint = ChangedCheckpoint(node =>
            node["Snapshot"]!["Motion"] = new System.Text.Json.Nodes.JsonObject { ["StartedMonotonicTime"] = "00:00:02" })
        }, false).State == "action_required",
            "Two conflicting stored start-monotonic anchors cannot accidentally conserve the wrong pause intervals.");
        Require(HCWritePlanner.Create(source with { HeartRate = [source.HeartRate[0] with { BootReceiptMonotonicTicks = 900000000 }] }, false).State == "action_required",
            "A UTC-inside but boot-outside HR receipt cannot be silently accepted.");
        Require(HCWritePlanner.Create(source with { HeartRate = [source.HeartRate[0] with { BootReceiptMonotonicTicks = 120000000 }] }, false).State == "action_required",
            "Contradictory HR clocks inside the same window require action rather than a fabricated corrected timestamp.");

        // New synthetic host fixture with the ACTUAL journal field shape: boot start 1 s, onset 1 s,
        // confirming sample 11.5 s, detection 13 s (elapsed 12 s). Exclude elapsed 0..10, then pause 12..20.
        var autoCheckpoint = JsonSerializer.SerializeToElement(new
        {
            SchemaVersion = 1,
            Snapshot = new { Phase = "Finished", Motion = new { StartedMonotonicTime = TimeSpan.FromSeconds(1) }, Statistics = new { Version = 1, HasSyntheticTime = false, Total = new { MeasuredMetres = 123.5, KnownGpsActiveTime = TimeSpan.FromSeconds(35), UnknownGpsActiveTime = TimeSpan.FromSeconds(5) } } },
            Plan = new[] { new TimedPhase(RunPhase.Work, TimeSpan.FromSeconds(40), 4) },
            Completed = new[] { new CompletedPhase(new(RunPhase.Work, TimeSpan.FromSeconds(40), 4), TimeSpan.FromSeconds(40)) },
            TotalActiveTime = TimeSpan.FromSeconds(40),
            StartContext = new RunStartContext("Europe/Stockholm", 3600, 1000),
            PhaseBoundaries = new[] { new PhaseBoundaryFact(0, null, RunPhase.Work, null, 58000, 58010, started.AddSeconds(58.01), started.AddSeconds(58), false) }
        });
        var autoEvents = new[]
        {
            new ExportEvent(1, "auto-pause", started.AddSeconds(12).ToString("O", CultureInfo.InvariantCulture), 12000,
                JsonSerializer.SerializeToElement(new { AutomaticEvent = new AutomaticMotionEvent("auto-pause", 0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(11.5), TimeSpan.FromSeconds(13)) })),
            new ExportEvent(2, "auto-resume", started.AddSeconds(20).ToString("O", CultureInfo.InvariantCulture), 20000,
                JsonSerializer.SerializeToElement(new { AutomaticEvent = new AutomaticMotionEvent("auto-resume", 0, null, null, TimeSpan.FromSeconds(20.5), TimeSpan.FromSeconds(21)) }))
        };
        var autoSource = source with { Checkpoint = autoCheckpoint, Events = autoEvents, ObservedElapsedMilliseconds = 58200, LastObservedUtc = started.AddSeconds(58.2).ToString("O", CultureInfo.InvariantCulture) };
        var autoPlan = HCWritePlanner.Create(autoSource, false);
        Require(autoPlan.Plan is not null && autoPlan.Plan.Records[0].Pauses.SequenceEqual([new HCPause(started, started.AddSeconds(10)), new HCPause(started.AddSeconds(12), started.AddSeconds(20))]),
            "Actual auto-event shape preserves exact ten-second exclusion and the still-active 10..12-second receipt tail, with later pause separate.");

        foreach (var seconds in new[] { 9, 11 })
        {
            var activeSeconds = 50 - seconds;
            var alteredCheckpoint = System.Text.Json.Nodes.JsonNode.Parse(autoCheckpoint.GetRawText())!.AsObject();
            alteredCheckpoint["TotalActiveTime"] = TimeSpan.FromSeconds(activeSeconds).ToString("c", CultureInfo.InvariantCulture);
            alteredCheckpoint["Snapshot"]!["Statistics"]!["Total"]!["UnknownGpsActiveTime"] = TimeSpan.FromSeconds(activeSeconds - 35).ToString("c", CultureInfo.InvariantCulture);
            var alteredAuto = System.Text.Json.Nodes.JsonNode.Parse(autoEvents[0].Checkpoint.GetRawText())!.AsObject();
            alteredAuto["AutomaticEvent"]!["ExcludedThroughMonotonicTime"] = TimeSpan.FromSeconds(1 + seconds).ToString("c", CultureInfo.InvariantCulture);
            var altered = autoSource with { Checkpoint = JsonSerializer.SerializeToElement(alteredCheckpoint), Events = [autoEvents[0] with { Checkpoint = JsonSerializer.SerializeToElement(alteredAuto) }, autoEvents[1]] };
            var rejectedWindow = HCWritePlanner.Create(altered, false);
            Require(rejectedWindow.State == "action_required" && rejectedWindow.Reason!.Contains("ten-second", StringComparison.Ordinal),
                "A corrupted 9- or 11-second exclusion must reject even when adjusted active totals otherwise conserve the elapsed window.");
        }
        Require(HCWritePlanner.Create(source with { Checkpoint = ChangedCheckpoint(node => node["SchemaVersion"] = 2) }, false).State == "action_required",
            "Unknown future checkpoint versions cannot be silently interpreted as v1 with a similar shape.");
        Require(HCWritePlanner.Create(source with { Checkpoint = ChangedCheckpoint(node => node["Snapshot"]!["Statistics"]!["Version"] = 2) }, false).State == "action_required",
            "Unknown statistics versions cannot be silently read as accepted current measurement facts.");

        var remote = new Dictionary<string, HCPlannedRecord>(StringComparer.Ordinal);
        var calls = 0;
        var first = HCFakeReplay.Run(frozen, null, false, () => true, record =>
        {
            remote[record.ClientRecordId] = record;
            calls++;
            return calls == 2 ? throw new IOException("Simulated remote acceptance before local ACK/process loss.") : HCFakeWriteResult.Accepted;
        });
        Require(first.State == "pending" && remote.Count == 2, "Partial remote acceptance is not a local completion ACK.");
        var reopened = JsonSerializer.Deserialize<HCFakeAttempt>(JsonSerializer.Serialize(first))!;
        var retry = HCFakeReplay.Run(reopened.FrozenPlan, reopened, false, () => true, record =>
        {
            remote[record.ClientRecordId] = record;
            return HCFakeWriteResult.Accepted;
        });
        Require(retry.State == "simulated_complete" && remote.Count == frozen.Records.Count && retry.FrozenPlan.PayloadHash == frozen.PayloadHash,
            "Serialized crash/replay reuses frozen IDs/payload without remote duplicates; fake success is never production exported.");
        var permissionCalls = 0;
        var revoked = HCFakeReplay.Run(frozen, null, false, () => permissionCalls == 0, record => { permissionCalls++; return HCFakeWriteResult.Accepted; });
        Require(revoked.State == "action_required" && permissionCalls == 1, "Revocation between records leaves incomplete delivery action-required.");
        var blockedCalls = 0;
        _ = HCFakeReplay.Run(frozen, null, true, () => true, record => { blockedCalls++; return HCFakeWriteResult.Accepted; });
        _ = HCFakeReplay.Run(frozen with { Source = frozen.Source with { Synthetic = true } }, null, false, () => true, record => { blockedCalls++; return HCFakeWriteResult.Accepted; });
        _ = HCFakeReplay.Run(frozen with { PayloadHash = "tampered" }, null, false, () => true, record => { blockedCalls++; return HCFakeWriteResult.Accepted; });
        _ = HCFakeReplay.Run(frozen, retry with { FrozenPlan = frozen with { PayloadHash = "corrupt persisted attempt" } }, false, () => true,
            record => { blockedCalls++; return HCFakeWriteResult.Accepted; });
        Require(blockedCalls == 0, "The replay boundary independently rejects debug/synthetic/tampered plans before all writer calls.");
        if (arguments.Length == 2 && arguments[0] == "--hc-source-fixture")
        {
            var actual = JsonSerializer.Deserialize<ExportRunV1>(File.ReadAllText(arguments[1]), ExportRunV1.JsonOptions)
                ?? throw new InvalidDataException("Missing stored export fixture.");
            Require(HCWritePlanner.Create(actual.Run, false).Plan is null,
                "Actual stored debug/legacy exports remain blocked; fixtures are never relabelled as provider acceptance.");
            Console.WriteLine("PASS: read-only stored-source eligibility regression: " + Path.GetFileName(arguments[1]));
        }
        Console.WriteLine("PASS: detached HC plan, whole elapsed/pauses, partial measurements, stable IDs/hash and fake crash/permission replay without provider calls.");
    }
}
