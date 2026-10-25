using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.PullRequests;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Views.PullRequests;

/// <summary>
/// A pull request's document. Esc closes the Finish Review panel; Ctrl+Enter sends the conversation's comment,
/// or the review's summary as a comment.
/// </summary>
public sealed partial class PullRequestView : UserControl
{
    /// <summary>Creates the view; its data context is a <see cref="PullRequestViewModel"/>.</summary>
    public PullRequestView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private PullRequestViewModel? PullRequest => DataContext as PullRequestViewModel;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (PullRequest is not { } pullRequest)
        {
            return;
        }

        if (e.Key == Key.Escape && pullRequest.IsFinishReviewOpen)
        {
            pullRequest.CloseFinishReviewCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            if (pullRequest.IsFinishReviewOpen && ReviewSummaryBox.IsKeyboardFocusWithin)
            {
                _ = pullRequest.SubmitReviewCommand.ExecuteAsync(ReviewEvent.Comment);
                e.Handled = true;
            }
            else if (NewCommentBox.IsKeyboardFocusWithin && pullRequest.AddCommentCommand.CanExecute(null))
            {
                _ = pullRequest.AddCommentCommand.ExecuteAsync(null);
                e.Handled = true;
            }
        }
    }
}
