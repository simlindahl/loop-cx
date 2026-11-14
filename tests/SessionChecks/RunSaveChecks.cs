using LoopCx.Features.ActiveRun;

internal static class RunSaveChecks
{
    internal static void Run()
    {
        // Arrange
        var id = Guid.NewGuid();
        var utc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var buffer = new RunSaveBuffer();
        RunSaveCheckpoint Frame(long revision, bool finished = false)
        {
            return new(id, revision, utc, utc.AddSeconds(revision),
            revision * 1000, finished, "frozen-plan", "frozen-template", null, "checkpoint-" + revision);
        }

        var hr = new HeartRateObservation(TimeSpan.FromSeconds(1), utc.AddSeconds(1), 100, "valid");
        buffer.Add(new(1000, 1000000000, 1, 2, 5));
        buffer.Capture(Frame(1), "pause", hr);
        RunSaveBatch? failedBatch = null;
        var calls = 0;

        // Act
        var failed = buffer.Save(batch => { failedBatch = batch; calls++; throw new IOException("native commit failure"); });
        buffer.Capture(Frame(2), "resume", null);
        buffer.Add(new(3000, 3000000000, 3, 4, 5));
        buffer.Capture(Frame(3, true), "finish", null);
        _ = buffer.Save(_ => throw new InvalidOperationException("No unattended retry is permitted."));
        var repeated = buffer.Save(_ => { calls++; throw new IOException("still failing"); }, true);
        var committed = new List<RunSaveBatch>();
        var saved = buffer.Save(committed.Add, true);

        // Assert
        if (failed || repeated || !saved || calls != 2 || buffer.Status.State != "saved"
            || committed.Count != 2 || !ReferenceEquals(failedBatch, committed[0])
            || !committed.SelectMany(batch => batch.Events).Select(item => item.Command).SequenceEqual(["pause", "resume", "finish"]) || committed.Sum(batch => batch.Locations.Count) != 2 || committed.Sum(batch => batch.HeartRate.Count) != 1
            || committed[0].Events[0].Checkpoint != "checkpoint-1" || committed[0].HeartRate[0] != hr
            || committed[1].Latest != Frame(3, true))
        {
            throw new InvalidOperationException("Retry must preserve each original fact and frozen terminal provenance, clearing error only on commit.");
        }

        // Arrange: the store commits but its local completion return throws.
        var uncertain = new RunSaveBuffer();
        uncertain.Capture(Frame(4, true), "finish", null);
        RunSaveBatch? durable = null;

        // Act
        _ = uncertain.Save(batch => { durable = batch; throw new IOException("EndTransaction outcome unknown locally"); });
        var recovered = uncertain.Save(batch =>
        {
            if (!ReferenceEquals(batch, durable)) throw new InvalidOperationException("Retry must use the exact uncertain batch revision.");
        }, true);

        // Assert
        if (!recovered || uncertain.Status.State != "saved")
            throw new InvalidOperationException("An uncertain local completion cannot confirm Saved before idempotent store retry succeeds.");
        // Arrange
        var stored = Frame(5, true) with { Checkpoint = /*lang=json,strict*/ "{\"SaveRevision\":5}" };
        var unavailable = new RunSaveBuffer();
        unavailable.Capture(stored, "finish", null);
        string[] corrupt = ["broken-json", "[]", /*lang=json,strict*/ "{\"SaveRevision\":\"5\"}", /*lang=json,strict*/ "{\"SaveRevision\":0}", /*lang=json,strict*/ "{\"SaveRevision\":9223372036854775808}", /*lang=json,strict*/ "{\"SaveRevision\":6}", /*lang=json,strict*/ "{\"SaveRevision\":5,\"changed\":true}"];

        // Act
        foreach (var bytes in corrupt)
        {
            try { _ = RunSaveRevision.AlreadyCommitted(bytes, stored); }
            catch (RunSaveReadException) { continue; }
            throw new InvalidOperationException("Malformed/conflicting stored checkpoints cannot be repaired or overwritten.");
        }
        _ = unavailable.Save(_ => RunSaveRevision.AlreadyCommitted("broken-json", stored));
        var retriedUnavailable = unavailable.Save(_ => throw new InvalidOperationException("Irrecoverable reads cannot offer retry."), true);

        // Assert
        if (!RunSaveRevision.AlreadyCommitted(stored.Checkpoint, stored) || RunSaveRevision.AlreadyCommitted("{}", stored)
            || unavailable.Status.CanRetry || unavailable.Status.State != "storage_unavailable" || retriedUnavailable)
        {
            throw new InvalidOperationException("Known committed revision is idempotent; unreadable stored data is explicitly nonretryable.");
        }

        // Arrange: platform persistence maps known native corruption/read failures to this explicit boundary.
        IOException[] failures = [new RunSaveReadException("native database corrupt"), new RunSaveReadException("native database cannot open"),
            new RunSaveReadException("native checkpoint read I/O denied"), new IOException("transaction ABORT"),
            new IOException("database busy"), new IOException("write full")];
        foreach (var error in failures)
        {
            var failedStore = new RunSaveBuffer();
            failedStore.Capture(stored, "finish", null);
            var retryCalls = 0;

            // Act
            _ = failedStore.Save(_ => throw error);
            var canRetry = failedStore.Status.CanRetry;
            _ = failedStore.Save(_ => { retryCalls++; }, true);

            // Assert
            var retryExpected = error is not RunSaveReadException;
            if (canRetry != retryExpected || retryCalls != (retryExpected ? 1 : 0))
                throw new InvalidOperationException("Structural native read failures disable retry; ordinary transaction write failures retain it.");
        }

        Console.WriteLine("PASS: failed/repeated/final saves retain raw events and terminal facts; explicit retry reuses uncertain commit revision.");
    }
}
