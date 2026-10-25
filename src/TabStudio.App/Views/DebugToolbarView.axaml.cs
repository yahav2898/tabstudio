using Avalonia.Controls;

namespace TabStudio.App.Views;

/// <summary>The toolbar's debugging buttons: Start Without Debugging, and while debugging Break All, Stop, Restart and the steps.</summary>
public sealed partial class DebugToolbarView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DebugToolbarView()
    {
        InitializeComponent();
    }
}
