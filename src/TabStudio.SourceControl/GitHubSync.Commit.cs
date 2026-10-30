using System.Text;
using TabStudio.SourceControl.GitHub;
using TabStudio.SourceControl.Merging;

namespace TabStudio.SourceControl;

public sealed partial class GitHubSync
{
    /// <summary>
    /// Commits changes to the branch on GitHub. The commit is created on GitHub directly, so this also pushes.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="paths">The files to include; every changed file when <see langword="null"/>.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the commit before the branch moves.</param>
    /// <exception cref="SyncException">
    /// Nothing to commit, conflicts are unresolved, GitHub has newer commits (pull first), or the folder holds a
    /// tag or commit rather than a branch.
    /// </exception>
    public async Task<CommitResult> CommitAsync(string message, IReadOnlyCollection<string>? paths = null, IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        RequireSignedIn("commit");
        var branch = RequireBranch("commit");

        var hashes = HashCache.Load(Root);
        var changes = WorkingTree.GetChanges(Root, Metadata.Files, hashes, cancellationToken);
        if (paths is not null)
        {
            var selected = new HashSet<string>(paths, StringComparer.Ordinal);
            changes = [.. changes.Where(c => selected.Contains(c.Path))];
        }

        if (changes.Count == 0)
        {
            throw new SyncException(SyncFailure.NothingToCommit, "There are no changes to commit.");
        }

        var unresolved = changes
            .Where(c => c.Kind != ChangeKind.Deleted && Metadata.Conflicts.Contains(c.Path) && HasConflictMarkers(c.Path))
            .Select(c => c.Path)
            .ToList();
        if (unresolved.Count > 0)
        {
            throw new SyncException(SyncFailure.UnresolvedConflicts, $"Resolve the conflicts in {Describe(unresolved)} before committing.");
        }

        progress?.Report(new SyncProgress("Checking GitHub for new commits"));
        var head = await _client.GetBranchHeadAsync(Link.Owner, Link.Repository, branch, cancellationToken).ConfigureAwait(false);
        if (head != Link.BaseCommit)
        {
            throw BehindRemote(branch);
        }

        var uploaded = await UploadAsync(_client, Link.Owner, Link.Repository, Root, changes, hashes, progress, cancellationToken, Metadata.Files).ConfigureAwait(false);
        var treeChanges = changes.Select(c => c.Kind == ChangeKind.Deleted
            ? new GitTreeChange(c.Path, Metadata.Files[c.Path].Mode, null)
            : new GitTreeChange(c.Path, uploaded[c.Path].Mode, uploaded[c.Path].Sha));

        progress?.Report(new SyncProgress("Creating the commit"));
        var parent = await _client.GetCommitAsync(Link.Owner, Link.Repository, Link.BaseCommit, cancellationToken).ConfigureAwait(false);
        var tree = await _client.CreateTreeAsync(Link.Owner, Link.Repository, parent.TreeSha, treeChanges, cancellationToken).ConfigureAwait(false);
        var commit = await _client.CreateCommitAsync(Link.Owner, Link.Repository, message, tree, [Link.BaseCommit], cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.UpdateBranchAsync(Link.Owner, Link.Repository, branch, commit.Sha, force: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch (GitHubApiException ex) when (ex.IsRejected)
        {
            // Someone pushed between the check above and now, or the branch is protected: then it has not moved, and
            // "pull first" sent the user round in circles, since a pull finds nothing new.
            var now = await _client.GetBranchHeadAsync(Link.Owner, Link.Repository, branch, CancellationToken.None).ConfigureAwait(false);
            if (now != Link.BaseCommit)
            {
                throw BehindRemote(branch);
            }

            throw new SyncException(
                SyncFailure.Other,
                $"GitHub did not let this commit onto {branch}: {ex.Message.TrimEnd('.')}. If {branch} is protected, switch to a new branch, commit there and open a pull request.");
        }

        var files = new Dictionary<string, TrackedFile>(Metadata.Files, StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (change.Kind == ChangeKind.Deleted)
            {
                files.Remove(change.Path);
            }
            else
            {
                files[change.Path] = uploaded[change.Path];
            }
        }

        var committed = changes.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        Save(new SyncMetadata(Link with { BaseCommit = commit.Sha }, files) { Conflicts = [.. Metadata.Conflicts.Where(c => !committed.Contains(c))] });
        hashes.Save(Root);
        foreach (var path in committed)
        {
            ConflictStore.Delete(Root, path);
        }

        return new CommitResult(commit, changes.Count);
    }

    /// <summary>Throws away local changes: modified and deleted files are restored, new files are deleted.</summary>
    /// <param name="paths">The files to restore.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    public async Task DiscardAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var hashes = HashCache.Load(Root);
        var restore = new Dictionary<string, TrackedFile>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (Metadata.Files.TryGetValue(path, out var file))
            {
                restore[path] = file;
            }
            else
            {
                FileFetcher.DeleteFile(Root, path);
                hashes.Forget(path);
            }
        }

        await new FileFetcher(_client, Link.Owner, Link.Repository)
            .WriteAsync(Root, Link.BaseCommit, restore, hashes, progress: null, cancellationToken).ConfigureAwait(false);

        var discarded = paths.ToHashSet(StringComparer.Ordinal);
        Save(Metadata with { Conflicts = [.. Metadata.Conflicts.Where(c => !discarded.Contains(c))] });
        hashes.Save(Root);
        foreach (var path in discarded)
        {
            ConflictStore.Delete(Root, path);
        }
    }

    /// <summary>Settles a file a pull could not merge.</summary>
    /// <param name="path">The conflicted file.</param>
    /// <param name="resolution">How to settle it.</param>
    /// <param name="cancellationToken">Cancels the download of either version.</param>
    public async Task ResolveAsync(string path, ConflictResolution resolution, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!Metadata.Conflicts.Contains(path))
        {
            return;
        }

        var conflict = ConflictStore.Load(Root, path);
        switch (resolution)
        {
            case ConflictResolution.KeepMine when conflict?.Mine is { } mine:
                await FileFetcher.WriteFileAsync(Root, path, mine, cancellationToken).ConfigureAwait(false);
                break;
            case ConflictResolution.KeepMine when conflict is not null:
                FileFetcher.DeleteFile(Root, path);
                break;
            case ConflictResolution.TakeTheirs when Metadata.Files.TryGetValue(path, out var theirs):
                var content = await _client.GetBlobAsync(Link.Owner, Link.Repository, theirs.Sha, cancellationToken).ConfigureAwait(false);
                await FileFetcher.WriteFileAsync(Root, path, content, cancellationToken).ConfigureAwait(false);
                break;
            case ConflictResolution.TakeTheirs:
                FileFetcher.DeleteFile(Root, path);
                break;
        }

        ConflictStore.Delete(Root, path);
        Save(Metadata with { Conflicts = [.. Metadata.Conflicts.Where(c => c != path)] });
    }

    private bool HasConflictMarkers(string path)
    {
        var full = WorkingTree.FullPath(Root, path);
        return File.Exists(full) && TextContent.TryDecode(File.ReadAllBytes(full), out var text) && TextMerge.HasConflictMarkers(text);
    }

    private void RequireSignedIn(string action)
    {
        if (!_client.IsSignedIn)
        {
            throw new SyncException(SyncFailure.NotSignedIn, $"Sign in to GitHub to {action}.");
        }
    }

    private static SyncException BehindRemote(string branch) =>
        new(SyncFailure.BehindRemote, $"GitHub has new commits on '{branch}'. Pull them first, then commit again.");

    private static string Describe(List<string> paths) =>
        paths.Count == 1 ? paths[0] : $"{paths.Count} files";
}

/// <summary>Detects whether bytes are UTF-8 text that can be merged line by line.</summary>
internal static class TextContent
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Decodes UTF-8 text; fails for binary content (a NUL byte) and invalid UTF-8.</summary>
    public static bool TryDecode(byte[] content, out string text)
    {
        text = string.Empty;
        if (content.AsSpan(0, Math.Min(content.Length, 8000)).Contains((byte)0))
        {
            return false;
        }

        try
        {
            text = Strict.GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Encodes text the way <see cref="TryDecode"/> read it: a byte order mark is kept as part of the text.</summary>
    public static byte[] Encode(string text) => Strict.GetBytes(text);
}
