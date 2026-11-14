using System.Text.Json;
using LoopCx.Features.ActiveRun;

namespace LoopCx;

// Backend seam for a later approved Settings surface. Invalid saved configuration is never overwritten.
internal static class HeartRatePreferences
{
    private const string Key = "heart-rate-configuration-v1";

    internal static HeartRateSetup Load() => HeartRateConfiguration.ReadOptional(Preferences.Default.Get(Key, ""));

    internal static void Save(HeartRateConfiguration value)
    {
        value.Validate();
        Preferences.Default.Set(Key, JsonSerializer.Serialize(value));
    }
}
