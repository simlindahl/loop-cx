using System.Text.Json;
using Microsoft.Maui.Storage;

namespace LoopCx.Features.ActiveRun;

internal static class CorePreferences
{
    private const string Key = "core-preferences-v1";

    internal static CorePreferencesState Load()
    {
        var json = Preferences.Default.Get(Key, string.Empty);
        if (json.Length == 0) return CorePreferencesState.Default;

        try
        {
            var state = JsonSerializer.Deserialize<CorePreferencesState>(json)
                ?? throw new ArgumentException("Saved Core selections are empty.");
            state.Validate();

            return state;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
        {
            throw new InvalidDataException("Saved Core settings could not be read. Their original data has been preserved.", error);
        }
    }

    internal static void Save(CorePreferencesState state)
    {
        state.Validate();
        Preferences.Default.Set(Key, JsonSerializer.Serialize(state));
    }
}
