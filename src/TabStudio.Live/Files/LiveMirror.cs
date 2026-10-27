using System.Text.Json;
using TabStudio.Link.Language;
using TabStudio.Live.Protocol;

namespace TabStudio.Live.Files;

/// <summary>
/// A guest's copy of the files a host shares, in a private folder of the guest's, so the guest's IDE opens the
/// workspace like any other and its IntelliSense runs on the guest's own device. It is kept between connections of
/// one session, so a guest that reconnects only fetches what changed, and deleted when the guest leaves.
/// </summary>
/// <remarks>
/// Every path the host names is checked with TabStudio Link's <see cref="LinkPathMap"/>, as Link's own mirror does, so a
/// host can never write outside the folder. Not thread-safe.
/// </remarks>
public sealed class LiveMirror
{
    private const string IndexFileName = ".tabstudio-live-index.json";

    private readonly LinkPathMap _map;
    private readonly string _indexPath;
    private Dictionary<string, string> _index;

    /// <summary>Opens or creates the mirror of one session's workspace.</summary>
    /// <param name="root">The folder of the copy, which holds nothing else.</param>
    public LiveMirror(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        Directory.CreateDirectory(root);
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _map = new LinkPathMap("/", '/', Root);
        _indexPath = Path.Combine(Path.GetDirectoryName(Root) ?? Root, Path.GetFileName(Root) + IndexFileName);
        _index = LoadIndex(_indexPath);
    }

    /// <summary>The folder of the copy.</summary>
    public string Root { get; }

    /// <summary>The full path of a shared file in the copy, or <see langword="null"/> when its path is unsafe.</summary>
    /// <param name="relativePath">The file's path in the workspace.</param>
    public string? PathOf(string relativePath) =>
        LiveWorkspacePaths.Normalize(relativePath) is { } normalized ? _map.RelativeToHost(normalized) : null;

    /// <summary>The files the copy lacks or has in another version.</summary>
    /// <param name="files">The host's files.</param>
    public IReadOnlyList<LiveFileEntry> Missing(IReadOnlyList<LiveFileEntry> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var missing = new List<LiveFileEntry>();
        foreach (var file in files)
        {
            if (PathOf(file.Path) is not { } path)
            {
                continue;
            }

            if (!_index.TryGetValue(file.Path, out var hash) || !string.Equals(hash, file.Hash, StringComparison.Ordinal) || !HasLength(path, file.Length))
            {
                missing.Add(file);
            }
        }

        return missing;
    }

    /// <summary>Writes a file the host sent.</summary>
    /// <param name="file">The file's entry.</param>
    /// <param name="content">Its bytes.</param>
    /// <returns>Whether it was written: its path is safe and its bytes match the entry.</returns>
    public bool Write(LiveFileEntry file, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);
        if (PathOf(file.Path) is not { } path || !string.Equals(LiveWorkspaceFiles.HashOf(content), file.Hash, StringComparison.Ordinal))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        _index[file.Path] = file.Hash;
        return true;
    }

    /// <summary>Writes text a participant typed into the copy, as the host's save of a shared document does.</summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="content">Its bytes.</param>
    public void WriteSaved(string relativePath, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (PathOf(relativePath) is not { } path)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        _index[relativePath] = LiveWorkspaceFiles.HashOf(content);
    }

    /// <summary>Removes files the host no longer shares.</summary>
    /// <param name="files">Every file the host shares now.</param>
    public void RemoveOthers(IReadOnlyList<LiveFileEntry> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var kept = files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in _index.Keys.Where(p => !kept.Contains(p)).ToList())
        {
            Remove(stale);
        }
    }

    /// <summary>Removes one file the host no longer shares.</summary>
    /// <param name="relativePath">The file.</param>
    public void Remove(string relativePath)
    {
        if (PathOf(relativePath) is { } path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Left behind; it is not part of the workspace any more either way.
            }
        }

        _index.Remove(relativePath);
    }

    /// <summary>Saves which version of each file the copy has, so a reconnection fetches only what changed.</summary>
    public void SaveIndex()
    {
        try
        {
            File.WriteAllText(_indexPath, JsonSerializer.Serialize(_index, LiveJsonContext.Default.DictionaryStringString));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Without the index the next connection fetches every file again.
        }
    }

    /// <summary>Deletes the copy: nothing the host shared stays on the device.</summary>
    public void Delete()
    {
        _index = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }

            File.Delete(_indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the app's cache is cleaned again when the next session starts.
        }
    }

    private static bool HasLength(string path, long length)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length == length;
    }

    private static Dictionary<string, string> LoadIndex(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize(File.ReadAllText(path), LiveJsonContext.Default.DictionaryStringString) is { } index)
            {
                return new Dictionary<string, string>(index, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged index: every file is fetched again.
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
