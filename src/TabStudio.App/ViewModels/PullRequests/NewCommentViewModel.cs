using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// A comment being written on a line, or a range of lines, of a file's diff. Added comments go into the
/// signed-in account's pending review, which is kept on GitHub until the review is finished.
/// </summary>
public sealed partial class NewCommentViewModel : ObservableObject
{
    private readonly FileReviewViewModel _owner;

    internal NewCommentViewModel(FileReviewViewModel owner, DiffSide side, int line, int? startLine)
    {
        _owner = owner;
        Side = side;
        Line = line;
        StartLine = startLine == line ? null : startLine;
    }

    /// <summary>Raised when the comment box should take the keyboard focus, as it does when it opens.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>Which version of the file the lines count in.</summary>
    public DiffSide Side { get; }

    /// <summary>The line, or the last line of the range.</summary>
    public int Line { get; }

    /// <summary>The first line of a range, or <see langword="null"/> for one line.</summary>
    public int? StartLine { get; }

    /// <summary>"Comment on line 12" or "Comment on lines 10–12 (removed)".</summary>
    public string LocationText => "Comment on " + ReviewDisplay.Lines(Line, StartLine) + (Side == DiffSide.Left ? " (removed)" : string.Empty);

    /// <summary>The comment.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Whether the comment is being added.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial bool IsSending { get; private set; }

    /// <summary>Why the comment could not be added.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>The add button's label: the first comment starts the review, as on GitHub.</summary>
    public string SubmitText => _owner.PullRequest.HasPendingReview ? "Add Review Comment" : "Start a Review";

    /// <summary>Asks the view to put the keyboard focus in the comment box.</summary>
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ErrorText = null;
        IsSending = true;
        try
        {
            ErrorText = await _owner.SubmitCommentAsync(this);
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _owner.CancelComment(this);

    private bool CanSubmit() => !IsSending && !string.IsNullOrWhiteSpace(Text);
}
