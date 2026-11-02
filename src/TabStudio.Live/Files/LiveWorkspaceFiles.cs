using System.Collections.Concurrent;
using System.Security.Cryptography;
using TabStudio.Core.Workspaces;

namespace TabStudio.Live.Files;

/// <summary>
/// The files a host shares: every file under the workspace's folder except the folders a workspace leaves out (build
/// output, <c>.git</c> and other dot folders), links, and files too large to send, with their hashes, which are kept
/// while a file's length and time stay the same. Nothing outside the folder is ever listed.
/// </summary>
/// <remarks>Thread-safe.</remarks>
public sealed class LiveWorkspaceFiles
{
    private readonly string _root;
    private readonly ConcurrentDictionary<string, CachedHash> _hashes = new(StringComparer.Ordinal);

    /// <summary>Creates the list of a workspace's folder; nothing is read until <see cref="Collect"/>.</summary>
    /// <param name="root">The workspace's folder.</param>
    public LiveWorkspaceFiles(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    /// <summary>The workspace's folder.</summary>
    public string Root => _root;

    /// <summary>Lists and hashes every shared file.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="IOException">The workspace has more files, or more bytes, than a session shares.</exception>
    public IReadOnlyList<LiveFileEntry> Collect(CancellationToken cancellationToken) => Collect(new DirectoryInfo(_root), cancellationToken);

    /// <summary>
    /// The entries of the files in a folder that appeared in the workspace, such as one renamed, moved or unzipped
    /// into it; none when the path is not such a folder.
    /// </summary>
    /// <param name="relativePath">The folder.</param>
    public IReadOnlyList<LiveFileEntry> FindFolder(string relativePath)
    {
        if (LiveWorkspacePaths.ToFullPath(_root, relativePath) is not { } full
            || relativePath.Split('/').Any(PathUtilities.IsIgnoredDirectory))
        {
            return [];
        }

        var folder = new DirectoryInfo(full);
        try
        {
            return folder.Exists && !folder.Attributes.HasFlag(FileAttributes.ReparsePoint) && folder.LinkTarget is null
                ? Collect(folder, CancellationToken.None)
                : [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private List<LiveFileEntry> Collect(DirectoryInfo start, CancellationToken cancellationToken)
    {
        var entries = new List<LiveFileEntry>();
        long total = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(start);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemInfo[] children;
            try
            {
                children = directory.GetFileSystemInfos();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                if (child.Attributes.HasFlag(FileAttributes.ReparsePoint) || child.LinkTarget is not null)
                {
                    continue;
                }

                if (child is DirectoryInfo folder)
                {
                    if (!PathUtilities.IsIgnoredDirectory(folder.Name))
                    {
                        pending.Push(folder);
                    }

                    continue;
                }

                if (child is not FileInfo file || file.Length > LiveProtocol.MaxFileLength || Entry(file) is not { } entry)
                {
                    continue;
                }

                total += entry.Length;
                entries.Add(entry);
                if (entries.Count > LiveProtocol.MaxFiles || total > LiveProtocol.MaxTotalLength)
                {
                    throw new IOException(
                        $"The workspace is larger than a TabStudio Live session shares: at most {LiveProtocol.MaxFiles} files and {LiveProtocol.MaxTotalLength / (1024 * 1024)} MB. Open a smaller folder to share it.");
                }
            }
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return entries;
    }

    /// <summary>The entry of one file as it is now, or <see langword="null"/> when it is gone or not shared.</summary>
    /// <param name="relativePath">The file.</param>
    public LiveFileEntry? Find(string relativePath)
    {
        if (LiveWorkspacePaths.ToFullPath(_root, relativePath) is not { } full)
        {
            return null;
        }

        var file = new FileInfo(full);
        return file.Exists && file.Length <= LiveProtocol.MaxFileLength ? Entry(file) : null;
    }

    /// <summary>The bytes of a shared file, or <see langword="null"/> when it is gone or not shared.</summary>
    /// <param name="relativePath">The file.</param>
    public byte[]? Read(string relativePath)
    {
        if (LiveWorkspacePaths.ToFullPath(_root, relativePath) is not { } full)
        {
            return null;
        }

        try
        {
            var file = new FileInfo(full);
            return file.Exists && file.Length <= LiveProtocol.MaxFileLength ? File.ReadAllBytes(full) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The SHA-256 of some bytes in hexadecimal, as entries carry it.</summary>
    /// <param name="content">The bytes.</param>
    public static string HashOf(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));

    private LiveFileEntry? Entry(FileInfo file)
    {
        if (LiveWorkspacePaths.ToRelativePath(_root, file.FullName) is not { } relative)
        {
            return null;
        }

        var stamp = (file.Length, file.LastWriteTimeUtc.Ticks);
        if (!_hashes.TryGetValue(relative, out var cached) || cached.Stamp != stamp)
        {
            try
            {
                cached = new CachedHash(stamp, HashOf(File.ReadAllBytes(file.FullName)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            _hashes[relative] = cached;
        }

        return new LiveFileEntry(relative, file.Length, cached.Hash);
    }

    /// <summary>A file's hash, and the length and time it was taken at.</summary>
    private sealed record CachedHash((long Length, long Ticks) Stamp, string Hash);
}
