using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

/// <summary>
/// Source control for a folder linked to a GitHub repository, without Git installed: changes are found by
/// comparing files with the last synced commit, and commits, pulls and branches go through GitHub's API.
/// </summary>
/// <remarks>
/// <para>
/// A commit is created on GitHub directly, so committing and pushing are one step. Pulling merges GitHub's
/// changes into the folder file by file, with a three-way line merge where both sides changed a file.
/// </para>
/// <para>
/// What TabStudio knows about the folder lives in its <c>.tabstudio</c> folder, which is never committed.
/// Operations on one folder must not run at the same time.
/// </para>
/// </remarks>
public sealed partial class GitHubSync
{
    private readonly GitHubClient _client;

    private GitHubSync(GitHubClient client, string root, SyncMetadata metadata)
    {
        _client = client;
        Root = root;
        Metadata = metadata;
    }

    /// <summary>The linked folder.</summary>
    public string Root { get; }

    /// <summary>The repository, branch and files of the last sync.</summary>
    public SyncMetadata Metadata { get; private set; }

    /// <summary>The repository and branch.</summary>
    public GitHubLink Link => Metadata.Link;

    /// <summary>Opens a linked folder.</summary>
    /// <param name="client">The GitHub client to use.</param>
    /// <param name="root">The folder.</param>
    /// <returns>The folder's source control, or <see langword="null"/> when it is not linked to GitHub.</returns>
    public static GitHubSync? Open(GitHubClient client, string root)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(root);
        return SyncMetadata.Load(root) is { } metadata ? new GitHubSync(client, root, metadata) : null;
    }

    /// <summary>Finds the linked folder that contains a path: the path itself or one of its parents.</summary>
    /// <param name="path">A file or folder.</param>
    /// <returns>The linked folder, or <see langword="null"/>.</returns>
    public static string? FindRoot(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        for (var folder = Directory.Exists(path) ? Path.GetFullPath(path) : Path.GetDirectoryName(Path.GetFullPath(path)); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            if (SyncMetadata.Exists(folder))
            {
                return folder;
            }
        }

        return null;
    }

    /// <summary>Lists what changed since the last sync, and optionally how many commits GitHub has that the folder lacks.</summary>
    /// <param name="checkGitHub">Whether to ask GitHub for new commits.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public async Task<SyncStatus> GetStatusAsync(bool checkGitHub = false, CancellationToken cancellationToken = default)
    {
        var changes = await Task.Run(() => ScanChanges(cancellationToken), cancellationToken).ConfigureAwait(false);
        int? incoming = null;
        if (checkGitHub && Link.Branch is { } branch)
        {
            var head = await _client.GetBranchHeadAsync(Link.Owner, Link.Repository, branch, cancellationToken).ConfigureAwait(false);
            incoming = head == Link.BaseCommit
                ? 0
                : (await _client.CompareAsync(Link.Owner, Link.Repository, Link.BaseCommit, head, cancellationToken).ConfigureAwait(false)).AheadBy;
        }

        return new SyncStatus(Link, changes, Metadata.Conflicts, incoming);
    }

    /// <summary>Reads a file as it was in the last synced commit, for showing what changed.</summary>
    /// <param name="relativePath">The path relative to the root, with <c>/</c> separators.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The content, or <see langword="null"/> when the file is new.</returns>
    public async Task<byte[]?> GetBaseContentAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Metadata.Files.TryGetValue(relativePath, out var file)
            ? await _client.GetBlobAsync(Link.Owner, Link.Repository, file.Sha, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>Lists the repository's branches.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<GitHubBranch>> GetBranchesAsync(CancellationToken cancellationToken = default) =>
        _client.GetBranchesAsync(Link.Owner, Link.Repository, cancellationToken);

    /// <summary>Lists the latest commits of the branch, newest first.</summary>
    /// <param name="count">How many commits, at most 100.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<GitCommit>> GetHistoryAsync(int count = 30, CancellationToken cancellationToken = default) =>
        _client.GetHistoryAsync(Link.Owner, Link.Repository, Link.Branch ?? Link.BaseCommit, count, cancellationToken);

    /// <summary>Opens a pull request from the folder's branch.</summary>
    /// <param name="title">The title.</param>
    /// <param name="body">The description, in Markdown.</param>
    /// <param name="baseBranch">The branch to merge into; the repository's default branch when <see langword="null"/>.</param>
    /// <param name="draft">Whether to open it as a draft.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<GitHubPullRequest> CreatePullRequestAsync(string title, string? body = null, string? baseBranch = null, bool draft = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var branch = RequireBranch("open a pull request");
        baseBranch ??= (await _client.GetRepositoryAsync(Link.Owner, Link.Repository, cancellationToken).ConfigureAwait(false)).DefaultBranch;
        if (string.Equals(baseBranch, branch, StringComparison.Ordinal))
        {
            throw new SyncException(SyncFailure.Other, $"A pull request needs a branch other than '{baseBranch}'. Create a branch for your changes first.");
        }

        return await _client.CreatePullRequestAsync(Link.Owner, Link.Repository, title, body, branch, baseBranch, draft, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The changes since the last sync, using and refreshing the hash cache.</summary>
    private IReadOnlyList<FileChange> ScanChanges(CancellationToken cancellationToken)
    {
        var hashes = HashCache.Load(Root);
        var changes = WorkingTree.GetChanges(Root, Metadata.Files, hashes, cancellationToken);
        hashes.Save(Root);
        return changes;
    }

    private string RequireBranch(string action) =>
        Link.Branch ?? throw new SyncException(SyncFailure.Detached, $"This folder holds commit {Link.BaseCommit[..7]}, not a branch. Create a branch to {action}.");

    private void Save(SyncMetadata metadata)
    {
        metadata.Save(Root);
        Metadata = metadata;
    }

    /// <summary>Reads every file of a tree, walking folder by folder when the tree is too large to read at once.</summary>
    private static Task<Dictionary<string, TrackedFile>> ReadFilesAsync(GitHubClient client, string owner, string repository, string treeSha, CancellationToken cancellationToken) =>
        ReadFilesAsync(client, owner, repository, treeSha, null, cancellationToken);

    /// <summary>Reads the regular files of a tree; <paramref name="others"/> receives its submodules and symbolic links.</summary>
    private static async Task<Dictionary<string, TrackedFile>> ReadFilesAsync(
        GitHubClient client,
        string owner,
        string repository,
        string treeSha,
        List<GitTreeEntry>? others,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, TrackedFile>(StringComparer.Ordinal);
        var tree = await client.GetTreeAsync(owner, repository, treeSha, recursive: true, cancellationToken).ConfigureAwait(false);
        if (!tree.Truncated)
        {
            foreach (var entry in tree.Entries)
            {
                if (entry.IsRegularFile)
                {
                    files[entry.Path] = new TrackedFile(entry.Sha, entry.Mode);
                }
                else if (entry.Kind == GitTreeEntryKind.Commit || (entry.Kind == GitTreeEntryKind.Blob && entry.Mode == GitTreeEntry.SymbolicLinkMode))
                {
                    others?.Add(entry);
                }
            }

            return files;
        }

        var pending = new Queue<(string Prefix, string Sha)>();
        pending.Enqueue((string.Empty, treeSha));
        while (pending.TryDequeue(out var folder))
        {
            var level = await client.GetTreeAsync(owner, repository, folder.Sha, recursive: false, cancellationToken).ConfigureAwait(false);
            foreach (var entry in level.Entries)
            {
                var path = folder.Prefix + entry.Path;
                if (entry.Kind == GitTreeEntryKind.Tree)
                {
                    pending.Enqueue((path + "/", entry.Sha));
                }
                else if (entry.IsRegularFile)
                {
                    files[path] = new TrackedFile(entry.Sha, entry.Mode);
                }
                else if (entry.Kind == GitTreeEntryKind.Commit || (entry.Kind == GitTreeEntryKind.Blob && entry.Mode == GitTreeEntry.SymbolicLinkMode))
                {
                    others?.Add(entry with { Path = path });
                }
            }
        }

        return files;
    }
}
