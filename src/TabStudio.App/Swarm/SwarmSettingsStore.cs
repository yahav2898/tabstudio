using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.App.Services;
using TabStudio.App.Swarm.LayoutChecks;

namespace TabStudio.App.Swarm;

/// <summary>A virtual device the user added to the Device Matrix, as it is kept between sessions.</summary>
/// <param name="Id">A stable id, which its screen keeps.</param>
/// <param name="Name">What it is called, such as "Small phone".</param>
/// <param name="FormFactor">The kind of device.</param>
/// <param name="Width">Its width now, rotated or not.</param>
/// <param name="Height">Its height now.</param>
/// <param name="Scale">Physical pixels per device-independent pixel.</param>
/// <param name="IsDark">Whether the system theme is dark.</param>
/// <param name="IsRightToLeft">Whether text runs right to left.</param>
/// <param name="FontScale">The system font scale.</param>
public sealed record VirtualDeviceSettings(
    string Id,
    string Name,
    FormFactor FormFactor,
    double Width,
    double Height,
    double Scale,
    bool IsDark = false,
    bool IsRightToLeft = false,
    double FontScale = 1)
{
    /// <summary>The device's screen.</summary>
    public ScreenProfile ToProfile() =>
        new(Name, FormFactor, Width, Height, Scale, IsDark, IsRightToLeft ? TextDirection.RightToLeft : TextDirection.LeftToRight, FontScale);

    /// <summary>A device as a screen describes it.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="profile">Its screen.</param>
    public static VirtualDeviceSettings From(string id, ScreenProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new VirtualDeviceSettings(id, profile.Name, profile.FormFactor, profile.Width, profile.Height, profile.Scale, profile.IsDark, profile.Direction == TextDirection.RightToLeft, profile.FontScale);
    }
}

/// <summary>What the Device Matrix remembers: its virtual devices, its zoom, and which layout checks run.</summary>
/// <param name="Devices">The virtual devices, in the order they were added; <see langword="null"/> before any were chosen.</param>
/// <param name="Rules">Which layout checks run.</param>
/// <param name="Zoom">How large the matrix draws screens, 1 for their real size.</param>
public sealed record SwarmSettings(IReadOnlyList<VirtualDeviceSettings>? Devices, LayoutCheckSettings Rules, double Zoom = 0.5)
{
    /// <summary>The settings before the user changed any.</summary>
    public static SwarmSettings Default { get; } = new(null, LayoutCheckSettings.All);
}

/// <summary>
/// Keeps the Device Matrix's settings in the app's state folder, so the virtual devices, their themes, directions and
/// font scales, and the layout checks the user turned off, come back in every workspace.
/// </summary>
public sealed class SwarmSettingsStore
{
    private readonly string _file;
    private readonly Lock _gate = new();

    /// <summary>Creates the store over its file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public SwarmSettingsStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = Path.Combine(paths.State, "swarm.json");
    }

    /// <summary>The saved settings, or the defaults.</summary>
    public SwarmSettings Load()
    {
        lock (_gate)
        {
            try
            {
                using var stream = File.OpenRead(_file);
                return JsonSerializer.Deserialize(stream, SwarmJsonContext.Default.SwarmSettings) ?? SwarmSettings.Default;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                return SwarmSettings.Default;
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
                    JsonSerializer.Serialize(stream, settings, SwarmJsonContext.Default.SwarmSettings);
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

/// <summary>The settings' JSON, generated at compile time so it works trimmed on Android.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SwarmSettings))]
internal sealed partial class SwarmJsonContext : JsonSerializerContext
{
}
