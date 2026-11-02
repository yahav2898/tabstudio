using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

public sealed partial class GitHubSync
{
    /// <summary>Downloads a repository into a new folder and links the folder to it.</summary>
    /// <param name="client">The GitHub client to use.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">A branch, tag or commit; the default branch when <see langword="null"/>.</param>
    /// <param name="folder">The folder to create. It must not exist or be empty.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">
    /// Cancels the download; the folder is then left incomplete and unlinked, and <see cref="IsIncompleteClone"/> says so.
    /// </param>
    /// <remarks>
    /// As <c>git clone --recurse-submodules</c> followed by <c>git lfs pull</c> does, submodules on GitHub are downloaded
    /// at the commits the repository pins, each into its own linked folder, and files stored with Git LFS are downloaded
    /// in place of their pointers. Symbolic links that stay inside the repository are created. What could not be done is
    /// in <see cref="CloneNotes"/>.
    /// </remarks>
    public static Task<GitHubSync> CloneAsync(
        GitHubClient client,
        string owner,
        string repository,
        string? reference,
        string folder,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        CloneAsync(client, owner, repository, reference, folder, progress, depth: 0, cancellationToken);

    /// <summary>
    /// What the person should know about the download that made this folder, such as a submodule on another host or
    /// a Git LFS file that could not be downloaded; empty when everything arrived.
    /// </summary>
    public IReadOnlyList<string> CloneNotes { get; private set; } = [];

    /// <summary>
    /// Whether a folder holds a download that never finished, because it was cancelled, failed or TabStudio was ended
    /// in the middle of it.
    /// </summary>
    /// <param name="folder">The folder.</param>
    public static bool IsIncompleteClone(string folder) => File.Exists(IncompleteMarker(folder));

    /// <summary>How deep submodules of submodules are followed, as a guard against a repository that includes itself.</summary>
    private const int MaximumSubmoduleDepth = 4;

    private static string IncompleteMarker(string folder) => Path.Combine(folder, SyncMetadata.FolderName, "incomplete");

    private static async Task<GitHubSync> CloneAsync(
        GitHubClient client,
        string owner,
        string repository,
        string? reference,
        string folder,
        IProgress<SyncProgress>? progress,
        int depth,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            throw new SyncException($"The folder '{folder}' is not empty.");
        }

        progress?.Report(new SyncProgress("Connecting to GitHub"));
        var info = await client.GetRepositoryAsync(owner, repository, cancellationToken).ConfigureAwait(false);
        reference ??= info.DefaultBranch;

        var isBranch = await IsBranchAsync(client, info, reference, cancellationToken).ConfigureAwait(false);
        var commit = await client.ResolveCommitAsync(info.Owner, info.Name, reference, cancellationToken).ConfigureAwait(false);
        var others = new List<GitTreeEntry>();
        var files = await ReadFilesAsync(client, info.Owner, info.Name, commit.TreeSha, others, cancellationToken).ConfigureAwait(false);

        // Marked before the first file and unmarked after the last, so a download Android ended can be told from one
        // that finished, and cleaned up.
        Directory.CreateDirectory(Path.Combine(folder, SyncMetadata.FolderName));
        await File.WriteAllTextAsync(IncompleteMarker(folder), commit.Sha, cancellationToken).ConfigureAwait(false);

        var hashes = HashCache.Empty();
        var fetcher = new FileFetcher(client, info.Owner, info.Name);
        var notes = new List<string>(await fetcher.WriteAsync(folder, commit.Sha, files, hashes, progress, cancellationToken).ConfigureAwait(false));
        notes.AddRange(CaseClashes(files.Keys));
        notes.AddRange(await WriteSymbolicLinksAsync(fetcher, folder, [.. others.Where(e => e.Kind == GitTreeEntryKind.Blob)], cancellationToken).ConfigureAwait(false));
        var submodules = others.Where(e => e.Kind == GitTreeEntryKind.Commit).ToList();
        if (submodules.Count > 0)
        {
            notes.AddRange(await CloneSubmodulesAsync(client, info, folder, submodules, progress, depth, cancellationToken).ConfigureAwait(false));
        }

        var metadata = new SyncMetadata(new GitHubLink(info.Owner, info.Name, isBranch ? reference : null, commit.Sha), files);
        metadata.Save(folder);
        hashes.Save(folder);
        File.Delete(IncompleteMarker(folder));
        progress?.Report(new SyncProgress("Done", files.Count, files.Count));
        return new GitHubSync(client, folder, metadata) { CloneNotes = notes };
    }

    /// <summary>
    /// Downloads the submodules on GitHub at the commits the repository pins, each into its own linked folder, which the
    /// repository's Git Changes leaves out as Git leaves out a submodule's files. Before 0.21.2 their folders were left
    /// empty, so a repository that builds its dependencies from submodules could not build.
    /// </summary>
    private static async Task<List<string>> CloneSubmodulesAsync(
        GitHubClient client,
        GitHubRepositoryInfo info,
        string folder,
        List<GitTreeEntry> submodules,
        IProgress<SyncProgress>? progress,
        int depth,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var gitmodules = Path.Combine(folder, ".gitmodules");
        var urls = File.Exists(gitmodules)
            ? Submodules.ReadUrls(await File.ReadAllTextAsync(gitmodules, cancellationToken).ConfigureAwait(false))
            : new Dictionary<string, string>(StringComparer.Ordinal);
        if (depth >= MaximumSubmoduleDepth)
        {
            notes.Add($"{submodules.Count} submodules more than {MaximumSubmoduleDepth} levels deep were not downloaded.");
            return notes;
        }

        foreach (var submodule in submodules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!urls.TryGetValue(submodule.Path, out var url))
            {
                notes.Add($"The submodule {submodule.Path} is not in .gitmodules, so its folder is empty.");
                continue;
            }

            if (Submodules.ResolveGitHub(url, info.Owner, info.Name) is not { } target)
            {
                notes.Add($"The submodule {submodule.Path} comes from {url}, which is not on GitHub, so its folder is empty: download its files at commit {submodule.Sha[..7]} into it.");
                continue;
            }

            var destination = WorkingTree.FullPath(folder, submodule.Path);
            progress?.Report(new SyncProgress($"Downloading the submodule {submodule.Path}"));
            try
            {
                var sync = await CloneAsync(client, target.Owner, target.Repository, submodule.Sha, destination, progress, depth + 1, cancellationToken).ConfigureAwait(false);
                notes.AddRange(sync.CloneNotes.Select(n => $"{submodule.Path}: {n}"));
            }
            catch (Exception ex) when (ex is GitHubApiException or SyncException)
            {
                DeleteQuietly(destination);
                notes.Add($"The submodule {submodule.Path} ({target.Owner}/{target.Repository}) could not be downloaded, so its folder is empty: {SyncErrors.Describe(ex)}");
            }
        }

        return notes;
    }

    /// <summary>
    /// Creates the repository's symbolic links that point to something inside it; before 0.21.2 they were left out, so a
    /// shared configuration file linked into several projects was missing from each.
    /// </summary>
    private static async Task<List<string>> WriteSymbolicLinksAsync(FileFetcher fetcher, string folder, List<GitTreeEntry> links, CancellationToken cancellationToken)
    {
        var skipped = new List<string>();
        foreach (var link in links.Take(500))
        {
            try
            {
                var target = System.Text.Encoding.UTF8.GetString(await fetcher.ReadAsync(link.Sha, cancellationToken).ConfigureAwait(false)).Trim();
                var full = WorkingTree.FullPath(folder, link.Path);
                var parent = Path.GetDirectoryName(full)!;
                var resolved = Path.GetFullPath(Path.Combine(parent, target.Replace('/', Path.DirectorySeparatorChar)));
                var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                // Only inside the repository, and never to a folder that holds the link, which would make a loop.
                if (Path.IsPathRooted(target) || !resolved.StartsWith(root, StringComparison.Ordinal)
                    || (parent + Path.DirectorySeparatorChar).StartsWith(resolved.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    skipped.Add(link.Path);
                    continue;
                }

                Directory.CreateDirectory(parent);
                if (!File.Exists(full) && !Directory.Exists(full))
                {
                    File.CreateSymbolicLink(full, target);
                }
            }
            catch (Exception ex) when (ex is GitHubApiException or IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException)
            {
                skipped.Add(link.Path);
            }
        }

        skipped.AddRange(links.Skip(500).Select(l => l.Path));
        return skipped.Count switch
        {
            0 => [],
            1 => [$"The symbolic link {skipped[0]} was not created: it points outside the repository, or this storage does not allow links."],
            _ => [$"{skipped.Count} symbolic links, such as {skipped[0]}, were not created: they point outside the repository, or this storage does not allow links."],
        };
    }

    /// <summary>
    /// Files whose names differ only in case, which overwrite each other where names ignore case, as on a memory card.
    /// </summary>
    private static IEnumerable<string> CaseClashes(IEnumerable<string> paths)
    {
        var clashes = paths.GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (clashes.Count > 0)
        {
            yield return $"{(clashes.Count == 1 ? "1 file has" : $"{clashes.Count} files have")} a name that differs from another's only in case, such as {clashes[0]}: where names ignore case, as on a memory card, one replaces the other.";
        }
    }

    private static void DeleteQuietly(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An empty submodule folder is what Git leaves too.
        }
    }

    /// <summary>
    /// Publishes a folder as a new GitHub repository in the signed-in account: every file the folder's
    /// <c>.gitignore</c> files do not exclude goes into the first commit, and the folder is linked to the repository.
    /// </summary>
    /// <param name="client">A signed-in GitHub client.</param>
    /// <param name="folder">The folder to publish.</param>
    /// <param name="name">The new repository's name.</param>
    /// <param name="isPrivate">Whether only invited people can see it.</param>
    /// <param name="message">The first commit's message.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    public static async Task<GitHubSync> PublishAsync(
        GitHubClient client,
        string folder,
        string name,
        bool isPrivate,
        string message = "Add project files",
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!client.IsSignedIn)
        {
            throw new SyncException(SyncFailure.NotSignedIn, "Sign in to GitHub to publish a repository.");
        }

        if (SyncMetadata.Exists(folder))
        {
            throw new SyncException("This folder is already linked to a GitHub repository.");
        }

        var paths = WorkingTree.EnumerateFiles(folder);
        progress?.Report(new SyncProgress("Creating the repository"));
        var info = await client.CreateRepositoryAsync(name, isPrivate, cancellationToken: cancellationToken).ConfigureAwait(false);
        var initial = await client.GetBranchHeadAsync(info.Owner, info.Name, info.DefaultBranch, cancellationToken).ConfigureAwait(false);

        var hashes = HashCache.Empty();
        var uploads = paths.Select(p => new FileChange(p, ChangeKind.Added)).ToList();
        var blobs = await UploadAsync(client, info.Owner, info.Name, folder, uploads, hashes, progress, cancellationToken).ConfigureAwait(false);

        // A tree built from scratch, not on top of the initial commit's, so the README GitHub created is
        // replaced by exactly the folder's files.
        var tree = await client.CreateTreeAsync(
            info.Owner,
            info.Name,
            baseTree: null,
            blobs.Select(b => new GitTreeChange(b.Key, b.Value.Mode, b.Value.Sha)),
            cancellationToken).ConfigureAwait(false);
        var commit = await client.CreateCommitAsync(info.Owner, info.Name, message, tree, [initial], cancellationToken).ConfigureAwait(false);
        await client.UpdateBranchAsync(info.Owner, info.Name, info.DefaultBranch, commit.Sha, cancellationToken: cancellationToken).ConfigureAwait(false);

        var metadata = new SyncMetadata(new GitHubLink(info.Owner, info.Name, info.DefaultBranch, commit.Sha), blobs);
        metadata.Save(folder);
        hashes.Save(folder);
        return new GitHubSync(client, folder, metadata);
    }

    private static async Task<bool> IsBranchAsync(GitHubClient client, GitHubRepositoryInfo info, string reference, CancellationToken cancellationToken)
    {
        if (string.Equals(reference, info.DefaultBranch, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            await client.GetBranchHeadAsync(info.Owner, info.Name, reference, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (GitHubApiException ex) when (ex.IsNotFound)
        {
            return false;
        }
    }

    /// <summary>Uploads the content of added and modified files.</summary>
    /// <returns>The uploaded files with their hashes and modes.</returns>
    private static async Task<Dictionary<string, TrackedFile>> UploadAsync(
        GitHubClient client,
        string owner,
        string repository,
        string root,
        IReadOnlyList<FileChange> changes,
        HashCache hashes,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, TrackedFile>? tracked = null)
    {
        var uploads = changes.Where(c => c.Kind != ChangeKind.Deleted).ToList();
        var result = new Dictionary<string, TrackedFile>(StringComparer.Ordinal);
        var gate = new Lock();
        var done = 0;
        progress?.Report(new SyncProgress(uploads.Count == 1 ? "Uploading 1 file" : $"Uploading {uploads.Count} files", 0, uploads.Count));

        var options = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(uploads, options, async (change, token) =>
        {
            var content = await File.ReadAllBytesAsync(WorkingTree.FullPath(root, change.Path), token).ConfigureAwait(false);
            var sha = await client.CreateBlobAsync(owner, repository, content, token).ConfigureAwait(false);
            var mode = tracked?.TryGetValue(change.Path, out var previous) == true ? previous.Mode : GitObjects.RegularFileMode;
            lock (gate)
            {
                result[change.Path] = new TrackedFile(sha, mode);
                hashes.Remember(root, change.Path, sha);
                done++;
                progress?.Report(new SyncProgress($"Uploading {uploads.Count} files", done, uploads.Count));
            }
        }).ConfigureAwait(false);

        return result;
    }
}
