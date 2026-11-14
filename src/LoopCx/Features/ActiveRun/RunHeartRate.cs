namespace LoopCx.Features.ActiveRun;

public sealed record HeartRatePolicy(int Version, TimeSpan MaximumGap)
{
    // Synthetic-test pilot, not a calibrated physical-device guarantee.
    public static HeartRatePolicy Pilot { get; } = new(1, TimeSpan.FromSeconds(5));
}

public sealed record HeartRateZone(int Number, int MinimumBpm, int MaximumBpm);

public sealed record HeartRateConfiguration(int Version, string? DeviceAddress, HeartRatePolicy Policy,
    IReadOnlyList<HeartRateZone> Zones)
{
    public HeartRateReserveProfile? ReserveProfile { get; init; }
    public string? DeviceName { get; init; }

    public static HeartRateConfiguration Empty { get; } = new(1, null, HeartRatePolicy.Pilot, []);

    public static HeartRateSetup ReadOptional(string? json)
    {
        try
        {
            var configuration = string.IsNullOrEmpty(json) ? Empty :
                System.Text.Json.JsonSerializer.Deserialize<HeartRateConfiguration>(json)
                    ?? throw new InvalidDataException("Missing heart-rate configuration.");
            configuration.Validate();

            return new(configuration, null);
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or ArgumentException or InvalidDataException or NotSupportedException)
        {
            // A sensor configuration is optional. Preserve its stored bytes and disable only this source.
            return new(Empty, "Heart-rate settings could not be read or are unsupported. HR is unavailable; the timed workout can continue.");
        }
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Policy);
        ArgumentNullException.ThrowIfNull(Zones);

        if (Version != 1 || Policy.Version != 1 || Policy.MaximumGap <= TimeSpan.Zero || Policy.MaximumGap > TimeSpan.FromSeconds(30))
            throw new ArgumentException("The heart-rate quality policy is not supported.");

        if (DeviceAddress is not null && (DeviceAddress.Length != 17 || DeviceAddress.Where(static (_, index) => index % 3 == 2).Any(static character => character != ':') ||
            DeviceAddress.Where(static (_, index) => index % 3 != 2).Any(static character => !Uri.IsHexDigit(character))))
        {
            throw new ArgumentException("Choose a valid heart-rate device address.");
        }

        for (var index = 0; index < Zones.Count; index++)
        {
            var zone = Zones[index];

            if (zone is null || zone.Number != index + 1 || zone.MinimumBpm <= 0 || zone.MaximumBpm < zone.MinimumBpm ||
                (index > 0 && zone.MinimumBpm != Zones[index - 1].MaximumBpm + 1))
            {
                throw new ArgumentException("Personal zones must have ordered, contiguous, non-overlapping whole-BPM boundaries.");
            }
        }

        if (ReserveProfile is not null && !ReserveProfile.ResolveZones().SequenceEqual(Zones))
            throw new ArgumentException("Stored HRR inputs and resolved zones disagree.");

        if (Zones.Count is not (0 or 5)) throw new ArgumentException("Supply all five personal zones or leave zones unconfigured.");
    }
}

public sealed record HeartRateSetup(HeartRateConfiguration Configuration, string? ConfigurationError);

// Standard 2A37 supplies no acquisition timestamp. These clocks are local callback receipt only.
public sealed record HeartRateObservation(TimeSpan ReceiptMonotonicTime, DateTimeOffset ReceiptUtc,
    int? Bpm, string Quality);

public sealed record HeartRateBracket(TimeSpan From, TimeSpan To, int Bpm, int? Zone);

public sealed record HeartRateState(HeartRateConfiguration Configuration, int? CurrentBpm, string Quality,
    TimeSpan? LastReceiptMonotonicTime, string? ConfigurationError = null);

public sealed class RunHeartRate
{
    private readonly HeartRateConfiguration configuration;
    private HeartRateObservation? previous;
    private TimeSpan? lastReceipt;
    private string quality = "unavailable";
    private readonly string? configurationError;

    public RunHeartRate(HeartRateConfiguration configuration, string? configurationError = null)
    {
        configuration.Validate();
        this.configurationError = configurationError;
        if (configurationError is not null) quality = "configuration-invalid";
        this.configuration = configuration with { Zones = Array.AsReadOnly(configuration.Zones.ToArray()) };
    }

    public HeartRateBracket? Observe(HeartRateObservation observation)
    {
        // Duplicate/stale packets never replace newer evidence or create a second interval.
        if (lastReceipt is { } last && observation.ReceiptMonotonicTime <= last) return null;

        lastReceipt = observation.ReceiptMonotonicTime;
        quality = observation.Quality;
        HeartRateBracket? bracket = null;
        var valid = observation.Bpm is > 0 && observation.Quality == "valid";

        if (valid && previous is { Bpm: { } bpm } prior &&
            observation.ReceiptMonotonicTime - prior.ReceiptMonotonicTime <= configuration.Policy.MaximumGap)
        {
            var zone = configuration.Zones.SingleOrDefault(item => bpm >= item.MinimumBpm && bpm <= item.MaximumBpm)?.Number;
            bracket = new(prior.ReceiptMonotonicTime, observation.ReceiptMonotonicTime, bpm, zone);
        }

        previous = valid ? observation : null;

        return bracket;
    }

    public HeartRateState Snapshot(TimeSpan now)
    {
        var fresh = previous is not null && now >= previous.ReceiptMonotonicTime &&
            now - previous.ReceiptMonotonicTime <= configuration.Policy.MaximumGap;

        return new(configuration, fresh ? previous!.Bpm : null, fresh ? quality : previous is null ? quality : "stale", lastReceipt, configurationError);
    }
}
