using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.History;

public sealed record InterruptedRun(StoredRunSummary Summary, DateTimeOffset? LastCommittedObservation,
    DateTimeOffset? SavedLocally)
{
    public StoredMeasurementDetails Measurements { get; init; } = StoredMeasurementDetails.Unknown;
}

public static class InterruptedRunProjection
{
    public static bool IsUnreadableRow(Exception error)
    {
        return error is System.Text.Json.JsonException or IOException
        or InvalidDataException or InvalidOperationException or ArgumentException;
    }

    public static void EnsureNoActiveOwner(Guid recordedId, Guid? ownerId)
    {
        if (recordedId == ownerId) throw new RunSaveReadException("A current service-owned recording cannot be recovered.");
    }

    public static InterruptedRun Read(StoredRunRow row, string observedUtc, string? savedUtc)
    {
        if (row.Status != "interrupted") throw new RunSaveReadException("The recording is not interrupted. No data was changed.");
        DateTimeOffset? saved = null;
        if (savedUtc is not null)
        {
            if (!DateTimeOffset.TryParseExact(savedUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new RunSaveReadException("The local save acknowledgement is unreadable. No data was changed.");
            saved = parsed;
        }

        using var checkpoint = JsonDocument.Parse(row.Checkpoint);

        return new(StoredRunProjection.Summary(row, true),
            DateTimeOffset.TryParse(observedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var observed) ? observed : null, saved)
        {
            Measurements = StoredMeasurementProjection.Read(checkpoint.RootElement, configuration: null, interrupted: true)
        };
    }
}
