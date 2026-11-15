using System.Text.Json;
using System.Text.Json.Serialization;
using LoopCx.Features.ExportRun;

namespace LoopCx.Features.ActiveRun;

public sealed record RunPointProvenance(int Version, Guid RunId, long PointId, string Outcome,
    long? DrawablePredecessor, string? BreakReason)
{
    private static readonly JsonSerializerOptions StoredOptions = new(ExportRunV1.JsonOptions)
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static RunPointProvenance Parse(string json) => JsonSerializer.Deserialize<RunPointProvenance>(json, StoredOptions)
        ?? throw new InvalidDataException("Stored point provenance must be an object.");
}

// Records the actual callback assessment. It neither validates GPS again nor allocates distance.
public sealed class RunRouteCapture(Guid runId)
{
    private long sequence;
    private long? previousId;
    private TimeSpan? previousTime;
    private string? boundary = "Run start";

    public void Break(string reason)
    {
        previousId = null;
        previousTime = null;
        boundary = reason;
    }

    public RunSaveLocation Capture(RunSaveLocation raw, DistanceObservation? actual, string reason)
    {
        if (runId == Guid.Empty) throw new InvalidDataException("Point provenance requires the owning run ID.");
        var number = checked(++sequence);
        var outcome = actual is null ? "not-assessed" : actual.Ignored ? "ignored" : actual.Valid ? "valid" : "rejected";
        if (actual is not null && actual.MonotonicTime != TimeSpan.FromMilliseconds(raw.MonotonicNanoseconds / 1_000_000))
            throw new InvalidDataException("Point provenance must describe this callback, not an earlier distance observation.");

        long? predecessor = outcome == "valid" && actual!.PreviousMonotonicTime == previousTime ? previousId : null;
        var why = outcome == "ignored" ? "Duplicate or out-of-order delivery"
            : outcome != "valid" ? reason : predecessor is not null ? null : boundary ?? reason;
        var fact = new RunPointProvenance(1, runId, number, outcome, predecessor, why);
        if (outcome == "valid")
        {
            previousId = number;
            previousTime = actual!.MonotonicTime;
            boundary = null;
        }
        else if (outcome != "ignored") Break(reason);

        return raw with { Provenance = fact };
    }
}
