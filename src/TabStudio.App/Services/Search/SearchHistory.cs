using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Services.Search;

/// <summary>
/// What the user opened from the search, most recent first: shown before anything is typed, and ranked higher when it
/// matches again. The last ones are kept in the app's state folder, so the ranking survives a restart.
/// </summary>
public sealed class SearchHistory
{
    /// <summary>How many openings are remembered.</summary>
    public const int Capacity = 50;

    private readonly string? _file;
    private readonly List<string> _ids;
    private readonly List<SearchItem> _items = [];

    /// <summary>Creates a history kept in a file, or in memory only.</summary>
    /// <param name="paths">The app's folders, or <see langword="null"/> to keep nothing on disk.</param>
    public SearchHistory(AppPaths? paths = null)
    {
        _file = paths is null ? null : Path.Combine(paths.State, "search-history.json");
        _ids = Load(_file);
    }

    /// <summary>The items opened in this session, most recent first.</summary>
    public IReadOnlyList<SearchItem> Recent => _items;

    /// <summary>Remembers that an item was opened.</summary>
    /// <param name="item">The item.</param>
    public void Record(SearchItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.RemoveAll(other => other.Id == item.Id);
        _items.Insert(0, item);
        _ids.Remove(item.Id);
        _ids.Insert(0, item.Id);
        if (_ids.Count > Capacity)
        {
            _ids.RemoveRange(Capacity, _ids.Count - Capacity);
        }

        if (_items.Count > Capacity)
        {
            _items.RemoveRange(Capacity, _items.Count - Capacity);
        }

        Save();
    }

    /// <summary>How much higher an item ranks for having been opened: the more recently, the more.</summary>
    /// <param name="item">The item.</param>
    public int Boost(SearchItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = _ids.IndexOf(item.Id);
        return index < 0 ? 0 : 150 - (index * 150 / Capacity);
    }

    private static List<string> Load(string? file)
    {
        if (file is null)
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(file);
            return JsonSerializer.Deserialize(stream, SearchHistoryJson.Default.ListString) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return [];
        }
    }

    private void Save()
    {
        if (_file is null)
        {
            return;
        }

        try
        {
            var temporary = _file + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_ids, SearchHistoryJson.Default.ListString));
            File.Move(temporary, _file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The ranking is a convenience; it lasts the session without the file.
        }
    }
}

/// <summary>Source-generated JSON for the search history.</summary>
[JsonSerializable(typeof(List<string>))]
internal sealed partial class SearchHistoryJson : JsonSerializerContext
{
}
