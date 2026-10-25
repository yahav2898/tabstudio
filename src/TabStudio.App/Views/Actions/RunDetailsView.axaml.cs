using Avalonia.Controls;

namespace TabStudio.App.Views.Actions;

/// <summary>
/// A run's details in the GitHub Actions window: the run, its jobs with their steps, and the buttons that re-run,
/// cancel or open it on GitHub, enabled by the run's state.
/// </summary>
public sealed partial class RunDetailsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public RunDetailsView() => InitializeComponent();
}
