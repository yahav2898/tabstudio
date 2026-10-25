using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.SourceControl;

/// <summary>
/// Remembers the hash of each file together with its size and modification time, so finding changes only
/// reads files that were touched. This is what Git's index does, including its guard against "racy" files:
/// a file modified within two seconds of being hashed is hashed again next time.
/// </summary>
public sealed class HashCache
{
    private const string FileName = "hashes.json";
    private static readonly long RacyWindow = TimeSpan.FromSeconds(2).Ticks;

    private readonly Dictionary<string, CachedHash> _entries;
    private bool _changed;

    private HashCache(Dictionary<string, CachedHash> entries) => _entries = entries;

    /// <summary>An empty cache, which hashes every file.</summary>
    public static HashCache Empty() => new(new Dictionary<string, CachedHash>(StringComparer.Ordinal));

    /// <summary>Reads a linked folder's cache; an empty one when there is none or it is damaged.</summary>
    /// <param name="root">The linked folder.</param>
    public static HashCache Load(string root)
    {
        try
        {
            using var stream = File.OpenRead(PathFor(root));
            var entries = JsonSerializer.Deserialize(stream, HashCacheJsonContext.Default.DictionaryStringCachedHash);
            return entries is null ? Empty() : new HashCache(new Dictionary<string, CachedHash>(entries, StringComparer.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Empty();
        }
    }

    /// <summary>Writes the cache if anything changed. Failures are ignored: the cache only saves time.</summary>
    /// <param name="root">The linked folder.</param>
    public void Save(string root)
    {
        if (!_changed)
        {
            return;
        }

        try
        {
            var path = PathFor(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, _entries, HashCacheJsonContext.Default.DictionaryStringCachedHash);
            }

            File.Move(temporary, path, overwrite: true);
            _changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Next time the files are simply hashed again.
        }
    }

    /// <summary>The Git blob hash of a file, read from the cache when the file has not changed since.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="relativePath">The path relative to the root, with <c>/</c> separators.</param>
    public string GetHash(string root, string relativePath)
    {
        var info = new FileInfo(WorkingTree.FullPath(root, relativePath));
        var ticks = info.LastWriteTimeUtc.Ticks;
        if (_entries.TryGetValue(relativePath, out var cached)
            && cached.Length == info.Length
            && cached.LastWriteTicks == ticks
            && cached.RecordedTicks - ticks > RacyWindow)
        {
            return cached.Sha;
        }

        var sha = GitObjects.BlobHash(File.ReadAllBytes(info.FullName));
        Record(relativePath, info.Length, ticks, sha);
        return sha;
    }

    /// <summary>Records the hash of a file TabStudio just wrote.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="relativePath">The path relative to the root, with <c>/</c> separators.</param>
    /// <param name="sha">The file's hash.</param>
    public void Remember(string root, string relativePath, string sha)
    {
        var info = new FileInfo(WorkingTree.FullPath(root, relativePath));
        Record(relativePath, info.Length, info.LastWriteTimeUtc.Ticks, sha);
    }

    /// <summary>Forgets a file, for example after deleting it.</summary>
    /// <param name="relativePath">The path relative to the root, with <c>/</c> separators.</param>
    public void Forget(string relativePath) => _changed |= _entries.Remove(relativePath);

    private void Record(string relativePath, long length, long ticks, string sha)
    {
        _entries[relativePath] = new CachedHash(length, ticks, DateTime.UtcNow.Ticks, sha);
        _changed = true;
    }

    private static string PathFor(string root) => Path.Combine(root, SyncMetadata.FolderName, FileName);
}

/// <summary>A cached file hash.</summary>
/// <param name="Length">The file's size when hashed.</param>
/// <param name="LastWriteTicks">Its modification time when hashed.</param>
/// <param name="RecordedTicks">When it was hashed.</param>
/// <param name="Sha">The hash.</param>
internal sealed record CachedHash(long Length, long LastWriteTicks, long RecordedTicks, string Sha);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, CachedHash>))]
internal sealed partial class HashCacheJsonContext : JsonSerializerContext
{
}
