using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.History;

internal static class StoredHistoryChecks
{
    internal static void Run()
    {
        // Arrange: frozen v1 is deliberately different from today's Core and Settings.
        var id = Guid.NewGuid();
        if (StoredRunProjection.TargetSummary(new(CoreTargetMode.Pace, 320), false) != "Work target: 5:20 min/km · intended, not measured"
            || StoredRunProjection.TargetSummary(new(CoreTargetMode.HeartRateZone, 2), true) != "Continuous target: Zone 2 · intended, not measured"
            || StoredRunProjection.TargetSummary(new(CoreTargetMode.Pace, 320, 420), false) != "Work target: 5:20 min/km · Recovery: 7:00 min/km · intended, not measured"
            || StoredRunProjection.TargetSummary(new(), false) != "Target not set"
            || StoredRunProjection.TargetSummary(null, false) != "Unavailable")
        {
            throw new InvalidOperationException("Stored target summaries must distinguish configured recovery, continuous intent and unavailable legacy facts.");
        }

        RunNotificationEntry.Request(id, id);
        if (!RunNotificationEntry.Consume(id) || RunNotificationEntry.Consume(id))
            throw new InvalidOperationException("Notification entry must be consumed once for the same recording.");
        RunNotificationEntry.Request(id, id);
        if (RunNotificationEntry.Consume(Guid.NewGuid()))
            throw new InvalidOperationException("A queued old notification cannot redirect a replacement recording.");
        RunNotificationEntry.Request(Guid.NewGuid(), id);
        RunNotificationEntry.Request(id, null);
        if (RunNotificationEntry.Consume(id))
            throw new InvalidOperationException("Stale or absent recording entry must not be retained.");
        var started = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var legacy = TimedSession.Intervals(5, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(2),
            8, 2, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));
        var plan = legacy.Plan.ToArray();
        var main = new MeasuredStatistics(TimeSpan.FromMinutes(23), 4000, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(3))
        { AverageHeartRate = 118, KnownHeartRateActiveTime = TimeSpan.FromMinutes(10) };
        var total = new MeasuredStatistics(TimeSpan.FromMinutes(38), 6000, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(8));
        var stats = new RunStatisticsState(1, DistancePolicy.Pilot, TimeSpan.FromMinutes(40), total, main, [main], null, false);
        PhaseBoundaryFact Boundary(int previous, int? next, long elapsed)
        {
            return new(previous, next, plan[previous].Kind,
            next is { } index ? plan[index].Kind : null, elapsed, elapsed + 10, started.AddMilliseconds(elapsed + 10), started.AddMilliseconds(elapsed), false);
        }

        var boundaries = new[] { Boundary(0, 1, 600000), Boundary(9, 10, 2070000), Boundary(10, null, 2370000) };
        var checkpoint = JsonSerializer.SerializeToElement(new
        {
            SchemaVersion = 1,
            Snapshot = new { Statistics = stats },
            Plan = plan,
            Completed = plan.Select(phase => new CompletedPhase(phase, phase.Duration)),
            TotalActiveTime = TimeSpan.FromMinutes(38),
            MainActiveTime = TimeSpan.FromMinutes(23),
            PhaseBoundaries = boundaries
        });
        var template = JsonSerializer.SerializeToElement(new CoreTemplate("C1", "LOOP Core", "intervals", 1, null));
        var setup = JsonSerializer.SerializeToElement(new HeartRateSetup(new(1, null, HeartRatePolicy.Pilot, [new(1, 100, 119)]), null));
        var snapshot = new ExportJournalSnapshot(id, started.ToString("O"), started.AddMinutes(40).ToString("O"), 2400000,
            "finished", JsonSerializer.SerializeToElement(plan), template, setup, checkpoint, "disabled_synthetic", [], [], []);

        StoredMeasurementChecks.Run(snapshot);

        // Act
        var result = StoredRunProjection.Detail(snapshot, null);
        var legacyUnknown = StoredRunProjection.Detail(snapshot with { Checkpoint = JsonSerializer.SerializeToElement(new { legacy = true }), Template = null, HeartRateSetup = null }, null);
        var noFacts = StoredRunProjection.Detail(snapshot with
        {
            Checkpoint = JsonSerializer.SerializeToElement(new
            {
                SchemaVersion = 1,
                Snapshot = new { Statistics = stats },
                Plan = plan,
                Completed = plan.Select(phase => new CompletedPhase(phase, phase.Duration))
            })
        }, null);
        var unacknowledged = StoredRunProjection.Detail(snapshot with { ExportStatus = "exported" }, null);
        var interruptedRow = new StoredRunRow(id, started.ToString("O"), "interrupted", template.GetRawText(), checkpoint.GetRawText(), null, null);
        var mixedRows = new[] { interruptedRow, interruptedRow with { Id = Guid.NewGuid(), Checkpoint = /*lang=json,strict*/ "{\"SchemaVersion\":2}" },
            interruptedRow with { Id = Guid.NewGuid(), Checkpoint = "[]" }, interruptedRow with { Id = Guid.NewGuid(), Template = "{}" } };
        var originalRows = JsonSerializer.Serialize(mixedRows);
        var readableRows = new List<InterruptedRun>();
        var unreadableRows = 0;
        foreach (var row in mixedRows)
        {
            try { readableRows.Add(InterruptedRunProjection.Read(row, started.ToString("O"), null)); }
            catch (Exception issue) when (InterruptedRunProjection.IsUnreadableRow(issue)) { unreadableRows++; }
        }
        if (readableRows.Count != 1 || readableRows[0].Summary.Id != id || unreadableRows != 3
            || JsonSerializer.Serialize(mixedRows) != originalRows)
        {
            throw new InvalidOperationException("An unreadable interrupted row must not hide valid archive entries or mutate stored facts.");
        }

        var prefix = InterruptedRunProjection.Read(interruptedRow, started.AddMinutes(40).ToString("O"), null);
        var savedPrefix = InterruptedRunProjection.Read(interruptedRow, started.AddMinutes(40).ToString("O"), started.AddDays(1).ToString("O"));
        var unknownPrefix = InterruptedRunProjection.Read(interruptedRow with { Checkpoint = /*lang=json,strict*/ "{\"legacy\":true}", Template = null }, "unknown", null);
        if (prefix.SavedLocally is not null || savedPrefix.SavedLocally != started.AddDays(1)
            || prefix.Summary.TotalActive != result.Summary.TotalActive || JsonSerializer.Serialize(savedPrefix.Summary) != JsonSerializer.Serialize(prefix.Summary)
            || prefix.Summary.ExportLabel != "Not exported · interrupted" || unknownPrefix.Summary.Total is not null
            || unknownPrefix.LastCommittedObservation is not null)
        {
            throw new InvalidOperationException("Recovery acknowledgement must not rewrite committed facts or manufacture an end/export.");
        }

        try { _ = InterruptedRunProjection.Read(interruptedRow, "unknown", "bad-marker"); throw new InvalidOperationException("Malformed marker accepted."); }
        catch (RunSaveReadException) { }
        InterruptedRunProjection.EnsureNoActiveOwner(id, Guid.NewGuid());
        try { InterruptedRunProjection.EnsureNoActiveOwner(id, id); throw new InvalidOperationException("Active owner accepted for recovery."); }
        catch (RunSaveReadException) { }
        if (LoopCx.Features.HealthConnectExport.HCWritePlanner.Create(snapshot with { Status = "interrupted", ExportStatus = null }, false).Plan is not null)
            throw new InvalidOperationException("A saved partial must remain ineligible for normal provider export.");
        var unknownOffset = StoredRunProjection.Detail(snapshot with
        {
            Checkpoint = JsonSerializer.SerializeToElement(new { StartContext = new { TimeZoneId = "Unknown" } })
        }, null);

        // Assert
        if (result.Summary.TotalActive != TimeSpan.FromMinutes(38) || result.MainActive != TimeSpan.FromMinutes(23)
            || result.MainElapsed != TimeSpan.FromSeconds(1470) || result.TotalElapsed != TimeSpan.FromSeconds(2370)
            || result.Main?.AverageHeartRate != 118 || result.Main.AveragePaceSecondsPerKilometre is not null
            || result.Warmup != TimeSpan.FromMinutes(10) || result.Cooldown != TimeSpan.FromMinutes(5)
            || result.Zones?.Single().MaximumBpm != 119 || !result.Summary.Source.Contains("revision 1", StringComparison.Ordinal)
            || result.Summary.ExportLabel != "Not exported · debug/synthetic" || unacknowledged.Summary.ExportLabel == "Exported"
            || legacyUnknown.Summary.Total is not null || legacyUnknown.TotalElapsed is not null || legacyUnknown.Targets is not null
            || legacyUnknown.Zones is not null || noFacts.MainElapsed is not null || noFacts.TotalElapsed is not null
            || unknownOffset.Summary.StartOffset is not null || legacyUnknown.Warmup is not null || legacyUnknown.Cooldown is not null)
        {
            throw new InvalidOperationException("Stored detail must preserve frozen values and provenance; absent facts or ACK cannot be invented.");
        }

        try { _ = StoredRunProjection.Detail(snapshot with { Status = "interrupted" }, null); }
        catch (InvalidDataException)
        {
            Console.WriteLine("PASS: finished History preserves frozen plan/stats/zones, elapsed boundary evidence and legacy unknowns; interrupted/exported claims guarded.");
            return;
        }
        throw new InvalidOperationException("Interrupted recordings must not be relabelled finished.");
    }
}
