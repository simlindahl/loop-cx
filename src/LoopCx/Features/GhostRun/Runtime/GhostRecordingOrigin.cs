namespace LoopCx.Features.GhostRun;

public enum GhostOriginKind { NormalService = 1, DebugService = 2, SyntheticFixture = 3 }

// Creation path only: no claim of physical accuracy or current grants.
public sealed record GhostRecordingOrigin(int Version, GhostOriginKind Kind)
{
    public void Validate()
    {
        if (Version != 1 || !Enum.IsDefined(Kind))
            throw new InvalidDataException("The frozen Ghost creation origin is unsupported.");
    }

    internal static GhostRecordingOrigin ForService()
    {
#if DEBUG
        return new(1, GhostOriginKind.DebugService);
#else
        return new(1, GhostOriginKind.NormalService);
#endif
    }
}
