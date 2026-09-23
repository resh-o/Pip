using System.Text.Json;

namespace DeskPet.Core.Config;

/// <summary>Converts <see cref="Settings"/> to and from its JSON file contents; no file I/O here.</summary>
public static class SettingsSerializer
{
    public static string Serialize(Settings settings) =>
        JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings);

    /// <summary>
    /// Unknown properties are ignored and missing ones keep their defaults. Returns false with default
    /// settings when the text is not a valid settings object, so a corrupt file never blocks startup.
    /// </summary>
    public static bool TryDeserialize(string? json, out Settings settings)
    {
        settings = new Settings();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            if (JsonSerializer.Deserialize(json, SettingsJsonContext.Default.Settings) is not { } parsed)
            {
                return false;
            }

            settings = SettingsSanitizer.Sanitize(parsed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
