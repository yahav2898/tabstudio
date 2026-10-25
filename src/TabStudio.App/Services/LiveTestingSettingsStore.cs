using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.LiveTesting;

namespace TabStudio.App.Services;

/// <summary>When Live Unit Testing runs the tests an edit affects.</summary>
public enum LiveTestingTrigger
{
    /// <summary>A moment after the user stops typing, as in Visual Studio.</summary>
    AfterTyping,

    /// <summary>When a file is saved, which saves battery on small devices.</summary>
    OnSave,
}

/// <summary>What Live Unit Testing remembers about one solution.</summary>
/// <param name="Trigger">When it runs tests.</param>
/// <param name="Rules">The projects and tests the user included or excluded.</param>
public sealed record LiveTestingSettings(LiveTestingTrigger Trigger, IReadOnlyList<LiveTestRule> Rules);

/// <summary>
/// Saves the Live Unit Testing settings of every solution in the app's state folder, so includes, excludes and the
/// trigger come back when the solution is opened again, without writing anything into the solution's folder.
/// </summary>
public sealed class LiveTestingSettingsStore
{
    private readonly string _file;
    private readonly Lock _gate = new();

    /// <summary>Creates the store over its file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public LiveTestingSettingsStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = Path.Combine(paths.State, "live-unit-testing.json");
    }

    /// <summary>The settings of a solution, or <see langword="null"/> when none were saved.</summary>
    /// <param name="solutionPath">The solution, project or folder that is open.</param>
    public LiveTestingSettings? Load(string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(solutionPath);
        lock (_gate)
        {
            return ReadAll().GetValueOrDefault(solutionPath);
        }
    }

    /// <summary>Saves the settings of a solution, keeping those of the others.</summary>
    /// <param name="solutionPath">The solution, project or folder that is open.</param>
    /// <param name="settings">Its settings.</param>
    public void Save(string solutionPath, LiveTestingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(solutionPath);
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            var all = ReadAll();
            all[solutionPath] = settings;
            try
            {
                var temporary = _file + ".tmp";
                using (var stream = File.Create(temporary))
                {
                    JsonSerializer.Serialize(stream, all, LiveTestingJsonContext.Default.DictionaryStringLiveTestingSettings);
                }

                File.Move(temporary, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: the settings only spare the user from choosing again.
            }
        }
    }

    private Dictionary<string, LiveTestingSettings> ReadAll()
    {
        try
        {
            if (File.Exists(_file))
            {
                using var stream = File.OpenRead(_file);
                if (JsonSerializer.Deserialize(stream, LiveTestingJsonContext.Default.DictionaryStringLiveTestingSettings) is { } all)
                {
                    return new Dictionary<string, LiveTestingSettings>(all, StringComparer.Ordinal);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged file starts over.
        }

        return new Dictionary<string, LiveTestingSettings>(StringComparer.Ordinal);
    }
}

/// <summary>The JSON shape of the Live Unit Testing settings file.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Dictionary<string, LiveTestingSettings>))]
internal sealed partial class LiveTestingJsonContext : JsonSerializerContext
{
}
