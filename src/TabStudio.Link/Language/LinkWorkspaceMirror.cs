using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.Link.Language;

/// <summary>
/// The host's copy of a tablet's workspace, in a folder of the host's own. It is kept between connections, so a
/// tablet that reconnects only sends what changed. The tablet's disk is never touched: files arrive over the link.
/// </summary>
/// <remarks>
/// An index next to the files remembers the hash of every file the tablet sent. Files the host made itself, such as
/// the <c>obj</c> folders of package restores, are not in the index and are left alone. The three most recently
/// used workspaces of a tablet are kept.
/// </remarks>
internal sealed class LinkWorkspaceMirror
{
    private const string IndexFileName = ".tabstudio-link-index.json";
    private const int KeptWorkspaces = 3;

    private readonly string _directory;
    private PrepareWorkspaceRequest? _pending;
    private LinkPathMap? _pendingMap;
    private Dictionary<string, string> _index = new(StringComparer.Ordinal);

    /// <summary>Creates the mirror of one tablet's workspaces.</summary>
    /// <param name="directory">The folder for this tablet's workspaces, on the host.</param>
    public LinkWorkspaceMirror(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
    }

    /// <summary>The path map of the workspace loaded last.</summary>
    public LinkPathMap? Current { get; private set; }

    /// <summary>Compares the tablet's file list with the mirror.</summary>
    /// <param name="request">The tablet's files.</param>
    /// <returns>The positions, in the list, of the files the mirror lacks or has in another version.</returns>
    public List<int> Prepare(PrepareWorkspaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Separator.Length != 1 || request.ClientRoot.Length == 0)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The workspace's root or separator is missing.");
        }

        var root = Path.Combine(_directory, FolderName(request.Name, request.ClientRoot));
        Directory.CreateDirectory(root);
        Directory.SetLastWriteTimeUtc(root, DateTime.UtcNow);
        RemoveOldWorkspaces(root);

        _index = LoadIndex(root);
        _pendingMap = new LinkPathMap(request.ClientRoot, request.Separator[0], root);
        _pending = request;

        var missing = new List<int>();
        for (var i = 0; i < request.Files.Count; i++)
        {
            var file = request.Files[i];
            if (_pendingMap.RelativeToHost(file.Path) is not { } path)
            {
                // A name this host cannot store; the file is left out.
                continue;
            }

            if (!_index.TryGetValue(file.Path, out var hash) || !string.Equals(hash, file.Hash, StringComparison.Ordinal) || !HasLength(path, file.Length))
            {
                missing.Add(i);
            }
        }

        return missing;
    }

    /// <summary>Writes files the tablet sent.</summary>
    /// <param name="positions">The files' positions in the prepared list.</param>
    /// <param name="contents">Their bytes, in the same order.</param>
    public void Write(IReadOnlyList<int> positions, IReadOnlyList<byte[]> contents)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(contents);
        if (_pending is not { } pending || _pendingMap is not { } map)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "Send the workspace's file list first.");
        }

        if (positions.Count != contents.Count)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "Every file needs its content.");
        }

        for (var i = 0; i < positions.Count; i++)
        {
            if (positions[i] < 0 || positions[i] >= pending.Files.Count)
            {
                throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, $"There is no file {positions[i]}.");
            }

            var file = pending.Files[positions[i]];
            if (map.RelativeToHost(file.Path) is not { } path)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, contents[i]);
            _index[file.Path] = file.Hash;
        }
    }

    /// <summary>Removes the files the tablet no longer has, saves the index, and makes the workspace current.</summary>
    /// <returns>The workspace's path map.</returns>
    public LinkPathMap Commit()
    {
        if (_pending is not { } pending || _pendingMap is not { } map)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "Send the workspace's file list first.");
        }

        var kept = pending.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in _index.Keys.Where(p => !kept.Contains(p)).ToList())
        {
            if (map.RelativeToHost(path) is { } stale)
            {
                try
                {
                    File.Delete(stale);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Left behind; it is not part of the workspace any more either way.
                }
            }

            _index.Remove(path);
        }

        SaveIndex(map.HostRoot, _index);
        Current = map;
        _pending = null;
        _pendingMap = null;
        return map;
    }

    /// <summary>A folder name for a workspace: its name, made safe, and a hash of the tablet's path.</summary>
    private static string FolderName(string name, string clientRoot)
    {
        var safe = new StringBuilder();
        foreach (var c in name)
        {
            safe.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-');
            if (safe.Length == 32)
            {
                break;
            }
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientRoot)))[..12];
        var trimmed = safe.ToString().Trim('.', '-');
        return trimmed.Length == 0 ? hash : $"{trimmed}-{hash}";
    }

    private void RemoveOldWorkspaces(string current)
    {
        try
        {
            var old = new DirectoryInfo(_directory).GetDirectories()
                .Where(d => !string.Equals(d.FullName, Path.GetFullPath(current), StringComparison.Ordinal))
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .Skip(KeptWorkspaces - 1);
            foreach (var directory in old)
            {
                directory.Delete(recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cleaning up is best effort.
        }
    }

    private static bool HasLength(string path, long length)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length == length;
    }

    private static Dictionary<string, string> LoadIndex(string root)
    {
        try
        {
            var path = Path.Combine(root, IndexFileName);
            if (File.Exists(path) && JsonSerializer.Deserialize(File.ReadAllText(path), LinkMirrorJsonContext.Default.DictionaryStringString) is { } index)
            {
                return new Dictionary<string, string>(index, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged index: every file is sent again.
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static void SaveIndex(string root, Dictionary<string, string> index)
    {
        var path = Path.Combine(root, IndexFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(index, LinkMirrorJsonContext.Default.DictionaryStringString));
    }
}

/// <summary>Source-generated JSON for the mirror's index.</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LinkMirrorJsonContext : JsonSerializerContext
{
}
