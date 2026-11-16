using System.Globalization;
using System.Text;

namespace LoopCx.Features.ExportRun;

// Eligibility is a local disposition check; the existing export contract remains unchanged.
public static class RunJsonSharing
{
    public const string FileName = "loop-cx-run.json";
    public const string CacheFolder = "loop-json-share";
    private static readonly object FileGate = new();

    public static string Project(ExportJournalSnapshot snapshot, string? recoverySavedUtc, Guid? activeOwner)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Id == activeOwner)
        {
            throw new InvalidDataException("A current recording or unconfirmed final save cannot be shared.");
        }

        if (LoopCx.Features.GhostRun.GhostRaceCheckpoint.IsGhost(snapshot.Checkpoint))
            return ExportRunV3.Project(snapshot, recoverySavedUtc);

        var confirmedPartial = snapshot.Status == "interrupted" && recoverySavedUtc is not null
            && DateTimeOffset.TryParseExact(recoverySavedUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        if (snapshot.Status != "finished" && !confirmedPartial)
        {
            throw new InvalidDataException("Only a finished or confirmed locally saved partial recording can be shared.");
        }

        return ExportRunV2.Project(snapshot);
    }

    public static string PrepareFile(string cacheDirectory, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        lock (FileGate)
        {
            var root = Path.Combine(Path.GetFullPath(cacheDirectory), CacheFolder);
            Directory.CreateDirectory(root);
            CleanupOldFiles(root);
            var operation = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(operation);
            var pending = Path.Combine(operation, ".writing");
            var complete = Path.Combine(operation, FileName);
            try
            {
                var bytes = new UTF8Encoding(false, true).GetBytes(json);
                using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes);
                    output.Flush(true);
                }
                File.Move(pending, complete); // Nothing is handed to Android before publication succeeds.

                return complete;
            }
            catch
            {
                try { File.Delete(pending); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                throw;
            }
        }
    }

    private static void CleanupOldFiles(string root)
    {
        // Never infer recipient consumption from chooser return. Only age out our own old cache entries.
        var cutoff = DateTime.UtcNow.AddDays(-7);
        foreach (var directory in Directory.EnumerateDirectories(root).Take(128))
        {
            try
            {
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)
                    || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
                    || Directory.EnumerateDirectories(directory).Any()) continue;

                var files = Directory.EnumerateFiles(directory).ToArray();
                if (files.Any(file => Path.GetFileName(file) is not (FileName or ".writing")
                    || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || File.GetLastWriteTimeUtc(file) >= cutoff)) continue;
                if (files.Length == 0 && Directory.GetLastWriteTimeUtc(directory) >= cutoff) continue;

                foreach (var file in files) File.Delete(file);
                Directory.Delete(directory, false);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
        }
    }
}
