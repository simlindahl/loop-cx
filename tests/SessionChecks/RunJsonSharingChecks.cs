using System.Text;
using System.Text.Json;
using LoopCx.Features.ExportRun;

internal static class RunJsonSharingChecks
{
    internal static void Run(ExportJournalSnapshot source, string[] arguments)
    {
        // Arrange
        var finished = source with { Status = "finished" };
        var partial = source with { Status = "interrupted", ExportStatus = null };
        var legacy = finished with { Checkpoint = JsonSerializer.SerializeToElement(new { legacy = "unknown facts stay unknown" }) };
        var acknowledgement = "2026-11-14T12:00:00.0000000+00:00";

        // Act
        var completeJson = RunJsonSharing.Project(finished, null, null);
        var partialJson = RunJsonSharing.Project(partial, acknowledgement, Guid.NewGuid());
        var legacyJson = RunJsonSharing.Project(legacy, null, null);

        // Assert
        if (completeJson != ExportRunV1.Project(finished) || partialJson != ExportRunV1.Project(partial)
            || legacyJson != ExportRunV1.Project(legacy) || partialJson.Contains(acknowledgement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Sharing must preserve the exact existing JSON v1 projection without inventing recovery or end facts.");
        }

        RejectUnconfirmedOrActive(source, acknowledgement);
        CheckCompletePrivateFiles(completeJson, partialJson);
        if (arguments.Length == 2 && arguments[0] == "--export-json-share-samples")
        {
            var destination = Path.GetFullPath(arguments[1]);
            _ = Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "finished.json"), completeJson, new UTF8Encoding(false, true));
            File.WriteAllText(Path.Combine(destination, "confirmed-partial.json"), partialJson, new UTF8Encoding(false, true));
            File.WriteAllText(Path.Combine(destination, "legacy.json"), legacyJson, new UTF8Encoding(false, true));
        }
        Console.WriteLine("PASS: sharing eligibility, unchanged v1 facts, complete UTF-8 files, isolated operations, scoped stale cleanup and failed preparation.");
    }

    private static void RejectUnconfirmedOrActive(ExportJournalSnapshot source, string acknowledgement)
    {
        // Arrange
        var rejected = new[]
        {
            (Snapshot: source with { Status = "recording" }, Marker: null, Owner: null),
            (Snapshot: source with { Status = "interrupted" }, Marker: null, Owner: null),
            (Snapshot: source with { Status = "interrupted" }, Marker: (string?)"malformed", Owner: null),
            (Snapshot: source with { Status = "finished" }, Marker: null, Owner: (Guid?)source.Id),
            (Snapshot: source with { Status = "interrupted" }, Marker: (string?)acknowledgement, Owner: (Guid?)source.Id)
        };

        // Act
        var outcomes = rejected.Select(static test =>
        {
            try { _ = RunJsonSharing.Project(test.Snapshot, test.Marker, test.Owner); return false; }
            catch (InvalidDataException) { return true; }
        }).ToArray();

        // Assert
        if (outcomes.Any(static result => !result))
        {
            throw new InvalidOperationException("Active, failed-final-save and unconfirmed partial recordings cannot be shared.");
        }
    }

    private static void CheckCompletePrivateFiles(string completeJson, string partialJson)
    {
        // Arrange
        var temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loop-json-share-check-" + Guid.NewGuid().ToString("N")));
        if (!temporary.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsafe temporary test target.");
        }
        _ = Directory.CreateDirectory(temporary);
        try
        {
            var first = RunJsonSharing.PrepareFile(temporary, completeJson);
            var unrelated = Path.Combine(temporary, "unrelated.json");
            File.WriteAllText(unrelated, "keep", new UTF8Encoding(false, true));
            var foreignDirectory = Path.Combine(temporary, RunJsonSharing.CacheFolder, Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(foreignDirectory);
            var foreign = Path.Combine(foreignDirectory, "unrecognized.json");
            File.WriteAllText(foreign, "keep", new UTF8Encoding(false, true));
            File.SetLastWriteTimeUtc(foreign, DateTime.UtcNow.AddDays(-10));

            // Act
            var second = RunJsonSharing.PrepareFile(temporary, partialJson);
            var firstBytes = File.ReadAllBytes(first);
            var secondBytes = File.ReadAllBytes(second);
            File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddDays(-10));
            _ = RunJsonSharing.PrepareFile(temporary, completeJson);
            var invalidTextRejected = false;
            try { _ = RunJsonSharing.PrepareFile(temporary, "\uD800"); }
            catch (EncoderFallbackException) { invalidTextRejected = true; }
            var blocked = Path.Combine(temporary, "blocked");
            _ = Directory.CreateDirectory(blocked);
            File.WriteAllText(Path.Combine(blocked, RunJsonSharing.CacheFolder), "not a directory", new UTF8Encoding(false, true));
            var blockedRejected = false;
            try { _ = RunJsonSharing.PrepareFile(blocked, completeJson); }
            catch (IOException) { blockedRejected = true; }

            // Assert
            if (first == second || Path.GetFileName(first) != RunJsonSharing.FileName || Path.GetFileName(second) != RunJsonSharing.FileName
                || !firstBytes.AsSpan().SequenceEqual(new UTF8Encoding(false, true).GetBytes(completeJson))
                || !secondBytes.AsSpan().SequenceEqual(new UTF8Encoding(false, true).GetBytes(partialJson))
                || firstBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })
                || File.Exists(first) || !File.Exists(second) || !File.Exists(unrelated) || !File.Exists(foreign)
                || Directory.EnumerateFiles(temporary, ".writing", SearchOption.AllDirectories).Any()
                || !invalidTextRejected || !blockedRejected)
            {
                throw new InvalidOperationException("Share files must be complete UTF-8, isolated, retained after preparation, safely scoped and absent after failure.");
            }
        }
        finally { Directory.Delete(temporary, true); }
    }
}
