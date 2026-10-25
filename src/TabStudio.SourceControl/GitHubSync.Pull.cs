using TabStudio.SourceControl.GitHub;
using TabStudio.SourceControl.Merging;

namespace TabStudio.SourceControl;

public sealed partial class GitHubSync
{
    /// <summary>
    /// Brings the folder up to date with the branch on GitHub. Files changed only on GitHub are updated, files
    /// changed only here are kept, and files changed on both sides are merged line by line; where the same
    /// lines changed differently, the file gets conflict markers and is listed in <see cref="SyncMetadata.Conflicts"/>.
    /// </summary>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the pull; files already written stay, and pulling again finishes it.</param>
    public async Task<PullResult> PullAsync(IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var branch = RequireBranch("pull");
        progress?.Report(new SyncProgress("Checking GitHub for new commits"));
        var head = await _client.GetBranchHeadAsync(Link.Owner, Link.Repository, branch, cancellationToken).ConfigureAwait(false);
        if (head == Link.BaseCommit)
        {
            return new PullResult(head, [], []);
        }

        return await CheckOutAsync(branch, head, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a branch at the folder's commit and switches to it. Local changes stay and go into the branch's next commit.</summary>
    /// <param name="name">The new branch's name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task CreateBranchAsync(string name, CancellationToken cancellationToken = default)
    {
        RequireSignedIn("create a branch");
        if (!BranchNames.IsValid(name, out var reason))
        {
            throw new SyncException(reason);
        }

        try
        {
            await _client.CreateBranchAsync(Link.Owner, Link.Repository, name, Link.BaseCommit, cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubApiException ex) when (ex.IsRejected)
        {
            throw new SyncException($"A branch named '{name}' already exists.");
        }

        Save(Metadata with { Link = Link with { Branch = name } });
    }

    /// <summary>Switches the folder to another branch, replacing its files with the branch's.</summary>
    /// <param name="name">The branch.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the switch.</param>
    /// <exception cref="SyncException">There are uncommitted changes or unresolved conflicts.</exception>
    public async Task<PullResult> SwitchBranchAsync(string name, IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (Metadata.Conflicts.Count > 0 || ScanChanges(cancellationToken).Count > 0)
        {
            throw new SyncException(SyncFailure.UncommittedChanges, "Commit or discard your changes before switching branches.");
        }

        var head = await _client.GetBranchHeadAsync(Link.Owner, Link.Repository, name, cancellationToken).ConfigureAwait(false);
        return await CheckOutAsync(name, head, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves the folder to a commit, keeping and merging local changes.</summary>
    private async Task<PullResult> CheckOutAsync(string branch, string head, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var commit = await _client.GetCommitAsync(Link.Owner, Link.Repository, head, cancellationToken).ConfigureAwait(false);
        var remote = await ReadFilesAsync(_client, Link.Owner, Link.Repository, commit.TreeSha, cancellationToken).ConfigureAwait(false);

        var hashes = HashCache.Load(Root);
        var local = WorkingTree.GetChanges(Root, Metadata.Files, hashes, cancellationToken).ToDictionary(c => c.Path, StringComparer.Ordinal);
        var fetcher = new FileFetcher(_client, Link.Owner, Link.Repository);
        var write = new Dictionary<string, TrackedFile>(StringComparer.Ordinal);
        var updated = new List<string>();
        var conflicts = new List<string>();

        foreach (var path in Metadata.Files.Keys.Union(remote.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Metadata.Files.TryGetValue(path, out var original);
            remote.TryGetValue(path, out var theirs);
            if (original?.Sha == theirs?.Sha)
            {
                continue;
            }

            if (!local.TryGetValue(path, out var mine))
            {
                // Changed only on GitHub.
                if (theirs is null)
                {
                    FileFetcher.DeleteFile(Root, path);
                    hashes.Forget(path);
                }
                else
                {
                    write[path] = theirs;
                }

                updated.Add(path);
                continue;
            }

            if (mine.Kind == ChangeKind.Deleted)
            {
                if (theirs is not null)
                {
                    // Deleted here, changed on GitHub: GitHub's version comes back, as a conflict.
                    ConflictStore.Save(Root, path, mine: null);
                    write[path] = theirs;
                    conflicts.Add(path);
                }

                continue;
            }

            var current = await File.ReadAllBytesAsync(WorkingTree.FullPath(Root, path), cancellationToken).ConfigureAwait(false);
            if (theirs is not null && GitObjects.BlobHash(current) == theirs.Sha)
            {
                // The same change on both sides.
                continue;
            }

            ConflictStore.Save(Root, path, current);
            if (theirs is null)
            {
                // Changed here, deleted on GitHub: the local file stays, as a conflict.
                conflicts.Add(path);
                continue;
            }

            progress?.Report(new SyncProgress($"Merging {path}"));
            var merged = await MergeAsync(fetcher, original, theirs, current, cancellationToken).ConfigureAwait(false);
            if (merged is null || !merged.IsClean)
            {
                if (merged is not null)
                {
                    await FileFetcher.WriteFileAsync(Root, path, TextContent.Encode(merged.Text), cancellationToken).ConfigureAwait(false);
                }

                conflicts.Add(path);
            }
            else
            {
                await FileFetcher.WriteFileAsync(Root, path, TextContent.Encode(merged.Text), cancellationToken).ConfigureAwait(false);
                ConflictStore.Delete(Root, path);
                updated.Add(path);
            }
        }

        await fetcher.WriteAsync(Root, head, write, hashes, progress, cancellationToken).ConfigureAwait(false);
        updated.AddRange(write.Keys.Where(p => !conflicts.Contains(p)));

        var allConflicts = Metadata.Conflicts.Union(conflicts, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Save(new SyncMetadata(Link with { Branch = branch, BaseCommit = head }, remote) { Conflicts = allConflicts });
        hashes.Save(Root);
        return new PullResult(head, [.. updated.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], conflicts);
    }

    /// <summary>Merges a file changed on both sides; null when it is not text.</summary>
    private static async Task<MergeResult?> MergeAsync(FileFetcher fetcher, TrackedFile? original, TrackedFile theirs, byte[] mine, CancellationToken cancellationToken)
    {
        var baseBytes = original is null ? [] : await fetcher.ReadAsync(original.Sha, cancellationToken).ConfigureAwait(false);
        var theirBytes = await fetcher.ReadAsync(theirs.Sha, cancellationToken).ConfigureAwait(false);
        if (!TextContent.TryDecode(baseBytes, out var baseText)
            || !TextContent.TryDecode(theirBytes, out var theirText)
            || !TextContent.TryDecode(mine, out var myText))
        {
            return null;
        }

        return TextMerge.Merge(baseText, myText, theirText);
    }
}

/// <summary>Git's rules for branch names, so a bad name is caught before GitHub refuses it.</summary>
public static class BranchNames
{
    /// <summary>Whether a branch name is valid.</summary>
    /// <param name="name">The name.</param>
    /// <param name="reason">Why it is not, for the user.</param>
    public static bool IsValid(string? name, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = "Enter a branch name.";
        }
        else if (name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '~' or '^' or ':' or '?' or '*' or '[' or '\\'))
        {
            reason = "Branch names cannot contain spaces or any of ~ ^ : ? * [ \\.";
        }
        else if (name.Contains("..", StringComparison.Ordinal) || name.Contains("@{", StringComparison.Ordinal) || name.Contains("//", StringComparison.Ordinal))
        {
            reason = "Branch names cannot contain '..', '//' or '@{'.";
        }
        else if (name.StartsWith('-') || name.StartsWith('/') || name.EndsWith('/') || name.EndsWith('.') || name.EndsWith(".lock", StringComparison.Ordinal) || name == "@")
        {
            reason = "Branch names cannot start with '-' or '/', or end with '/', '.' or '.lock'.";
        }
        else if (name.Split('/').Any(part => part.StartsWith('.')))
        {
            reason = "No part of a branch name can start with a dot.";
        }

        return reason.Length == 0;
    }
}
