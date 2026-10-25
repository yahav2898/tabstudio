using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.App.Services;

namespace TabStudio.App.Swarm;

/// <summary>What the tablet remembers about its swarm between runs of the app.</summary>
/// <param name="IsOn">Whether the tablet coordinates a swarm, so paired devices can rejoin by themselves.</param>
/// <param name="MirrorInput">Whether an input on any screen happens on every screen.</param>
/// <param name="PauseAllOnBreakpoint">Whether a breakpoint on one device pauses every device.</param>
public sealed record SwarmSettings(bool IsOn = false, bool MirrorInput = false, bool PauseAllOnBreakpoint = true);

/// <summary>Keeps the <see cref="SwarmSettings"/> in the app's state folder.</summary>
/// <remarks>Thread-safe; a damaged file reads as the defaults.</remarks>
public sealed class SwarmSettingsStore
{
    private readonly string _file;
    private readonly Lock _gate = new();

    /// <summary>Creates the store over its file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public SwarmSettingsStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = Path.Combine(paths.State, "Swarm", "settings.json");
    }

    /// <summary>The saved settings, or the defaults.</summary>
    public SwarmSettings Load()
    {
        lock (_gate)
        {
            try
            {
                using var stream = File.OpenRead(_file);
                return JsonSerializer.Deserialize(stream, SwarmSettingsJsonContext.Default.SwarmSettings) ?? new SwarmSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new SwarmSettings();
            }
        }
    }

    /// <summary>Saves the settings.</summary>
    /// <param name="settings">The settings.</param>
    public void Save(SwarmSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                var temporary = _file + ".tmp";
                using (var stream = File.Create(temporary))
                {
                    JsonSerializer.Serialize(stream, settings, SwarmSettingsJsonContext.Default.SwarmSettings);
                }

                File.Move(temporary, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: the settings only spare the user from choosing again.
            }
        }
    }
}

/// <summary>Source-generated JSON for the swarm's settings.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SwarmSettings))]
internal sealed partial class SwarmSettingsJsonContext : JsonSerializerContext
{
}
