using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.History;

internal static class StoredMeasurementChecks
{
    internal static void Run(ExportJournalSnapshot existing)
    {
        // Applied plan has an extension; completed work is shortened by skip. Elapsed includes actual pause.
        TimedPhase[] plan = [new(RunPhase.Warmup, TimeSpan.FromSeconds(60), 2),
            new(RunPhase.Work, TimeSpan.FromSeconds(180), 7), new(RunPhase.Recovery, TimeSpan.FromSeconds(60), 2),
            new(RunPhase.Cooldown, TimeSpan.FromSeconds(60), 2)];
        CompletedPhase[] completed = [new(plan[0], TimeSpan.FromSeconds(60)), new(plan[1], TimeSpan.FromSeconds(120)),
            new(plan[2], TimeSpan.FromSeconds(60)), new(plan[3], TimeSpan.FromSeconds(60))];
        var configuration = new HeartRateConfiguration(1, null, HeartRatePolicy.Pilot,
            [new(1, 100, 119), new(2, 120, 139), new(3, 140, 159), new(4, 160, 179), new(5, 180, 199)]);
        var main = Scope(180, 1250, 150, 100, 90, 10);
        var whole = Scope(300, 1600, 260, 220, 200, 20);
        var full = Scope(120, 1000, 120, 80, 70, 10);
        var partial = Scope(60, 250, 30, 20, 20, 0);
        PhaseBoundaryFact Boundary(int previous, int? next, long milliseconds)
        {
            return new(previous, next, plan[previous].Kind,
            next is { } index ? plan[index].Kind : null, milliseconds, milliseconds + 100, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, false);
        }

        var checkpoint = JsonSerializer.SerializeToNode(new
        {
            SchemaVersion = 1,
            Plan = plan,
            Completed = completed,
            MainActiveTime = TimeSpan.FromSeconds(180),
            TotalActiveTime = TimeSpan.FromSeconds(300),
            PhaseBoundaries = new[] { Boundary(0, 1, 60000), Boundary(1, 2, 210000), Boundary(2, 3, 270000), Boundary(3, null, 330000) },
            Snapshot = new
            {
                Statistics = new RunStatisticsState(1, DistancePolicy.Pilot, TimeSpan.FromSeconds(330), whole, main, [full, partial], partial, false),
                Distance = new DistanceState(DistancePolicy.Pilot, 1600, 1250, true, "finished",
                    [new(1, 1000, TimeSpan.FromSeconds(120), false), new(2, 250, TimeSpan.FromSeconds(60), true)])
            }
        })!.AsObject();
        var snapshot = existing with { HeartRateSetup = JsonSerializer.SerializeToElement(new HeartRateSetup(configuration, null)) };
        StoredRunDetail Read(JsonObject value)
        {
            return StoredRunProjection.Detail(snapshot with { Checkpoint = JsonSerializer.SerializeToElement(value) }, null);
        }

        var original = checkpoint.ToJsonString();
        var result = Read(checkpoint).Measurements;
        if (result.Splits is not { Count: 2 } splits || splits[0].FinalPartial || !splits[1].FinalPartial
            || splits[0].Pace != 120 || splits[1].Pace is not null || splits[1].HeartRate != 130
            || result.Phases![1].Active != TimeSpan.FromSeconds(120) || result.Phases[1].Elapsed != TimeSpan.FromSeconds(150)
            || result.Main.Known != TimeSpan.FromSeconds(100) || result.Main.Unknown != TimeSpan.FromSeconds(80)
            || result.Main.Outside != TimeSpan.FromSeconds(10) || result.Whole.Known != TimeSpan.FromSeconds(220)
            || result.Main.Zones[0].Active != TimeSpan.FromSeconds(90) || result.Main.Zones[1].Active != TimeSpan.Zero
            || checkpoint.ToJsonString() != original)
        {
            throw new InvalidOperationException("Stored split, phase and Main/Whole zone projections must retain frozen scopes without mutation or duplicate CurrentSplit.");
        }

        var noGps = new StoredSplit(1, true, TimeSpan.FromMinutes(1), 0, null, null, TimeSpan.Zero, TimeSpan.FromMinutes(1), null, null);
        if (noGps.HasMeasuredDistance || !(noGps with { GpsKnown = TimeSpan.FromSeconds(1) }).HasMeasuredDistance)
            throw new InvalidOperationException("No supported GPS is unavailable; supported stationary zero remains distinct.");

        var missing = checkpoint.DeepClone().AsObject();
        var missingMain = missing["Snapshot"]!["Statistics"]!["Main"]!.AsObject();
        _ = missingMain.Remove("KnownHeartRateActiveTime");
        _ = missingMain.Remove("HeartRateZones");
        _ = missing.Remove("PhaseBoundaries");
        var legacy = Read(missing).Measurements;
        if (legacy.Main.Known is not null || legacy.Main.Unknown is not null || legacy.Main.Outside is not null
            || legacy.Main.Zones.Any(zone => zone.Active is not null) || legacy.Phases!.Any(phase => phase.Elapsed is not null)
            || legacy.Phases![1].Active != TimeSpan.FromSeconds(120))
        {
            throw new InvalidOperationException("Missing legacy HR fields and boundaries are unavailable, not zero or planned elapsed.");
        }

        var missingUnknown = checkpoint.DeepClone().AsObject();
        _ = missingUnknown["Snapshot"]!["Statistics"]!["Main"]!.AsObject().Remove("UnknownHeartRateActiveTime");
        if (Read(missingUnknown).Measurements.Main.Unknown is not null)
            throw new InvalidOperationException("An absent legacy unknown-time field is not silently filled by subtraction.");

        var empty = checkpoint.DeepClone().AsObject();
        var zeroMain = empty["Snapshot"]!["Statistics"]!["Main"]!.AsObject();
        zeroMain["KnownHeartRateActiveTime"] = "00:00:00";
        zeroMain["HeartRateZones"] = new JsonArray();
        zeroMain["UnclassifiedHeartRateActiveTime"] = "00:00:00";
        if (Read(empty).Measurements.Main.Zones.Any(zone => zone.Active != TimeSpan.Zero))
            throw new InvalidOperationException("Explicit empty zone durations may represent zero only with valid conservation and frozen boundaries.");
        zeroMain["HeartRateZones"] = null;
        if (Read(empty).Measurements.Main.Zones.Any(zone => zone.Active is not null))
            throw new InvalidOperationException("Null zone durations are not an explicit empty measurement.");

        var mismatch = checkpoint.DeepClone().AsObject();
        mismatch["Snapshot"]!["Statistics"]!["CompletedSplits"]!.AsArray().RemoveAt(0);
        mismatch["Completed"]![1]!["Plan"]!["Duration"] = "00:09:00";
        if (Read(mismatch).Measurements is not { Splits: null } inconsistent || inconsistent.Phases![1].Active is not null)
            throw new InvalidOperationException("Mismatched split lists or phase instances cannot be aligned using guessed totals.");
        var wrongZone = checkpoint.DeepClone().AsObject();
        wrongZone["Snapshot"]!["Statistics"]!["Main"]!["UnclassifiedHeartRateActiveTime"] = "00:00:09";
        if (Read(wrongZone).Measurements.Main.Zones.Any(zone => zone.Active is not null))
            throw new InvalidOperationException("Contradictory zone conservation must not become apparently complete zone times.");

        Console.WriteLine("PASS: stored detail projections retain final splits, extended/skipped phase active versus paused elapsed, distinct Main/Whole zones and legacy presence without mutating facts.");
    }

    private static MeasuredStatistics Scope(int active, double metres, int gps, int hr, int zone, int outside)
    {
        return new(TimeSpan.FromSeconds(active), metres, TimeSpan.FromSeconds(gps), TimeSpan.FromSeconds(active - gps))
        {
            AverageHeartRate = 130,
            KnownHeartRateActiveTime = TimeSpan.FromSeconds(hr),
            HeartRateZones = [new(1, TimeSpan.FromSeconds(zone))],
            UnclassifiedHeartRateActiveTime = TimeSpan.FromSeconds(outside)
        };
    }
}
