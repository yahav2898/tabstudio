namespace TabStudio.SourceControl.GitHub;

/// <summary>Which pull requests to list.</summary>
public enum PullRequestStateFilter
{
    /// <summary>Pull requests that are open, drafts included.</summary>
    Open,

    /// <summary>Pull requests that were merged or closed.</summary>
    Closed,

    /// <summary>Every pull request.</summary>
    All,
}

/// <summary>Where a pull request stands.</summary>
public enum PullRequestState
{
    /// <summary>Open for review.</summary>
    Open,

    /// <summary>Closed without being merged.</summary>
    Closed,

    /// <summary>Merged into its base branch.</summary>
    Merged,
}

/// <summary>One end of a pull request: a branch, the commit at its tip, and the repository the branch is in.</summary>
/// <param name="Name">The branch name, such as <c>feature/login</c>.</param>
/// <param name="Sha">The commit at the branch's tip when GitHub last looked.</param>
/// <param name="RepositoryFullName">"owner/repository", or <see langword="null"/> when the repository was deleted, as a fork can be.</param>
public sealed record PullRequestBranch(string Name, string Sha, string? RepositoryFullName)
{
    /// <summary>The owner of the branch's repository, or <see langword="null"/> when it was deleted.</summary>
    public string? Owner => RepositoryFullName?.Split('/', 2)[0];
}

/// <summary>A pull request, as GitHub lists it.</summary>
/// <param name="Number">Its number in the repository.</param>
/// <param name="Title">Its title.</param>
/// <param name="Body">Its description, in Markdown, or <see langword="null"/> when it has none.</param>
/// <param name="State">Whether it is open, merged or closed.</param>
/// <param name="IsDraft">Whether it is a draft, which cannot be merged.</param>
/// <param name="Author">The login of the person who opened it.</param>
/// <param name="Head">The branch with the changes.</param>
/// <param name="Base">The branch the changes go into.</param>
/// <param name="CreatedAt">When it was opened.</param>
/// <param name="UpdatedAt">When anything about it last changed.</param>
/// <param name="MergedAt">When it was merged, if it was.</param>
/// <param name="WebUri">Its page on GitHub.</param>
/// <param name="RequestedReviewers">The logins of the people asked to review it who have not reviewed yet.</param>
/// <param name="NodeId">Its GraphQL id, for the operations only GraphQL offers.</param>
public sealed record PullRequestInfo(
    int Number,
    string Title,
    string? Body,
    PullRequestState State,
    bool IsDraft,
    string Author,
    PullRequestBranch Head,
    PullRequestBranch Base,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MergedAt,
    Uri WebUri,
    IReadOnlyList<string> RequestedReviewers,
    string NodeId)
{
    /// <summary>Whether the changes come from a branch of another repository, usually a fork.</summary>
    /// <param name="owner">The owner of the pull request's repository.</param>
    /// <param name="repository">The pull request's repository.</param>
    public bool IsFromFork(string owner, string repository) =>
        !string.Equals(Head.RepositoryFullName, $"{owner}/{repository}", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What GitHub worked out about whether a pull request can be merged.</summary>
public enum MergeableState
{
    /// <summary>GitHub has not worked it out yet; ask again in a moment.</summary>
    Unknown,

    /// <summary>It can be merged.</summary>
    Clean,

    /// <summary>The branches have conflicting changes.</summary>
    Conflicts,

    /// <summary>A branch protection rule, such as required reviews or checks, blocks merging.</summary>
    Blocked,

    /// <summary>The head branch is behind the base branch, and the repository requires it to be up to date.</summary>
    Behind,

    /// <summary>It can be merged, but some checks failed.</summary>
    Unstable,

    /// <summary>It can be merged, and pre-receive hooks run when it is.</summary>
    HasHooks,

    /// <summary>It is a draft.</summary>
    Draft,
}

/// <summary>A pull request with the details GitHub only gives one at a time.</summary>
/// <param name="Info">What lists show.</param>
/// <param name="IsMergeable">Whether it can be merged without conflicts; <see langword="null"/> while GitHub works it out.</param>
/// <param name="Mergeable">Whether it can be merged, all things considered.</param>
/// <param name="Additions">Lines added.</param>
/// <param name="Deletions">Lines removed.</param>
/// <param name="ChangedFiles">Files changed.</param>
/// <param name="Commits">Commits on the head branch that the base lacks.</param>
/// <param name="MergeCommitSha">The commit it was merged as, once merged.</param>
/// <param name="MergedBy">The login of the person who merged it, once merged.</param>
public sealed record PullRequestDetails(
    PullRequestInfo Info,
    bool? IsMergeable,
    MergeableState Mergeable,
    int Additions,
    int Deletions,
    int ChangedFiles,
    int Commits,
    string? MergeCommitSha,
    string? MergedBy);

/// <summary>How a pull request changes a file.</summary>
public enum PullRequestFileStatus
{
    /// <summary>The file is new.</summary>
    Added,

    /// <summary>The file was deleted.</summary>
    Removed,

    /// <summary>The file's content changed.</summary>
    Modified,

    /// <summary>The file moved, possibly with changes.</summary>
    Renamed,

    /// <summary>The file is a copy of another one.</summary>
    Copied,

    /// <summary>The file's mode changed, such as becoming executable.</summary>
    Changed,

    /// <summary>The file is listed without changes.</summary>
    Unchanged,
}

/// <summary>A file a pull request changes.</summary>
/// <param name="Path">The path in the head commit, with <c>/</c> separators.</param>
/// <param name="PreviousPath">For a renamed file, its path in the base.</param>
/// <param name="Status">How it changed.</param>
/// <param name="Additions">Lines added.</param>
/// <param name="Deletions">Lines removed.</param>
/// <param name="Patch">The unified diff GitHub shows, or <see langword="null"/> for binary and very large changes.</param>
/// <param name="Sha">The file's blob in the head commit; <see langword="null"/> for a deleted file.</param>
public sealed record PullRequestFile(string Path, string? PreviousPath, PullRequestFileStatus Status, int Additions, int Deletions, string? Patch, string? Sha)
{
    /// <summary>The status letter Visual Studio shows: A, D, M or R.</summary>
    public string StatusLetter => Status switch
    {
        PullRequestFileStatus.Added or PullRequestFileStatus.Copied => "A",
        PullRequestFileStatus.Removed => "D",
        PullRequestFileStatus.Renamed => "R",
        _ => "M",
    };
}

/// <summary>Which version of a file a line of a diff belongs to.</summary>
public enum DiffSide
{
    /// <summary>The base: the version before the changes, where removed lines are.</summary>
    Left,

    /// <summary>The head: the version with the changes, where added and unchanged lines are.</summary>
    Right,
}

/// <summary>A comment on a line, or a range of lines, of a pull request's diff.</summary>
/// <param name="Id">Its id.</param>
/// <param name="NodeId">Its GraphQL id.</param>
/// <param name="Path">The file.</param>
/// <param name="Line">The line it is on in the current diff, or <see langword="null"/> when the change it was about is gone (outdated).</param>
/// <param name="Side">Which version of the file <paramref name="Line"/> counts in.</param>
/// <param name="StartLine">The first line of a multi-line comment.</param>
/// <param name="StartSide">Which version <paramref name="StartLine"/> counts in.</param>
/// <param name="OriginalLine">The line it was written on, in the commit it was written on.</param>
/// <param name="OriginalStartLine">The first line of a multi-line comment when it was written.</param>
/// <param name="InReplyToId">The comment this one replies to, for replies.</param>
/// <param name="Author">The login of who wrote it.</param>
/// <param name="Body">The text, in Markdown.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="UpdatedAt">When it was last edited.</param>
/// <param name="DiffHunk">The part of the diff it was written on, ending with its line.</param>
/// <param name="CommitId">The commit its line counts in.</param>
/// <param name="ReviewId">The review it belongs to.</param>
/// <param name="WebUri">Its place on GitHub, once it is visible there.</param>
public sealed record ReviewComment(
    long Id,
    string NodeId,
    string Path,
    int? Line,
    DiffSide Side,
    int? StartLine,
    DiffSide? StartSide,
    int? OriginalLine,
    int? OriginalStartLine,
    long? InReplyToId,
    string Author,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string DiffHunk,
    string CommitId,
    long? ReviewId,
    Uri? WebUri)
{
    /// <summary>Whether the lines it was written on changed since, so it no longer has a place in the current diff.</summary>
    public bool IsOutdated => Line is null;

    /// <summary>The line it belongs on: the current one, or for an outdated comment the one it was written on.</summary>
    public int? AnchorLine => Line ?? OriginalLine;

    /// <summary>The text of the line it was written on, from the end of its diff hunk, without the diff's prefix.</summary>
    public string? CommentedLineText
    {
        get
        {
            var lines = DiffHunk.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
            var last = lines[^1];
            return last.Length == 0 || last.StartsWith("@@", StringComparison.Ordinal) ? null : last[1..];
        }
    }
}

/// <summary>What a review says about a pull request.</summary>
public enum ReviewState
{
    /// <summary>Started but not submitted: only its author sees it.</summary>
    Pending,

    /// <summary>Comments without a verdict.</summary>
    Commented,

    /// <summary>Approved.</summary>
    Approved,

    /// <summary>Changes requested.</summary>
    ChangesRequested,

    /// <summary>Dismissed by a maintainer.</summary>
    Dismissed,
}

/// <summary>A review of a pull request.</summary>
/// <param name="Id">Its id.</param>
/// <param name="NodeId">Its GraphQL id.</param>
/// <param name="Author">The login of the reviewer.</param>
/// <param name="State">What it says.</param>
/// <param name="Body">Its summary, in Markdown, if it has one.</param>
/// <param name="SubmittedAt">When it was submitted; <see langword="null"/> while pending.</param>
/// <param name="CommitId">The commit it reviewed.</param>
/// <param name="WebUri">Its place on GitHub.</param>
public sealed record PullRequestReview(long Id, string NodeId, string Author, ReviewState State, string? Body, DateTimeOffset? SubmittedAt, string? CommitId, Uri? WebUri);

/// <summary>How a review is submitted.</summary>
public enum ReviewEvent
{
    /// <summary>Comments without a verdict.</summary>
    Comment,

    /// <summary>Approves the changes.</summary>
    Approve,

    /// <summary>Asks for changes before merging.</summary>
    RequestChanges,
}

/// <summary>A comment to add to a review.</summary>
/// <param name="Path">The file.</param>
/// <param name="Line">The line, or the last line of a range.</param>
/// <param name="Side">Which version of the file the line counts in.</param>
/// <param name="Body">The text, in Markdown.</param>
/// <param name="StartLine">The first line of a range, or <see langword="null"/> for one line.</param>
/// <param name="StartSide">Which version <paramref name="StartLine"/> counts in.</param>
public sealed record NewReviewComment(string Path, int Line, DiffSide Side, string Body, int? StartLine = null, DiffSide? StartSide = null);

/// <summary>A comment in a pull request's conversation, outside the diff.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Author">The login of who wrote it.</param>
/// <param name="Body">The text, in Markdown.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="WebUri">Its place on GitHub.</param>
public sealed record IssueComment(long Id, string Author, string Body, DateTimeOffset CreatedAt, Uri? WebUri);

/// <summary>Whether a review thread is resolved, and its GraphQL id for changing that.</summary>
/// <param name="ThreadId">The thread's GraphQL id.</param>
/// <param name="IsResolved">Whether someone marked it resolved.</param>
/// <param name="CanResolve">Whether the signed-in account may resolve it, or reopen it when resolved.</param>
public sealed record ReviewThreadState(string ThreadId, bool IsResolved, bool CanResolve);

/// <summary>How a pull request's commits go into its base branch.</summary>
public enum MergeMethod
{
    /// <summary>A merge commit that keeps every commit.</summary>
    Merge,

    /// <summary>One commit with all the changes.</summary>
    Squash,

    /// <summary>Every commit replayed onto the base, without a merge commit.</summary>
    Rebase,
}

/// <summary>A repository's rules for merging pull requests.</summary>
/// <param name="AllowMergeCommit">Whether merge commits are allowed.</param>
/// <param name="AllowSquashMerge">Whether squashing is allowed.</param>
/// <param name="AllowRebaseMerge">Whether rebasing is allowed.</param>
/// <param name="DeleteBranchOnMerge">Whether GitHub deletes head branches once they are merged.</param>
/// <param name="MergeCommitTitle">The default title of a merge commit: <c>MERGE_MESSAGE</c> or <c>PR_TITLE</c>.</param>
/// <param name="MergeCommitMessage">The default message of a merge commit: <c>PR_TITLE</c>, <c>PR_BODY</c> or <c>BLANK</c>.</param>
/// <param name="SquashCommitTitle">The default title of a squashed commit: <c>COMMIT_OR_PR_TITLE</c> or <c>PR_TITLE</c>.</param>
/// <param name="SquashCommitMessage">The default message of a squashed commit: <c>COMMIT_MESSAGES</c>, <c>PR_BODY</c> or <c>BLANK</c>.</param>
public sealed record RepositoryMergeSettings(
    bool AllowMergeCommit,
    bool AllowSquashMerge,
    bool AllowRebaseMerge,
    bool DeleteBranchOnMerge,
    string MergeCommitTitle,
    string MergeCommitMessage,
    string SquashCommitTitle,
    string SquashCommitMessage)
{
    /// <summary>GitHub's defaults, for repositories that do not say.</summary>
    public static RepositoryMergeSettings Default { get; } = new(true, true, true, false, "MERGE_MESSAGE", "PR_TITLE", "COMMIT_OR_PR_TITLE", "COMMIT_MESSAGES");

    /// <summary>The methods the repository allows, in the order GitHub offers them.</summary>
    public IReadOnlyList<MergeMethod> AllowedMethods
    {
        get
        {
            var methods = new List<MergeMethod>(3);
            if (AllowMergeCommit)
            {
                methods.Add(MergeMethod.Merge);
            }

            if (AllowSquashMerge)
            {
                methods.Add(MergeMethod.Squash);
            }

            if (AllowRebaseMerge)
            {
                methods.Add(MergeMethod.Rebase);
            }

            return methods;
        }
    }
}

/// <summary>The outcome of a merge.</summary>
/// <param name="Sha">The commit the base branch moved to.</param>
/// <param name="Message">GitHub's message, such as "Pull Request successfully merged".</param>
public sealed record MergeResult(string Sha, string Message);
