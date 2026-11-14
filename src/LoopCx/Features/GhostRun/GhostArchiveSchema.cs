namespace LoopCx.Features.GhostRun;

/// <summary>Explicit boundaries in the canonical DDL keep trigger bodies intact; not a SQL parser.</summary>
public static class GhostArchiveSchema
{
    public const string ResourceName = "LoopCx.GhostRun.Schema1.sql";

    public static IReadOnlyList<string> Statements(string canonicalScript)
    {
        ArgumentNullException.ThrowIfNull(canonicalScript);
        var blocks = canonicalScript.ReplaceLineEndings("\n").Split("-- archive-statement\n", StringSplitOptions.None);
        return blocks.Length < 2 || blocks[0].Split('\n').Any(static line => line.Trim().Length != 0 && !line.TrimStart().StartsWith("--", StringComparison.Ordinal))
            || blocks.Skip(1).Any(static block => string.IsNullOrWhiteSpace(block))
            ? throw new InvalidDataException("The canonical archive statement boundaries or comment-only prefix are invalid.")
            : (IReadOnlyList<string>)Array.AsReadOnly(blocks.Skip(1).Select(static block => block.Trim()).ToArray());
    }
}

public sealed record ArchivedGhostRoute(GhostRoute Route, RouteCapturePolicy Policy);
public sealed record ArchivedGhostAttempt(GhostAttempt Attempt, string? QualityReason, IReadOnlyList<RecordedRouteFix> Raw);
public enum GhostArchiveWrite { Inserted, AlreadyStored }
