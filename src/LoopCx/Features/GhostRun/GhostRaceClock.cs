namespace LoopCx.Features.GhostRun;

public enum GhostClockState { Running, Finished, Incomplete, Interrupted }
public sealed record TrainingPause(TimeSpan StartedAt, TimeSpan? EndedAt);
public sealed record GhostClockSnapshot(TimeSpan RaceElapsed, TimeSpan TrainingActive,
    bool TrainingPaused, GhostClockState State);

/// <summary>Keeps race elapsed time independent of training pause and UI observation frequency.</summary>
public sealed class GhostRaceClock
{
    private readonly TimeSpan startedAt;
    private TimeSpan observedAt;
    private TimeSpan? endedAt;
    private GhostClockState state = GhostClockState.Running;
    private readonly List<TrainingPause> pauses = [];

    public GhostRaceClock(TimeSpan startedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(startedAt, TimeSpan.Zero);
        this.startedAt = observedAt = startedAt;
    }

    public IReadOnlyList<TrainingPause> Pauses => Array.AsReadOnly(pauses.ToArray());

    public GhostClockSnapshot Current
    {
        get
        {
            var end = endedAt ?? observedAt;
            var elapsed = end - startedAt;
            var paused = TimeSpan.Zero;
            foreach (var pause in pauses)
            {
                if (pause.StartedAt >= end) continue;
                var until = pause.EndedAt is { } resumed && resumed < end ? resumed : end;
                paused += until - pause.StartedAt;
            }
            var trainingPaused = pauses.Any(p => p.StartedAt <= end && (p.EndedAt is null || p.EndedAt > end));
            return new(elapsed, elapsed - paused, trainingPaused, state);
        }
    }

    public GhostClockSnapshot Observe(TimeSpan monotonicNow)
    {
        if (state != GhostClockState.Running) return Current;
        if (monotonicNow < observedAt) throw new ArgumentOutOfRangeException(nameof(monotonicNow), "The monotonic clock cannot go backwards.");
        observedAt = monotonicNow;
        return Current;
    }

    public GhostClockSnapshot PauseTraining(TimeSpan monotonicNow)
    {
        Observe(monotonicNow);
        if (state == GhostClockState.Running && (pauses.Count == 0 || pauses[^1].EndedAt is not null))
            pauses.Add(new(monotonicNow, null));
        return Current;
    }

    public GhostClockSnapshot ResumeTraining(TimeSpan monotonicNow)
    {
        Observe(monotonicNow);
        if (state == GhostClockState.Running && pauses.Count > 0 && pauses[^1].EndedAt is null)
            pauses[^1] = pauses[^1] with { EndedAt = monotonicNow };
        return Current;
    }

    /// <summary>Freezes at the matcher's observed finish, which may arrive after a newer timer tick.</summary>
    public GhostClockSnapshot CompleteAt(TimeSpan monotonicFinish)
    {
        if (state != GhostClockState.Running) return Current;
        ArgumentOutOfRangeException.ThrowIfLessThan(monotonicFinish, startedAt);
        if (monotonicFinish > observedAt) Observe(monotonicFinish);
        endedAt = monotonicFinish;
        state = GhostClockState.Finished;
        return Current;
    }

    public GhostClockSnapshot EndAttempt(TimeSpan monotonicNow)
    {
        Observe(monotonicNow);
        return StopAtLastObservation(GhostClockState.Incomplete);
    }

    public GhostClockSnapshot InterruptAtLastObservation() =>
        StopAtLastObservation(GhostClockState.Interrupted);

    private GhostClockSnapshot StopAtLastObservation(GhostClockState reason)
    {
        if (state == GhostClockState.Running)
        {
            endedAt = observedAt;
            state = reason;
        }
        return Current;
    }
}
