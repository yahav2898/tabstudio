using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.Views.PullRequests;

/// <summary>
/// The review items under one line: conversations with their reply boxes, Claude's drafts and a comment being
/// written. In a comment box, Ctrl+Enter sends and Esc cancels.
/// </summary>
public sealed partial class ReviewAnchorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ReviewAnchorView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not TextBox { DataContext: { } item })
        {
            return;
        }

        var send = e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control;
        var cancel = e.Key == Key.Escape;
        switch (item)
        {
            case NewCommentViewModel comment when send && comment.SubmitCommand.CanExecute(null):
                _ = comment.SubmitCommand.ExecuteAsync(null);
                break;
            case NewCommentViewModel comment when cancel:
                comment.CancelCommand.Execute(null);
                break;
            case ReviewThreadViewModel thread when send && thread.SendReplyCommand.CanExecute(null):
                _ = thread.SendReplyCommand.ExecuteAsync(null);
                break;
            case ReviewThreadViewModel thread when cancel:
                thread.CancelReplyCommand.Execute(null);
                break;
            case DraftCommentViewModel draft when send || cancel:
                draft.FinishEditingCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
