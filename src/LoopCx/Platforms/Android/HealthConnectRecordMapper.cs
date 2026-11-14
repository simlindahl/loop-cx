using Android.Health.Connect.DataTypes;
using Android.Health.Connect.DataTypes.Units;
using Java.Time;
using LoopCx.Features.HealthConnectExport;
using System.Runtime.Versioning;

namespace LoopCx;

internal sealed record HCNativeRecords(string State, string? Reason, IReadOnlyList<Android.Health.Connect.DataTypes.Record> Records);

// Constructs SDK values only. This adapter has no provider, permission or acknowledgement dependency.
[SupportedOSPlatform("android34.0")]
internal static class HealthConnectRecordMapper
{
    internal static HCNativeRecords Create(HCWritePlanV1 plan, bool debugBuild)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (debugBuild || plan.Source.DebugBuild || plan.Source.Synthetic)
            return new("disabled_synthetic", "Debug or synthetic delivery is not eligible.", []);
        if (!plan.Source.Finished || plan.MappingVersion != 1 || !plan.HasValidHash())
            return new("action_required", "The frozen plan is incomplete or altered.", []);
        if (plan.Records.Count == 0 || plan.Records.Any(static record => record.StartOffsetSeconds is null || record.EndOffsetSeconds is null))
            return new("action_required", "Frozen start and end offsets are required by this SDK adapter.", []);
        try
        {
            return new("constructed", null, BuildRecords(plan));
        }
        catch (Exception problem) when (problem is Java.Lang.RuntimeException or ArgumentException or InvalidOperationException)
        {
            return new("action_required", problem.Message, []);
        }
    }

#if DEBUG
    internal static void RunIsolatedProbe(Android.Content.Context context)
    {
        if (Android.OS.Build.Hardware != "ranchu" || Android.OS.Build.Product != "sdk_gphone64_x86_64"
            || Android.OS.Build.Model != "sdk_gphone64_x86_64"
            || Android.OS.Build.SupportedAbis?.FirstOrDefault() != "x86_64"
            || Android.OS.Build.Fingerprint?.StartsWith("google/sdk_gphone64_x86_64/", StringComparison.Ordinal) != true)
            return;
        var path = Path.Combine(context.FilesDir!.AbsolutePath, "hc-record-mapping-probe.json");
        try
        {
            var start = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
            var end = start.AddMinutes(2);
            var id = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var gate = new HCSourceGate(true, true, true);
            HCPlannedRecord[] values = [
                new("isolated-sdk-session", 1, HCRecordKind.RunningSession, start, end, 3600, 7200,
                    [new(start.AddSeconds(10), start.AddSeconds(20))], null, [], "Synthetic SDK construction only."),
                new("isolated-sdk-distance", 1, HCRecordKind.Distance, start, end, 3600, 7200,
                    [], 123.5, [], "Accepted partial distance fixture."),
                new("isolated-sdk-heart-rate", 1, HCRecordKind.HeartRate, start, end, 3600, 7200,
                    [], null, [new(1, start.AddSeconds(1), 1), new(2, start.AddSeconds(2), 300)], "Receipt-time fixture.")];
            var warnings = Array.Empty<string>();
            var plan = new HCWritePlanV1(1, id, gate, values, warnings, HCWritePlanV1.Hash(1, id, gate, values, warnings));
            if (Create(plan, true).State != "disabled_synthetic") throw new InvalidOperationException("Synthetic export guard failed.");
            // This private synthetic construction has no delivery path, provider instance or insert method.
            var built = BuildRecords(plan);
            var session = (ExerciseSessionRecord)built[0];
            var distance = (DistanceRecord)built[1];
            var hr = (HeartRateRecord)built[2];
            if (session.Segments?.Single().SegmentType != ExerciseSegmentTypeEnum.Pause
                || session.StartZoneOffset?.TotalSeconds != 3600 || session.EndZoneOffset?.TotalSeconds != 7200
                || distance.Distance?.InMeters != 123.5 || hr.Samples?.Count != 2
                || hr.Samples[0].BeatsPerMinute != 1 || hr.Samples[1].BeatsPerMinute != 300
                || built.Select(static record => record.Metadata?.ClientRecordId).SequenceEqual(values.Select(static value => value.ClientRecordId)) != true)
                throw new InvalidOperationException("SDK object readback did not preserve frozen facts.");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
            {
                State = "isolated_sdk_objects_pass",
                ProviderCalls = 0,
                SyntheticExportBlocked = true,
                RecordTypes = built.Select(static record => record.GetType().Name).ToArray(),
                PauseType = (int)session.Segments.Single().SegmentType,
                DistanceMetres = distance.Distance.InMeters,
                HeartRate = hr.Samples.Select(static sample => new { sample.BeatsPerMinute, EpochMilliseconds = sample.Time!.ToEpochMilli() }).ToArray()
            }), new System.Text.UTF8Encoding(false));
        }
        catch (Exception problem) when (problem is Java.Lang.RuntimeException or ArgumentException or InvalidOperationException or IOException)
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { State = "failed", Error = problem.Message }), new System.Text.UTF8Encoding(false));
        }
    }
#endif

    private static System.Collections.ObjectModel.ReadOnlyCollection<Android.Health.Connect.DataTypes.Record> BuildRecords(HCWritePlanV1 plan)
    {
        var records = new List<Android.Health.Connect.DataTypes.Record>();
        foreach (var record in plan.Records)
        {
            var metadata = new Metadata.Builder().SetClientRecordId(record.ClientRecordId)!
                .SetClientRecordVersion(record.ClientRecordVersion)!.Build()!;
            var start = Instant.OfEpochMilli(record.StartUtc.ToUnixTimeMilliseconds())!;
            var end = Instant.OfEpochMilli(record.EndUtc.ToUnixTimeMilliseconds())!;
            var startOffset = ZoneOffset.OfTotalSeconds(record.StartOffsetSeconds!.Value)!;
            var endOffset = ZoneOffset.OfTotalSeconds(record.EndOffsetSeconds!.Value)!;
            switch (record.Kind)
            {
                case HCRecordKind.RunningSession:
                    var pauses = record.Pauses.Select(static pause => new ExerciseSegment.Builder(
                        Instant.OfEpochMilli(pause.StartUtc.ToUnixTimeMilliseconds())!,
                        Instant.OfEpochMilli(pause.EndUtc.ToUnixTimeMilliseconds())!, ExerciseSegmentTypeEnum.Pause).Build()!).ToList();
                    var notes = string.Join(" ", plan.Records.Select(static item => item.Provenance).Where(static text => text is not null));
                    records.Add(new ExerciseSessionRecord.Builder(metadata, start, end, ExerciseSessionTypeEnum.Running)
                        .SetStartZoneOffset(startOffset)!.SetEndZoneOffset(endOffset)!.SetSegments(pauses)!
                        .SetNotes(notes)!.Build()!);
                    break;
                case HCRecordKind.Distance:
                    records.Add(new DistanceRecord.Builder(metadata, start, end, Length.FromMeters(record.AcceptedMetres!.Value)!)
                        .SetStartZoneOffset(startOffset)!.SetEndZoneOffset(endOffset)!.Build()!);
                    break;
                case HCRecordKind.HeartRate:
                    var samples = record.HeartRateSamples.Select(static sample => new HeartRateRecord.HeartRateSample(
                        sample.Bpm, Instant.OfEpochMilli(sample.ReceiptUtc.ToUnixTimeMilliseconds())!)).ToList();
                    records.Add(new HeartRateRecord.Builder(metadata, start, end, samples)
                        .SetStartZoneOffset(startOffset)!.SetEndZoneOffset(endOffset)!.Build()!);
                    break;
                default:
                    throw new ArgumentException("Unsupported frozen record kind.");
            }
        }

        return records.AsReadOnly();
    }
}
