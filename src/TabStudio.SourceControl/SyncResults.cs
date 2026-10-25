using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

/// <summary>Progress of a source control operation.</summary>
/// <param name="Message">What is happening, such as "Downloading 12 files".</param>
/// <param name="Completed">How many steps are done.</param>
/// <param name="Total">How many steps there are, or 0 when unknown.</param>
public sealed record SyncProgress(string Message, int Completed = 0, int Total = 0)
{
    /// <summary>The fraction done, or <see langword="null"/> when unknown.</summary>
    public double? Fraction => Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : null;
}

/// <summary>The state of a linked folder.</summary>
/// <param name="Link">The repository, branch and last synced commit.</param>
/// <param name="Changes">Files that differ from the last synced commit.</param>
/// <param name="Conflicts">Files a pull could not merge.</param>
/// <param name="IncomingCommits">
/// Commits on GitHub that the folder does not have yet, or <see langword="null"/> when GitHub was not asked.
/// </param>
public sealed record SyncStatus(GitHubLink Link, IReadOnlyList<FileChange> Changes, IReadOnlyList<string> Conflicts, int? IncomingCommits)
{
    /// <summary>Whether there is anything to commit.</summary>
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>The outcome of a commit.</summary>
/// <param name="Commit">The new commit, already on GitHub.</param>
/// <param name="Files">How many files it changed.</param>
public sealed record CommitResult(GitCommit Commit, int Files);

/// <summary>The outcome of a pull or a branch switch.</summary>
/// <param name="Head">The commit the folder now matches.</param>
/// <param name="Updated">Files that were written, deleted or merged.</param>
/// <param name="Conflicts">Files with changes on both sides that could not be merged.</param>
public sealed record PullResult(string Head, IReadOnlyList<string> Updated, IReadOnlyList<string> Conflicts)
{
    /// <summary>Whether the folder already matched GitHub.</summary>
    public bool WasUpToDate => Updated.Count == 0 && Conflicts.Count == 0;
}

/// <summary>How to settle a conflicted file.</summary>
public enum ConflictResolution
{
    /// <summary>The file has been edited to its final content: keep it as it is.</summary>
    MarkResolved,

    /// <summary>Throw away GitHub's version and keep the local file as it was before the pull.</summary>
    KeepMine,

    /// <summary>Throw away the local changes and use GitHub's version.</summary>
    TakeTheirs,
}
