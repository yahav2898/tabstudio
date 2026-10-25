using Avalonia.Controls;
using Avalonia.Styling;
using TabStudio.App.ViewModels.Swarm;

namespace TabStudio.App.Views.Swarm;

/// <summary>
/// The device's page in a swarm: the tablet's app full screen under a small overlay. Tells the swarm the size the app
/// gets, for the tablet's Swarm window and the layout checks.
/// </summary>
public sealed partial class SwarmDevicePageView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SwarmDevicePageView()
    {
        InitializeComponent();
        Screen.SizeChanged += (_, _) => ReportSize();
        DataContextChanged += (_, _) => ReportSize();
        ActualThemeVariantChanged += (_, _) => ReportSize();
    }

    private void ReportSize()
    {
        if (DataContext is SwarmDevicePageViewModel page && Screen.Bounds is { Width: > 0, Height: > 0 } bounds)
        {
            page.ReportSize(bounds.Width, bounds.Height, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1, ActualThemeVariant != ThemeVariant.Light);
        }
    }
}
