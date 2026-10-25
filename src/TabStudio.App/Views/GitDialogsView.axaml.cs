using Avalonia.Controls;

namespace TabStudio.App.Views;

/// <summary>The Git Changes window's dialogs: the branch picker, the pull request form and the history list.</summary>
public sealed partial class GitDialogsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public GitDialogsView()
    {
        InitializeComponent();
    }
}
