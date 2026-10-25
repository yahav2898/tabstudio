using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services.Appearance;

/// <summary>The IDE's theme, as Visual Studio offers it under Tools &gt; Theme.</summary>
public enum AppTheme
{
    /// <summary>TabStudio's dark theme, the default.</summary>
    Dark,

    /// <summary>TabStudio's light theme, after Visual Studio's Light.</summary>
    Light,

    /// <summary>Dark or light as the system is, changing when the system does.</summary>
    System,
}

/// <summary>The user's choices for how the IDE looks.</summary>
/// <param name="Theme">The IDE's theme.</param>
public sealed record AppearanceSettings(AppTheme Theme)
{
    /// <summary>The settings before the user changed anything: the dark theme TabStudio has always had.</summary>
    public static AppearanceSettings Default { get; } = new(AppTheme.Dark);
}

/// <summary>Keeps the appearance settings in the app's state folder.</summary>
/// <param name="paths">The app's folders.</param>
public sealed class AppearanceSettingsStore(AppPaths paths)
{
    private string FilePath => Path.Combine(paths.State, "appearance.json");

    /// <summary>Reads the settings; the defaults when there are none or the file is damaged.</summary>
    public AppearanceSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize(stream, AppearanceSettingsJson.Default.AppearanceSettings) ?? AppearanceSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return AppearanceSettings.Default;
        }
    }

    /// <summary>Saves the settings. A failure to save is not an error: the choice applies until the app closes.</summary>
    /// <param name="settings">The settings.</param>
    public void Save(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            Directory.CreateDirectory(paths.State);
            var temporary = FilePath + ".tmp";
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, settings, AppearanceSettingsJson.Default.AppearanceSettings);
            }

            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The theme still applies for this session.
        }
    }
}

/// <summary>Source-generated JSON for the appearance settings.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppearanceSettings))]
internal sealed partial class AppearanceSettingsJson : JsonSerializerContext
{
}
