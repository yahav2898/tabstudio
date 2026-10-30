using Avalonia.Controls;

namespace TabStudio.App.Views.Actions;

/// <summary>
/// The GitHub Actions window's dialog, drawn over the IDE: Run workflow, with the branch to run on and a control
/// for each of the workflow's inputs (text, number, toggle, choice list, environment).
/// </summary>
public sealed partial class ActionsDialogsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ActionsDialogsView()
    {
        InitializeComponent();
        TabStudio.App.Controls.DialogDismiss.Attach(this, DispatchScrim, () => (DataContext as TabStudio.App.ViewModels.Actions.ActionsViewModel)?.CloseDispatchFormCommand.Execute(null));
    }
}
