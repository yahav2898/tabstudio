using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// The review of one file of a pull request, in its diff tab: the threads, Claude's drafts and a comment being
/// written, each under its line of the diff; collapsed runs of unchanged lines open by themselves when a thread
/// is in them. Comments go only on lines GitHub accepts: the changed lines and the few around them.
/// </summary>
public sealed partial class FileReviewViewModel : ObservableObject, IReviewAnchorSource, IDisposable
{
    private readonly HashSet<object> _collapsed = [];
    private DocumentViewModel? _document;
    private NewCommentViewModel? _composer;
    private bool _rebuilding;
    private bool _disposed;

    internal FileReviewViewModel(PullRequestViewModel pullRequest, PullRequestFileViewModel file)
    {
        PullRequest = pullRequest;
        File = file;
        pullRequest.ReviewChanged += OnReviewChanged;
        pullRequest.PropertyChanged += OnPullRequestPropertyChanged;
    }

    /// <inheritdoc />
    public event EventHandler? AnchorsChanged;

    /// <summary>The pull request.</summary>
    public PullRequestViewModel PullRequest { get; }

    /// <summary>The file.</summary>
    public PullRequestFileViewModel File { get; }

    /// <summary>The file's path in the repository.</summary>
    public string Path => File.Path;

    /// <inheritdoc />
    public IReadOnlyList<ReviewAnchorViewModel> Anchors { get; private set; } = [];

    /// <inheritdoc />
    public bool CanComment => PullRequest.IsOpen;

    /// <summary>"2 conversations", for the review bar.</summary>
    public string ThreadsText => ReviewDisplay.Count(PullRequest.ThreadsFor(Path).Count(t => !t.IsPending), "conversation");

    /// <summary>"1 Claude draft", for the review bar, or <see langword="null"/> when Claude drafted nothing here.</summary>
    public string? DraftsText => PullRequest.DraftsFor(Path).Count() is var count and > 0 ? ReviewDisplay.Count(count, "Claude draft") : null;

    /// <summary>Why the last comment could not start where the user asked, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? NoticeText { get; private set; }

    /// <summary>Whether to show <see cref="NoticeText"/> instead of the hint.</summary>
    public bool HasNotice => NoticeText is not null;

    /// <summary>The hint under the review bar on how to comment.</summary>
    public string CommentHint => CanComment
        ? "Tap a line's margin, or select lines and press Ctrl+Shift+Enter, to comment."
        : "This pull request is closed, so it takes no new comments.";

    /// <summary>Connects the review to its diff's document, whose lines it places items under.</summary>
    /// <param name="document">The diff tab's document.</param>
    internal void Attach(DocumentViewModel document)
    {
        _document = document;
        document.ComparisonChanged += OnComparisonChanged;
        Rebuild();
    }

    /// <inheritdoc />
    public bool CanCommentOn(int line) => CanComment && Target(line) is not null;

    /// <inheritdoc />
    public string? StartComment(int firstLine, int lastLine)
    {
        if (!CanComment)
        {
            NoticeText = "This pull request is closed, so it takes no new comments.";
        }
        else if (Target(lastLine) is not { } end)
        {
            NoticeText = "GitHub takes comments only on the changed lines and the three lines around them.";
        }
        else
        {
            // A range must stay on one side of the diff; otherwise the comment goes on its last line.
            var start = firstLine < lastLine && Target(firstLine) is { } first && first.Side == end.Side && first.Line < end.Line ? first.Line : (int?)null;
            NoticeText = null;
            _composer = new NewCommentViewModel(this, end.Side, end.Line, start);
            Rebuild();
        }

        return NoticeText;
    }

    /// <inheritdoc />
    public void SetExpanded(ReviewAnchorViewModel anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        foreach (var item in anchor.Items)
        {
            if (anchor.IsExpanded)
            {
                _collapsed.Remove(item);
            }
            else
            {
                _collapsed.Add(item);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PullRequest.ReviewChanged -= OnReviewChanged;
        PullRequest.PropertyChanged -= OnPullRequestPropertyChanged;
        if (_document is not null)
        {
            _document.ComparisonChanged -= OnComparisonChanged;
        }
    }

    /// <summary>Adds the comment being written to the pending review.</summary>
    /// <returns>Why it could not be added, or <see langword="null"/>.</returns>
    internal async Task<string?> SubmitCommentAsync(NewCommentViewModel comment)
    {
        var error = await PullRequest.AddPendingCommentAsync(new NewReviewComment(Path, comment.Line, comment.Side, comment.Text.Trim(), comment.StartLine, comment.StartLine is null ? null : comment.Side));
        if (error is null && ReferenceEquals(_composer, comment))
        {
            _composer = null;
            Rebuild();
        }

        return error;
    }

    /// <summary>Closes the comment being written without adding it.</summary>
    internal void CancelComment(NewCommentViewModel comment)
    {
        if (ReferenceEquals(_composer, comment))
        {
            _composer = null;
            Rebuild();
        }
    }

    /// <summary>Opens the pull request's Finish Review panel.</summary>
    [RelayCommand]
    private void FinishReview() => PullRequest.ShowFinishReview();

    /// <summary>Shows the pull request's document.</summary>
    [RelayCommand]
    private void ShowPullRequest() => PullRequest.Show();

    /// <summary>The line and side a comment on a line of the diff goes on, when GitHub accepts comments there.</summary>
    private (DiffSide Side, int Line)? Target(int documentLine)
    {
        if (DiffLines() is not { } lines || documentLine < 1 || documentLine > lines.Count)
        {
            return null;
        }

        var line = lines[documentLine - 1];
        var patch = File.Patch;
        var known = patch.Lines.Count > 0;
        return line switch
        {
            { Kind: DiffLineKind.Removed, OldNumber: { } old } when !known || patch.Contains(DiffSide.Left, old) => (DiffSide.Left, old),
            { Kind: DiffLineKind.Added, NewNumber: { } added } when !known || patch.Contains(DiffSide.Right, added) => (DiffSide.Right, added),
            { Kind: DiffLineKind.Unchanged, NewNumber: { } unchanged } when known && patch.Contains(DiffSide.Right, unchanged) => (DiffSide.Right, unchanged),
            _ => null,
        };
    }

    /// <summary>Places the threads, drafts and the comment being written under their lines of the diff.</summary>
    private void Rebuild()
    {
        if (_disposed || _document?.Comparison is null)
        {
            return;
        }

        var items = new List<(object Item, DiffSide Side, int? Line)>();
        items.AddRange(PullRequest.ThreadsFor(Path).Select(t => ((object)t, t.Side, t.Line)));
        items.AddRange(PullRequest.DraftsFor(Path).Select(d => ((object)d, d.Side, (int?)d.Line)));
        if (_composer is { } composer)
        {
            items.Add((composer, composer.Side, composer.Line));
        }

        _rebuilding = true;
        try
        {
            // Opening a collapsed run moves the lines after it, so every item is revealed before any is placed.
            foreach (var (_, side, line) in items)
            {
                Reveal(side, line);
            }
        }
        finally
        {
            _rebuilding = false;
        }

        var anchors = items
            .Select(i => (i.Item, Index: Locate(i.Side, i.Line)))
            .GroupBy(i => i.Index)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var lineItems = g.Select(i => i.Item).OrderBy(Order).ToList();
                var collapsed = lineItems.All(i => _collapsed.Contains(i) || i is ReviewThreadViewModel { IsResolved: true });
                var writing = _composer is not null && lineItems.Contains(_composer);
                return new ReviewAnchorViewModel(g.Key + 1, lineItems, !collapsed || writing);
            })
            .ToList();

        Anchors = anchors;
        OnPropertyChanged(nameof(ThreadsText));
        OnPropertyChanged(nameof(DraftsText));
        AnchorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Opens the collapsed run of unchanged lines that hides a line, if one does.</summary>
    private void Reveal(DiffSide side, int? line)
    {
        if (line is not { } number || _document is not { } document)
        {
            return;
        }

        for (var attempt = 0; attempt < 4 && DiffLines() is { } lines; attempt++)
        {
            if (IndexOf(lines, side, number) is not null)
            {
                return;
            }

            var hidden = -1;
            for (var i = 0; i < lines.Count && hidden < 0; i++)
            {
                if (lines[i].Kind == DiffLineKind.Collapsed && NumberBefore(lines, i, side) < number && number < NumberAfter(lines, i, side))
                {
                    hidden = i;
                }
            }

            if (hidden < 0)
            {
                return;
            }

            document.ExpandComparison(hidden);
        }
    }

    /// <summary>The 0-based diff line an item goes under: its own line, else the nearest line before it on its side, else the first.</summary>
    private int Locate(DiffSide side, int? line)
    {
        if (line is not { } number || DiffLines() is not { Count: > 0 } lines)
        {
            return 0;
        }

        if (IndexOf(lines, side, number) is { } exact)
        {
            return exact;
        }

        // An outdated comment's line may be gone: it goes under the nearest earlier line.
        var nearest = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (NumberOf(lines[i], side) is { } n && n <= number)
            {
                nearest = i;
            }
        }

        return nearest;
    }

    private IReadOnlyList<DiffLine>? DiffLines() => _document?.Comparison?.Diff.Lines;

    private static int? IndexOf(IReadOnlyList<DiffLine> lines, DiffSide side, int number)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (NumberOf(lines[i], side) == number)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>A diff line's number on a side: removed lines only count on the left, added ones only on the right.</summary>
    private static int? NumberOf(DiffLine line, DiffSide side) => line.Kind switch
    {
        DiffLineKind.Removed => side == DiffSide.Left ? line.OldNumber : null,
        DiffLineKind.Added => side == DiffSide.Right ? line.NewNumber : null,
        DiffLineKind.Unchanged => side == DiffSide.Left ? line.OldNumber : line.NewNumber,
        _ => null,
    };

    private static int NumberBefore(IReadOnlyList<DiffLine> lines, int index, DiffSide side)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (NumberOf(lines[i], side) is { } number)
            {
                return number;
            }
        }

        return 0;
    }

    private static int NumberAfter(IReadOnlyList<DiffLine> lines, int index, DiffSide side)
    {
        for (var i = index + 1; i < lines.Count; i++)
        {
            if (NumberOf(lines[i], side) is { } number)
            {
                return number;
            }
        }

        return int.MaxValue;
    }

    /// <summary>Threads first, then Claude's drafts, then the comment being written, as they read under a line.</summary>
    private static int Order(object item) => item switch
    {
        ReviewThreadViewModel => 0,
        DraftCommentViewModel => 1,
        _ => 2,
    };

    private void OnReviewChanged(object? sender, EventArgs e) => Rebuild();

    private void OnComparisonChanged(object? sender, EventArgs e)
    {
        if (!_rebuilding)
        {
            Rebuild();
        }
    }

    private void OnPullRequestPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PullRequestViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(CanComment));
            OnPropertyChanged(nameof(CommentHint));
        }
    }
}
