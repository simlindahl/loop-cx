using System.Globalization;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
using LoopCx.Features.ExportRun;
using LoopCx.Features.HealthConnectExport;

namespace LoopCx.Features.History;

public sealed record StoredRunRow(Guid Id, string StartedUtc, string Status, string? Template, string Checkpoint,
    string? ExportStatus, string? AttemptJson);
public sealed record StoredRunArchive(IReadOnlyList<StoredRunRow> Finished, int InterruptedCount, int UnreadableCount = 0,
    IReadOnlyList<InterruptedRun>? Interrupted = null);
public sealed record StoredRunSummary(Guid Id, DateTimeOffset? StartedUtc, string Name, string Source,
    TimeSpan? TotalActive, MeasuredStatistics? Total, string ExportLabel, bool Synthetic, string? StartZone, int? StartOffset);
public sealed record StoredRunDetail(StoredRunSummary Summary, MeasuredStatistics? Main, TimeSpan? MainActive,
    TimeSpan? TotalElapsed, TimeSpan? MainElapsed, TimeSpan? Warmup, TimeSpan? Cooldown,
    IReadOnlyList<TimedPhase>? Plan, CoreTargets? Targets, IReadOnlyList<HeartRateZone>? Zones);

// Read stored facts only. No Settings/default factory, raw-data recomputation or persistence dependency.
public static class StoredRunProjection
{
    public static string TargetSummary(CoreTargets? target, bool continuous)
    {
        if (target is null) return "Unavailable";
        if (target.Mode == CoreTargetMode.None) return "RPE only";
        target.Validate(continuous);
        static string Value(CoreTargetMode mode, int value)
        {
            return mode == CoreTargetMode.Pace
            ? $"{value / 60}:{value % 60:00} min/km" : $"Zone {value}";
        }

        var text = $"{(continuous ? "Continuous target" : "Work target")}: {Value(target.Mode, target.Work!.Value)}";
        if (target.Recovery is { } recovery) text += $" · Recovery: {Value(target.Mode, recovery)}";

        return text + " · intended, not measured";
    }

    public static StoredRunSummary Summary(StoredRunRow row, bool interrupted = false)
    {
        if (row.Status != (interrupted ? "interrupted" : "finished") || row.Id == Guid.Empty)
            throw new InvalidDataException("The stored recording status does not match this view.");
        using var document = JsonDocument.Parse(row.Checkpoint);
        var checkpoint = document.RootElement;
        ValidateCheckpoint(checkpoint);
        var template = row.Template is null ? null : JsonSerializer.Deserialize<CoreTemplate>(row.Template);
        if (template is not null && (string.IsNullOrWhiteSpace(template.TemplateId) || string.IsNullOrWhiteSpace(template.Source) || template.TemplateRevision <= 0))
            throw new InvalidDataException("Stored source metadata is malformed.");
        var statistics = Read<RunStatisticsState>(checkpoint, "Snapshot", "Statistics");
        if (statistics is { Version: not 1 }) throw new InvalidDataException("Stored statistics version is unsupported.");
        var start = Read<RunStartContext>(checkpoint, "StartContext");
        int? offset = checkpoint.TryGetProperty("StartContext", out var context) && context.ValueKind == JsonValueKind.Object
            && context.TryGetProperty("UtcOffsetSeconds", out var offsetValue) && offsetValue.ValueKind == JsonValueKind.Number && offsetValue.TryGetInt32(out var seconds)
            && seconds is >= -50400 and <= 50400 && seconds % 60 == 0 ? seconds : null;
        return new(row.Id, DateTimeOffset.TryParse(row.StartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc) ? utc : null,
            template?.ShortcutName ?? template?.ProtocolId ?? template?.TemplateId ?? "Recorded run",
            template is null ? "Source unavailable" : $"{template.Source} · {template.ProtocolId ?? template.TemplateId} · revision {template.TemplateRevision}",
            Duration(checkpoint, "TotalActiveTime"), statistics?.Total, interrupted ? "Not exported · interrupted" : ExportLabel(row), statistics?.HasSyntheticTime == true,
            start?.TimeZoneId, offset);
    }

    public static StoredRunDetail Detail(ExportJournalSnapshot stored, string? attemptJson)
    {
        var row = new StoredRunRow(stored.Id, stored.StartedUtc, stored.Status, stored.Template?.GetRawText(),
            stored.Checkpoint.GetRawText(), stored.ExportStatus, attemptJson);
        var summary = Summary(row);
        var checkpoint = stored.Checkpoint;
        var statistics = Read<RunStatisticsState>(checkpoint, "Snapshot", "Statistics");
        var plan = Read<TimedPhase[]>(checkpoint, "Plan");
        var completed = Read<CompletedPhase[]>(checkpoint, "Completed");
        var boundaries = Read<PhaseBoundaryFact[]>(checkpoint, "PhaseBoundaries");
        var template = stored.Template is { } metadata ? metadata.Deserialize<CoreTemplate>() : null;
        var setup = stored.HeartRateSetup is { } heartRate ? heartRate.Deserialize<HeartRateSetup>() : null;
        if (setup?.Configuration?.Zones?.Any(static zone => zone is null) == true)
            throw new InvalidDataException("Malformed stored heart-rate zones.");
        if (plan?.Any(static phase => phase is null || !Enum.IsDefined(phase.Kind) || phase.Duration <= TimeSpan.Zero) == true
            || completed?.Any(static phase => phase is null || phase.Plan is null || phase.ActiveTime < TimeSpan.Zero) == true)
        {
            throw new InvalidDataException("Malformed stored phase plan.");
        }
        template?.Targets?.Validate(plan is { Length: 1 } && plan[0].Kind == RunPhase.Work);

        TimeSpan? elapsed = null, mainElapsed = null;
        if (plan is { Length: > 0 } && boundaries is not null)
        {
            if (boundaries.Any(boundary => boundary is null || boundary.PreviousPhaseIndex < 0 || boundary.PreviousPhaseIndex >= plan.Length
                || boundary.PreviousPhase != plan[boundary.PreviousPhaseIndex].Kind || boundary.EffectiveElapsedMilliseconds < 0
                || (boundary.NextPhaseIndex is { } index && (index != boundary.PreviousPhaseIndex + 1 || index >= plan.Length || boundary.NextPhase != plan[index].Kind))))
            {
                throw new InvalidDataException("Malformed stored phase facts.");
            }

            var finish = boundaries.SingleOrDefault(boundary => boundary.PreviousPhaseIndex == plan.Length - 1 && boundary.NextPhaseIndex is null);
            if (finish is { EffectiveElapsedMilliseconds: >= 0 }) elapsed = TimeSpan.FromMilliseconds(finish.EffectiveElapsedMilliseconds);
            var first = Array.FindIndex(plan, static phase => phase.Kind is RunPhase.Work or RunPhase.Recovery);
            var last = Array.FindLastIndex(plan, static phase => phase.Kind is RunPhase.Work or RunPhase.Recovery);
            var begin = first == 0 ? 0L : boundaries.SingleOrDefault(boundary => boundary.NextPhaseIndex == first)?.EffectiveElapsedMilliseconds;
            var end = boundaries.SingleOrDefault(boundary => boundary.PreviousPhaseIndex == last && boundary.NextPhase is not (RunPhase.Work or RunPhase.Recovery))?.EffectiveElapsedMilliseconds;
            if (first >= 0 && begin is >= 0 && end >= begin) mainElapsed = TimeSpan.FromMilliseconds(end.Value - begin.Value);
        }
        return new(summary, statistics?.Main, Duration(checkpoint, "MainActiveTime"), elapsed, mainElapsed,
            CompletedTime(completed, plan, RunPhase.Warmup), CompletedTime(completed, plan, RunPhase.Cooldown), plan, template?.Targets, setup?.Configuration?.Zones);
    }

    private static TimeSpan? CompletedTime(CompletedPhase[]? phases, TimedPhase[]? plan, RunPhase kind)
    {
        if (phases is null || plan is null) return null;
        var expected = plan.Count(phase => phase.Kind == kind);
        var recorded = phases.Where(phase => phase.Plan.Kind == kind).ToArray();
        return recorded.Length == expected ? TimeSpan.FromTicks(recorded.Sum(static phase => phase.ActiveTime.Ticks)) : null;
    }

    private static string ExportLabel(StoredRunRow row)
    {
        if (row.ExportStatus == "disabled_synthetic") return "Not exported · debug/synthetic";
        if (row.ExportStatus is "pending" or "action_required")
        {
            using var checkpoint = JsonDocument.Parse(row.Checkpoint);
            if (!HCConsent.Eligible(checkpoint.RootElement)) return "Not exported · local only";
        }
        if (row.ExportStatus == "pending") return "Pending";
        if (row.ExportStatus == "action_required") return "Action required";
        if (row.ExportStatus != "exported") return "Export status unavailable";
        try
        {
            var attempt = row.AttemptJson is null ? null : JsonSerializer.Deserialize<HCDurableAttempt>(row.AttemptJson);
            attempt?.Validate(false);
            return attempt is { State: "acknowledged" } && attempt.FrozenPlan.RunId == row.Id ? "Exported" : "Export status unavailable";
        }
        catch (Exception error) when (error is JsonException or InvalidDataException) { return "Action required"; }
    }

    private static void ValidateCheckpoint(JsonElement checkpoint)
    {
        if (checkpoint.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Stored checkpoint is not readable.");
        if (checkpoint.TryGetProperty("SchemaVersion", out var version)
            && (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1))
        {
            throw new InvalidDataException("Stored checkpoint version is unsupported.");
        }
    }

    private static T? Read<T>(JsonElement root, params string[] path) where T : class
    {
        foreach (var name in path)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out root) || root.ValueKind == JsonValueKind.Null) return null;
        }
        return root.Deserialize<T>();
    }

    private static TimeSpan? Duration(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && TimeSpan.TryParseExact(value.GetString(), "c", CultureInfo.InvariantCulture, out var duration)
        && duration >= TimeSpan.Zero ? duration : null;
    }
}
