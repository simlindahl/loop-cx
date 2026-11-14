namespace LoopCx.Features.ActiveRun;

public sealed record HeartRateReserveProfile(int Version, Guid ConfigurationRevision, int RestingBpm,
    int MaximumBpm, string Method, string RoundingPolicy)
{
    public static HeartRateReserveProfile Create(int restingBpm, int maximumBpm)
    {
        return new(1, Guid.NewGuid(), restingBpm, maximumBpm, "heart-rate-reserve", "ceiling-whole-bpm-v1");
    }

    public IReadOnlyList<HeartRateZone> ResolveZones()
    {
        if (Version != 1 || ConfigurationRevision == Guid.Empty || Method != "heart-rate-reserve" || RoundingPolicy != "ceiling-whole-bpm-v1")
            throw new ArgumentException("The heart-rate reserve profile is not supported.");
        if (RestingBpm <= 0 || MaximumBpm <= RestingBpm)
            throw new ArgumentException("Use positive whole bpm. Maximum must exceed resting heart rate.");
        int[] percentages = [40, 60, 70, 80, 90];
        var lower = percentages.Select(percent => checked((int)((((long)RestingBpm * 100) + ((long)percent * (MaximumBpm - RestingBpm)) + 99) / 100))).ToArray();
        return lower.Zip(lower.Skip(1), static (previous, next) => next <= previous).Any(static collapsed => collapsed)
            ? throw new ArgumentException("Use values that produce distinct whole-bpm zone ranges.")
            : (IReadOnlyList<HeartRateZone>)Array.AsReadOnly(lower.Select((minimum, index) => new HeartRateZone(index + 1, minimum,
            index == 4 ? MaximumBpm : lower[index + 1] - 1)).ToArray());
    }
}
