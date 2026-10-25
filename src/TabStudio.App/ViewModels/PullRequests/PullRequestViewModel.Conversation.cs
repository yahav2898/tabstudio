using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>The conversation: comments and reviews, oldest first, and adding a comment.</summary>
public sealed partial class PullRequestViewModel
{
    private IReadOnlyList<IssueComment> _conversation = [];

    /// <summary>The conversation, oldest first, starting with the pull request being opened.</summary>
    public ObservableCollection<TimelineItemViewModel> Timeline { get; } = [];

    /// <summary>The comment being written at the end of the conversation.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommentCommand))]
    public partial string NewComment { get; set; } = string.Empty;

    /// <summary>Whether the comment is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommentCommand))]
    public partial bool IsSendingComment { get; private set; }

    /// <summary>Builds the conversation from the comments and the submitted reviews.</summary>
    private void BuildTimeline(IReadOnlyList<PullRequestReview> reviews, IReadOnlyList<ReviewComment> comments)
    {
        Timeline.Clear();
        if (Info is not { } info)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var items = new List<TimelineItemViewModel>
        {
            new(TimelineKind.Opened, info.Author, "opened this pull request", ReviewDisplay.Age(info.CreatedAt, now), info.CreatedAt, [], null),
        };

        items.AddRange(_conversation.Select(c => new TimelineItemViewModel(
            TimelineKind.Comment, c.Author, "commented", ReviewDisplay.Age(c.CreatedAt, now), c.CreatedAt, MarkdownParser.Parse(c.Body), null)));

        foreach (var review in reviews.Where(r => r.State != ReviewState.Pending))
        {
            if (review.SubmittedAt is not { } submitted)
            {
                continue;
            }

            var onFiles = comments.Count(c => c.ReviewId == review.Id);
            var (kind, action) = review.State switch
            {
                ReviewState.Approved => (TimelineKind.Approved, "approved these changes"),
                ReviewState.ChangesRequested => (TimelineKind.ChangesRequested, "requested changes"),
                ReviewState.Dismissed => (TimelineKind.Reviewed, "reviewed (dismissed)"),
                _ => (TimelineKind.Reviewed, "reviewed"),
            };

            // A lone comment or reply on the diff is a review of its own on GitHub; its thread shows it already.
            if (kind == TimelineKind.Reviewed && review.Body is null)
            {
                continue;
            }

            var note = onFiles > 0 ? ReviewDisplay.Count(onFiles, "comment") + " on files" : null;
            items.Add(new TimelineItemViewModel(kind, review.Author, action, ReviewDisplay.Age(submitted, now), submitted, review.Body is { } body ? MarkdownParser.Parse(body) : [], note));
        }

        if (info.MergedAt is { } merged)
        {
            items.Add(new TimelineItemViewModel(TimelineKind.Merged, Details?.MergedBy ?? info.Author, $"merged this into {info.Base.Name}", ReviewDisplay.Age(merged, now), merged, [], null));
        }

        foreach (var item in items.OrderBy(i => i.Time))
        {
            Timeline.Add(item);
        }

        OnPropertyChanged(nameof(ConversationTabText));
    }

    /// <summary>Adds a comment to the conversation.</summary>
    [RelayCommand(CanExecute = nameof(CanAddComment))]
    private async Task AddCommentAsync()
    {
        if (!RequireSignIn("comment"))
        {
            return;
        }

        ErrorText = null;
        IsSendingComment = true;
        try
        {
            var comment = await _host.CreateClient().CreateIssueCommentAsync(Owner, Repository, Number, NewComment.Trim(), _lifetime.Token);
            _conversation = [.. _conversation, comment];
            NewComment = string.Empty;
            BuildTimeline(_reviews, _publishedComments);
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            IsSendingComment = false;
        }
    }

    private bool CanAddComment() => !IsSendingComment && !string.IsNullOrWhiteSpace(NewComment);
}
