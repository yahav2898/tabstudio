using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services;

/// <summary>How big the IDE is drawn on the external display in desktop mode.</summary>
public enum DesktopScale
{
    /// <summary>Picked from the display's size and the density the system reports for it.</summary>
    Automatic,

    /// <summary>One display pixel per unit: the most space, as a desktop IDE on a 24-inch 1080p monitor.</summary>
    Percent100,

    /// <summary>1.25 pixels per unit.</summary>
    Percent125,

    /// <summary>1.5 pixels per unit.</summary>
    Percent150,

    /// <summary>1.75 pixels per unit.</summary>
    Percent175,

    /// <summary>Two pixels per unit, for 4K monitors and television sets.</summary>
    Percent200,
}

/// <summary>The user's choices for desktop mode.</summary>
/// <param name="UseDisplayAutomatically">Whether to move the IDE to a display as soon as one is connected, without asking.</param>
/// <param name="Scale">How big the IDE is drawn on the display.</param>
public sealed record DesktopModeSettings(bool UseDisplayAutomatically, DesktopScale Scale)
{
    /// <summary>The settings before the user changed anything: ask when a display is connected, and pick the size.</summary>
    public static DesktopModeSettings Default { get; } = new(false, DesktopScale.Automatic);
}

/// <summary>Keeps the desktop mode settings in the app's state folder.</summary>
/// <param name="paths">The app's folders.</param>
public sealed class DesktopModeSettingsStore(AppPaths paths)
{
    private string FilePath => Path.Combine(paths.State, "desktop-mode.json");

    /// <summary>Reads the settings; the defaults when there are none or the file is damaged.</summary>
    public DesktopModeSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize(stream, DesktopModeSettingsJson.Default.DesktopModeSettings) ?? DesktopModeSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return DesktopModeSettings.Default;
        }
    }

    /// <summary>Saves the settings. A failure to save is not an error: the choice applies until the app closes.</summary>
    /// <param name="settings">The settings.</param>
    public void Save(DesktopModeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            Directory.CreateDirectory(paths.State);
            var temporary = FilePath + ".tmp";
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, settings, DesktopModeSettingsJson.Default.DesktopModeSettings);
            }

            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The setting still applies for this session.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(DesktopModeSettings))]
internal sealed partial class DesktopModeSettingsJson : JsonSerializerContext
{
}
