using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Core.Previews;

namespace TabStudio.App.Services;

/// <summary>
/// Remembers how each project's app preview shows its window (Fit, 100% or Fill), in a file in the app's state
/// folder, so a project opens with the mode last chosen for it without writing anything into the project's folder.
/// </summary>
public sealed class PreviewZoomStore
{
    private readonly string _file;
    private readonly Lock _gate = new();

    /// <summary>Creates the store over its file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public PreviewZoomStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = Path.Combine(paths.State, "preview-zoom.json");
    }

    /// <summary>The mode chosen for a project, or <see cref="PreviewZoomMode.Fit"/> when none was.</summary>
    /// <param name="projectPath">The project file.</param>
    public PreviewZoomMode Load(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        lock (_gate)
        {
            return ReadAll().TryGetValue(projectPath, out var mode) && Enum.IsDefined(mode) ? mode : PreviewZoomMode.Fit;
        }
    }

    /// <summary>Saves the mode chosen for a project, keeping those of the others.</summary>
    /// <param name="projectPath">The project file.</param>
    /// <param name="mode">The mode.</param>
    public void Save(string projectPath, PreviewZoomMode mode)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        lock (_gate)
        {
            var all = ReadAll();
            if (all.TryGetValue(projectPath, out var saved) && saved == mode)
            {
                return;
            }

            all[projectPath] = mode;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                var temporary = _file + ".tmp";
                using (var stream = File.Create(temporary))
                {
                    JsonSerializer.Serialize(stream, all, PreviewZoomJsonContext.Default.DictionaryStringPreviewZoomMode);
                }

                File.Move(temporary, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: the mode only spares the user from choosing again.
            }
        }
    }

    private Dictionary<string, PreviewZoomMode> ReadAll()
    {
        try
        {
            if (File.Exists(_file))
            {
                using var stream = File.OpenRead(_file);
                if (JsonSerializer.Deserialize(stream, PreviewZoomJsonContext.Default.DictionaryStringPreviewZoomMode) is { } all)
                {
                    return new Dictionary<string, PreviewZoomMode>(all, StringComparer.Ordinal);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            // A damaged file starts over.
        }

        return new Dictionary<string, PreviewZoomMode>(StringComparer.Ordinal);
    }
}

/// <summary>The JSON shape of the preview zoom file: each project's mode by name.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Dictionary<string, PreviewZoomMode>))]
internal sealed partial class PreviewZoomJsonContext : JsonSerializerContext
{
}
