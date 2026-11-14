#if DEBUG
using Android.Content;
using Android.Database.Sqlite;
using LoopCx.Features.ActiveRun;
using System.Security.Cryptography;
using System.Text.Json;

namespace LoopCx;

internal static class RunSaveCorruptionProbe
{
    private static readonly string[] Sidecars = ["", "-journal", "-wal", "-shm"];
    internal static void Run(Context context)
    {
        if (Android.OS.Build.Hardware != "ranchu" || Android.OS.Build.Product != "sdk_gphone64_x86_64"
            || Android.OS.Build.Model != "sdk_gphone64_x86_64"
            || Android.OS.Build.SupportedAbis?.FirstOrDefault() != "x86_64"
            || Android.OS.Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) != true)
            return;

        var name = "run-save-corrupt-probe-" + Guid.NewGuid().ToString("N") + ".db";
        var path = context.GetDatabasePath(name)!.AbsolutePath;
        var report = Path.Combine(context.FilesDir!.AbsolutePath, "run-save-corruption-probe.json");
        try
        {
            // Create/close the real helper at its own unique path before replacing ONLY that scratch file.
            using (var seed = RunJournal.OpenCorruptionProbe(name))
            {
                SQLiteDatabase? seedDatabase = null;
                var seedClosed = false;
                try
                {
                    seedDatabase = seed.WritableDatabase!;
                    seedDatabase.ExecSQL("CREATE TABLE probe_sentinel (value TEXT NOT NULL)");
                    seedDatabase.ExecSQL("INSERT INTO probe_sentinel VALUES ('synthetic scratch only')");
                }
                finally
                {
                    try
                    {
                        seed.Close();
                        seedClosed = seedDatabase?.IsOpen == false;
                    }
                    finally { seedDatabase?.Dispose(); }
                }
                if (!seedClosed) throw new InvalidOperationException("Scratch seed must be closed before replacing its bytes.");
            }
            File.WriteAllBytes(path, Enumerable.Repeat((byte)0x58, 4096).ToArray());
            var before = Inventory(path);
            var callbacks = 0;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var helper = RunJournal.OpenCorruptionProbe(name);
                IOException? failure = null;
                try { _ = helper.WritableDatabase; }
                catch (SQLiteException error) { failure = RunJournal.ClassifyNativeFailure(error); }
                finally { helper.Close(); }
                if (failure is not RunSaveReadException || helper.ProbeCorruptionCallbacks == 0)
                    throw new InvalidOperationException("Corrupt helper must fail nonretryably through the actual custom handler, never create a replacement.");
                callbacks += helper.ProbeCorruptionCallbacks;
                if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(Inventory(path)))
                    throw new InvalidOperationException("Owned corrupt file or sidecars changed during failed open.");
                var buffer = new RunSaveBuffer();
                buffer.Capture(new(Guid.NewGuid(), 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
                    0, true, "[]", "{}", null, "{}"), null, null);
                buffer.Save(_ => throw failure);
                if (buffer.Status.CanRetry || buffer.Status.State != "storage_unavailable")
                    throw new InvalidOperationException("Actual native corruption must disable unsupported Retry saving.");
            }
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                State = "passed",
                Database = name,
                Before = before,
                After = Inventory(path),
                Opens = 2,
                CorruptionCallbacks = callbacks,
                CorruptionTarget = "uniquely owned scratch database only"
            }), new System.Text.UTF8Encoding(false));
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or Java.Lang.RuntimeException)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { State = "failed", Database = name, Error = error.Message }), new System.Text.UTF8Encoding(false));
        }
        // Retain only this owned scratch evidence for independent inspection and explicitly scoped QA cleanup.
    }

    private static object[] Inventory(string path) => Sidecars
        .Select(suffix =>
        {
            var file = path + suffix;
            return (object)new
            {
                Suffix = suffix,
                Exists = File.Exists(file),
                Length = File.Exists(file) ? new FileInfo(file).Length : (long?)null,
                Sha256 = File.Exists(file) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))) : null
            };
        }).ToArray();
}
#endif
