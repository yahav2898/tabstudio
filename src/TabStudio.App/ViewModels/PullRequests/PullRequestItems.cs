using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>A row of the Pull Requests window.</summary>
public sealed class PullRequestItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="pullRequest">The pull request.</param>
    /// <param name="commentCount">How many comments its conversation has, or <see langword="null"/> when unknown.</param>
    /// <param name="login">The signed-in account's login, to mark reviews it is asked for.</param>
    /// <param name="now">The current time, for the age.</param>
    /// <param name="open">Opens the pull request.</param>
    public PullRequestItemViewModel(PullRequestInfo pullRequest, int? commentCount, string? login, DateTimeOffset now, IAsyncRelayCommand<PullRequestItemViewModel?> open)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        Info = pullRequest;
        CommentCount = commentCount;
        IsReviewRequested = login is not null && pullRequest.RequestedReviewers.Contains(login, StringComparer.OrdinalIgnoreCase);
        Age = pullRequest.State switch
        {
            PullRequestState.Merged => "merged " + ReviewDisplay.Age(pullRequest.MergedAt ?? pullRequest.UpdatedAt, now),
            PullRequestState.Closed => "closed " + ReviewDisplay.Age(pullRequest.UpdatedAt, now),
            _ => "opened " + ReviewDisplay.Age(pullRequest.CreatedAt, now),
        };
        Open = open;
    }

    /// <summary>The pull request.</summary>
    public PullRequestInfo Info { get; }

    /// <summary>Its number.</summary>
    public int Number => Info.Number;

    /// <summary>"#12".</summary>
    public string NumberText => "#" + Info.Number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Its title.</summary>
    public string Title => Info.Title;

    /// <summary>Who opened it.</summary>
    public string Author => Info.Author;

    /// <summary>"feature/login → main".</summary>
    public string BranchText => $"{Info.Head.Name} → {Info.Base.Name}";

    /// <summary>Whether it is a draft.</summary>
    public bool IsDraft => Info.IsDraft && Info.State == PullRequestState.Open;

    /// <summary>Whether it was merged.</summary>
    public bool IsMerged => Info.State == PullRequestState.Merged;

    /// <summary>Whether it was closed without merging.</summary>
    public bool IsClosed => Info.State == PullRequestState.Closed;

    /// <summary>"opened 3 days ago", "merged yesterday".</summary>
    public string Age { get; }

    /// <summary>How many comments its conversation has, or <see langword="null"/> when unknown.</summary>
    public int? CommentCount { get; }

    /// <summary>Whether to show the comment count.</summary>
    public bool HasComments => CommentCount > 0;

    /// <summary>Whether the signed-in account is asked to review it.</summary>
    public bool IsReviewRequested { get; }

    /// <summary>Opens the pull request.</summary>
    public IAsyncRelayCommand<PullRequestItemViewModel?> Open { get; }
}

/// <summary>What happened in a pull request's conversation.</summary>
public enum TimelineKind
{
    /// <summary>The pull request was opened.</summary>
    Opened,

    /// <summary>A comment.</summary>
    Comment,

    /// <summary>A review with comments only.</summary>
    Reviewed,

    /// <summary>A review that approved the changes.</summary>
    Approved,

    /// <summary>A review that asked for changes.</summary>
    ChangesRequested,

    /// <summary>The pull request was merged.</summary>
    Merged,
}

/// <summary>An entry of a pull request's conversation: a comment, a review or an event.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Author">Who did it.</param>
/// <param name="Action">"commented", "approved these changes"...</param>
/// <param name="When">When, such as "2 days ago".</param>
/// <param name="Time">When, for sorting.</param>
/// <param name="Body">The text, read as Markdown; empty for events.</param>
/// <param name="Note">A note under it, such as "3 comments on files", or <see langword="null"/>.</param>
public sealed record TimelineItemViewModel(TimelineKind Kind, string Author, string Action, string When, DateTimeOffset Time, IReadOnlyList<MarkdownBlock> Body, string? Note)
{
    /// <summary>The letter on the author's avatar.</summary>
    public string Initial => ReviewDisplay.Initial(Author);

    /// <summary>Whether there is text to show.</summary>
    public bool HasBody => Body.Count > 0;

    /// <summary>Whether there is a note to show.</summary>
    public bool HasNote => Note is not null;
}

/// <summary>Where a reviewer stands.</summary>
public enum ReviewerState
{
    /// <summary>Asked to review, has not yet.</summary>
    Requested,

    /// <summary>Left comments.</summary>
    Commented,

    /// <summary>Approved.</summary>
    Approved,

    /// <summary>Asked for changes.</summary>
    ChangesRequested,
}

/// <summary>A reviewer of a pull request and their latest verdict.</summary>
/// <param name="Login">Their login.</param>
/// <param name="State">Their latest verdict.</param>
public sealed record ReviewerViewModel(string Login, ReviewerState State)
{
    /// <summary>The letter on their avatar.</summary>
    public string Initial => ReviewDisplay.Initial(Login);

    /// <summary>"Approved", "Requested changes"...</summary>
    public string StateText => State switch
    {
        ReviewerState.Approved => "Approved",
        ReviewerState.ChangesRequested => "Requested changes",
        ReviewerState.Commented => "Commented",
        _ => "Review requested",
    };
}

/// <summary>A commit of a pull request.</summary>
/// <param name="Sha">Its hash.</param>
/// <param name="Summary">The first line of its message.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="When">When, such as "2 days ago".</param>
/// <param name="Open">Opens it on GitHub.</param>
public sealed record PullRequestCommitItemViewModel(string Sha, string Summary, string Author, string When, IAsyncRelayCommand<PullRequestCommitItemViewModel?> Open)
{
    /// <summary>The first seven characters of the hash.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
}

/// <summary>A way to merge, as the merge panel offers it.</summary>
/// <param name="Method">The method.</param>
/// <param name="Label">"Squash and merge".</param>
/// <param name="Description">What it does to the base branch.</param>
public sealed record MergeMethodOption(MergeMethod Method, string Label, string Description)
{
    /// <summary>The option for a method, described for the base branch.</summary>
    /// <param name="method">The method.</param>
    /// <param name="baseBranch">The branch the changes go into.</param>
    public static MergeMethodOption For(MergeMethod method, string baseBranch) => method switch
    {
        MergeMethod.Squash => new(method, "Squash and merge", $"Combines the commits into one commit on {baseBranch}."),
        MergeMethod.Rebase => new(method, "Rebase and merge", $"Adds the commits to {baseBranch} one by one, without a merge commit."),
        _ => new(method, "Create a merge commit", $"Adds every commit to {baseBranch} with a merge commit."),
    };

    /// <inheritdoc />
    public override string ToString() => Label;
}
