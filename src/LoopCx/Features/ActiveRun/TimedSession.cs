namespace LoopCx.Features.ActiveRun;

public enum RunPhase { Warmup, Work, Recovery, Cooldown }
public sealed record TimedPhase(RunPhase Kind, TimeSpan Duration, int TargetRpe);
public sealed record CompletedPhase(TimedPhase Plan, TimeSpan ActiveTime);

/// <summary>Owns timed phase transitions independently of Android and the UI.</summary>
public sealed class TimedSession
{
    private readonly List<TimedPhase> phases;
    private readonly List<CompletedPhase> completed = [];
    private int index;

    public TimedSession(IEnumerable<TimedPhase> plan)
    {
        phases = plan.ToList();
        if (phases.Count == 0 || phases.Any(p => p.Duration <= TimeSpan.Zero ||
            p.TargetRpe is < 1 or > 10))
            throw new ArgumentException("Every phase needs positive duration and target RPE 1–10.", nameof(plan));
    }

    public IReadOnlyList<TimedPhase> Plan => phases.AsReadOnly();
    public IReadOnlyList<CompletedPhase> Completed => completed.AsReadOnly();
    public TimedPhase? Current => IsFinished ? null : phases[index];
    public bool IsFinished => index == phases.Count;
    public bool IsPaused { get; private set; }
    public TimeSpan PhaseActiveTime { get; private set; }
    public TimeSpan TotalActiveTime { get; private set; }
    public TimeSpan MainActiveTime { get; private set; }
    public TimeSpan Remaining => Current is { } phase ? phase.Duration - PhaseActiveTime : TimeSpan.Zero;

    // The caller supplies elapsed monotonic time, allowing deterministic simulation.
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (IsPaused) return;
        while (elapsed > TimeSpan.Zero && Current is { } phase)
        {
            var consumed = elapsed < Remaining ? elapsed : Remaining;
            PhaseActiveTime += consumed;
            TotalActiveTime += consumed;
            if (phase.Kind is RunPhase.Work or RunPhase.Recovery)
                MainActiveTime += consumed;
            elapsed -= consumed;
            if (Remaining == TimeSpan.Zero) FinishPhase();
        }
    }

    public bool Pause()
    {
        if (IsFinished || IsPaused) return false;
        IsPaused = true;
        return true;
    }

    public bool Resume()
    {
        if (!IsPaused) return false;
        IsPaused = false;
        return true;
    }

    public bool Extend()
    {
        if (Current is { Kind: RunPhase.Warmup or RunPhase.Cooldown } phase)
        {
            phases[index] = phase with { Duration = phase.Duration + TimeSpan.FromMinutes(1) };
            return true;
        }
        return false;
    }

    public bool SkipPreparation()
    {
        if (Current?.Kind is not (RunPhase.Warmup or RunPhase.Cooldown)) return false;
        FinishPhase();
        return true;
    }

    private void FinishPhase()
    {
        completed.Add(new CompletedPhase(phases[index], PhaseActiveTime));
        index++;
        PhaseActiveTime = TimeSpan.Zero;
        if (IsFinished) IsPaused = false;
    }

    public static TimedSession Continuous(TimeSpan duration, int targetRpe) =>
        new([new TimedPhase(RunPhase.Work, duration, targetRpe)]);

    public static TimedSession Intervals(int repetitions, TimeSpan work, TimeSpan recovery,
        int workRpe, int recoveryRpe, TimeSpan warmup, TimeSpan cooldown)
    {
        if (repetitions < 1 || warmup < TimeSpan.Zero || cooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(repetitions));
        var plan = new List<TimedPhase>();
        if (warmup > TimeSpan.Zero) plan.Add(new(RunPhase.Warmup, warmup, recoveryRpe));
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            plan.Add(new(RunPhase.Work, work, workRpe));
            if (repetition < repetitions - 1)
                plan.Add(new(RunPhase.Recovery, recovery, recoveryRpe));
        }
        if (cooldown > TimeSpan.Zero) plan.Add(new(RunPhase.Cooldown, cooldown, recoveryRpe));
        return new TimedSession(plan);
    }
}
