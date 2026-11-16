using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.GhostRun;

internal static class GhostOriginChecks
{
    internal static void Run()
    {
        // Arrange
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var route = new GhostRoute(new(Guid.NewGuid(), Guid.NewGuid()), "Origin host fixture", Guid.NewGuid(), "origin-host-v1",
            [new(0, 0, 0), new(0, .0001, 11.11950802335329)]);
        var profile = new GhostComparisonProfile("ghost-service-pilot-v1", new(route.RuleVersion, .5, .5, .1, TimeSpan.FromSeconds(3), 4, 5, 500),
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), HeartRateConfiguration.Empty, MovementPolicy.Pilot, DistancePolicy.Pilot);
        var origin = GhostRecordingOrigin.ForService();
        var race = new GhostRace(Guid.NewGuid(), route, GhostMode.YearBest, null, profile,
            new(99900, 100000, start.ToUnixTimeMilliseconds(), 0, 0, .1), 100000, start, "UTC", TimeSpan.Zero, null, origin);
        var first = race.Checkpoint(1, 100000);
        var buffer = new RunSaveBuffer();
        void Capture(GhostRaceCheckpoint checkpoint)
        {
            buffer.Capture(new(checkpoint.Id, checkpoint.SaveRevision, start,
            start.AddMilliseconds(checkpoint.ObservedBootMilliseconds - 100000), checkpoint.ObservedBootMilliseconds - 100000,
            false, "[]", /*lang=json,strict*/ "{\"ActivityKind\":\"ghost\"}", null, JsonSerializer.Serialize(checkpoint)), "origin-host", null);
        }

        Capture(first);
        var path = Path.GetTempFileName();

        try
        {
            // Act
            RunSaveBatch? frozen = null;
            var failed = buffer.Save(batch =>
            {
                frozen = batch;
                File.WriteAllText(path, batch.Latest.Checkpoint, new System.Text.UTF8Encoding(false));
                throw new IOException("Synthetic uncertain return after writing the host checkpoint file.");
            });
            var cold = GhostRaceCheckpoint.Read(File.ReadAllText(path));
            race.Observe(101000);
            Capture(race.Checkpoint(2, 101000));
            var retriedFrozen = false;
            var succeeded = buffer.Save(batch =>
            {
                if (batch.Latest.Revision == 1)
                {
                    retriedFrozen = ReferenceEquals(batch, frozen) && batch.Latest.Checkpoint == File.ReadAllText(path);
                }

                Require(GhostRaceCheckpoint.Read(batch.Latest.Checkpoint).Origin == origin, "Every frozen batch retains actual origin.");
                File.WriteAllText(path, batch.Latest.Checkpoint, new System.Text.UTF8Encoding(false));
            }, explicitRetry: true);
            var reopened = GhostStoredRun.Read(first.Id, "interrupted", File.ReadAllText(path));
            var legacyBytes = JsonSerializer.Serialize(first with { Origin = null });
            var legacyRoundtrip = JsonSerializer.Serialize(GhostRaceCheckpoint.Read(legacyBytes));

            // Assert
#if DEBUG
            Require(origin.Kind == GhostOriginKind.DebugService, "Compile-time Debug service context cannot claim NormalService.");
#else
            Require(origin.Kind == GhostOriginKind.NormalService, "Compile-time normal service path captures NormalService.");
#endif
            Require(!failed && cold.Origin == origin && succeeded && retriedFrozen && reopened.Checkpoint.Origin == origin
                && reopened.Status == "interrupted" && reopened.Checkpoint.SaveRevision == 2,
                "Actual model capture, failed frozen retry, host file cold read and interrupted prefix preserve the same immutable origin.");
            Require(!legacyBytes.Contains("\"Origin\"", StringComparison.Ordinal) && legacyBytes == legacyRoundtrip,
                "Legacy absent origin remains omitted and exact typed JSON bytes roundtrip without backfill.");
            Reject(first with { Origin = new(2, GhostOriginKind.NormalService) });
            Reject(first with { Origin = new(1, (GhostOriginKind)99) });
        }
        finally
        {
            File.Delete(path);
        }

        Console.WriteLine("PASS: immutable Ghost creation origin capture, frozen Retry, serialized reopen, interruption and legacy bytes.");
    }

    private static void Reject(GhostRaceCheckpoint checkpoint)
    {
        try { _ = GhostRaceCheckpoint.Read(JsonSerializer.Serialize(checkpoint)); }
        catch (InvalidDataException) { return; }

        throw new InvalidOperationException("Unsupported creation origin was accepted.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
