using Avalonia.Controls;

namespace TabStudio.App.Views;

/// <summary>
/// The popup of a Live Unit Testing marker: what the marker means and the tests that run its line. Tapping a test
/// opens it; its play button runs it again.
/// </summary>
public sealed partial class LiveTestPopupView : UserControl
{
    /// <summary>Creates the view.</summary>
    public LiveTestPopupView()
    {
        InitializeComponent();
    }
}
