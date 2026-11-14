namespace LoopCx.Features.ActiveRun;

public sealed record MeasuredStatistics(TimeSpan ActiveTime, double MeasuredMetres, TimeSpan KnownGpsActiveTime,
    TimeSpan UnknownGpsActiveTime)
{
    public double? AveragePaceSecondsPerKilometre => UnknownGpsActiveTime == TimeSpan.Zero && MeasuredMetres > 0
        ? ActiveTime.TotalSeconds * 1000 / MeasuredMetres : null;
    public double? AverageHeartRate { get; }
    public TimeSpan KnownHeartRateActiveTime { get; }
    public TimeSpan UnknownHeartRateActiveTime => ActiveTime;
}

public sealed record RunStatisticsState(int Version, DistancePolicy Policy, TimeSpan ObservedElapsed,
    MeasuredStatistics Total, MeasuredStatistics Main, IReadOnlyList<MeasuredStatistics> CompletedSplits,
    MeasuredStatistics? CurrentSplit, bool HasSyntheticTime);

/// <summary>Union of supported GPS brackets intersected with compact active intervals; no extrapolation.</summary>
public sealed class RunStatistics
{
    private bool hasSyntheticTime;
    private readonly List<Interval> known = [];
    private readonly List<Interval> excluded = [];
    private readonly List<ActiveInterval> active = [];

    private readonly record struct Interval(long Start, long End);
    private readonly record struct ActiveInterval(long Start, long End, bool Main);

    // The sole service clock supplies chronological non-overlapping pieces, compacted at state boundaries.
    public void Advance(TimeSpan from, TimeSpan to, RunPhase? phase, bool paused, bool synthetic = false)
    {
        if (paused || phase is null || to <= from) return;

        hasSyntheticTime |= synthetic;
        var main = phase is RunPhase.Work or RunPhase.Recovery;
        var interval = new ActiveInterval(from.Ticks, to.Ticks, main);

        if (active.Count > 0 && active[^1].End == interval.Start && active[^1].Main == main)
        {
            active[^1] = active[^1] with { End = interval.End };
        }
        else
        {
            active.Add(interval);
        }
    }

    public void Observe(DistanceObservation observation)
    {
        if (observation.Valid && observation.PreviousMonotonicTime is { } previous)
        {
            Union(known, previous.Ticks, observation.MonotonicTime.Ticks);
        }
    }

    public void Exclude(TimeSpan from, TimeSpan to) => Union(excluded, from.Ticks, to.Ticks);

    public RunStatisticsState Snapshot(TimeSpan elapsed, DistanceState distance, bool mainRunning)
    {
        var totalTime = ActiveTicks(false);
        var mainTime = ActiveTicks(true);
        var splits = new List<MeasuredStatistics>();
        long cursor = 0;

        foreach (var split in distance.Splits)
        {
            var end = cursor + split.ActiveTime.Ticks;
            splits.Add(Scope(true, cursor, end, split.MeasuredMetres));
            cursor = end;
        }

        var remainingMetres = Math.Max(0, distance.MainMeasuredMetres - distance.Splits.Sum(static split => split.MeasuredMetres));
        var current = mainRunning || mainTime > cursor ? Scope(true, cursor, mainTime, remainingMetres) : null;

        return new(1, distance.Policy, elapsed, Scope(false, 0, totalTime, distance.TotalMeasuredMetres),
            Scope(true, 0, mainTime, distance.MainMeasuredMetres), splits, current, hasSyntheticTime);
    }

    private long ActiveTicks(bool mainOnly) => Pieces(mainOnly).Sum(static interval => interval.End - interval.Start);

    private MeasuredStatistics Scope(bool mainOnly, long start, long end, double metres)
    {
        long cursor = 0;
        long covered = 0;

        foreach (var piece in Pieces(mainOnly))
        {
            var length = piece.End - piece.Start;
            var lower = Math.Max(start, cursor);
            var upper = Math.Min(end, cursor + length);

            if (upper > lower)
            {
                var from = piece.Start + lower - cursor;
                var to = piece.Start + upper - cursor;

                foreach (var bracket in known)
                {
                    covered += Math.Max(0, Math.Min(to, bracket.End) - Math.Max(from, bracket.Start));
                }
            }

            cursor += length;
        }

        var ticks = end - start;

        return new(TimeSpan.FromTicks(ticks), metres, TimeSpan.FromTicks(covered), TimeSpan.FromTicks(ticks - covered));
    }

    private IEnumerable<Interval> Pieces(bool mainOnly)
    {
        foreach (var interval in active)
        {
            if (mainOnly && !interval.Main) continue;

            var cursor = interval.Start;

            foreach (var removal in excluded)
            {
                if (removal.End <= cursor) continue;
                if (removal.Start >= interval.End) break;

                if (removal.Start > cursor) yield return new(cursor, Math.Min(removal.Start, interval.End));

                cursor = Math.Max(cursor, removal.End);
                if (cursor >= interval.End) break;
            }

            if (cursor < interval.End) yield return new(cursor, interval.End);
        }
    }

    private static void Union(List<Interval> intervals, long start, long end)
    {
        if (end <= start) return;

        var index = 0;

        while (index < intervals.Count && intervals[index].End < start) index++;

        while (index < intervals.Count && intervals[index].Start <= end)
        {
            start = Math.Min(start, intervals[index].Start);
            end = Math.Max(end, intervals[index].End);
            intervals.RemoveAt(index);
        }

        intervals.Insert(index, new(start, end));
    }
}
