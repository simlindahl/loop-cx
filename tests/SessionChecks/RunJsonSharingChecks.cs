using System.Text;
using System.Text.Json;
using LoopCx.Features.ActiveRun;
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
        if (completeJson != ExportRunV2.Project(finished) || partialJson != ExportRunV2.Project(partial)
            || legacyJson != ExportRunV2.Project(legacy) || partialJson.Contains(acknowledgement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Sharing must preserve the normal JSON v2 projection without inventing recovery or end facts.");
        }

        CheckPointProvenance(finished);
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
        Console.WriteLine("PASS: sharing eligibility, v2 sharing with unchanged frozen facts, complete UTF-8 files, isolated operations, scoped stale cleanup and failed preparation.");
    }

    private static void CheckPointProvenance(ExportJournalSnapshot source)
    {
        var capture = new RunRouteCapture(source.Id);
        RunSaveLocation Raw(int second) => new(second * 1000L, second * 1_000_000_000L, 1, 2, 5);
        var first = capture.Capture(Raw(1), new(true, null, TimeSpan.FromSeconds(1)), "Acquiring");
        var second = capture.Capture(Raw(2), new(true, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)), "Tracking");
        var ignored = capture.Capture(Raw(1), new(false, null, TimeSpan.FromSeconds(1)) { Ignored = true }, "Tracking");
        var afterIgnored = capture.Capture(Raw(3), new(true, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)), "Tracking");
        if (ignored.Provenance!.Outcome != "ignored" || ignored.Provenance.BreakReason != "Duplicate or out-of-order delivery"
            || ignored.Provenance.DrawablePredecessor is not null || afterIgnored.Provenance!.DrawablePredecessor != second.Provenance!.PointId)
            throw new InvalidOperationException("Ignored delivery has its own outcome reason and preserves the newer valid chain.");
        capture.Break("Manual pause");
        var unassessed = capture.Capture(Raw(3), null, "Automatic pause");
        var resumed = capture.Capture(Raw(4), new(true, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)), "Reanchor");
        if (second.Provenance!.DrawablePredecessor != first.Provenance!.PointId
            || unassessed.Provenance!.Outcome != "not-assessed" || resumed.Provenance!.DrawablePredecessor is not null)
            throw new InvalidOperationException("Only the actual callback may link; pause/unassessed facts break continuity.");

        var points = new[] { first, second, unassessed, resumed }.Select((point, index) =>
            new ExportLocation(index + 1, point.UtcMilliseconds, point.MonotonicNanoseconds,
                point.Latitude, point.Longitude, point.Accuracy)
            { Provenance = point.Provenance }).ToArray();
        var stored = source with { Locations = points };
        var v1 = ExportRunV1.Project(stored);
        if (v1.Contains("provenance", StringComparison.Ordinal)
            || v1 != ExportRunV1.Project(stored with { Locations = points.Select(point => point with { Provenance = null }).ToArray() }))
            throw new InvalidOperationException("Version 1 must retain its actual historical shape independently of route metadata.");
        _ = ExportRunV2.Project(stored);
        foreach (var bad in new[]
        {
            second.Provenance! with { Version = 2 },
            second.Provenance! with { RunId = Guid.NewGuid() },
            second.Provenance! with { DrawablePredecessor = 3 },
            second.Provenance! with { DrawablePredecessor = 2 },
            second.Provenance! with { Outcome = "rejected" }
        })
        {
            var invalid = stored with { Locations = [points[0], points[1] with { Provenance = bad }] };
            try { _ = ExportRunV2.Project(invalid); }
            catch (InvalidDataException) { continue; }
            throw new InvalidOperationException("Invalid version/cross-run/forward/self/unsupported links must fail closed.");
        }
        try { _ = capture.Capture(Raw(5), new(true, null, TimeSpan.FromSeconds(4)), "stale observation"); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("A prior callback observation cannot become new raw point provenance.");
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
