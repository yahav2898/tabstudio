using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// The review: threads and the signed-in account's pending review, whose comments wait on GitHub until the review is
/// finished as a comment, an approval or a request for changes, or is discarded.
/// </summary>
public sealed partial class PullRequestViewModel
{
    private readonly Dictionary<long, ReviewThreadViewModel> _threads = [];
    private IReadOnlyList<PullRequestReview> _reviews = [];
    private IReadOnlyList<ReviewComment> _publishedComments = [];
    private PullRequestReview? _pendingReview;
    private IReadOnlyDictionary<long, ReviewThreadState> _threadStates = new Dictionary<long, ReviewThreadState>();

    /// <summary>Every review thread, published and pending, by file and line.</summary>
    public IReadOnlyList<ReviewThreadViewModel> Threads { get; private set; } = [];

    /// <summary>Whether the signed-in account has a review in progress.</summary>
    public bool HasPendingReview => _pendingReview is not null;

    /// <summary>How many comments the pending review holds.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingText), nameof(HasPendingComments), nameof(FinishReviewNote))]
    public partial int PendingCount { get; private set; }

    /// <summary>Whether the pending review holds comments.</summary>
    public bool HasPendingComments => PendingCount > 0;

    /// <summary>"2 pending comments".</summary>
    public string PendingText => ReviewDisplay.Count(PendingCount, "pending comment");

    /// <summary>Whether the Finish Review panel is open.</summary>
    [ObservableProperty]
    public partial bool IsFinishReviewOpen { get; private set; }

    /// <summary>The review's summary.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitReviewCommand))]
    public partial string ReviewSummary { get; set; } = string.Empty;

    /// <summary>Whether the review is being submitted or discarded.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitReviewCommand), nameof(DiscardReviewCommand))]
    public partial bool IsSubmittingReview { get; private set; }

    /// <summary>Why the review could not be submitted.</summary>
    [ObservableProperty]
    public partial string? ReviewErrorText { get; private set; }

    /// <summary>Whether the signed-in account may approve or request changes: not on its own pull request, as GitHub rules.</summary>
    public bool CanApprove => !IsMine;

    /// <summary>Why Approve and Request Changes are not offered, on the signed-in account's own pull request.</summary>
    public string? OwnPullRequestNote => IsMine ? "This is your pull request, so GitHub lets you comment on it but not approve it or request changes." : null;

    /// <summary>What submitting sends, such as "Your 2 pending comments are sent with the review."</summary>
    public string FinishReviewNote => PendingCount switch
    {
        0 => "No comments on files are pending. The summary is sent on its own.",
        1 => "Your pending comment is sent with the review.",
        _ => $"Your {PendingText} are sent with the review.",
    };

    /// <summary>
    /// Adds a comment to the pending review, starting one when there is none. Called by a new comment and by
    /// keeping one of Claude's drafts.
    /// </summary>
    /// <param name="comment">The comment.</param>
    /// <returns>Why it could not be added, or <see langword="null"/>.</returns>
    internal Task<string?> AddPendingCommentAsync(NewReviewComment comment) => RunReviewActionAsync("review", async (client, token) =>
    {
        if (_pendingReview is { } pending)
        {
            await client.AddPendingReviewCommentAsync(pending.NodeId, comment, token);
        }
        else
        {
            _pendingReview = await client.CreateReviewAsync(Owner, Repository, Number, Info?.Head.Sha, null, null, [comment], token);
        }
    });

    /// <summary>Replies to a thread: into the pending review while there is one, as GitHub does, and at once otherwise.</summary>
    /// <param name="thread">The thread.</param>
    /// <param name="body">The reply.</param>
    /// <returns>Why it could not be sent, or <see langword="null"/>.</returns>
    internal Task<string?> ReplyAsync(ReviewThreadViewModel thread, string body) => RunReviewActionAsync("reply", async (client, token) =>
    {
        if (_pendingReview is { } pending)
        {
            await client.AddPendingReplyAsync(pending.NodeId, thread.Thread.Root.NodeId, body, token);
        }
        else
        {
            await client.ReplyToReviewCommentAsync(Owner, Repository, Number, thread.Thread.Root.Id, body, token);
        }
    });

    /// <summary>Marks a thread resolved, or opens it again.</summary>
    /// <param name="thread">The thread.</param>
    /// <param name="resolved">Whether it is resolved.</param>
    /// <returns>Why it could not be changed, or <see langword="null"/>.</returns>
    internal Task<string?> SetResolvedAsync(ReviewThreadViewModel thread, bool resolved) => RunReviewActionAsync("resolve conversations", async (client, token) =>
    {
        if (_threadStates.TryGetValue(thread.Thread.Root.Id, out var state))
        {
            await client.SetReviewThreadResolvedAsync(state.ThreadId, resolved, token);
        }
    });

    /// <summary>Takes a comment out of the pending review.</summary>
    /// <param name="comment">The pending comment.</param>
    internal async Task DeletePendingCommentAsync(ReviewCommentViewModel comment) =>
        ErrorText = await RunReviewActionAsync("review", (client, token) => client.DeleteReviewCommentAsync(Owner, Repository, comment.Comment.Id, token));

    /// <summary>
    /// Reads the review from GitHub: its threads, which ones are resolved, and the signed-in account's pending
    /// review with its comments.
    /// </summary>
    private async Task LoadReviewAsync(GitHubClient client, CancellationToken token)
    {
        var comments = client.GetReviewCommentsAsync(Owner, Repository, Number, token);
        var reviews = client.GetReviewsAsync(Owner, Repository, Number, token);
        var states = GetThreadStatesAsync(client, token);
        await Task.WhenAll(comments, reviews, states);

        var login = _host.Account.Login;
        _reviews = reviews.Result;
        _publishedComments = comments.Result;
        _threadStates = states.Result;
        _pendingReview = login is null ? null : _reviews.LastOrDefault(r => r.State == ReviewState.Pending && string.Equals(r.Author, login, StringComparison.OrdinalIgnoreCase));
        var pending = _pendingReview is { } review
            ? await client.GetReviewCommentsOfReviewAsync(Owner, Repository, Number, review.Id, token)
            : [];

        BuildThreads(pending);
        BuildTimeline(_reviews, _publishedComments);
        BuildReviewers(_reviews);
        UpdateFileCounts();
        OnPropertyChanged(nameof(HasPendingReview));
        DiscardReviewCommand.NotifyCanExecuteChanged();
        RaiseReviewChanged();
    }

    /// <summary>Groups the comments into threads, keeping each thread's view model so a reply being written stays.</summary>
    private void BuildThreads(IReadOnlyList<ReviewComment> pending)
    {
        var pendingIds = pending.Select(c => c.Id).ToHashSet();
        var all = _publishedComments.Concat(pending);
        var now = DateTimeOffset.Now;
        var threads = new List<ReviewThreadViewModel>();
        foreach (var thread in ReviewThread.Group(all))
        {
            if (!_threads.TryGetValue(thread.Root.Id, out var model))
            {
                model = new ReviewThreadViewModel(this, thread);
                _threads[thread.Root.Id] = model;
            }

            model.Update(thread, pendingIds, _threadStates.GetValueOrDefault(thread.Root.Id), now);
            threads.Add(model);
        }

        foreach (var gone in _threads.Keys.Except(threads.Select(t => t.Thread.Root.Id)).ToList())
        {
            _threads.Remove(gone);
        }

        Threads = threads;
        PendingCount = pendingIds.Count;
        OnPropertyChanged(nameof(Threads));
    }

    /// <summary>Which threads are resolved; GitHub tells only signed-in accounts, through GraphQL.</summary>
    private async Task<IReadOnlyDictionary<long, ReviewThreadState>> GetThreadStatesAsync(GitHubClient client, CancellationToken token)
    {
        if (!_host.Account.IsSignedIn)
        {
            return new Dictionary<long, ReviewThreadState>();
        }

        try
        {
            return await client.GetReviewThreadStatesAsync(Owner, Repository, Number, token);
        }
        catch (GitHubApiException)
        {
            // Without GraphQL, threads show without their resolved state.
            return new Dictionary<long, ReviewThreadState>();
        }
    }

    /// <summary>Runs a change to the review on GitHub, then reads the review again.</summary>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    private async Task<string?> RunReviewActionAsync(string action, Func<GitHubClient, CancellationToken, Task> change)
    {
        if (!RequireSignIn(action))
        {
            return ErrorText;
        }

        var token = _lifetime.Token;
        var client = _host.CreateClient();
        try
        {
            await change(client, token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return SyncErrors.Describe(ex);
        }

        try
        {
            await LoadReviewAsync(client, token);
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ErrorText = SyncErrors.Describe(ex);
        }

        return null;
    }

    [RelayCommand]
    private void OpenFinishReview()
    {
        ReviewErrorText = null;
        IsFinishReviewOpen = true;
    }

    [RelayCommand]
    private void CloseFinishReview() => IsFinishReviewOpen = false;

    /// <summary>Submits the review as a comment, an approval or a request for changes, with the pending comments.</summary>
    [RelayCommand(CanExecute = nameof(CanSubmitReview))]
    private async Task SubmitReviewAsync(ReviewEvent verdict)
    {
        ReviewErrorText = verdict switch
        {
            ReviewEvent.RequestChanges when string.IsNullOrWhiteSpace(ReviewSummary) => "Say what needs to change in the summary.",
            ReviewEvent.Comment when string.IsNullOrWhiteSpace(ReviewSummary) && PendingCount == 0 => "Write a summary, or add comments on the files first.",
            ReviewEvent.Approve or ReviewEvent.RequestChanges when IsMine => OwnPullRequestNote,
            _ => null,
        };
        if (ReviewErrorText is not null || !RequireSignIn("review"))
        {
            return;
        }

        IsSubmittingReview = true;
        var summary = string.IsNullOrWhiteSpace(ReviewSummary) ? null : ReviewSummary.Trim();
        var error = await RunReviewActionAsync("review", async (client, token) =>
        {
            if (_pendingReview is { } pending)
            {
                await client.SubmitReviewAsync(Owner, Repository, Number, pending.Id, verdict, summary, token);
            }
            else
            {
                await client.CreateReviewAsync(Owner, Repository, Number, Info?.Head.Sha, summary, verdict, [], token);
            }
        });
        IsSubmittingReview = false;
        if (error is not null)
        {
            ReviewErrorText = error;
            return;
        }

        ReviewSummary = string.Empty;
        IsFinishReviewOpen = false;

        // A review takes the reviewer off the requested list, and shows in the conversation.
        await LoadAsync();
        InfoText = verdict switch
        {
            ReviewEvent.Approve => $"Approved pull request {NumberText}.",
            ReviewEvent.RequestChanges => $"Requested changes on pull request {NumberText}.",
            _ => $"Commented on pull request {NumberText}.",
        };
        _host.ReportStatus(InfoText);
    }

    /// <summary>Deletes the pending review and its comments, after asking.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscardReview))]
    private async Task DiscardReviewAsync()
    {
        if (_pendingReview is not { } pending
            || !await _host.Prompt.ConfirmAsync("Discard Your Review?", $"Your {PendingText} on pull request {NumberText} are deleted from GitHub.", "Discard Review", destructive: true))
        {
            return;
        }

        IsSubmittingReview = true;
        var error = await RunReviewActionAsync("review", (client, token) => client.DeletePendingReviewAsync(Owner, Repository, Number, pending.Id, token));
        IsSubmittingReview = false;
        ReviewErrorText = error;
        if (error is null)
        {
            IsFinishReviewOpen = false;
            InfoText = "Discarded your pending review.";
        }
    }

    private bool CanSubmitReview(ReviewEvent verdict) => !IsSubmittingReview;

    private bool CanDiscardReview() => !IsSubmittingReview && HasPendingReview;
}
