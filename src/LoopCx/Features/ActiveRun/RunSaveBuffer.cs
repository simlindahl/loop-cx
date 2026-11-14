namespace LoopCx.Features.ActiveRun;

public sealed record RunSaveStatus(string State, string? Error = null, bool CanRetry = true);
public sealed class RunSaveReadException(string message, Exception? inner = null) : IOException(message, inner);
public sealed record RunSaveLocation(long UtcMilliseconds, long MonotonicNanoseconds, double Latitude, double Longitude, double? Accuracy);
public sealed record RunSaveEvent(string Command, DateTimeOffset ObservedUtc, long ElapsedMilliseconds, string Checkpoint);
public sealed record RunSaveCheckpoint(Guid Id, long Revision, DateTimeOffset StartedUtc, DateTimeOffset ObservedUtc,
    long ElapsedMilliseconds, bool Finished, string OriginalPlan, string TemplateMetadata, string? HeartRateMetadata, string Checkpoint);
public sealed record RunSaveBatch(RunSaveCheckpoint Latest, IReadOnlyList<RunSaveLocation> Locations,
    IReadOnlyList<HeartRateObservation> HeartRate, IReadOnlyList<RunSaveEvent> Events);

// One service owner; retain only the latest state and the uncommitted raw/event prefix, not one frame per tick.
public sealed class RunSaveBuffer
{
    private readonly List<RunSaveLocation> locations = [];
    private readonly List<HeartRateObservation> heartRate = [];
    private readonly List<RunSaveEvent> events = [];
    private RunSaveCheckpoint? latest;
    private RunSaveBatch? frozen;
    public RunSaveStatus Status { get; private set; } = new("recording");

    public void Add(RunSaveLocation location)
    {
        locations.Add(location);
    }

    public void Capture(RunSaveCheckpoint checkpoint, string? command, HeartRateObservation? observation)
    {
        latest = checkpoint;
        if (command is not null) events.Add(new(command, checkpoint.ObservedUtc, checkpoint.ElapsedMilliseconds, checkpoint.Checkpoint));
        if (observation is not null) heartRate.Add(observation);
    }

    public bool Save(Action<RunSaveBatch> commit, bool explicitRetry = false)
    {
        if (Status.Error is not null && (!explicitRetry || !Status.CanRetry)) return false;
        try
        {
            while (frozen is not null || latest is not null)
            {
                if (frozen is null)
                {
                    frozen = new(latest!, [.. locations], [.. heartRate], [.. events]);
                    latest = null;
                    locations.Clear(); heartRate.Clear(); events.Clear();
                }
                commit(frozen); // Return only after the native transaction actually ends.
                var finished = frozen.Latest.Finished;
                frozen = null;
                Status = new(finished ? "saved" : "recording");
            }
            return true;
        }
        catch (IOException error)
        {
            Status = new(error is RunSaveReadException ? "storage_unavailable" : "save_failed", error.Message, error is not RunSaveReadException);
            return false;
        }
    }
}

public static class RunSaveRevision
{
    public static bool AlreadyCommitted(string stored, RunSaveCheckpoint pending)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(stored);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new RunSaveReadException("Stored checkpoint is not an object. No repair or overwrite was attempted.");
            if (!document.RootElement.TryGetProperty("SaveRevision", out var value)) return false; // Legacy absence.
            if (value.ValueKind != System.Text.Json.JsonValueKind.Number || !value.TryGetInt64(out var revision) || revision <= 0)
                throw new RunSaveReadException("Stored save revision is malformed. No repair or overwrite was attempted.");
            if (revision == pending.Revision && stored == pending.Checkpoint) return true;
            return revision >= pending.Revision
                ? throw new RunSaveReadException("Stored checkpoint conflicts with the pending revision. No overwrite was attempted.")
                : false;
        }
        catch (System.Text.Json.JsonException error)
        {
            throw new RunSaveReadException("Stored checkpoint could not be read. No repair or overwrite was attempted.", error);
        }
    }
}
