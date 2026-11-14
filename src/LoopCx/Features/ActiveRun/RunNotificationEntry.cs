namespace LoopCx.Features.ActiveRun;

// A destination request only: never starts, restores or changes a recording.
public static class RunNotificationEntry
{
    public const string Action = "se.loopcx.running.OPEN_RUN";
    public const string RunIdExtra = "notification-run-id";
    private static Guid? pending;
    public static event Action? Changed;

    public static void Request(Guid requested, Guid? current)
    {
        if (requested == Guid.Empty || requested != current) return;
        pending = requested;
        Changed?.Invoke();
    }

    public static bool Consume(Guid? current)
    {
        var requested = pending;
        pending = null;
        return current is not null && requested == current;
    }
}
