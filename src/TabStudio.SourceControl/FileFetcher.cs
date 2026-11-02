using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

/// <summary>
/// Writes files of a commit into a folder. A few files are downloaded one by one; many are taken from one zip
/// archive of the commit, and any file the archive lacks or holds differently (GitHub applies
/// <c>export-ignore</c> and <c>export-subst</c> attributes to archives) is then downloaded on its own. Signed out,
/// those come from the raw file host, since GitHub's API answers only 60 requests an hour without a token.
/// </summary>
internal sealed class FileFetcher(GitHubClient client, string owner, string repository)
{
    /// <summary>From this many files on, one archive is cheaper than a request per file.</summary>
    internal const int ArchiveThreshold = 40;

    /// <summary>The largest archive TabStudio downloads: 1 GB.</summary>
    internal const long MaximumArchiveBytes = 1L << 30;

    private const int Parallelism = 4;

    /// <summary>The largest file GitHub stores outside Git LFS: 100 MB.</summary>
    private const long MaximumFileBytes = 100L << 20;

    /// <summary>Writes files of a commit.</summary>
    /// <param name="root">The folder to write into.</param>
    /// <param name="commitSha">The commit the files belong to, for the archive.</param>
    /// <param name="files">The files, by path.</param>
    /// <param name="hashes">Learns the hash of every written file.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>What the person should know, such as Git LFS files that could not be downloaded; empty when nothing.</returns>
    public async Task<IReadOnlyList<string>> WriteAsync(
        string root,
        string commitSha,
        IReadOnlyDictionary<string, TrackedFile> files,
        HashCache hashes,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var pointers = new ConcurrentDictionary<string, LfsPointer>(StringComparer.Ordinal);
        var remaining = files.Count >= ArchiveThreshold
            ? await WriteFromArchiveAsync(root, commitSha, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false)
            : [.. files.Keys];

        await WriteBlobsAsync(root, commitSha, remaining, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false);
        return await WriteLfsObjectsAsync(root, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The largest Git LFS file TabStudio downloads: 2 GB.</summary>
    internal const long MaximumLfsBytes = 2L << 30;

    /// <summary>
    /// Replaces the Git LFS pointers that were written with the files they stand for, as <c>git lfs pull</c> does after
    /// a clone: before 0.21.2 a repository's images, models and binaries stored with Git LFS arrived as three lines of
    /// text. The hash cache keeps the pointer's hash for each, so Git Changes does not see them as changed. A file that
    /// cannot be downloaded keeps its pointer and is named in the returned notes.
    /// </summary>
    private async Task<IReadOnlyList<string>> WriteLfsObjectsAsync(
        string root,
        IReadOnlyDictionary<string, TrackedFile> files,
        HashCache hashes,
        ConcurrentDictionary<string, LfsPointer> pointers,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (pointers.IsEmpty)
        {
            return [];
        }

        if (!client.CanReadLfs)
        {
            return [$"{Files(pointers.Count)} stored with Git LFS hold only their pointers: this server does not serve Git LFS files to TabStudio."];
        }

        var wanted = pointers.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        var total = wanted.Sum(p => p.Value.Size);
        if (FreeSpace(root) is { } free && total > free)
        {
            return [$"{Files(wanted.Count)} stored with Git LFS ({FormatSize(total)}) were not downloaded: the tablet has {FormatSize(free)} free. They hold only their pointers; free some space and download the repository again."];
        }

        var failed = new List<string>();
        var done = 0;
        progress?.Report(new SyncProgress($"Downloading {Files(wanted.Count)} stored with Git LFS", 0, wanted.Count));
        foreach (var batch in wanted.Chunk(100))
        {
            IReadOnlyList<LfsObjectLink> links;
            try
            {
                links = await client.GetLfsDownloadLinksAsync(owner, repository, [.. batch.Select(p => (p.Value.Oid, p.Value.Size)).Distinct()], cancellationToken).ConfigureAwait(false);
            }
            catch (GitHubApiException ex)
            {
                failed.Add($"{Files(batch.Length)} stored with Git LFS were not downloaded and hold only their pointers: {ex.Message}");
                continue;
            }

            var byOid = links.GroupBy(l => l.Oid, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var (path, pointer) in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!byOid.TryGetValue(pointer.Oid, out var link))
                    {
                        throw new GitHubApiException("the server did not answer for it.");
                    }

                    await DownloadLfsObjectAsync(root, path, pointer, link, cancellationToken).ConfigureAwait(false);
                    hashes.Remember(root, path, files[path].Sha);
                }
                catch (GitHubApiException ex)
                {
                    failed.Add($"{path} is stored with Git LFS and holds only its pointer: {ex.Message.TrimEnd('.')}.");
                }

                progress?.Report(new SyncProgress($"Downloading {Files(wanted.Count)} stored with Git LFS", ++done, wanted.Count));
            }
        }

        return failed.Count <= 5
            ? failed
            : [.. failed.Take(4), $"{failed.Count - 4} more files stored with Git LFS hold only their pointers."];
    }

    private async Task DownloadLfsObjectAsync(string root, string path, LfsPointer pointer, LfsObjectLink link, CancellationToken cancellationToken)
    {
        if (pointer.Size > MaximumLfsBytes)
        {
            throw new GitHubApiException($"it is {FormatSize(pointer.Size)}, more than TabStudio downloads.");
        }

        var full = WorkingTree.FullPath(root, path);
        var temporary = Path.Combine(root, SyncMetadata.FolderName, $"lfs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
        try
        {
            string hash;
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                await client.DownloadLfsObjectAsync(link, stream, cancellationToken).ConfigureAwait(false);
                stream.Position = 0;
                hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            }

            if (!string.Equals(hash, pointer.Oid, StringComparison.Ordinal))
            {
                throw new GitHubApiException("what the server sent is not the file the pointer names.");
            }

            File.SetAttributes(full, FileAttributes.Normal);
            File.Move(temporary, full, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static long? FreeSpace(string root)
    {
        try
        {
            return new DriveInfo(Path.GetFullPath(root)).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

    /// <summary>A Git LFS pointer: the small text a repository holds in place of a file stored with Git LFS.</summary>
    /// <param name="Oid">The file's SHA-256, in hexadecimal.</param>
    /// <param name="Size">The file's size in bytes.</param>
    internal sealed record LfsPointer(string Oid, long Size)
    {
        private const string Version = "version https://git-lfs.github.com/spec/v1\n";

        /// <summary>Reads a pointer; <see langword="null"/> when the content is not one.</summary>
        /// <param name="content">A file's content.</param>
        public static LfsPointer? TryParse(ReadOnlySpan<byte> content)
        {
            // A pointer is at most a few hundred bytes and starts with its version line.
            if (content.Length is < 100 or > 1024 || !content.StartsWith(Encoding.ASCII.GetBytes(Version)))
            {
                return null;
            }

            string? oid = null;
            long? size = null;
            foreach (var line in Encoding.ASCII.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("oid sha256:", StringComparison.Ordinal) && line.Length == 11 + 64)
                {
                    oid = line[11..];
                }
                else if (line.StartsWith("size ", StringComparison.Ordinal) && long.TryParse(line.AsSpan(5), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    size = value;
                }
            }

            return oid is not null && size is { } length && oid.All(char.IsAsciiHexDigitLower) ? new LfsPointer(oid, length) : null;
        }
    }

    /// <summary>Downloads one file's content.</summary>
    /// <param name="sha">The blob's hash.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public Task<byte[]> ReadAsync(string sha, CancellationToken cancellationToken) =>
        client.GetBlobAsync(owner, repository, sha, cancellationToken);

    /// <summary>Extracts the wanted files from the commit's archive.</summary>
    /// <returns>The files that still have to be downloaded one by one.</returns>
    private async Task<List<string>> WriteFromArchiveAsync(
        string root,
        string commitSha,
        IReadOnlyDictionary<string, TrackedFile> files,
        HashCache hashes,
        ConcurrentDictionary<string, LfsPointer> pointers,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(root, SyncMetadata.FolderName, $"download-{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
        try
        {
            await using (var stream = File.Create(temporary))
            {
                var bytes = new Progress<(long Received, long? Total)>(p =>
                    progress?.Report(new SyncProgress($"Downloading {FormatSize(p.Received)}", (int)(p.Received / 1024), (int)((p.Total ?? 0) / 1024))));
                await client.DownloadArchiveAsync(owner, repository, commitSha, stream, MaximumArchiveBytes, bytes, cancellationToken).ConfigureAwait(false);
            }

            var missing = new HashSet<string>(files.Keys, StringComparer.Ordinal);
            using var zip = ZipFile.OpenRead(temporary);
            var written = 0;
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = StripRoot(entry.FullName);
                if (path is null || !files.TryGetValue(path, out var file) || !missing.Contains(path))
                {
                    continue;
                }

                byte[] content;
                using (var source = entry.Open())
                using (var buffer = new MemoryStream(checked((int)entry.Length)))
                {
                    await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                    content = buffer.ToArray();
                }

                if (!string.Equals(GitObjects.BlobHash(content), file.Sha, StringComparison.Ordinal))
                {
                    continue;
                }

                await WriteFileAsync(root, path, content, cancellationToken).ConfigureAwait(false);
                hashes.Remember(root, path, file.Sha);
                missing.Remove(path);
                if (LfsPointer.TryParse(content) is { } pointer)
                {
                    pointers[path] = pointer;
                }
                if (++written % 50 == 0)
                {
                    progress?.Report(new SyncProgress("Writing files", written, files.Count));
                }
            }

            return [.. missing];
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private async Task WriteBlobsAsync(
        string root,
        string commitSha,
        List<string> paths,
        IReadOnlyDictionary<string, TrackedFile> files,
        HashCache hashes,
        ConcurrentDictionary<string, LfsPointer> pointers,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return;
        }

        var done = 0;
        var gate = new Lock();
        progress?.Report(new SyncProgress(paths.Count == 1 ? "Downloading 1 file" : $"Downloading {paths.Count} files", 0, paths.Count));
        var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(paths, options, async (path, token) =>
        {
            var content = await ReadFileAsync(commitSha, path, files[path].Sha, token).ConfigureAwait(false);
            await WriteFileAsync(root, path, content, token).ConfigureAwait(false);
            if (LfsPointer.TryParse(content) is { } pointer)
            {
                pointers[path] = pointer;
            }
            lock (gate)
            {
                hashes.Remember(root, path, files[path].Sha);
                done++;
                progress?.Report(new SyncProgress($"Downloading {paths.Count} files", done, paths.Count));
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads one file of a commit: signed out from the raw file host, which does not use the API's 60 requests an
    /// hour, and through the API when signed in, when there is no raw host, or when what it sent is not the blob.
    /// </summary>
    private async Task<byte[]> ReadFileAsync(string commitSha, string path, string sha, CancellationToken cancellationToken)
    {
        if (!client.IsSignedIn
            && client.CanReadRawFiles
            && await client.TryGetRawFileAsync(owner, repository, commitSha, path, MaximumFileBytes, cancellationToken).ConfigureAwait(false) is { } raw
            && string.Equals(GitObjects.BlobHash(raw), sha, StringComparison.Ordinal))
        {
            return raw;
        }

        return await client.GetBlobAsync(owner, repository, sha, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a file, creating its folder and replacing a read-only file.</summary>
    internal static async Task WriteFileAsync(string root, string relativePath, byte[] content, CancellationToken cancellationToken)
    {
        var full = WorkingTree.FullPath(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if (File.Exists(full))
        {
            File.SetAttributes(full, FileAttributes.Normal);
        }

        await File.WriteAllBytesAsync(full, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a file and any folders it leaves empty, up to the root.</summary>
    internal static void DeleteFile(string root, string relativePath)
    {
        var full = WorkingTree.FullPath(root, relativePath);
        if (File.Exists(full))
        {
            File.SetAttributes(full, FileAttributes.Normal);
            File.Delete(full);
        }

        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        for (var folder = Path.GetDirectoryName(full); folder is not null && folder.Length > rootFull.Length; folder = Path.GetDirectoryName(folder))
        {
            if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any())
            {
                break;
            }

            Directory.Delete(folder);
        }
    }

    /// <summary>The path of an archive entry without the archive's top-level folder; null for folders.</summary>
    private static string? StripRoot(string name)
    {
        var normalized = name.Replace('\\', '/');
        var slash = normalized.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || normalized.EndsWith('/'))
        {
            return null;
        }

        return normalized[(slash + 1)..];
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover download in .tabstudio is harmless and never committed.
        }
    }
}
