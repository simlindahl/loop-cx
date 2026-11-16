using LoopCx.Features.ActiveRun;
using Microsoft.AspNetCore.Components;

namespace LoopCx.Features.GhostRun.Ui;

public partial class GhostLivePage
{
    [Parameter, EditorRequired] public RunSnapshot State { get; set; } = null!;
    [Parameter] public bool Visible { get; set; } = true;
    [Parameter] public EventCallback OnCatalogue { get; set; }
    [Parameter] public EventCallback<RouteKey> OnRoute { get; set; }
    [Parameter] public EventCallback<Guid> OnViewRun { get; set; }
    [Parameter] public EventCallback OnTraining { get; set; }
    [Parameter] public EventCallback<Guid> OnEndRequest { get; set; }
    [Parameter] public EventCallback<Guid> OnPauseManually { get; set; }
    private GhostStoredRun? result;
    private Guid? resultOwner;
    private bool attempted, reading, disposed;
    private long generation;
    private string Heading => State.Phase == "Finished" ? State.Ghost!.Clock.State == GhostClockState.Finished ? "Race finished" : "Attempt ended"
        : State.Paused ? State.Ghost!.PauseReason == PauseReason.Automatic ? "Training autopaused" : "Training paused"
        : State.Ghost!.Match.Problem == RouteMatchProblem.OffRoute ? "Return to the route"
        : State.Ghost.Match.Metres is null ? "GPS uncertain" : "Ghost Run";
    private static string Time(TimeSpan value) => $"{(long)value.TotalMinutes}:{value.Seconds:00}";
    private static string Gap(double value, string unit) => value == 0 ? "Level" : $"{Math.Abs(value):0} {unit} {(value > 0 ? "ahead" : "behind")}";
    protected override async Task OnParametersSetAsync()
    {
        if (resultOwner != State.Id) { generation++; resultOwner = State.Id; attempted = false; result = null; }
        if (State.Storage?.State == "saved" && !attempted) await ReadResult();
    }
    private async Task ReadResult()
    {
        attempted = true; reading = true;
        var id = State.Id; var request = ++generation;
        try
        {
            var stored = await Task.Run(() => RunJournal.Shared.ReadGhostRun(id));
            if (!disposed && request == generation && State.Id == id) result = stored;
        }
        catch (Exception issue) when (issue is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or KeyNotFoundException or global::Android.Database.Sqlite.SQLiteException) { }
        finally { if (request == generation) reading = false; }
    }
    public void Dispose() { disposed = true; generation++; GC.SuppressFinalize(this); }

}
