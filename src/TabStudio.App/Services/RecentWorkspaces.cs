using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services;

/// <summary>A workspace the user opened before, shown on the start screen.</summary>
/// <param name="Path">The solution, project or folder that was opened.</param>
/// <param name="Name">The display name, such as the solution name.</param>
/// <param name="Description">A short summary of what it is, such as "C# · 2 projects".</param>
/// <param name="LastOpened">When it was last opened.</param>
public sealed record RecentWorkspace(string Path, string Name, string Description, DateTimeOffset LastOpened);

/// <summary>
/// The list of recently opened workspaces, newest first, stored as JSON in the app's state folder.
/// </summary>
public sealed class RecentWorkspaces
{
    /// <summary>How many entries are kept.</summary>
    public const int Capacity = 30;

    private readonly string _file;
    private readonly Lock _gate = new();
    private List<RecentWorkspace>? _entries;

    /// <summary>Creates the list over a JSON file, which is read on first use.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public RecentWorkspaces(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = paths.RecentWorkspacesFile;
    }

    /// <summary>The entries, newest first. Entries whose files no longer exist are left out.</summary>
    public IReadOnlyList<RecentWorkspace> GetAll()
    {
        lock (_gate)
        {
            return Entries.Where(e => File.Exists(e.Path) || Directory.Exists(e.Path)).ToList();
        }
    }

    /// <summary>Records that a workspace was opened, moving it to the top.</summary>
    /// <param name="entry">The workspace.</param>
    public void Add(RecentWorkspace entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            Entries.RemoveAll(e => string.Equals(e.Path, entry.Path, StringComparison.Ordinal));
            Entries.Insert(0, entry);
            if (Entries.Count > Capacity)
            {
                Entries.RemoveRange(Capacity, Entries.Count - Capacity);
            }

            Save();
        }
    }

    /// <summary>Removes a workspace from the list. Its files are not touched.</summary>
    /// <param name="path">The workspace path.</param>
    public void Remove(string path)
    {
        lock (_gate)
        {
            if (Entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.Ordinal)) > 0)
            {
                Save();
            }
        }
    }

    private List<RecentWorkspace> Entries => _entries ??= Load();

    private List<RecentWorkspace> Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                using var stream = File.OpenRead(_file);
                return JsonSerializer.Deserialize(stream, StateJsonContext.Default.ListRecentWorkspace) ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged list is not worth failing over; start a new one.
        }

        return [];
    }

    private void Save()
    {
        try
        {
            var temporary = _file + ".tmp";
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, Entries, StateJsonContext.Default.ListRecentWorkspace);
            }

            File.Move(temporary, _file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The list is a convenience; failing to save it must never interrupt the user.
        }
    }
}

/// <summary>Source-generated JSON serialization for TabStudio's state files, so they work with trimming.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<RecentWorkspace>))]
[JsonSerializable(typeof(SessionState))]
internal sealed partial class StateJsonContext : JsonSerializerContext
{
}
