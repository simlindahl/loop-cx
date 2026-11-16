namespace LoopCx.Features.GhostRun;

// One local request. Cancellation and service acceptance share this lock, without any native I/O inside it.
public sealed class GhostStartAdmission : IDisposable
{
    private readonly object gate = new();
    private readonly CancellationToken cancellation;
    private readonly CancellationTokenRegistration registration;
    private readonly TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool accepted, finished;

    public GhostStartAdmission(CancellationToken cancellation)
    {
        this.cancellation = cancellation;
        registration = cancellation.Register(Cancel);
    }

    public Task<string?> Completion => completion.Task;
    public bool Pending { get { lock (gate) return !accepted && !finished && !cancellation.IsCancellationRequested; } }

    private void Cancel()
    {
        lock (gate)
        {
            if (accepted || finished) return;
            finished = true;
            completion.TrySetCanceled(cancellation);
        }
    }

    public bool TryAccept()
    {
        lock (gate)
        {
            if (accepted || finished) return false;
            if (cancellation.IsCancellationRequested)
            {
                finished = true;
                completion.TrySetCanceled(cancellation);
                return false;
            }
            accepted = true;
            return true;
        }
    }

    public void Complete(string? error)
    {
        lock (gate)
        {
            if (finished) return;
            finished = true;
            completion.TrySetResult(error);
        }
    }

    public void Dispose() => registration.Dispose();
}
