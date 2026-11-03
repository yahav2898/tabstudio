using TabStudio.SourceControl.Git;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

/// <summary>Where a sync reads file contents and writes a commit's files from: GitHub's API, or the folder's Git clone.</summary>
internal interface IFileSource
{
    /// <summary>Reads a blob.</summary>
    Task<byte[]> ReadAsync(string sha, CancellationToken cancellationToken);

    /// <summary>Writes files of a commit into the folder.</summary>
    /// <returns>What the person should know, such as Git LFS files that could not be downloaded.</returns>
    Task<IReadOnlyList<string>> WriteAsync(string root, string commitSha, IReadOnlyDictionary<string, TrackedFile> files, HashCache hashes, IProgress<SyncProgress>? progress, CancellationToken cancellationToken);
}

public sealed partial class GitHubSync
{
    /// <summary>
    /// Downloads a repository from any Git host into a new folder as a Git clone, and links the folder to it: GitLab,
    /// Bitbucket, Azure DevOps, Codeberg, Gitea, a self-hosted server, or GitHub itself. The folder gets a
    /// <c>.git</c> that Git in the Terminal can use, and commits, pulls and branches go through Git's own protocol.
    /// </summary>
    /// <param name="client">The GitHub client, whose HTTP client is used; its token signs in to github.com.</param>
    /// <param name="address">The repository's address.</param>
    /// <param name="credentials">The sign-in for a private repository or for pushing, kept in the clone's remote as Git keeps it.</param>
    /// <param name="reference">A branch, tag or full commit hash; the remote's default branch when <see langword="null"/>.</param>
    /// <param name="folder">The folder to create. It must not exist, be empty, or hold a download of this repository that did not finish.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">
    /// Cancels the download. What arrived stays in the folder, which <see cref="IsIncompleteClone"/> then reports, and
    /// downloading into it again goes on from there.
    /// </param>
    /// <remarks>
    /// The clone is shallow and partial (<c>--depth 1 --filter=blob:none</c>) where the server allows: the commit's files
    /// come in batches, each kept as it arrives, so neither a large repository nor a broken connection starts over.
    /// </remarks>
    public static Task<GitHubSync> CloneAsync(
        GitHubClient client,
        GitRemoteAddress address,
        GitCredentials? credentials,
        string? reference,
        string folder,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        CloneGitAsync(client, address, credentials, reference, folder, progress, 0, cancellationToken);

    private static async Task<GitHubSync> CloneGitAsync(
        GitHubClient client,
        GitRemoteAddress address,
        GitCredentials? credentials,
        string? reference,
        string folder,
        IProgress<SyncProgress>? progress,
        int depth,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(address);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() && !IsIncompleteClone(folder))
        {
            throw new SyncException($"The folder '{folder}' is not empty.");
        }

        progress?.Report(new SyncProgress($"Connecting to {address.Host}"));
        Directory.CreateDirectory(Path.Combine(folder, SyncMetadata.FolderName));
        await File.WriteAllTextAsync(IncompleteMarker(folder), address.Address + "\n", cancellationToken).ConfigureAwait(false);

        var signIn = credentials ?? client.GitCredentialsFor(address.Address);
        var remote = new GitHttpClient(client.Http, address.Address, signIn);
        var gitPath = Path.Combine(folder, ".git");
        RemoteRefs refs;
        try
        {
            refs = await remote.ListRefsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            throw new GitTransportException($"The connection to {address.Host} broke: {ex.Message}", ex);
        }

        var (commit, branch) = ResolveReference(address, refs, reference);
        var partial = refs.CanFilter && refs.CanWantAnyObject;
        var git = File.Exists(Path.Combine(gitPath, "HEAD"))
            ? GitDirectory.Open(gitPath)
            : GitDirectory.Create(gitPath, ConfiguredRemote(address, credentials), branch, partial);
        var copy = new GitWorkingCopy(folder, git, remote, address.Address, client.Http, signIn);

        await copy.FetchCommitAsync(commit, [], depth: 1, progress, cancellationToken).ConfigureAwait(false);
        var entries = copy.Entries(commit);
        var files = GitWorkingCopy.Files(entries);

        // Files a broken download already wrote are kept.
        var hashes = HashCache.Load(folder);
        var write = files
            .Where(f => !File.Exists(WorkingTree.FullPath(folder, f.Key)) || hashes.GetHash(folder, f.Key) != f.Value.Sha)
            .ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
        var notes = new List<string>(await copy.WriteFilesAsync(write, hashes, progress, cancellationToken).ConfigureAwait(false));
        notes.AddRange(CaseClashes(files.Keys));
        var skipped = await copy.WriteSymbolicLinksAsync(entries.Values.Where(e => e.IsSymbolicLink).Take(500), cancellationToken).ConfigureAwait(false);
        if (skipped.Count > 0)
        {
            notes.Add($"{(skipped.Count == 1 ? "The symbolic link" : $"{skipped.Count} symbolic links, such as")} {skipped[0]} {(skipped.Count == 1 ? "was" : "were")} not created: it points outside the repository, or this storage does not allow links.");
        }

        var pins = entries.Where(e => e.Value.IsSubmodule).ToDictionary(e => e.Key, e => e.Value.Id, StringComparer.Ordinal);
        if (pins.Count > 0)
        {
            notes.AddRange(await CloneGitSubmodulesAsync(client, address, folder, pins, progress, depth, cancellationToken).ConfigureAwait(false));
        }

        copy.MoveBranch(branch, commit, $"clone: from {address.Address}");
        if (branch is not null && refs.Head is { } head && head.StartsWith("refs/heads/", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(git.CommonPath, "refs", "remotes", "origin", "HEAD"), $"ref: refs/remotes/origin/{head["refs/heads/".Length..]}\n");
        }

        git.Exclude(SyncMetadata.FolderName + "/");
        copy.WriteIndex(commit, hashes);

        var link = new GitHubLink(address.Owner, address.Name, branch, commit) { Remote = address.Address.ToString() };
        var metadata = new SyncMetadata(link, files) { Submodules = pins };
        metadata.Save(folder);
        hashes.Save(folder);
        File.Delete(IncompleteMarker(folder));
        progress?.Report(new SyncProgress("Done", files.Count, files.Count));
        return new GitHubSync(client, folder, metadata, copy) { CloneNotes = notes };
    }

    /// <summary>The address written into the clone's configuration: with the sign-in in it when one was given, as Git keeps it.</summary>
    private static string ConfiguredRemote(GitRemoteAddress address, GitCredentials? credentials)
    {
        if (credentials is null)
        {
            return address.Address.ToString();
        }

        var builder = new UriBuilder(address.Address)
        {
            UserName = Uri.EscapeDataString(credentials.UserName),
            Password = Uri.EscapeDataString(credentials.Password),
        };
        return builder.Uri.AbsoluteUri;
    }

    private static (string Commit, string? Branch) ResolveReference(GitRemoteAddress address, RemoteRefs refs, string? reference)
    {
        if (reference is null)
        {
            var head = refs.Head is { } named && refs.Refs.ContainsKey(named) ? named
                : new[] { "refs/heads/main", "refs/heads/master" }.FirstOrDefault(refs.Refs.ContainsKey)
                ?? refs.Refs.Keys.FirstOrDefault(k => k.StartsWith("refs/heads/", StringComparison.Ordinal));
            if (head is null)
            {
                throw new SyncException($"The repository at {address.Address} is empty: it has no branch to download.");
            }

            return (refs.Refs[head].Id, head["refs/heads/".Length..]);
        }

        if (refs.Refs.TryGetValue("refs/heads/" + reference, out var branch))
        {
            return (branch.Id, reference);
        }

        if (refs.Refs.TryGetValue("refs/tags/" + reference, out var tag))
        {
            return (tag.Peeled ?? tag.Id, null);
        }

        if (GitHash.IsHash(reference) && refs.CanWantAnyObject)
        {
            return (reference.ToLowerInvariant(), null);
        }

        throw new SyncException($"{address.Host} has no branch or tag '{reference}' in {address.FullName}.");
    }

    /// <summary>Downloads submodules, on any host, at the commits the repository pins, each as its own clone.</summary>
    private static async Task<List<string>> CloneGitSubmodulesAsync(
        GitHubClient client,
        GitRemoteAddress parent,
        string folder,
        IReadOnlyDictionary<string, string> pins,
        IProgress<SyncProgress>? progress,
        int depth,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        if (depth >= MaximumSubmoduleDepth)
        {
            notes.Add($"{pins.Count} submodules more than {MaximumSubmoduleDepth} levels deep were not downloaded.");
            return notes;
        }

        var gitmodules = Path.Combine(folder, ".gitmodules");
        var urls = File.Exists(gitmodules)
            ? Submodules.ReadUrls(await File.ReadAllTextAsync(gitmodules, cancellationToken).ConfigureAwait(false))
            : new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, sha) in pins)
        {
            var destination = WorkingTree.FullPath(folder, path);
            if (SyncMetadata.Load(destination) is { } existing && existing.Link.BaseCommit == sha)
            {
                continue;
            }

            notes.AddRange(await CloneSubmoduleAsync(client, parent, folder, path, sha, urls, progress, depth, cancellationToken).ConfigureAwait(false));
        }

        return notes;
    }

    private static async Task<List<string>> CloneSubmoduleAsync(
        GitHubClient client,
        GitRemoteAddress parent,
        string folder,
        string path,
        string sha,
        IReadOnlyDictionary<string, string> urls,
        IProgress<SyncProgress>? progress,
        int depth,
        CancellationToken cancellationToken)
    {
        if (!urls.TryGetValue(path, out var url))
        {
            return [$"The submodule {path} is not in .gitmodules, so its folder is empty."];
        }

        if (Submodules.Resolve(url, parent.Address) is not { } address)
        {
            return [$"The submodule {path} comes from {url}, which TabStudio cannot reach over HTTPS, so its folder is empty: download its files at commit {sha[..7]} into it."];
        }

        var destination = WorkingTree.FullPath(folder, path);
        progress?.Report(new SyncProgress($"Downloading the submodule {path}"));
        try
        {
            if (Directory.Exists(destination) && !SyncMetadata.Exists(destination) && !IsIncompleteClone(destination))
            {
                DeleteQuietly(destination);
            }

            var sync = await CloneGitAsync(client, address, null, sha, destination, progress, depth + 1, cancellationToken).ConfigureAwait(false);
            return [.. sync.CloneNotes.Select(n => $"{path}: {n}")];
        }
        catch (Exception ex) when (ex is GitTransportException or SyncException or InvalidDataException or MissingObjectException)
        {
            DeleteQuietly(destination);
            Directory.CreateDirectory(destination);
            return [$"The submodule {path} ({address.FullName}) could not be downloaded, so its folder is empty: {SyncErrors.Describe(ex)}"];
        }
    }

    /// <summary>
    /// Links a Git clone whose <c>.git</c> holds its commit, on any host, so commits and pulls from here update
    /// <c>.git</c> as Git would: the clone's commit is the base, and the branch, the remote's tracking branch and the
    /// index move with every commit and pull.
    /// </summary>
    private static GitHubSync LinkWorkingCopy(GitHubClient client, string root, string gitDirectory, Uri? remote, string owner, string repository, bool isGitHub)
    {
        var copy = GitWorkingCopy.Open(root, gitDirectory, client.Http, remote, remote is null ? null : client.GitCredentialsFor(remote));
        var head = copy.Git.Head ?? throw new SyncException("This clone has no commit yet. Make its first commit in the Terminal, then link it.");
        var entries = copy.Entries(head);

        // A GitHub repository is recorded by its github.com address, so pull requests and Actions know it, and reached at
        // the GitHub client's own Git address.
        var recorded = isGitHub ? $"https://github.com/{owner}/{repository}.git" : copy.RemoteAddress.ToString();
        var link = new GitHubLink(owner, repository, copy.Git.CurrentBranch, head) { Remote = recorded };
        var metadata = new SyncMetadata(link, GitWorkingCopy.Files(entries))
        {
            Submodules = entries.Where(e => e.Value.IsSubmodule).ToDictionary(e => e.Key, e => e.Value.Id, StringComparer.Ordinal),
        };
        metadata.Save(root);
        HashCache.Empty().Save(root);
        copy.Git.Exclude(SyncMetadata.FolderName + "/");
        return new GitHubSync(client, root, metadata, copy);
    }

    /// <summary>Opens the Git clone of a folder linked through Git's own protocol; <see langword="null"/> when its <c>.git</c> is gone.</summary>
    private static GitWorkingCopy? OpenWorkingCopy(GitHubClient client, string root, GitHubLink link)
    {
        var gitDirectory = GitDirectoryOf(root);
        if (gitDirectory is null || link.Remote is null || !Uri.TryCreate(link.Remote, UriKind.Absolute, out var remote))
        {
            return null;
        }

        if (link.IsGitHub && client.GitAddress(link.Owner, link.Repository) is { } github)
        {
            remote = github;
        }

        try
        {
            var configured = GitDirectory.Open(gitDirectory).RemoteAddress();
            var written = GitRemoteAddress.TryParse(configured, out var parsed, out _, out var credentials) && parsed.Address == remote ? credentials : null;
            return GitWorkingCopy.Open(root, gitDirectory, client.Http, remote, written ?? client.GitCredentialsFor(remote));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SyncException)
        {
            return null;
        }
    }

    /// <summary>The Git directory of a folder: its <c>.git</c> folder, or where a <c>.git</c> file points.</summary>
    internal static string? GitDirectoryOf(string root)
    {
        var dotGit = Path.Combine(root, ".git");
        if (Directory.Exists(dotGit))
        {
            return dotGit;
        }

        if (File.Exists(dotGit) && File.ReadAllText(dotGit).Trim() is { } text && text.StartsWith("gitdir:", StringComparison.Ordinal))
        {
            var target = Path.GetFullPath(Path.Combine(root, text["gitdir:".Length..].Trim()));
            return Directory.Exists(target) ? target : null;
        }

        return null;
    }

    private GitWorkingCopy RequireGit() =>
        _git ?? throw new SyncException("This folder's .git is missing or damaged, so TabStudio cannot commit or pull here. Download the repository again.");

    private IFileSource Source() => _git is null ? new FileFetcher(_client, Link.Owner, Link.Repository) : new GitFileSource(RequireGit());

    private async Task<int> IncomingViaGitAsync(string branch, CancellationToken cancellationToken)
    {
        var git = RequireGit();
        var head = await git.RemoteBranchAsync(branch, refresh: true, cancellationToken).ConfigureAwait(false);
        if (head is null || head == Link.BaseCommit)
        {
            return 0;
        }

        await git.FetchCommitAsync(head, [Link.BaseCommit], depth: 0, progress: null, cancellationToken).ConfigureAwait(false);
        return git.IsAncestor(head, Link.BaseCommit) ? 0 : git.CountSince(head, Link.BaseCommit);
    }

    private async Task<IReadOnlyList<GitHubBranch>> BranchesViaGitAsync(CancellationToken cancellationToken)
    {
        var refs = await RequireGit().RefsAsync(refresh: true, cancellationToken).ConfigureAwait(false);
        return [.. refs.Refs.Values
            .Where(r => r.Name.StartsWith("refs/heads/", StringComparison.Ordinal))
            .Select(r => new GitHubBranch(r.Name["refs/heads/".Length..], r.Id, false))
            .OrderBy(b => b.Name, StringComparer.Ordinal)];
    }

    /// <summary>Commits through Git's own protocol: the commit is made in <c>.git</c> and pushed, then the index follows it.</summary>
    private async Task<CommitResult> CommitViaGitAsync(string message, string branch, IReadOnlyList<FileChange> changes, HashCache hashes, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var git = RequireGit();
        progress?.Report(new SyncProgress($"Checking {Link.Host} for new commits"));
        var remoteHead = await git.RemoteBranchAsync(branch, refresh: true, cancellationToken).ConfigureAwait(false);
        if (remoteHead is not null && remoteHead != Link.BaseCommit)
        {
            if (!git.Objects.Contains(remoteHead) || !git.IsAncestor(remoteHead, Link.BaseCommit))
            {
                throw BehindRemote(branch);
            }
        }

        var lfs = LfsAttributes.Select(Root, changes.Where(c => c.Kind != ChangeKind.Deleted && !IsLfsPointerFile(c.Path)).Select(c => c.Path));
        if (lfs.Count > 0)
        {
            progress?.Report(new SyncProgress(lfs.Count == 1 ? "Uploading 1 file to Git LFS" : $"Uploading {lfs.Count} files to Git LFS"));
        }

        var pointers = await git.UploadLfsAsync(lfs, cancellationToken).ConfigureAwait(false);
        var tracked = new Dictionary<string, (string Mode, string Id)?>(StringComparer.Ordinal);
        var files = new Dictionary<string, TrackedFile>(Metadata.Files, StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (change.Kind == ChangeKind.Deleted)
            {
                tracked[change.Path] = null;
                files.Remove(change.Path);
                continue;
            }

            var content = pointers.TryGetValue(change.Path, out var pointer)
                ? pointer
                : await File.ReadAllBytesAsync(WorkingTree.FullPath(Root, change.Path), cancellationToken).ConfigureAwait(false);
            var id = git.Objects.Write(GitObjectKind.Blob, content);
            var mode = Metadata.Files.TryGetValue(change.Path, out var previous) ? previous.Mode : GitObjects.RegularFileMode;
            tracked[change.Path] = (mode, id);
            files[change.Path] = new TrackedFile(id, mode);
            hashes.Remember(Root, change.Path, id);
        }

        progress?.Report(new SyncProgress("Creating the commit"));
        var commit = git.CreateCommit(Link.BaseCommit, tracked, message);
        progress?.Report(new SyncProgress($"Pushing to {Link.Host}"));
        var refused = await git.PushAsync(branch, remoteHead ?? GitHash.Zero, commit, remoteHasCommit: false, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            if (refused.Contains("fast-forward", StringComparison.OrdinalIgnoreCase) || refused.Contains("fetch first", StringComparison.OrdinalIgnoreCase) || refused.Contains("stale", StringComparison.OrdinalIgnoreCase))
            {
                throw BehindRemote(branch);
            }

            throw new SyncException(SyncFailure.Other, $"{Link.Host} did not let this commit onto {branch}: {refused}. If {branch} is protected, switch to a new branch, commit there and open a merge request.");
        }

        git.MoveBranch(branch, commit, "commit: " + message.Split('\n', 2)[0]);
        var committed = changes.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        Save(new SyncMetadata(Link with { BaseCommit = commit }, files) { Conflicts = [.. Metadata.Conflicts.Where(c => !committed.Contains(c))], Submodules = Metadata.Submodules });
        git.WriteIndex(commit, hashes);
        foreach (var path in committed)
        {
            ConflictStore.Delete(Root, path);
        }

        var data = git.ReadCommit(commit);
        var author = data.Author is { } a ? new GitSignature(a.Name, a.Email, a.When) : null;
        return new CommitResult(new GitCommit(commit, data.Tree, message, data.Parents, author), changes.Count);
    }

    /// <summary>Fetches a commit through Git's own protocol and moves the folder to it, keeping and merging local changes.</summary>
    private async Task<PullResult> CheckOutViaGitAsync(string? branch, string head, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var git = RequireGit();
        progress?.Report(new SyncProgress($"Downloading from {Link.Host}"));
        await git.FetchCommitAsync(head, [Link.BaseCommit], depth: 0, progress, cancellationToken).ConfigureAwait(false);
        var entries = git.Entries(head);
        var remote = GitWorkingCopy.Files(entries);
        var pins = entries.Where(e => e.Value.IsSubmodule).ToDictionary(e => e.Key, e => e.Value.Id, StringComparer.Ordinal);
        var result = await ApplyAsync(branch, head, remote, pins, new GitFileSource(git), progress, cancellationToken).ConfigureAwait(false);
        var hashes = HashCache.Load(Root);
        git.MoveBranch(branch, head, branch is null ? $"checkout: moving to {head}" : $"pull: fast-forward to {head[..7]}");
        git.WriteIndex(head, hashes);
        return result;
    }

    private bool IsLfsPointerFile(string path)
    {
        var info = new FileInfo(WorkingTree.FullPath(Root, path));
        return info.Length is >= 100 and <= 1024 && FileFetcher.LfsPointer.TryParse(File.ReadAllBytes(info.FullName)) is not null;
    }

    /// <summary>
    /// Moves each submodule whose pin changed to its new commit, as <c>git submodule update</c> does after a pull: a
    /// submodule folder TabStudio linked is checked out at the pinned commit, and one that is empty is downloaded.
    /// </summary>
    private async Task<List<string>> UpdateSubmodulesAsync(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var moved = new List<string>();
        var gitmodules = Path.Combine(Root, ".gitmodules");
        Dictionary<string, string>? urls = null;
        foreach (var (path, sha) in after.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = WorkingTree.FullPath(Root, path);
            if (Open(_client, folder) is { } submodule)
            {
                if (submodule.Link.BaseCommit != sha)
                {
                    progress?.Report(new SyncProgress($"Updating the submodule {path}"));
                    await submodule.MoveToCommitAsync(sha, progress, cancellationToken).ConfigureAwait(false);
                    moved.Add(path);
                }

                continue;
            }

            if (before.GetValueOrDefault(path) == sha && Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                continue;
            }

            urls ??= File.Exists(gitmodules)
                ? Submodules.ReadUrls(await File.ReadAllTextAsync(gitmodules, cancellationToken).ConfigureAwait(false))
                : new Dictionary<string, string>(StringComparer.Ordinal);
            var notes = _git is null
                ? await CloneSubmodulesAsync(_client, await _client.GetRepositoryAsync(Link.Owner, Link.Repository, cancellationToken).ConfigureAwait(false), Root, [new GitTreeEntry(path, TreeCodec.SubmoduleMode, GitTreeEntryKind.Commit, sha, null)], progress, 0, cancellationToken).ConfigureAwait(false)
                : await CloneSubmoduleAsync(_client, new GitRemoteAddress(RequireGit().RemoteAddress, Link.Owner, Link.Repository), Root, path, sha, urls, progress, 0, cancellationToken).ConfigureAwait(false);
            if (SyncMetadata.Load(folder)?.Link.BaseCommit == sha)
            {
                moved.Add(path);
            }

            CloneNotes = [.. CloneNotes, .. notes];
        }

        return moved;
    }

    /// <summary>Moves the folder to a commit that need not be on a branch, as a submodule follows its pin.</summary>
    private Task<PullResult> MoveToCommitAsync(string sha, IProgress<SyncProgress>? progress, CancellationToken cancellationToken) =>
        _git is null ? CheckOutAsync(null, sha, progress, cancellationToken) : CheckOutViaGitAsync(null, sha, progress, cancellationToken);

    /// <summary>File contents from the folder's Git clone.</summary>
    private sealed class GitFileSource(GitWorkingCopy git) : IFileSource
    {
        public Task<byte[]> ReadAsync(string sha, CancellationToken cancellationToken) => git.ReadBlobAsync(sha, cancellationToken);

        public Task<IReadOnlyList<string>> WriteAsync(string root, string commitSha, IReadOnlyDictionary<string, TrackedFile> files, HashCache hashes, IProgress<SyncProgress>? progress, CancellationToken cancellationToken) =>
            git.WriteFilesAsync(files, hashes, progress, cancellationToken);
    }
}
