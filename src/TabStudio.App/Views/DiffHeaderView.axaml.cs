using Avalonia.Controls;

namespace TabStudio.App.Views;

/// <summary>The header of a comparison tab: the file, the versions compared, and the added and removed line counts.</summary>
public sealed partial class DiffHeaderView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DiffHeaderView()
    {
        InitializeComponent();
    }
}
