using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services.Extensions;

/// <summary>What the user chose about extensions.</summary>
/// <param name="ColorTheme">The chosen color theme's key, or <see langword="null"/> for TabStudio's own.</param>
/// <param name="IconTheme">The chosen file icon theme's key, or <see langword="null"/> for TabStudio's badges.</param>
/// <param name="Registry">The Open VSX registry's address, or <see langword="null"/> for open-vsx.org.</param>
public sealed record ExtensionSettings(string? ColorTheme = null, string? IconTheme = null, string? Registry = null)
{
    /// <summary>The settings of a new installation.</summary>
    public static ExtensionSettings Default { get; } = new();
}

/// <summary>Keeps <see cref="ExtensionSettings"/> in a file in <see cref="AppPaths.State"/>.</summary>
/// <param name="paths">Where the app keeps its state.</param>
public sealed class ExtensionSettingsStore(AppPaths paths)
{
    private const string FileName = "extensions.json";

    private readonly string _file = Path.Combine(paths.State, FileName);

    /// <summary>Reads the settings; the defaults when there are none or the file is damaged.</summary>
    public ExtensionSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(_file);
            return JsonSerializer.Deserialize(stream, ExtensionSettingsJsonContext.Default.ExtensionSettings) ?? ExtensionSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return ExtensionSettings.Default;
        }
    }

    /// <summary>Writes the settings atomically.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>Whether they were written; a full or read-only disk only loses the change.</returns>
    public bool Save(ExtensionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var temporary = _file + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, settings, ExtensionSettingsJsonContext.Default.ExtensionSettings);
            }

            File.Move(temporary, _file, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Source-generated JSON for the extension settings.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ExtensionSettings))]
internal sealed partial class ExtensionSettingsJsonContext : JsonSerializerContext
{
}
