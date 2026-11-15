using System.Text.Json;
using LoopCx.Features.ActiveRun;

internal static class CorePreferencesChecks
{
    internal static void Run()
    {
        // Arrange: the Training view was loaded before the independent Settings destination saved audio.
        var legacyId = Guid.NewGuid();
        var explicitId = Guid.NewGuid();
        var initial = CorePreferencesState.Default with
        {
            Shortcuts = [new(legacyId, "Legacy", new("C2")), new(explicitId, "Explicit", new("C2", Audio: new(1, AudioSummaryTrigger.Distance, 250.5)))]
        };
        CorePreferences.Save(initial);
        var cachedTraining = CorePreferences.Load();
        var frozenBefore = CoreTemplate.Resolve(cachedTraining.C2Selection with { Audio = cachedTraining.EffectiveAudio }, cachedTraining.WarmupCooldown).Template;
        var frozenJson = JsonSerializer.Serialize(frozenBefore);
        CorePreferences.Save(CorePreferences.Load() with { Audio = AudioConfiguration.Parse(AudioSummaryTrigger.ActiveTime, "0:05") });

        // Act: a still-cached view saves a target after returning; production Save reads current storage.
        var target = new CoreTargets(CoreTargetMode.Pace, 300);
        var updated = CorePreferences.Update(current => current with { C2Selection = current.C2Selection with { Targets = target } });
        var reopened = CorePreferences.Load();
        var frozenAfter = CoreTemplate.Resolve(reopened.C2Selection with { Audio = reopened.EffectiveAudio }, reopened.WarmupCooldown).Template;

        // Assert: unrelated Settings/shortcuts and already-frozen plans are preserved.
        if (cachedTraining.Audio is not null || updated.Audio?.Interval != 5 || reopened.Audio?.Interval != 5
            || reopened.C2Selection.Targets != target || frozenAfter.Audio?.Interval != 5
            || reopened.Shortcuts.Single(item => item.Id == legacyId).Request.Audio is not null
            || reopened.AudioFor(reopened.Shortcuts.Single(item => item.Id == legacyId).Request with { ShortcutId = legacyId }).Interval != 5
            || reopened.Shortcuts.Single(item => item.Id == explicitId).Request.Audio?.Interval != 250.5
            || JsonSerializer.Serialize(frozenBefore) != frozenJson || frozenBefore.Audio?.Interval != 300)
            throw new InvalidOperationException("A cached Training editor must not erase saved audio or rewrite frozen/shortcut settings.");

        // Arrange: another setting and a shortcut removal have happened since the view was loaded.
        var preparation = new C1WarmupCooldownSettings(1, 2);
        CorePreferences.Save(reopened with { WarmupCooldown = preparation, Shortcuts = reopened.Shortcuts.Where(item => item.Id != explicitId).ToArray() });

        // Act
        var durationSaved = CorePreferences.Update(current => current with { C2Selection = current.C2Selection with { ContinuousMinutes = 50 } });
        var preparationSaved = CorePreferences.Update(current => current with { WarmupCooldown = new(2, 3) });

        // Assert
        if (durationSaved.WarmupCooldown != preparation || durationSaved.Audio?.Interval != 5
            || durationSaved.Shortcuts.Any(item => item.Id == explicitId)
            || preparationSaved.C2Selection.ContinuousMinutes != 50 || preparationSaved.C2Selection.Targets != target
            || preparationSaved.Audio?.Interval != 5 || preparationSaved.Shortcuts.Count != 1)
            throw new InvalidOperationException("Intent-only saves must preserve other current settings and never restore removed shortcuts.");

        // Arrange: malformed persisted bytes must fail before the mutation or any write.
        const string corrupt = "{not valid settings";
        Microsoft.Maui.Storage.Preferences.Default.Set("core-preferences-v1", corrupt);
        var mutationCalled = false;
        Exception? failure = null;

        // Act
        try { _ = CorePreferences.Update(current => { mutationCalled = true; return current with { Audio = new() }; }); }
        catch (InvalidDataException problem) { failure = problem; }

        // Assert
        if (failure is null || mutationCalled || Microsoft.Maui.Storage.Preferences.Default.Get("core-preferences-v1", "") != corrupt)
            throw new InvalidOperationException("Unreadable settings must retain their exact bytes without mutation/default overwrite.");

        Console.WriteLine("PASS: real Core preferences write boundary preserves saved audio/current fields with a stale view, frozen plans and corrupt bytes.");
    }
}

// Host-only replacement of the MAUI key/value boundary; production CorePreferences is linked unchanged.
namespace Microsoft.Maui.Storage
{
    internal static class Preferences
    {
        internal static MemoryPreferences Default { get; } = new();
    }

    internal sealed class MemoryPreferences
    {
        private readonly Dictionary<string, string> values = [];
        public string Get(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        public void Set(string key, string value) => values[key] = value;
    }
}
