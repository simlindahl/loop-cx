using System.Globalization;

namespace LoopCx.Features.ActiveRun;

// Presentation only: freshness and pause accounting belong to the service snapshot.
public sealed record RunNotificationText(string Title, string Text, string ExpandedText, string PhaseCountdown)
{
    public static RunNotificationText Create(string phase, long remainingSeconds, PauseReason pauseReason,
        int? currentBpm, double? liveSecondsPerKilometre)
    {
        var phaseLabel = phase switch
        {
            "Warmup" => "Warm-up",
            "Cooldown" => "Cool-down",
            _ => phase
        };
        var remaining = Math.Max(0, remainingSeconds);
        var countdown = string.Create(CultureInfo.InvariantCulture, $"{remaining / 60}:{remaining % 60:00} remaining");
        var phaseCountdown = $"{phaseLabel} · {countdown}";
        var heartRate = currentBpm is > 0 ? currentBpm.Value.ToString(CultureInfo.InvariantCulture) + " bpm" : "unavailable";
        var pace = liveSecondsPerKilometre is { } seconds && double.IsFinite(seconds) && seconds > 0
            && seconds <= int.MaxValue && pauseReason == PauseReason.None
            ? Pace(seconds) : "unavailable";
        var measurements = $"HR {heartRate} · Pace {pace}";
        var title = pauseReason switch
        {
            PauseReason.Automatic => "Autopaused",
            PauseReason.Manual => "Manually paused",
            PauseReason.None => phaseCountdown,
            _ => phaseCountdown
        };
        var guidance = pauseReason switch
        {
            PauseReason.Automatic => "Move to resume automatically.",
            PauseReason.Manual => "Use Resume to continue.",
            PauseReason.None => null,
            _ => null
        };

        return new(title, guidance ?? measurements, guidance is null ? measurements : $"{guidance}\n{measurements}", phaseCountdown);
    }

    private static string Pace(double seconds)
    {
        var rounded = (long)Math.Round(seconds, MidpointRounding.AwayFromZero);

        return string.Create(CultureInfo.InvariantCulture, $"{rounded / 60}:{rounded % 60:00} min/km");
    }
}
