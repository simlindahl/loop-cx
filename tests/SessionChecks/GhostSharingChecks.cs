using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCx.Features.ExportRun;
using LoopCx.Features.GhostRun;

internal static class GhostSharingChecks
{
    internal static void Run(string[] arguments)
    {
        var document = JsonNode.Parse(File.ReadAllText("docs/export/sample-ghost-v3.json"))!;
        var source = JsonSerializer.Deserialize<ExportJournalSnapshot>(document["run"]!.ToJsonString(), ExportRunV1.JsonOptions)!;
        var original = JsonSerializer.Serialize(source);
        var json = RunJsonSharing.Project(source, null, null);
        using var result = JsonDocument.Parse(json);
        Require(result.RootElement.GetProperty("schemaVersion").GetInt32() == 3
            && JsonElement.DeepEquals(result.RootElement.GetProperty("run").GetProperty("checkpoint"), source.Checkpoint)
            && result.RootElement.GetProperty("run").GetProperty("localSavedPartUtc").ValueKind == JsonValueKind.Null
            && original == JsonSerializer.Serialize(source), "AI3 preserves full original frozen reference/checkpoint/raw facts without source mutation.");
        Reject(() => RunJsonSharing.Project(source, null, source.Id));
        Reject(() => RunJsonSharing.Project(Change(source, cp => cp["Snapshot"]!["Clock"]!["TrainingActive"] = "00:00:11"), null, null));
        Reject(() => RunJsonSharing.Project(Change(source, cp => cp["Snapshot"]!["ReferenceId"] = Guid.NewGuid()), null, null));
        var interrupted = Change(source, cp =>
        {
            cp["Snapshot"]!["Clock"]!["State"] = 0;
            cp["Snapshot"]!["QualityReason"] = null;
            cp["TerminalKind"] = null;
            cp["ArchiveDisposition"] = "not-terminal";
            cp.AsObject().Remove("TerminalContext");
        }) with
        { Status = "interrupted", Events = source.Events.Take(3).ToArray() };
        Reject(() => RunJsonSharing.Project(interrupted, null, null));
        Reject(() => RunJsonSharing.Project(interrupted, "not a date", null));
        const string marker = "2025-01-01T12:01:00.0000000+00:00";
        var partialJson = RunJsonSharing.Project(interrupted, marker, Guid.NewGuid());
        var stored = GhostStoredRun.Read(interrupted.Id, "interrupted", interrupted.Checkpoint.GetRawText(), marker);
        Require(stored.SavedLocally is not null && stored.Checkpoint.Snapshot.Clock.State == GhostClockState.Running,
            "Explicit saved-part confirmation does not fabricate a terminal clock or change checkpoint facts.");
        var legacy = Change(source, RemoveOptional) with
        {
            Events = source.Events.Select(item => item with { Checkpoint = Edit(item.Checkpoint, RemoveOptional) }).ToArray()
        };
        var legacyJson = RunJsonSharing.Project(legacy, null, null);
        Require(!JsonDocument.Parse(legacyJson).RootElement.GetProperty("run").GetProperty("checkpoint").TryGetProperty("MeasuredTrainingDistance", out _),
            "Absent legacy measured facts remain absent, never default zero or current policy.");
        if (arguments.Length == 2 && arguments[0] == "--export-ghost-ai3-samples")
        {
            Directory.CreateDirectory(arguments[1]);
            File.WriteAllText(Path.Combine(arguments[1], "incomplete.json"), json, new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(arguments[1], "interrupted.json"), partialJson, new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(arguments[1], "legacy.json"), legacyJson, new System.Text.UTF8Encoding(false));
        }
        Console.WriteLine("PASS: actual Ghost AI3 projection/eligibility, full frozen reference, pause-union clock, explicit interrupted acknowledgement and absent legacy facts.");
    }
    private static void RemoveOptional(JsonNode cp)
    {
        cp.AsObject().Remove("MeasuredTrainingDistance"); cp.AsObject().Remove("TerminalContext");
        cp["Profile"]!.AsObject().Remove("LivePace");
    }
    private static JsonElement Edit(JsonElement cp, Action<JsonNode> change)
    {
        var node = JsonNode.Parse(cp.GetRawText())!; change(node); return JsonSerializer.SerializeToElement(node);
    }
    private static ExportJournalSnapshot Change(ExportJournalSnapshot source, Action<JsonNode> change) => source with { Checkpoint = Edit(source.Checkpoint, change) };
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or JsonException) { return; }
        throw new InvalidOperationException("Malformed/unconfirmed Ghost sharing was accepted.");
    }
}
