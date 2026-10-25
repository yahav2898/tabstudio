using Avalonia.Controls;

namespace TabStudio.App.Views.Actions;

/// <summary>
/// The status bar's CI item: the workspace commit's checks at a glance (green passed, red failed, amber running
/// with how many are done), with the details in its tooltip. Tapping it opens GitHub Actions on the commit.
/// </summary>
public sealed partial class CiStatusBarItem : UserControl
{
    /// <summary>Creates the item.</summary>
    public CiStatusBarItem() => InitializeComponent();
}
