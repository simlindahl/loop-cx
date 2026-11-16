using System.Globalization;
using LoopCx.Features.ActiveRun;

namespace LoopCx.Features.GhostRun;

// Text only. Clock, nullable comparison and uncertainty come from the same service snapshot.
public sealed record GhostNotificationText(string Title, string Text, string ExpandedText)
{
    public static GhostNotificationText Create(GhostRaceSnapshot race, RunSaveStatus? storage)
    {
        ArgumentNullException.ThrowIfNull(race);
        var elapsed = race.Clock.RaceElapsed;
        var title = string.Create(CultureInfo.InvariantCulture, $"Ghost Run · {(long)elapsed.TotalMinutes}:{elapsed.Seconds:00}");
        var terminal = race.Clock.State != GhostClockState.Running;
        var comparison = race.ReferenceId is null ? "No ghost · setting a first time"
            : race.Gap.SecondsAhead is { } seconds && double.IsFinite(seconds) ? Difference(seconds, "sec") : "Comparison unavailable";
        var detail = race.RouteName + "\n" + comparison;

        if (race.ReferenceId is not null && race.Gap.MetresAhead is { } metres && double.IsFinite(metres))
            detail += metres == 0 ? "\n≈ 0 m along route" : "\n≈ " + Difference(metres, "m") + " along route";

        if (race.ReferenceId is not null && comparison == "Comparison unavailable")
            detail += race.Match.Problem == RouteMatchProblem.OffRoute ? "\nOff route · rejoin where you left the route" : "\nGPS position or comparison support unavailable";

        if (race.Clock.TrainingPaused && !terminal)
            detail += "\nTraining paused · race clock continues";

        var text = storage?.Error is not null
            ? terminal ? "Final save not confirmed · open LOOP Cx" : "Recent recording could not be saved · open LOOP Cx"
            : terminal ? storage?.State == "saved" ? "Saved locally" : "Saving recording…" : comparison;

        return new(title, text, text == comparison ? detail : text + "\n" + detail);
    }

    private static string Difference(double value, string unit)
    {
        return value == 0 ? "Level" : string.Create(CultureInfo.InvariantCulture, $"{Math.Abs(value):0} {unit} {(value > 0 ? "ahead" : "behind")}");
    }
}
