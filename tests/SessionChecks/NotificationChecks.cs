using LoopCx.Features.ActiveRun;

internal static class NotificationChecks
{
    public static void Run()
    {
        RunningNotificationShowsPhaseCountdownAndMeasuredValues();
        PausedNotificationRetainsFreshHeartRateAndPauseGuidance();
        MissingOrStaleMeasurementsStayUnavailable();
    }

    private static void RunningNotificationShowsPhaseCountdownAndMeasuredValues()
    {
        // Arrange
        const double secondsPerKilometre = 359.6;

        // Act
        var warmup = RunNotificationText.Create("Warmup", 65, PauseReason.None, 142, secondsPerKilometre);
        var cooldown = RunNotificationText.Create("Cooldown", 3600, PauseReason.None, null, null);

        // Assert
        Require(warmup.Title == "Warm-up · 1:05 remaining", "Warm-up uses a readable phase and minutes:seconds.");
        Require(warmup.Text == "HR 142 bpm · Pace 6:00 min/km", "Collapsed text includes both actual values with units and second carry.");
        Require(warmup.ExpandedText == warmup.Text, "Expanded running text preserves the same measurements.");
        Require(cooldown.Title == "Cool-down · 60:00 remaining", "Long durations remain unambiguous minutes:seconds.");
    }

    private static void PausedNotificationRetainsFreshHeartRateAndPauseGuidance()
    {
        // Arrange
        const int freshBpm = 140;

        // Act
        var automatic = RunNotificationText.Create("Work", 123, PauseReason.Automatic, freshBpm, 300);
        var manual = RunNotificationText.Create("Recovery", 45, PauseReason.Manual, freshBpm, 300);

        // Assert
        Require(automatic.Title == "Autopaused" && automatic.PhaseCountdown == "Work · 2:03 remaining",
            "Automatic pause retains the phase and frozen countdown.");
        Require(automatic.ExpandedText.Contains("HR 140 bpm · Pace unavailable", StringComparison.Ordinal), "Pause does not hide actual fresh HR or show old running pace.");
        Require(automatic.Text == "Move to resume automatically.", "Automatic guidance remains in collapsed text.");
        Require(manual.Title == "Manually paused" && manual.Text == "Use Resume to continue.",
            "Manual pause retains its separate command guidance.");
    }

    private static void MissingOrStaleMeasurementsStayUnavailable()
    {
        // Arrange
        var heartRate = new RunHeartRate(HeartRateConfiguration.Empty);
        _ = heartRate.Observe(new(TimeSpan.FromSeconds(1), DateTimeOffset.UnixEpoch, 150, "valid"));
        var stale = heartRate.Snapshot(TimeSpan.FromSeconds(20));

        // Act
        var notification = RunNotificationText.Create("Work", 10, PauseReason.None, stale.CurrentBpm, null);
        var invalid = RunNotificationText.Create("Work", 10, PauseReason.None, null, double.NaN);

        // Assert
        Require(notification.Text == "HR unavailable · Pace unavailable", "Service-owned staleness stays unavailable, never zero or held HR.");
        Require(invalid.Text == notification.Text, "Invalid optional measurements never become a displayed pace.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
