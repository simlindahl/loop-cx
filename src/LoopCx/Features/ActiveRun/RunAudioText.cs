using System.Globalization;

namespace LoopCx.Features.ActiveRun;

public static class RunAudioText
{
    private static string Time(TimeSpan value)
    {
        return $"{(long)value.TotalMinutes} {((long)value.TotalMinutes == 1 ? "minute" : "minutes")} {value.Seconds} {(value.Seconds == 1 ? "second" : "seconds")}";
    }

    private static string Number(double value)
    {
        return Math.Round(value).ToString(CultureInfo.InvariantCulture);
    }

    private static string Pace(double? value)
    {
        return value is > 0 && double.IsFinite(value.Value)
            ? Time(TimeSpan.FromSeconds(Math.Round(value.Value))) + " per kilometre" : "unavailable";
    }

    private static string HeartRate(double? value)
    {
        return value is > 0 && double.IsFinite(value.Value)
            ? Number(value.Value) + " beats per minute" : "unavailable";
    }

    private static string Scope(string name, MeasuredStatistics? value)
    {
        return value is null
            ? $"{name}. Active time unavailable. Recorded distance unavailable. Average pace unavailable. Average heart rate unavailable."
            : $"{name}. Active time {Time(value.ActiveTime)}. Recorded distance "
                + (value.KnownGpsActiveTime > TimeSpan.Zero || value.MeasuredMetres > 0 ? Number(value.MeasuredMetres) + " metres" : "unavailable")
                + $". Average pace {Pace(value.AveragePaceSecondsPerKilometre)}. Average heart rate {HeartRate(value.AverageHeartRate)}.";
    }

    public static string Summary(MeasuredStatistics? main, MeasuredStatistics? split, double? livePace, double? freshHeartRate)
    {
        return Scope("Main set", main) + " " + Scope("Current split", split)
        + $" Live pace {Pace(livePace)}. Heart rate {HeartRate(freshHeartRate)}.";
    }

    public static string Phase(RunPhase? phase, TimeSpan remaining, int repetition, int repetitions)
    {
        return phase switch
        {
            RunPhase.Warmup => $"Warm-up. {Time(remaining)} remaining.",
            RunPhase.Work => $"Work, {repetition} of {repetitions}. {Time(remaining)} remaining.",
            RunPhase.Recovery => $"Recovery. {Time(remaining)} remaining.",
            RunPhase.Cooldown => $"Cool-down. {Time(remaining)} remaining.",
            _ => "Training finished."
        };
    }
}
