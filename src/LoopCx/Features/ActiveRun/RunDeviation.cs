namespace LoopCx.Features.ActiveRun;

public enum AudioCueKind { Summary, Coaching, Phase }

public static class AudioPriority
{
    public static bool CanReplace(AudioCueKind incoming, AudioCueKind? current)
    {
        return current is null || incoming == AudioCueKind.Phase || incoming > current;
    }
}

public enum DeviationDirection { Faster, Slower, BelowZone, AboveZone }

public sealed record CoachingCue(long Generation, DeviationDirection Direction)
{
    public string Text => Direction switch
    {
        DeviationDirection.Faster => "Your pace is faster than the selected target. Ease back towards your planned pace.",
        DeviationDirection.Slower => "Your pace is slower than the selected target. Check your planned pace.",
        DeviationDirection.BelowZone => "Heart rate is below the selected zone. Stay with your planned effort.",
        DeviationDirection.AboveZone => "Heart rate is above the selected zone. Ease your effort if needed.",
        _ => throw new ArgumentOutOfRangeException(nameof(Direction))
    };
}

public sealed record DeviationObservation(int PhaseIndex, int SplitNumber, RunPhase? Phase, bool Paused,
    TimeSpan TotalActive, TimeSpan Now, CoreTargets? Targets, LivePaceState? Pace, HeartRateState? HeartRate);

// Confirmation uses the recording owner's corrected active counter; sensor timestamps prove continuity.
public sealed class RunDeviation
{
    private sealed record Key(int Phase, int Split, CoreTargetMode Mode, int Target, DeviationDirection Direction);
    private sealed record Supported(Key Key, TimeSpan Sample, TimeSpan Until);
    private Key? key;
    private TimeSpan startActive, lastActive, lastSample, supportedUntil;
    private long generation;
    private bool attempted;

    public CoachingCue? Observe(DeviationObservation input)
    {
        var current = Condition(input);

        if (current is null)
        {
            Reset();
            return null;
        }

        if (key != current.Key || input.TotalActive < lastActive || current.Sample < lastSample || current.Sample > supportedUntil)
        {
            Reset();
            key = current.Key;
            startActive = input.TotalActive;
        }

        lastActive = input.TotalActive;
        lastSample = current.Sample;
        supportedUntil = current.Until;

        return !attempted && input.TotalActive - startActive >= TimeSpan.FromSeconds(30)
            ? new(generation, current.Key.Direction) : null;
    }

    public void Attempted(CoachingCue cue)
    {
        if (cue.Generation == generation && key is not null) attempted = true;
    }

    public bool IsCurrent(CoachingCue cue, DeviationObservation input)
    {
        var current = Condition(input);

        return cue.Generation == generation && current is not null && current.Key == key
            && input.TotalActive >= lastActive && current.Sample >= lastSample && current.Sample <= supportedUntil;
    }

    private void Reset()
    {
        generation++;
        key = null;
        attempted = false;
    }

    private static Supported? Condition(DeviationObservation input)
    {
        if (input.Paused || input.Phase is not (RunPhase.Work or RunPhase.Recovery) || input.TotalActive < TimeSpan.Zero) return null;
        var target = input.Phase == RunPhase.Recovery ? input.Targets?.Recovery : input.Targets?.Work;
        if (target is not > 0) return null;
        DeviationDirection direction;
        TimeSpan sample, age;

        if (input.Targets!.Mode == CoreTargetMode.Pace && input.Pace is { Quality: "tracking", SecondsPerKilometre: > 0, LastAcceptedAt: { } paceAt } pace
            && double.IsFinite(pace.SecondsPerKilometre.Value))
        {
            var measured = pace.SecondsPerKilometre.Value;
            if (measured * 10 < target.Value * 9d) direction = DeviationDirection.Faster;
            else if (measured * 10 > target.Value * 11d) direction = DeviationDirection.Slower;
            else return null;
            sample = paceAt;
            age = pace.Policy.MaximumAge;
        }
        else if (input.Targets.Mode == CoreTargetMode.HeartRateZone && input.HeartRate is { Quality: "valid", CurrentBpm: > 0, LastReceiptMonotonicTime: { } hrAt } hr)
        {
            var zone = hr.Configuration.Zones.FirstOrDefault(zone => zone.Number == target);
            if (zone is null) return null;
            if (hr.CurrentBpm < zone.MinimumBpm) direction = DeviationDirection.BelowZone;
            else if (hr.CurrentBpm > zone.MaximumBpm) direction = DeviationDirection.AboveZone;
            else return null;
            sample = hrAt;
            age = hr.Configuration.Policy.MaximumGap;
        }
        else
        {
            return null;
        }

        return age <= TimeSpan.Zero || input.Now < sample || input.Now - sample > age
            ? null
            : new(new(input.PhaseIndex, input.SplitNumber, input.Targets.Mode, target.Value, direction), sample, sample + age);
    }
}
