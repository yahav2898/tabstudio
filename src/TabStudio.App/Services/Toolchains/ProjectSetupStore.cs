using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services.Toolchains;

/// <summary>What TabStudio remembers about setting projects up when they open.</summary>
/// <param name="SetUpProjectsWhenTheyOpen">
/// Whether opening a repository or folder installs what its projects need without asking: their toolchains from the
/// Toolchain Store and their packages. On by default; when off, TabStudio offers each install instead.
/// </param>
/// <param name="Projects">
/// The projects set up so far: for each manifest's path, the fingerprint it had when its packages were installed
/// (see <c>RunPlanner.FingerprintOf</c>), so a project is set up again only when its manifest changes.
/// </param>
public sealed record ProjectSetupSettings(bool SetUpProjectsWhenTheyOpen = true, IReadOnlyDictionary<string, string>? Projects = null)
{
    /// <summary>The settings of a new installation.</summary>
    public static ProjectSetupSettings Default { get; } = new();
}

/// <summary>Keeps <see cref="ProjectSetupSettings"/> in a file in <see cref="AppPaths.State"/>.</summary>
/// <param name="paths">Where the app keeps its state.</param>
public sealed class ProjectSetupStore(AppPaths paths)
{
    private const string FileName = "project-setup.json";

    private readonly string _file = Path.Combine(paths.State, FileName);

    /// <summary>Reads the settings; the defaults when there are none or the file is damaged.</summary>
    public ProjectSetupSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(_file);
            return JsonSerializer.Deserialize(stream, ProjectSetupJsonContext.Default.ProjectSetupSettings) ?? ProjectSetupSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return ProjectSetupSettings.Default;
        }
    }

    /// <summary>Writes the settings atomically, forgetting projects whose manifest is gone.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>Whether they were written; a full or read-only disk only loses the change.</returns>
    public bool Save(ProjectSetupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var kept = settings.Projects?.Where(p => File.Exists(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var temporary = _file + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, settings with { Projects = kept }, ProjectSetupJsonContext.Default.ProjectSetupSettings);
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

/// <summary>Source-generated JSON for the project setup settings.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProjectSetupSettings))]
internal sealed partial class ProjectSetupJsonContext : JsonSerializerContext
{
}
