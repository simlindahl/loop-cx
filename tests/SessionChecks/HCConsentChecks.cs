using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCx.Features.ExportRun;
using LoopCx.Features.HealthConnectExport;
using LoopCx.Features.History;

internal static class HCConsentChecks
{
    internal static void Run(ExportJournalSnapshot fixture)
    {
        // Arrange: a recording freezes its start intent, independently of future permission/Settings changes.
        var state = HCConsent.Off;
        var startedOff = Freeze(fixture, state.ForNewRun());
        state = state.Enable();
        var startedOptedIn = Freeze(fixture, state.ForNewRun());
        var frozenBytes = startedOptedIn.Checkpoint.GetRawText();
        var frozenId = startedOptedIn.Id;

        // Act: denied access changes no app intent; Off and re-enable affect only subsequent starts.
        state = JsonSerializer.Deserialize<HCConsent>(JsonSerializer.Serialize(state))!;
        state = state.Disable();
        var startedWhileOff = Freeze(fixture, state.ForNewRun());
        state = state.Enable();
        var afterReenable = Freeze(fixture, state.ForNewRun());
        var oldDetail = StoredRunProjection.Detail(startedOff, null);
        var pendingDetail = StoredRunProjection.Detail(startedOptedIn, null);

        // Assert: generic pre-existing queue rows or finishing after Enable never imply consent.
        if (HCConsent.Eligible(startedOff.Checkpoint) || HCConsent.Eligible(startedWhileOff.Checkpoint)
            || oldDetail.Summary.ExportLabel != "Not exported · local only"
            || !HCConsent.Eligible(startedOptedIn.Checkpoint) || pendingDetail.Summary.ExportLabel != "Pending"
            || startedOptedIn.Checkpoint.GetRawText() != frozenBytes || startedOptedIn.Id != frozenId
            || !HCConsent.Eligible(afterReenable.Checkpoint)
            || afterReenable.Checkpoint.GetProperty("HealthConnectConsentRevision").GetGuid()
                == startedOptedIn.Checkpoint.GetProperty("HealthConnectConsentRevision").GetGuid())
        {
            throw new InvalidOperationException("Consent must remain frozen at start through denial, Off, restart and re-enable; existing pending rows are not opt-in.");
        }

        // Arrange/Act: malformed and legacy checkpoint consent never becomes eligibility through defaults.
        foreach (var value in new[] { "null", "0", "\"not-a-revision\"", "\"00000000-0000-0000-0000-000000000000\"" })
        {
            using var bad = JsonDocument.Parse("{\"HealthConnectConsentRevision\":" + value + "}");
            if (HCConsent.Eligible(bad.RootElement)) throw new InvalidOperationException("Malformed consent allowed historical export.");
        }
        Console.WriteLine("PASS: prospective start consent survives denial/restart/Off/re-enable without historical backfill or changed frozen recording identity.");
    }

    private static ExportJournalSnapshot Freeze(ExportJournalSnapshot source, Guid? revision)
    {
        var checkpoint = JsonNode.Parse(source.Checkpoint.GetRawText())!;
        checkpoint["HealthConnectConsentRevision"] = revision?.ToString();
        return source with { Status = "finished", ExportStatus = "pending", Checkpoint = JsonSerializer.SerializeToElement(checkpoint) };
    }
}
