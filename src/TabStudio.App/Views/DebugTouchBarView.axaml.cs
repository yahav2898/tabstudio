using Avalonia.Controls;

namespace TabStudio.App.Views;

/// <summary>The floating debug buttons over the editor while debugging: Continue or Break, the steps, and Stop.</summary>
public sealed partial class DebugTouchBarView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DebugTouchBarView()
    {
        InitializeComponent();
    }
}
