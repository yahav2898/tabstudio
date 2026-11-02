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
    /// <param name="cancellationToken">Cancels the download; the folder is then left incomplete and unlinked.</param>
    public static async Task<GitHubSync> CloneAsync(
        GitHubClient client,
        string owner,
        string repository,
        string? reference,
        string folder,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
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
        var files = await ReadFilesAsync(client, info.Owner, info.Name, commit.TreeSha, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(folder);
        var hashes = HashCache.Empty();
        var fetcher = new FileFetcher(client, info.Owner, info.Name);
        await fetcher.WriteAsync(folder, commit.Sha, files, hashes, progress, cancellationToken).ConfigureAwait(false);

        var metadata = new SyncMetadata(new GitHubLink(info.Owner, info.Name, isBranch ? reference : null, commit.Sha), files);
        metadata.Save(folder);
        hashes.Save(folder);
        progress?.Report(new SyncProgress("Done", files.Count, files.Count));
        return new GitHubSync(client, folder, metadata);
    }

    /// <summary>
    /// Links a folder that Git cloned from GitHub to its repository without downloading it again: the files of the
    /// commit the clone holds become the base its changes are compared with. When GitHub does not have that commit
    /// (it was made in the clone and never pushed), the base is the branch on GitHub, so those commits' edits show as
    /// changes to commit. A branch GitHub does not have is linked as a commit, so a commit from here never lands on
    /// another branch. <c>.tabstudio</c> is added to the clone's <c>info/exclude</c>, so Git leaves it alone.
    /// </summary>
    /// <param name="client">The GitHub client to use; a public repository needs no account.</param>
    /// <param name="checkout">The clone, from <see cref="GitCheckout.Find"/>.</param>
    /// <param name="cancellationToken">Cancels the requests; the folder then stays unlinked.</param>
    public static async Task<GitHubSync> LinkCheckoutAsync(GitHubClient client, GitCheckout checkout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(checkout);
        if (SyncMetadata.Load(checkout.Root) is { } existing)
        {
            return new GitHubSync(client, checkout.Root, existing);
        }

        var info = await client.GetRepositoryAsync(checkout.Owner, checkout.Repository, cancellationToken).ConfigureAwait(false);
        var branch = checkout.Branch is { } name && await IsBranchAsync(client, info, name, cancellationToken).ConfigureAwait(false) ? name : null;
        GitCommit? commit = null;
        if (checkout.Commit is { } sha)
        {
            try
            {
                commit = await client.GetCommitAsync(info.Owner, info.Name, sha, cancellationToken).ConfigureAwait(false);
            }
            catch (GitHubApiException ex) when (ex.IsNotFound)
            {
                // A commit made in the clone and never pushed.
            }
        }

        if (commit is null)
        {
            var reference = branch ?? info.DefaultBranch;
            commit = await client.ResolveCommitAsync(info.Owner, info.Name, reference, cancellationToken).ConfigureAwait(false);
        }

        var files = await ReadFilesAsync(client, info.Owner, info.Name, commit.TreeSha, cancellationToken).ConfigureAwait(false);
        var metadata = new SyncMetadata(new GitHubLink(info.Owner, info.Name, branch, commit.Sha), files);
        metadata.Save(checkout.Root);
        HashCache.Empty().Save(checkout.Root);
        ExcludeSyncFolder(checkout.GitDirectory);
        return new GitHubSync(client, checkout.Root, metadata);
    }

    /// <summary>Adds <c>.tabstudio/</c> to a Git directory's <c>info/exclude</c>, unless it is there.</summary>
    private static void ExcludeSyncFolder(string gitDirectory)
    {
        try
        {
            var info = Path.Combine(gitDirectory, "info");
            var exclude = Path.Combine(info, "exclude");
            var pattern = SyncMetadata.FolderName + "/";
            var text = File.Exists(exclude) ? File.ReadAllText(exclude) : string.Empty;
            if (text.Split('\n').Any(line => line.Trim() is var trimmed && (trimmed == pattern || trimmed == SyncMetadata.FolderName || trimmed == "/" + pattern)))
            {
                return;
            }

            Directory.CreateDirectory(info);
            File.AppendAllText(exclude, (text.Length > 0 && !text.EndsWith('\n') ? "\n" : string.Empty) + pattern + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Git then lists .tabstudio as untracked; the link still works.
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
