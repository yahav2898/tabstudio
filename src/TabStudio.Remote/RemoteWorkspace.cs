using System.Security.Cryptography;
using TabStudio.Core.Workspaces;

namespace TabStudio.Remote;

/// <summary>An SFTP source cache shared by the editor, Solution Explorer and search. Saves detect conflicting remote edits.</summary>
public sealed class RemoteWorkspace(SshConnection connection, string remoteRoot, string cacheRoot) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string> _hashes = LoadHashes(cacheRoot);
    private string _remoteRoot = Normalize(remoteRoot);
    public SshConnection Connection { get; } = connection;
    public string RemoteRoot => _remoteRoot;
    public string LocalRoot { get; } = Path.GetFullPath(cacheRoot);
    public const int MaximumFiles = 10000;
    public const long MaximumFileBytes = 8 * 1024 * 1024;
    public const long MaximumCacheBytes = 128 * 1024 * 1024;

    /// <summary>Refreshes source files only; build artifacts and symlinks are excluded. Large trees fail explicitly.</summary>
    public async Task RefreshAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                var sftp = Connection.Sftp;
                if (!sftp.GetAttributes(_remoteRoot).IsDirectory) { throw new IOException("The remote path is not a directory."); }
                // Resolve the root once, then refuse symlinks below it so the cache cannot escape the requested tree.
                sftp.ChangeDirectory(_remoteRoot);
                _remoteRoot = Normalize(sftp.WorkingDirectory);
                Directory.CreateDirectory(LocalRoot);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var count = 0;
                long bytes = 0;
                void Walk(string remote, int depth)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (depth > 64) { throw new IOException("The remote folder exceeds the 64-level cache depth limit."); }
                    foreach (var item in sftp.ListDirectory(remote))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (item.Name is "." or ".." || item.IsSymbolicLink) { continue; }
                        if (item.Name.Contains('/') || item.Name.Contains('\\')) { throw new IOException("A remote file name cannot be represented safely on this device."); }
                        var path = ToLocalPath(item.FullName);
                        if (path is null) { throw new IOException("A remote listing escaped the opened folder."); }
                        if (item.IsDirectory)
                        {
                            if (PathUtilities.IsIgnoredDirectory(item.Name)) { continue; }
                            Directory.CreateDirectory(path);
                            Walk(item.FullName, depth + 1);
                        }
                        else if (item.IsRegularFile)
                        {
                            if (++count > MaximumFiles || item.Length > MaximumFileBytes || (bytes += item.Length) > MaximumCacheBytes)
                            {
                                throw new IOException("Open a smaller remote folder: the source cache supports 10,000 files, 8 MiB per file and 128 MiB in total.");
                            }
                            seen.Add(path);
                            progress?.Report(Path.GetRelativePath(LocalRoot, path));
                            using var data = new MemoryStream();
                            sftp.DownloadFile(item.FullName, data, transferred =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (transferred > MaximumFileBytes) { throw new IOException("A remote file grew beyond the 8 MiB cache limit."); }
                            });
                            var contents = data.ToArray();
                            var hash = Convert.ToHexString(SHA256.HashData(contents));
                            // Preserve locally modified cache files, including recovery text from a failed upload.
                            if (_hashes.TryGetValue(path, out var original) && File.Exists(path) && HashFile(path) != original) { continue; }
                            File.WriteAllBytes(path, contents);
                            _hashes[path] = hash;
                        }
                    }
                }
                Walk(_remoteRoot, 0);
                foreach (var stale in _hashes.Keys.Where(p => !seen.Contains(p)).ToList())
                {
                    if (File.Exists(stale) && HashFile(stale) == _hashes[stale]) { File.Delete(stale); _hashes.Remove(stale); }
                }
                SaveHashes();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Renci.SshNet.Common.SshException ex) { throw new IOException($"SFTP refresh failed: {ex.Message}", ex); }
        finally { _gate.Release(); }
    }

    /// <summary>Uploads through a sibling temporary file and an atomic OpenSSH rename. Failed uploads keep the editor dirty.</summary>
    public async Task SaveAsync(string localPath, CancellationToken cancellationToken = default)
    {
        var remote = ToRemotePath(localPath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sftp = Connection.Sftp;
                // Check canonical parent and file immediately before writing. A symlink introduced after refresh must not redirect a save.
                var parent = remote[..remote.LastIndexOf('/')];
                var known = _hashes.TryGetValue(Path.GetFullPath(localPath), out var expected);
                // A file added in the editor (Add > New Item, a new folder) has no baseline and does not exist yet on the
                // server: its folders are created below the opened root, and it is uploaded as a new file.
                if (!known && parent.Length > 0 && !sftp.Exists(parent)) { CreateDirectories(sftp, parent); }
                sftp.ChangeDirectory(parent.Length == 0 ? "/" : parent);
                if (Normalize(sftp.WorkingDirectory) != (parent.Length == 0 ? "/" : parent)) { throw new IOException("The remote file became a symbolic link. Refresh before saving."); }
                Renci.SshNet.Sftp.SftpFileAttributes? attributes = null;
                if (sftp.Exists(remote))
                {
                    if (sftp.Get(remote).IsSymbolicLink) { throw new IOException("The remote file became a symbolic link. Refresh before saving."); }
                    using var current = new MemoryStream();
                    sftp.DownloadFile(remote, current);
                    if (!known || Convert.ToHexString(SHA256.HashData(current.ToArray())) != expected)
                    {
                        throw new IOException("The remote file changed since it was downloaded. Your edits remain in the editor and cache. Preserve your changes, then choose Reload file from server in Remote Explorer to resolve the conflict.");
                    }
                    attributes = sftp.GetAttributes(remote);
                }
                else if (known)
                {
                    throw new IOException("The remote file was deleted since it was downloaded. Your edits remain in the editor and cache. Choose Refresh in Remote Explorer, then save again to create it.");
                }
                var temp = remote + ".tabstudio-" + Guid.NewGuid().ToString("N");
                try
                {
                    using var input = File.OpenRead(localPath);
                    sftp.UploadFile(input, temp, canOverride: false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (attributes is null)
                    {
                        sftp.RenameFile(temp, remote, isPosix: true);
                        _hashes[Path.GetFullPath(localPath)] = HashFile(localPath);
                        SaveHashes();
                        return;
                    }
                    var tempAttributes = sftp.GetAttributes(temp);
                    tempAttributes.OwnerCanRead = attributes.OwnerCanRead;
                    tempAttributes.OwnerCanWrite = attributes.OwnerCanWrite;
                    tempAttributes.OwnerCanExecute = attributes.OwnerCanExecute;
                    tempAttributes.GroupCanRead = attributes.GroupCanRead;
                    tempAttributes.GroupCanWrite = attributes.GroupCanWrite;
                    tempAttributes.GroupCanExecute = attributes.GroupCanExecute;
                    tempAttributes.OthersCanRead = attributes.OthersCanRead;
                    tempAttributes.OthersCanWrite = attributes.OthersCanWrite;
                    tempAttributes.OthersCanExecute = attributes.OthersCanExecute;
                    sftp.SetAttributes(temp, tempAttributes);
                    sftp.RenameFile(temp, remote, isPosix: true);
                    _hashes[Path.GetFullPath(localPath)] = HashFile(localPath);
                    SaveHashes();
                }
                finally { if (sftp.IsConnected && sftp.Exists(temp)) { sftp.DeleteFile(temp); } }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Renci.SshNet.Common.SshException ex) { throw new IOException($"SFTP save failed: {ex.Message}", ex); }
        finally { _gate.Release(); }
    }

    // Creates each missing folder from the opened root down; a folder that turns out to be a link is refused.
    private void CreateDirectories(Renci.SshNet.SftpClient sftp, string directory)
    {
        if (directory != _remoteRoot && !directory.StartsWith(_remoteRoot.TrimEnd('/') + "/", StringComparison.Ordinal)) { throw new IOException("The file is outside the remote folder."); }
        var path = _remoteRoot.TrimEnd('/');
        foreach (var part in directory[path.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            path += "/" + part;
            if (!sftp.Exists(path)) { sftp.CreateDirectory(path); }
            else if (!sftp.Get(path).IsDirectory) { throw new IOException($"The remote path {path} is not a folder."); }
        }
    }

    public string ToRemotePath(string localPath)
    {
        var relative = Path.GetRelativePath(LocalRoot, Path.GetFullPath(localPath));
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative)) { throw new IOException("The file is outside the remote cache."); }
        return relative == "." ? _remoteRoot : Normalize(_remoteRoot.TrimEnd('/') + "/" + relative.Replace(Path.DirectorySeparatorChar, '/'));
    }

    /// <summary>Explicitly discards one cached version and adopts the current server content as its new baseline.</summary>
    public async Task ReloadFileAsync(string localPath, CancellationToken cancellationToken = default)
    {
        var remote = ToRemotePath(localPath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                if (Connection.Sftp.Get(remote).IsSymbolicLink) { throw new IOException("The remote file is now a symbolic link."); }
                using var data = new MemoryStream();
                Connection.Sftp.DownloadFile(remote, data, transferred =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (transferred > MaximumFileBytes) { throw new IOException("The file exceeds the 8 MiB cache limit."); }
                });
                var contents = data.ToArray();
                File.WriteAllBytes(localPath, contents);
                _hashes[Path.GetFullPath(localPath)] = Convert.ToHexString(SHA256.HashData(contents));
                SaveHashes();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Renci.SshNet.Common.SshException ex) { throw new IOException("SFTP reload failed: " + ex.Message, ex); }
        finally { _gate.Release(); }
    }

    public string? ToLocalPath(string remotePath)
    {
        var full = Normalize(remotePath.StartsWith('/') ? remotePath : _remoteRoot.TrimEnd('/') + "/" + remotePath);
        var prefix = _remoteRoot.TrimEnd('/') + "/";
        if (full == _remoteRoot) { return LocalRoot; }
        if (!full.StartsWith(prefix, StringComparison.Ordinal)) { return null; }
        var relative = full[prefix.Length..];
        if (relative.Contains('\\') || (OperatingSystem.IsWindows() && relative.Contains(':'))) { return null; }
        return Path.GetFullPath(Path.Combine(LocalRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static string Normalize(string path)
    {
        if (!path.StartsWith('/') || path.Any(char.IsControl)) { throw new ArgumentException("Use an absolute remote path, for example /home/user/project.", nameof(path)); }
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") { continue; }
            if (part == "..") { if (parts.Count > 0) { parts.RemoveAt(parts.Count - 1); } }
            else { parts.Add(part); }
        }
        return "/" + string.Join('/', parts);
    }
    private static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static Dictionary<string, string> LoadHashes(string root)
    {
        var path = Path.Combine(root, ".tabstudio", "sftp-baseline.json");
        return File.Exists(path) ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new(StringComparer.Ordinal) : new(StringComparer.Ordinal);
    }
    private void SaveHashes()
    {
        Directory.CreateDirectory(Path.Combine(LocalRoot, ".tabstudio"));
        SshHostStore.AtomicWrite(Path.Combine(LocalRoot, ".tabstudio", "sftp-baseline.json"), System.Text.Json.JsonSerializer.Serialize(_hashes));
    }
    public void Dispose() => Connection.Dispose();
}
