using Avalonia.Controls;

namespace TabStudio.App.Views.PullRequests;

/// <summary>
/// The review bar of a pull request file's diff: the pull request, the file's conversations and Claude's drafts,
/// Add Comment and Finish Review.
/// </summary>
public sealed partial class ReviewDiffBar : UserControl
{
    /// <summary>Creates the bar.</summary>
    public ReviewDiffBar()
    {
        InitializeComponent();
        AddCommentButton.Click += (_, _) => AddCommentRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when Add Comment is pressed; the diff starts a comment on the selected lines.</summary>
    public event EventHandler? AddCommentRequested;
}
