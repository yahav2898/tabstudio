using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Controls;

namespace TabStudio.App.Tests.Design;

/// <summary>The adaptive bar as a control: it measures its items, shrinks them, and lists what overflowed in More.</summary>
public sealed class AdaptiveBarTests
{
    [AvaloniaFact]
    public void A_narrow_toolbar_shrinks_items_to_icons_and_moves_the_rest_into_More()
    {
        var bar = DesignSamples.Toolbar();
        var window = new Window { Width = 1280, Height = 60, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var save = bar.Children.OfType<AdaptiveBarItem>().Single(item => item.Label == "Run on In-process");
        Assert.Equal(AdaptiveMode.Full, bar.ModeOf(save));
        Assert.False(bar.ShowsMore);

        window.Width = 520;
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(bar.Overflowed);
        Assert.True(bar.ShowsMore);
        Assert.True(bar.Bounds.Width <= 520);
        var overflowed = bar.Overflowed.OfType<AdaptiveBarItem>().ToList();
        Assert.DoesNotContain(overflowed, item => item.Label == "Save");
        Assert.Contains(overflowed, item => item.Label == "Format Document");
        window.Close();
    }

    [AvaloniaFact]
    public void A_compact_item_keeps_its_name_in_the_tooltip()
    {
        var item = new AdaptiveBarItem { Icon = DesignSamples.Icon("Icon.Save"), Label = "Save All", Gesture = "Ctrl+Shift+S" };

        Assert.Equal("Save All (Ctrl+Shift+S)", ToolTip.GetTip(item));
        Assert.True(item.CanCompact);
        Assert.False(new AdaptiveBarItem { Label = "Ready" }.CanCompact);
    }
}
