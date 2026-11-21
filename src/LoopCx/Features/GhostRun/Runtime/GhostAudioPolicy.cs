using System.Globalization;
using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.GhostRun;

internal sealed record GhostAudioCue(Guid Owner, AudioCueKind Kind, string Text, Guid? Reference = null);

internal sealed class GhostAudioPolicy
{
    private readonly Guid owner;
    private readonly RunAudioPolicy? comparisonCadence;
    private GhostAudioCue? pending;
    private bool offRouteEpisode;

    public GhostAudioPolicy(Guid owner, AudioConfiguration? comparison = null)
    {
        this.owner = owner;
        comparisonCadence = comparison is not null ? new(comparison) : null;
    }

    public GhostAudioCue? Observe(GhostRaceSnapshot race, bool fresh, double measuredMetres)
    {
        if (race.Id != owner || race.Clock.State != GhostClockState.Running)
        {
            pending = null;
            return null;
        }

        var paused = IsPaused(race);
        var due = comparisonCadence?.Observe(0, RunPhase.Work, paused, race.Clock.TrainingActive, measuredMetres);
        var offRoute = IsOffRoute(race, fresh);
        var newDeparture = offRoute && !offRouteEpisode;

        // An uncertain gap alone does not prove the runner rejoined.
        if (offRoute || IsTracking(race, fresh))
        {
            offRouteEpisode = offRoute;
        }

        if (pending is { } cue && !IsCurrent(cue, race, fresh))
        {
            pending = null;
        }

        if (paused)
        {
            return null;
        }

        if (newDeparture)
        {
            return pending = new(owner, AudioCueKind.Coaching, "Off route. Rejoin where you left the route.");
        }

        if (due is { PhasePriority: false } && ComparisonText(race, fresh) is { } text)
        {
            return pending = new(owner, AudioCueKind.Summary, text, race.ReferenceId);
        }

        return null;
    }

    public bool IsCurrent(GhostAudioCue cue, GhostRaceSnapshot race, bool fresh)
    {
        if (!ReferenceEquals(pending, cue) || race.Id != cue.Owner
            || race.Clock.State != GhostClockState.Running || IsPaused(race))
        {
            return false;
        }

        return cue.Kind == AudioCueKind.Coaching ? IsOffRoute(race, fresh)
            : cue.Reference == race.ReferenceId && ComparisonText(race, fresh) == cue.Text;
    }

    private static bool IsPaused(GhostRaceSnapshot race)
    {
        return race.Clock.TrainingPaused || race.PauseReason != PauseReason.None;
    }

    private static bool IsOffRoute(GhostRaceSnapshot race, bool fresh)
    {
        return fresh && race.Match is { State: RouteMatchState.Uncertain, Problem: RouteMatchProblem.OffRoute };
    }

    private static bool IsTracking(GhostRaceSnapshot race, bool fresh)
    {
        return fresh && race.Match is { State: RouteMatchState.Tracking, Metres: not null, Problem: null };
    }

    private static string? ComparisonText(GhostRaceSnapshot race, bool fresh)
    {
        if (!IsTracking(race, fresh) || race.ReferenceId is null
            || race.Gap.SecondsAhead is not { } seconds || !double.IsFinite(seconds))
        {
            return null;
        }

        if (seconds == 0) return "Level.";

        var direction = seconds > 0 ? "ahead" : "behind";
        var magnitude = Math.Abs(seconds);
        if (magnitude < 1) return $"Less than one second {direction}.";

        var rounded = Math.Round(magnitude, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{rounded:0} {(rounded == 1 ? "second" : "seconds")} {direction}.");
    }
}
