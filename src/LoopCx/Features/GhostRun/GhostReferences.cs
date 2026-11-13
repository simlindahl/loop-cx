namespace LoopCx.Features.GhostRun;

public sealed record GhostRecords(int FrozenYear, GhostAttempt? YearBest, GhostAttempt? PersonalBest)
{
    public bool SameReference => YearBest is not null && YearBest.Id == PersonalBest?.Id;
    public GhostAttempt? ReferenceFor(GhostMode mode) => mode switch
    {
        GhostMode.YearBest => YearBest,
        GhostMode.PersonalBest => PersonalBest,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}

public static class GhostReferences
{
    public static GhostRecords Select(GhostRoute route, IEnumerable<GhostAttempt> attempts, int frozenYear)
    {
        if (frozenYear is < 1 or > 9999) throw new ArgumentOutOfRangeException(nameof(frozenYear));
        var unique = new Dictionary<Guid, GhostAttempt>();
        foreach (var attempt in attempts.Where(a => a.Route == route.Key))
        {
            if (unique.TryGetValue(attempt.SourceRunId, out var saved))
            {
                if (!saved.SameRecording(attempt))
                    throw new ArgumentException("Conflicting copies of one source recording cannot replace a record.");
            }
            else unique.Add(attempt.SourceRunId, attempt);
        }
        var eligible = unique.Values.Where(a => a.Quality == AttemptQuality.Verified &&
            a.RuleVersion == route.RuleVersion &&
            a.Points[0] is { Known: true, Metres: 0 } && a.Points[0].Elapsed == TimeSpan.Zero &&
            a.Points[^1].Known && a.Points[^1].Metres == route.LengthMetres &&
            a.Points[^1].Elapsed == a.Elapsed && a.Points.All(p => p.Metres <= route.LengthMetres))
            .OrderBy(a => a.Elapsed).ThenBy(a => a.StartedLocal.UtcDateTime).ThenBy(a => a.Id).ToArray();
        return new GhostRecords(frozenYear,
            eligible.FirstOrDefault(a => a.SavedLocalYear == frozenYear), eligible.FirstOrDefault());
    }
}

/// <summary>Uses recorded race elapsed time, never an active training clock or constant pace.</summary>
public sealed class GhostPlayback
{
    public GhostPlayback(GhostAttempt reference, TimeSpan maxInterpolationGap)
    {
        if (maxInterpolationGap <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxInterpolationGap));
        Reference = reference; MaxInterpolationGap = maxInterpolationGap;
    }

    public GhostAttempt Reference { get; }
    public TimeSpan MaxInterpolationGap { get; }

    public double? PositionAt(TimeSpan raceElapsed)
    {
        if (raceElapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(raceElapsed));
        var points = Reference.Points;
        if (raceElapsed < points[0].Elapsed) return null;
        if (raceElapsed >= Reference.Elapsed)
            return points[^1].Known && points[^1].Elapsed == Reference.Elapsed ? points[^1].Metres : null;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Elapsed == raceElapsed) return points[i].Known ? points[i].Metres : null;
            if (i == 0 || points[i].Elapsed < raceElapsed) continue;
            var a = points[i - 1]; var b = points[i];
            if (!CanInterpolate(a, b)) return null;
            var fraction = (raceElapsed - a.Elapsed).TotalSeconds / (b.Elapsed - a.Elapsed).TotalSeconds;
            return a.Metres + fraction * (b.Metres - a.Metres);
        }
        return null;
    }

    // First verified arrival, including the beginning of a recorded stop.
    public TimeSpan? ArrivalAt(double metres)
    {
        if (!double.IsFinite(metres) || metres < 0) throw new ArgumentOutOfRangeException(nameof(metres));
        var points = Reference.Points;
        if (!points[0].Known || points[0].Elapsed != TimeSpan.Zero || points[0].Metres != 0) return null;
        if (points[0].Metres == metres) return points[0].Elapsed;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i];
            // An earlier unknown interval may hide the first arrival. Do not guess.
            if (!CanInterpolate(a, b)) return null;
            if (a.Metres < metres && b.Metres >= metres)
            {
                var fraction = (metres - a.Metres) / (b.Metres - a.Metres);
                return a.Elapsed + TimeSpan.FromTicks((long)Math.Round((b.Elapsed - a.Elapsed).Ticks * fraction));
            }
        }
        return null;
    }

    public GhostGap Compare(TimeSpan raceElapsed, double? runnerMetres)
    {
        if (raceElapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(raceElapsed));
        if (runnerMetres is null) return new(null, null);
        if (!double.IsFinite(runnerMetres.Value) || runnerMetres.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(runnerMetres));
        var ghostMetres = PositionAt(raceElapsed);
        if (ghostMetres is null) return new(null, null);
        var arrival = ArrivalAt(runnerMetres.Value);
        return new(ghostMetres is null ? null : runnerMetres.Value - ghostMetres,
            arrival is null ? null : (arrival.Value - raceElapsed).TotalSeconds);
    }

    private bool CanInterpolate(GhostPoint a, GhostPoint b) =>
        a.Known && b.Known && b.Elapsed - a.Elapsed <= MaxInterpolationGap;
}

public sealed record GhostGap(double? MetresAhead, double? SecondsAhead);
