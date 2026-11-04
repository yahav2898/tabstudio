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
internal sealed class FileFetcher(GitHubClient client, string owner, string repository) : IFileSource
{
    /// <summary>From this many files on, one archive is cheaper than a request per file.</summary>
    internal const int ArchiveThreshold = 40;

    /// <summary>
    /// The largest archive TabStudio downloads: 1 GB. A larger repository's files come over Git's own protocol instead,
    /// in batches.
    /// </summary>
    internal static long MaximumArchiveBytes { get; set; } = 1L << 30;

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
        List<string> remaining = [.. files.Keys];
        var overArchiveLimit = false;
        if (files.Count >= ArchiveThreshold)
        {
            try
            {
                remaining = await WriteFromArchiveAsync(root, commitSha, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (GitHubApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge && client.GitAddress(owner, repository) is not null)
            {
                // Past GitHub's archive limit: the files come over Git's own protocol, in batches.
                overArchiveLimit = true;
            }
        }

        // A repository past the archive limit, or a file past the API's 100 MB, comes over Git's own protocol; a broken
        // download keeps each batch that arrived.
        if (overArchiveLimit)
        {
            await WriteWithGitAsync(root, remaining, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false);
            remaining = [];
        }

        var large = await WriteBlobsAsync(root, commitSha, remaining, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false);
        if (large.Count > 0)
        {
            await WriteWithGitAsync(root, large, files, hashes, pointers, progress, cancellationToken).ConfigureAwait(false);
        }

        hashes.Save(root);
        DeleteGitObjects(root);
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
        // Named by the commit and kept when the download breaks, so the next attempt asks only for the rest of it.
        var temporary = Path.Combine(root, SyncMetadata.FolderName, $"download-{commitSha}.zip");
        var tagPath = temporary + ".etag";
        Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
        var complete = false;
        try
        {
            var validator = new System.Runtime.CompilerServices.StrongBox<string?>(File.Exists(tagPath) ? File.ReadAllText(tagPath) : null);
            await using (var stream = new FileStream(temporary, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Seek(0, SeekOrigin.End);
                var bytes = new Progress<(long Received, long? Total)>(p =>
                    progress?.Report(new SyncProgress($"Downloading {FormatSize(p.Received)}", (int)(p.Received / 1024), (int)((p.Total ?? 0) / 1024))));
                try
                {
                    await client.DownloadArchiveAsync(owner, repository, commitSha, stream, MaximumArchiveBytes, validator, bytes, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (validator.Value is { } tag)
                    {
                        await File.WriteAllTextAsync(tagPath, tag, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }

            complete = true;
            var missing = new HashSet<string>(files.Keys, StringComparer.Ordinal);
            ZipArchive zip;
            try
            {
                zip = ZipFile.OpenRead(temporary);
            }
            catch (InvalidDataException)
            {
                // A damaged archive: every file is fetched on its own instead.
                return [.. missing];
            }

            using var archive = zip;
            var written = 0;
            foreach (var entry in archive.Entries)
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

                if (written % 500 == 0)
                {
                    hashes.Save(root);
                }
            }

            return [.. missing];
        }
        catch (GitHubApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
        {
            complete = true;
            throw;
        }
        finally
        {
            if (complete)
            {
                TryDelete(temporary);
                TryDelete(tagPath);
            }
        }
    }

    /// <returns>The files past the API's size limit, which have to come over Git's own protocol.</returns>
    private async Task<List<string>> WriteBlobsAsync(
        string root,
        string commitSha,
        List<string> paths,
        IReadOnlyDictionary<string, TrackedFile> files,
        HashCache hashes,
        ConcurrentDictionary<string, LfsPointer> pointers,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        var large = new List<string>();
        if (paths.Count == 0)
        {
            return large;
        }

        var done = 0;
        var gate = new Lock();
        progress?.Report(new SyncProgress(paths.Count == 1 ? "Downloading 1 file" : $"Downloading {paths.Count} files", 0, paths.Count));
        var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(paths, options, async (path, token) =>
        {
            byte[] content;
            try
            {
                content = await ReadFileAsync(commitSha, path, files[path].Sha, token).ConfigureAwait(false);
            }
            catch (GitHubApiException ex) when (IsTooLarge(ex) && client.GitAddress(owner, repository) is not null)
            {
                lock (gate)
                {
                    large.Add(path);
                }

                return;
            }

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
                if (done % 200 == 0)
                {
                    hashes.Save(root);
                }
            }
        }).ConfigureAwait(false);

        return large;
    }

    /// <summary>Whether GitHub refused a blob because the API does not serve files past 100 MB.</summary>
    private static bool IsTooLarge(GitHubApiException exception) =>
        exception.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.UnprocessableEntity or System.Net.HttpStatusCode.RequestEntityTooLarge
        && exception.Message.Contains("too large", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes files whose contents come over Git's own protocol from the repository's Git address, as <c>git fetch</c>
    /// gets them: by name, in batches, into an object database under <c>.tabstudio</c> that a broken download keeps, so
    /// neither GitHub's 1 GB archive limit nor its 100 MB API limit applies and an interrupted download goes on.
    /// </summary>
    private async Task WriteWithGitAsync(
        string root,
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

        var address = client.GitAddress(owner, repository)!;
        var remote = new Git.GitHttpClient(client.Http, address, client.GitCredentialsFor(address));
        var store = new Git.ObjectStore(GitObjectsFolder(root));
        try
        {
            var refs = await remote.ListRefsAsync(cancellationToken).ConfigureAwait(false);
            if (!refs.CanWantAnyObject)
            {
                throw new GitHubApiException("GitHub's Git server would not send the files by name.");
            }

            var missing = paths.Select(p => files[p].Sha).Distinct(StringComparer.Ordinal).Where(id => !store.Contains(id)).ToList();
            var done = 0;
            foreach (var batch in missing.Chunk(Git.GitWorkingCopy.BlobBatch))
            {
                progress?.Report(new SyncProgress($"Downloading {Files(missing.Count)}", done, missing.Count));
                await Git.GitWorkingCopy.FetchIntoAsync(remote, store, new Git.FetchRequest([.. batch], [], 0, false), promisor: false, $"Downloading {Files(missing.Count)}", progress, cancellationToken).ConfigureAwait(false);
                done += batch.Length;
            }
        }
        catch (Git.GitTransportException ex)
        {
            throw new GitHubApiException(ex.StatusCode ?? System.Net.HttpStatusCode.ServiceUnavailable, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidDataException)
        {
            throw new GitHubApiException($"The connection to GitHub broke during the download: {ex.Message}", ex);
        }

        var written = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var full = WorkingTree.FullPath(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            if (File.Exists(full))
            {
                File.SetAttributes(full, FileAttributes.Normal);
            }

            await using (var stream = new FileStream(full, FileMode.Create, FileAccess.Write))
            {
                if (!store.TryCopyBlob(files[path].Sha, stream))
                {
                    throw new GitHubApiException($"GitHub did not send {path}.");
                }
            }

            if (new FileInfo(full).Length <= 1024 && LfsPointer.TryParse(await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false)) is { } pointer)
            {
                pointers[path] = pointer;
            }

            hashes.Remember(root, path, files[path].Sha);
            if (++written % 200 == 0)
            {
                hashes.Save(root);
                progress?.Report(new SyncProgress("Writing files", written, paths.Count));
            }
        }
    }

    private static string GitObjectsFolder(string root) => Path.Combine(root, SyncMetadata.FolderName, "objects");

    private static void DeleteGitObjects(string root)
    {
        try
        {
            if (Directory.Exists(GitObjectsFolder(root)))
            {
                Directory.Delete(GitObjectsFolder(root), recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left over objects only take storage in .tabstudio.
        }
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
