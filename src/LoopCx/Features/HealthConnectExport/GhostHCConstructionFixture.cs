#if DEBUG
using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;

namespace LoopCx.Features.HealthConnectExport;

// Fixed SDK-object construction only. Never a production-eligible journal source or provider payload.
internal static class GhostHCConstructionFixture
{
    internal const string ExpectedHash = "3cb0428910ffe57f13df3ca143b9e57592068d5c1040e0bae8ee2c6394667480";
    internal static readonly Guid Id = Guid.Parse("69696969-6969-4969-8969-696969696969");
    internal static readonly DateTimeOffset Start = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    internal static HCWritePlanV1 Plan()
    {
        const string prefix = "loop-cx:69696969-6969-4969-8969-696969696969:hc-v2:";
        var gate = new HCSourceGate(true, true, true);
        const string note = "Synthetic Ghost SDK fixture; terminal kind matched-finish; not natural exercise or provider delivery. Frozen source is separately reported.";
        HCPlannedRecord[] records =
        [
            new(prefix + "session", 2, HCRecordKind.RunningSession, Start, Start.AddSeconds(20), 0, 0,
                [new(Start.AddSeconds(5), Start.AddSeconds(20))], null, [], note),
            new(prefix + "distance", 2, HCRecordKind.Distance, Start, Start.AddSeconds(20), 0, 0, [], 0, [], note),
            new(prefix + "heart-rate", 2, HCRecordKind.HeartRate, Start, Start.AddSeconds(20), 0, 0, [], null,
                [new(1, Start.AddTicks(1234), 1), new(2, Start.AddSeconds(20), 300)], note)
        ];
        string[] warnings = ["Fixed synthetic SDK object construction only; no production export."];

        return new(2, Id, gate, records, warnings, HCWritePlanV1.Hash(2, Id, gate, records, warnings));
    }

    internal static void Validate(HCWritePlanV1 plan)
    {
        var expected = Plan();

        if (!plan.HasValidHash() || expected.PayloadHash != ExpectedHash || plan.PayloadHash != ExpectedHash
            || JsonSerializer.Serialize(plan) != JsonSerializer.Serialize(expected))
        {
            throw new InvalidDataException("Only the exact fixed synthetic Ghost SDK payload may be constructed by this diagnostic seam.");
        }
    }

    internal static ExportJournalSnapshot Source()
    {
        static TimeSpan S(double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        var route = new GhostRoute(new(Guid.Parse("79797979-7979-4979-8979-797979797979"), Guid.Parse("89898989-8989-4989-8989-898989898989")),
            "Synthetic SDK construction fixture", Guid.Parse("99999999-9999-4999-8999-999999999999"), "synthetic-sdk-object-v1",
            [new(0, 0, 0), new(0, .0001, 11.11950802335329), new(0, .0002, 22.23901604670658)]);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1, S(3), 4, 5, 500),
            S(3), S(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot, LivePacePolicy.Pilot);
        var race = new GhostRace(Id, route, GhostMode.YearBest, null, profile,
            new(99900, 100000, Start.ToUnixTimeMilliseconds(), 0, 0, .1), 100000, Start, "UTC", TimeSpan.Zero,
            Guid.Parse("a9a9a9a9-a9a9-49a9-89a9-a9a9a9a9a9a9"), new(1, GhostOriginKind.SyntheticFixture));

        for (var second = 1; second <= 20; second++)
        {
            if (second == 5) race.Pause(105000);

            var receipt = 100000 + (second * 1000L) + (second == 20 ? 500 : 0);
            race.Observe(receipt);
            race.Location(new(Start.AddSeconds(second).ToUnixTimeMilliseconds(), (100000 + (second * 1000L)) * 1000000,
                0, second * .00001, .1), receipt);
        }

        var checkpoint = race.Checkpoint(1, 120500);
        checkpoint = checkpoint with
        {
            TerminalContext = GhostTerminalContext.Capture(checkpoint, Start.AddSeconds(20.5), TimeZoneInfo.Utc),
            MeasuredTrainingDistance = new(profile.TrainingDistance, 0, 0, true, "Synthetic zero distance", [])
        };

        return new(Id, Start.ToString("O", CultureInfo.InvariantCulture), Start.AddSeconds(20.5).ToString("O", CultureInfo.InvariantCulture),
            20500, "finished", JsonSerializer.SerializeToElement(Array.Empty<TimedPhase>()),
            JsonSerializer.SerializeToElement(new { Source = "Synthetic SDK construction fixture" }), null,
            JsonSerializer.SerializeToElement(checkpoint), "disabled_synthetic", [], [], []);
    }
}
#endif
