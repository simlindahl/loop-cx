using System.Text.Json;
using LoopCx.Features.ActiveRun;

internal static class HeartRateSettingsChecks
{
    internal static void Run()
    {
        var profile = HeartRateReserveProfile.Create(50, 190);
        var zones = profile.ResolveZones();
        if (!zones.Select(static zone => zone.MinimumBpm).SequenceEqual([106, 134, 148, 162, 176]) || zones[^1].MaximumBpm != 190)
            throw new InvalidOperationException("HRR must use exact rational ceiling thresholds and inclusive endpoints.");
        foreach (var (rest, max) in new[] { (0, 190), (50, 50), (-1, 190), (50, 51) })
        {
            try { _ = HeartRateReserveProfile.Create(rest, max).ResolveZones(); }
            catch (ArgumentException) { continue; }
            throw new InvalidOperationException("Invalid or collapsed profiles must not be saved.");
        }
        var configuration = HeartRateConfiguration.Empty with { ReserveProfile = profile, Zones = zones, DeviceAddress = "F0:F1:F2:F3:F4:F5" };
        configuration.Validate();
        var frozen = new RunHeartRate(configuration);
        var saved = JsonSerializer.Serialize(configuration);
        var reopened = HeartRateConfiguration.ReadOptional(saved);
        if (reopened.ConfigurationError is not null || reopened.Configuration.ReserveProfile != profile
            || !reopened.Configuration.Zones.SequenceEqual(zones))
        {
            throw new InvalidOperationException("Saved HRR method, inputs, revision and exact boundaries must roundtrip.");
        }

        var replacement = HeartRateReserveProfile.Create(60, 200);
        var future = configuration with { ReserveProfile = replacement, Zones = replacement.ResolveZones(), DeviceAddress = "A0:A1:A2:A3:A4:A5" };
        future.Validate();
        if (frozen.Snapshot(TimeSpan.Zero).Configuration.ReserveProfile != profile
            || frozen.Snapshot(TimeSpan.Zero).Configuration.DeviceAddress != "F0:F1:F2:F3:F4:F5" || JsonSerializer.Serialize(configuration) != saved)
        {
            throw new InvalidOperationException("Saving future settings cannot rewrite the active frozen profile.");
        }

        var legacy = HeartRateConfiguration.Empty with { Zones = zones };
        var restored = HeartRateConfiguration.ReadOptional(JsonSerializer.Serialize(legacy));
        if (restored.Configuration.ReserveProfile is not null || !restored.Configuration.Zones.SequenceEqual(zones))
            throw new InvalidOperationException("Legacy manual ranges retain unknown method provenance.");
        var stats = new RunStatistics();
        stats.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(4), RunPhase.Work, false);
        var hr = new RunHeartRate(configuration);
        _ = hr.Observe(new(TimeSpan.Zero, DateTimeOffset.UnixEpoch, 100, "valid"));
        stats.ObserveHeartRate(hr.Observe(new(TimeSpan.FromSeconds(1), DateTimeOffset.UnixEpoch.AddSeconds(1), 200, "valid")));
        stats.ObserveHeartRate(hr.Observe(new(TimeSpan.FromSeconds(2), DateTimeOffset.UnixEpoch.AddSeconds(2), 200, "valid")));
        stats.ObserveHeartRate(hr.Observe(new(TimeSpan.FromSeconds(3), DateTimeOffset.UnixEpoch.AddSeconds(3), null, "stale")));
        var distance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);
        var total = stats.Snapshot(TimeSpan.FromSeconds(4), distance.Snapshot, true).Total;
        if (total.KnownHeartRateActiveTime != TimeSpan.FromSeconds(2) || total.UnclassifiedHeartRateActiveTime != TimeSpan.FromSeconds(2)
            || total.UnknownHeartRateActiveTime != TimeSpan.FromSeconds(2))
        {
            throw new InvalidOperationException("Valid below/above-zone receipts remain known, unlike missing HR.");
        }

        Console.WriteLine("PASS: HRR integer thresholds, invalid ranges, frozen future settings, legacy provenance and known out-of-zone HR.");
    }
}
