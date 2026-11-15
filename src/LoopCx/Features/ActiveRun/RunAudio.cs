using System.Globalization;

namespace LoopCx.Features.ActiveRun;

public enum AudioSummaryTrigger { ActiveTime, Distance }
public sealed record AudioConfiguration(int Version = 1, AudioSummaryTrigger Trigger = AudioSummaryTrigger.ActiveTime, double Interval = 300)
{
    public void Validate()
    {
        if (Version != 1 || !Enum.IsDefined(Trigger) || !double.IsFinite(Interval) || Interval <= 0
            || (Trigger == AudioSummaryTrigger.ActiveTime && (Interval % 1 != 0 || Interval > int.MaxValue)))
        {
            throw new ArgumentException("Choose a positive summary interval.");
        }
    }

    public static AudioConfiguration Parse(AudioSummaryTrigger trigger, string value)
    {
        value = value.Trim();
        double interval;
        if (trigger == AudioSummaryTrigger.ActiveTime)
        {
            var parts = value.Split(':');
            if (parts.Length != 2 || parts[1].Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds > 59
                || minutes > (int.MaxValue - seconds) / 60)
            {
                throw new ArgumentException("Use a positive interval in min:ss, with seconds from 00 to 59.");
            }

            interval = (minutes * 60) + seconds;
        }
        else if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out interval))
        {
            throw new ArgumentException("Use a positive distance in metres.");
        }

        var result = new AudioConfiguration(1, trigger, interval);
        result.Validate();
        return result;
    }
}

public sealed record AudioCue(bool PhasePriority);

// Driven only by the recording owner's current state/counters. No wall clock or UI timer.
public sealed class RunAudioPolicy
{
    private readonly AudioConfiguration configuration;
    private int previousPhase = -1;
    private double consumedBoundary;
    private bool previouslyPaused;

    public RunAudioPolicy(AudioConfiguration configuration)
    {
        configuration.Validate();
        this.configuration = configuration;
    }

    public AudioCue? Observe(int phaseIndex, RunPhase? phase, bool paused, TimeSpan mainActive, double mainMetres)
    {
        var counter = configuration.Trigger == AudioSummaryTrigger.ActiveTime ? mainActive.TotalSeconds : mainMetres;
        // Remainder avoids an overflowing quotient for valid very small distance intervals.
        var boundary = double.IsFinite(counter) && counter >= configuration.Interval ? counter - (counter % configuration.Interval) : 0;
        var due = boundary > consumedBoundary;
        consumedBoundary = Math.Max(consumedBoundary, boundary); // Corrections never replay a consumed boundary.
        var changed = previousPhase != phaseIndex;
        previousPhase = phaseIndex;
        var resumed = previouslyPaused && !paused;
        previouslyPaused = paused;
        if (paused) return null;
        if (changed && phase is not null) return new(true); // A phase replaces a due summary; no backlog.
        return phase is not (RunPhase.Work or RunPhase.Recovery) || resumed ? null : due ? new(false) : null;
    }
}
