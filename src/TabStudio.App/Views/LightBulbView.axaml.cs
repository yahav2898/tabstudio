using Avalonia.Controls;
using TabStudio.App.Controls;

namespace TabStudio.App.Views;

/// <summary>
/// The light bulb: a yellow bulb when quick fixes are available, a screwdriver when there are only refactorings.
/// </summary>
public sealed partial class LightBulbView : UserControl
{
    /// <summary>Creates the view.</summary>
    public LightBulbView()
    {
        InitializeComponent();
        Glyph.Data = IntelliSenseIcons.LightBulb;
    }

    /// <summary>The control to open the Quick Actions menu at.</summary>
    public Control Anchor => Bulb;

    /// <summary>Shows the bulb for fixes, or the screwdriver for refactorings only.</summary>
    /// <param name="hasFixes">Whether any action fixes a diagnostic.</param>
    public void ShowFor(bool hasFixes)
    {
        Glyph.Data = hasFixes ? IntelliSenseIcons.LightBulb : IntelliSenseIcons.Screwdriver;
        ToolTip.SetTip(Bulb, hasFixes ? "Show potential fixes (Ctrl+.)" : "Quick Actions and Refactorings (Ctrl+.)");
    }
}
