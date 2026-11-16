namespace LoopCx.Features.HealthConnectExport;

public sealed record HCRecordReceipt(HCRecordKind Kind, string ClientRecordId, long ClientRecordVersion, string ProviderRecordId);
public sealed record HCDurableAttempt(int Version, HCWritePlanV1 FrozenPlan, Guid Token, int AttemptCount,
    string State, bool Simulation, IReadOnlyList<HCRecordReceipt>? Receipts = null)
{
    public void Validate(bool simulation)
    {
        ValidateMapping(simulation, 1);
    }

    public void ValidateGhost(ExportRun.ExportJournalSnapshot source, bool debugBuild)
    {
        ValidateMapping(false, 2);
        _ = GhostHCWritePlanner.RequireFrozenSource(source, FrozenPlan, debugBuild);
    }

    private void ValidateMapping(bool simulation, int mappingVersion)
    {
        if (Version != 1 || FrozenPlan is null || Token == Guid.Empty || AttemptCount <= 0 || Simulation != simulation
            || State is not ("in_flight" or "pending" or "action_required" or "acknowledged" or "simulated_complete"))
        {
            throw new InvalidDataException("The stored attempt contract is unsupported or malformed.");
        }

        var plan = FrozenPlan;
        if (plan.Source is null || plan.Records is null || plan.Warnings is null || plan.MappingVersion != mappingVersion || plan.RunId == Guid.Empty
            || !plan.Source.Finished || plan.Records.Count == 0 || plan.Records.Any(static record => record is null)
            || !plan.HasValidHash() || plan.Records.Count(static record => record.Kind == HCRecordKind.RunningSession) != 1
            || plan.Records[0].Kind != HCRecordKind.RunningSession
            || plan.Records.Select(static record => record.ClientRecordId).Distinct(StringComparer.Ordinal).Count() != plan.Records.Count)
        {
            throw new InvalidDataException("The frozen plan is incomplete or altered.");
        }

        if (simulation ? !plan.Source.Synthetic : plan.Source.DebugBuild || plan.Source.Synthetic)
            throw new InvalidDataException("Attempt origin and source eligibility disagree.");
        if (plan.Records.Any(record => !Enum.IsDefined(record.Kind) || string.IsNullOrWhiteSpace(record.ClientRecordId)
            || record.ClientRecordVersion != plan.MappingVersion || record.EndUtc <= record.StartUtc
            || record.StartOffsetSeconds is null || record.EndOffsetSeconds is null || record.Pauses is null || record.HeartRateSamples is null
            || record.Pauses.Any(static pause => pause is null) || record.HeartRateSamples.Any(static sample => sample is null)))
        {
            throw new InvalidDataException("The frozen native record contract is incomplete.");
        }

        if (State is "acknowledged" or "simulated_complete") ValidateReceipts(plan, Receipts);
        if ((State == "acknowledged" && simulation) || (State == "simulated_complete" && !simulation))
            throw new InvalidDataException("Simulation cannot become production acknowledgement.");
    }

    public static HCDurableAttempt Begin(HCWritePlanV1 plan, HCDurableAttempt? previous, bool simulation)
    {
        previous?.Validate(simulation);
        if (previous is not null && previous.FrozenPlan.PayloadHash != plan.PayloadHash)
            throw new InvalidDataException("Retry must retain the exact frozen payload.");
        var attempt = new HCDurableAttempt(1, previous?.FrozenPlan ?? plan, Guid.NewGuid(), checked((previous?.AttemptCount ?? 0) + 1), "in_flight", simulation);
        attempt.Validate(simulation);

        return attempt;
    }

    public static HCDurableAttempt BeginGhost(ExportRun.ExportJournalSnapshot source, HCWritePlanV1 plan,
        HCDurableAttempt? previous, bool debugBuild)
    {
        _ = GhostHCWritePlanner.RequireFrozenSource(source, plan, debugBuild);
        previous?.ValidateGhost(source, debugBuild);
        var attempt = new HCDurableAttempt(1, previous?.FrozenPlan ?? plan, Guid.NewGuid(),
            checked((previous?.AttemptCount ?? 0) + 1), "in_flight", false);
        attempt.ValidateGhost(source, debugBuild);

        return attempt;
    }

    public HCDurableAttempt? CompleteGhost(ExportRun.ExportJournalSnapshot source, Guid token, string hash,
        IReadOnlyList<HCRecordReceipt> receipts, bool debugBuild)
    {
        ValidateGhost(source, debugBuild);
        if (Token != token || FrozenPlan.PayloadHash != hash || State != "in_flight") return null;
        ValidateReceipts(FrozenPlan, receipts);

        return this with { State = "acknowledged", Receipts = [.. receipts] };
    }

    public static void ValidateReceipts(HCWritePlanV1 plan, IReadOnlyList<HCRecordReceipt>? receipts)
    {
        if (receipts is null || receipts.Count != plan.Records.Count)
            throw new InvalidDataException("Every intended record requires an acknowledgement.");
        for (var index = 0; index < receipts.Count; index++)
        {
            var expected = plan.Records[index];
            var actual = receipts[index];
            if (actual is null || actual.Kind != expected.Kind || actual.ClientRecordId != expected.ClientRecordId
                || actual.ClientRecordVersion != expected.ClientRecordVersion || string.IsNullOrWhiteSpace(actual.ProviderRecordId))
            {
                throw new InvalidDataException("Provider response does not match the frozen record order and identities.");
            }
        }
    }
}
