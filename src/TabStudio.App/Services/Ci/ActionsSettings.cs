using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services.Ci;

/// <summary>What the GitHub Actions window remembers between sessions.</summary>
/// <param name="WatchedRepositories">Other repositories to watch, as "owner/name", in the order they were added.</param>
/// <param name="NotifyWhenRunsFinish">Whether to show a notice when a run on the branch, or one started from TabStudio, finishes.</param>
/// <param name="UseSystemNotifications">Whether to also show it in the system's notifications, where the platform has them.</param>
public sealed record ActionsSettings(IReadOnlyList<string> WatchedRepositories, bool NotifyWhenRunsFinish = true, bool UseSystemNotifications = false)
{
    /// <summary>The settings of a new installation.</summary>
    public static ActionsSettings Default { get; } = new([]);
}

/// <summary>Keeps <see cref="ActionsSettings"/> in a file in <see cref="AppPaths.State"/>, next to the GitHub account.</summary>
/// <param name="paths">Where the app keeps its state.</param>
public sealed class ActionsSettingsStore(AppPaths paths)
{
    private const string FileName = "github-actions.json";

    private readonly string _file = Path.Combine(paths.State, FileName);

    /// <summary>Reads the settings; the defaults when there are none or the file is damaged.</summary>
    public ActionsSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(_file);
            return JsonSerializer.Deserialize(stream, ActionsSettingsJsonContext.Default.ActionsSettings) is { } settings
                ? settings with { WatchedRepositories = settings.WatchedRepositories ?? [] }
                : ActionsSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return ActionsSettings.Default;
        }
    }

    /// <summary>Writes the settings atomically.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>Whether they were written; a full or read-only disk only loses the change.</returns>
    public bool Save(ActionsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var temporary = _file + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, settings, ActionsSettingsJsonContext.Default.ActionsSettings);
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

/// <summary>Source-generated JSON for the Actions window's settings.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ActionsSettings))]
internal sealed partial class ActionsSettingsJsonContext : JsonSerializerContext
{
}
