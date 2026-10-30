using Avalonia.Controls;
using TabStudio.App.Controls;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The Git Changes window's dialogs: the branch picker, the pull request form and the history list.</summary>
public sealed partial class GitDialogsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public GitDialogsView()
    {
        InitializeComponent();
        DialogDismiss.Attach(this, BranchPickerScrim, () => ViewModel?.CloseBranchPickerCommand.Execute(null));
        DialogDismiss.Attach(this, PullRequestFormScrim, () => ViewModel?.ClosePullRequestFormCommand.Execute(null));
        DialogDismiss.Attach(this, HistoryScrim, () => ViewModel?.CloseHistoryCommand.Execute(null));
    }

    private GitChangesViewModel? ViewModel => DataContext as GitChangesViewModel;
}
