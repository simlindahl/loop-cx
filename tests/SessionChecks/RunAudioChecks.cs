using System.Text.Json;
using LoopCx.Features.ActiveRun;

internal static class RunAudioChecks
{
    internal static void Run()
    {
        // Arrange
        var global = CorePreferencesState.Default;
        var legacy = new CoreStartRequest("C2", ContinuousMinutes: 45, ShortcutId: Guid.NewGuid(), ShortcutName: "Legacy");
        var explicitShortcut = legacy with { Audio = AudioConfiguration.Parse(AudioSummaryTrigger.Distance, "250.5") };
        var changed = global with { Audio = AudioConfiguration.Parse(AudioSummaryTrigger.ActiveTime, "2:30") };
        // Act
        var frozen = CoreTemplate.Resolve(legacy with { Audio = global.AudioFor(legacy) }, new()).Template;
        var newlySaved = legacy with { Audio = global.EffectiveAudio };
        var globalSave = changed with { Shortcuts = [new(Guid.NewGuid(), "Legacy", legacy with { ShortcutId = null, ShortcutName = null })] };
        using var savedJson = JsonDocument.Parse(JsonSerializer.Serialize(globalSave));

        // Assert
        if (global.AudioFor(legacy).Interval != 300 || changed.AudioFor(legacy).Interval != 150
            || changed.AudioFor(explicitShortcut).Interval != 250.5 || frozen.Audio?.Interval != 300
            || changed.AudioFor(newlySaved).Interval != 300
            || changed.AudioFor(new("C2", Audio: explicitShortcut.Audio)).Interval != 150
            || savedJson.RootElement.GetProperty("Shortcuts")[0].GetProperty("Request").TryGetProperty("Audio", out _)
            || JsonSerializer.Serialize(legacy).Contains("\"Audio\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Audio must inherit only when absent, freeze at start and retain legacy absence.");
        }

        foreach (var invalid in new[] { "0:00", "1:60", "1:2", "-1:00", "1.5:00", "" })
        {
            try { _ = AudioConfiguration.Parse(AudioSummaryTrigger.ActiveTime, invalid); throw new InvalidOperationException("Invalid audio interval accepted."); }
            catch (ArgumentException) { }
        }
        foreach (var invalid in new[] { "0", "-1", "-1E-20", "NaN", "Infinity", "1E309", "1,5", "1,000", "" })
        {
            try { _ = AudioConfiguration.Parse(AudioSummaryTrigger.Distance, invalid); throw new InvalidOperationException("Invalid distance interval accepted."); }
            catch (ArgumentException) { }
        }

        foreach (var input in new[] { "250.5", "0.5", "0.00000000000000000001", "100000000000000000000" })
        {
            // Arrange
            var stored = AudioConfiguration.Parse(AudioSummaryTrigger.Distance, input);

            // Act: the actual editor uses this invariant display conversion before unchanged Save.
            var displayed = stored.Interval.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var resaved = AudioConfiguration.Parse(AudioSummaryTrigger.Distance, displayed);

            // Assert
            if (resaved != stored)
                throw new InvalidOperationException("An unchanged numeric audio setting must survive parse/display/Save without changing its value.");
        }

        foreach (var interval in new[] { "0.5", "0.00000000000000000001" })
        {
            // Arrange
            var small = new RunAudioPolicy(AudioConfiguration.Parse(AudioSummaryTrigger.Distance, interval));
            _ = small.Observe(0, RunPhase.Work, false, TimeSpan.Zero, 0);

            // Act
            var first = small.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(1), 1);
            var second = small.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(2), 2);
            var duplicate = small.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(2), 2);
            var rollback = small.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(1), 0.5);
            var recross = small.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(2), 2);
            var third = small.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(3), 3);

            // Assert
            if (first?.PhasePriority != false || second?.PhasePriority != false || third?.PhasePriority != false
                || duplicate is not null || rollback is not null || recross is not null)
            {
                throw new InvalidOperationException("Every positive representable interval must keep advancing without saturation or replay.");
            }
        }
        if (AudioConfiguration.Parse(AudioSummaryTrigger.ActiveTime, "35791394:07").Interval != int.MaxValue)
            throw new InvalidOperationException("The largest supported whole-second interval must not overflow.");
        try { _ = AudioConfiguration.Parse(AudioSummaryTrigger.ActiveTime, "35791394:08"); throw new InvalidOperationException("Overflowing min:ss accepted."); }
        catch (ArgumentException) { }

        var policy = new RunAudioPolicy(new(Interval: 5));
        AudioCue? At(int phase, RunPhase kind, bool paused, int active)
        {
            return policy.Observe(phase, kind, paused, TimeSpan.FromSeconds(active), 0);
        }

        if (At(0, RunPhase.Work, false, 0)?.PhasePriority != true) throw new InvalidOperationException("Initial phase missing.");
        _ = At(0, RunPhase.Work, false, 2);
        if (At(1, RunPhase.Recovery, false, 5)?.PhasePriority != true || At(1, RunPhase.Recovery, false, 6) is not null)
            throw new InvalidOperationException("Phase must replace a simultaneous summary without backlog.");
        _ = At(1, RunPhase.Recovery, true, 8);
        if (At(1, RunPhase.Recovery, false, 10) is not null || At(1, RunPhase.Recovery, false, 1) is not null)
            throw new InvalidOperationException("Resume or retrospective correction must not replay consumed thresholds.");
        if (At(1, RunPhase.Recovery, false, 20)?.PhasePriority != false || At(1, RunPhase.Recovery, false, 20) is not null)
            throw new InvalidOperationException("A delayed callback may produce one current summary, never a backlog.");
        _ = At(1, RunPhase.Recovery, false, 23);
        if (At(1, RunPhase.Recovery, false, 25)?.PhasePriority != false)
            throw new InvalidOperationException("Fresh active-time summaries must work without GPS.");
        var distancePolicy = new RunAudioPolicy(new(1, AudioSummaryTrigger.Distance, 250.5));
        _ = distancePolicy.Observe(0, RunPhase.Work, false, TimeSpan.Zero, 0);
        if (distancePolicy.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(10), 0) is not null
            || distancePolicy.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(11), 300)?.PhasePriority != false
            || distancePolicy.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(20), 501)?.PhasePriority != false
            || distancePolicy.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(10), 200) is not null
            || distancePolicy.Observe(0, RunPhase.Work, false, TimeSpan.FromSeconds(20), 501) is not null)
        {
            throw new InvalidOperationException("Accepted-distance gaps/corrections must consume without replay or split coupling.");
        }

        var scope = new MeasuredStatistics(TimeSpan.FromSeconds(25), 100, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(5)) { AverageHeartRate = 123 };
        var spoken = RunAudioText.Summary(scope, scope, null, null);
        if (!spoken.Contains("Main set.", StringComparison.Ordinal) || !spoken.Contains("Current split.", StringComparison.Ordinal)
            || spoken.Split("Active time 0 minutes 25 seconds", StringSplitOptions.None).Length != 3
            || spoken.Split("Recorded distance 100 metres", StringSplitOptions.None).Length != 3
            || spoken.Split("Average pace unavailable", StringSplitOptions.None).Length != 3
            || spoken.Split("Average heart rate 123", StringSplitOptions.None).Length != 3
            || !spoken.Contains("Live pace unavailable. Heart rate unavailable.", StringComparison.Ordinal)
            || spoken.Contains("RPE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Summary must use all measured scope fields and honest live unavailable values.");
        }

        Console.WriteLine("PASS: audio inheritance/freeze, decimal input, phase priority, pause/retro/gap consumption and measured summary fields.");
    }
}
