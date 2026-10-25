using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>A comment of a review thread: who wrote it, when, and its text as Markdown.</summary>
public sealed partial class ReviewCommentViewModel : ObservableObject
{
    private readonly PullRequestViewModel _owner;

    internal ReviewCommentViewModel(PullRequestViewModel owner, ReviewComment comment, bool isPending, DateTimeOffset now)
    {
        _owner = owner;
        Comment = comment;
        IsPending = isPending;
        Initial = ReviewDisplay.Initial(comment.Author);
        When = ReviewDisplay.Age(comment.CreatedAt, now);
        Body = MarkdownParser.Parse(comment.Body);
    }

    /// <summary>The comment.</summary>
    public ReviewComment Comment { get; }

    /// <summary>Who wrote it.</summary>
    public string Author => Comment.Author;

    /// <summary>The letter on the author's avatar.</summary>
    public string Initial { get; }

    /// <summary>When it was written, such as "2 hours ago".</summary>
    public string When { get; }

    /// <summary>The text, read as Markdown.</summary>
    public IReadOnlyList<MarkdownBlock> Body { get; }

    /// <summary>Whether it belongs to the signed-in account's pending review, so only its author sees it until the review is submitted.</summary>
    public bool IsPending { get; }

    /// <summary>Takes a pending comment out of the review.</summary>
    [RelayCommand]
    private Task DeleteAsync() => _owner.DeletePendingCommentAsync(this);
}

/// <summary>
/// A review thread, as it shows under its line in a diff or the editor: its comments, whether it is outdated,
/// pending or resolved, and a reply box. One instance per thread, kept across refreshes so a reply being
/// written is not lost.
/// </summary>
public sealed partial class ReviewThreadViewModel : ObservableObject
{
    private readonly PullRequestViewModel _owner;

    internal ReviewThreadViewModel(PullRequestViewModel owner, ReviewThread thread)
    {
        _owner = owner;
        Thread = thread;
    }

    /// <summary>The thread as GitHub has it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Path), nameof(Side), nameof(Line), nameof(StartLine), nameof(IsOutdated), nameof(LocationText))]
    public partial ReviewThread Thread { get; private set; }

    /// <summary>The file.</summary>
    public string Path => Thread.Path;

    /// <summary>Which version of the file the thread's lines count in.</summary>
    public DiffSide Side => Thread.Side;

    /// <summary>The line it belongs under, or <see langword="null"/> for a comment on the whole file.</summary>
    public int? Line => Thread.Line;

    /// <summary>The first line of a multi-line thread.</summary>
    public int? StartLine => Thread.StartLine;

    /// <summary>Whether the lines it was written on changed since.</summary>
    public bool IsOutdated => Thread.IsOutdated;

    /// <summary>The comments, the first one first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReviewCommentViewModel> Comments { get; private set; } = [];

    /// <summary>Whether the thread was started in the signed-in account's pending review.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReply))]
    public partial bool IsPending { get; private set; }

    /// <summary>Whether someone marked the thread resolved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolveText))]
    public partial bool IsResolved { get; private set; }

    /// <summary>Whether the signed-in account may resolve the thread, or reopen it.</summary>
    [ObservableProperty]
    public partial bool CanResolve { get; private set; }

    /// <summary>"Line 12", "Lines 10–12 (removed)" or "Whole file".</summary>
    public string LocationText => Line is { } line
        ? Capitalize(ReviewDisplay.Lines(line, StartLine)) + (Side == DiffSide.Left ? " (removed)" : string.Empty)
        : "Whole file";

    /// <summary>The resolve button's label.</summary>
    public string ResolveText => IsResolved ? "Unresolve" : "Resolve";

    /// <summary>Whether a reply can be added: not to a thread that is itself still pending.</summary>
    public bool CanReply => !IsPending;

    /// <summary>The reply button's label: replies go into the pending review while there is one, as on GitHub.</summary>
    public string SendReplyText => _owner.HasPendingReview ? "Add to Review" : "Reply";

    /// <summary>Whether the reply box is open.</summary>
    [ObservableProperty]
    public partial bool IsReplying { get; private set; }

    /// <summary>The reply being written.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendReplyCommand))]
    public partial string ReplyText { get; set; } = string.Empty;

    /// <summary>Whether the reply is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendReplyCommand))]
    public partial bool IsSending { get; private set; }

    /// <summary>Why the last action failed.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>Takes the thread's latest state after a refresh, keeping a reply that is being written.</summary>
    internal void Update(ReviewThread thread, IReadOnlySet<long> pendingIds, ReviewThreadState? state, DateTimeOffset now)
    {
        Thread = thread;
        Comments = [.. thread.Comments.Select(c => new ReviewCommentViewModel(_owner, c, pendingIds.Contains(c.Id), now))];
        IsPending = pendingIds.Contains(thread.Root.Id);
        IsResolved = state?.IsResolved ?? false;
        CanResolve = state?.CanResolve ?? false;
        OnPropertyChanged(nameof(SendReplyText));
    }

    [RelayCommand]
    private void StartReply()
    {
        ErrorText = null;
        IsReplying = true;
    }

    [RelayCommand]
    private void CancelReply()
    {
        IsReplying = false;
        ReplyText = string.Empty;
        ErrorText = null;
    }

    [RelayCommand(CanExecute = nameof(CanSendReply))]
    private async Task SendReplyAsync()
    {
        ErrorText = null;
        IsSending = true;
        try
        {
            ErrorText = await _owner.ReplyAsync(this, ReplyText.Trim());
            if (ErrorText is null)
            {
                IsReplying = false;
                ReplyText = string.Empty;
            }
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task ToggleResolvedAsync() => ErrorText = await _owner.SetResolvedAsync(this, !IsResolved);

    private bool CanSendReply() => !IsSending && !string.IsNullOrWhiteSpace(ReplyText);

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
