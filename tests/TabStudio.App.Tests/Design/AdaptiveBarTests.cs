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
    public void Folded_items_never_overlap_their_neighbours()
    {
        // Folded tool buttons were packed 36 pixels apart while ToolButton draws them 40 wide, so a tap near the edge
        // of one landed on the next.
        // At every width from a phone's to a tablet's, whatever is folded to its icon.
        var bar = DesignSamples.Toolbar();
        var window = new Window { Width = 1280, Height = 60, Content = bar };
        window.Show();
        var folded = false;
        for (var width = 1280; width >= 320; width -= 20)
        {
            window.Width = width;
            Dispatcher.UIThread.RunJobs();
            var shown = bar.Children.OfType<AdaptiveBarItem>().Where(item => bar.ModeOf(item) is AdaptiveMode.Full or AdaptiveMode.Compact).OrderBy(item => item.Bounds.X).ToList();
            folded |= shown.Any(item => bar.ModeOf(item) == AdaptiveMode.Compact);
            for (var i = 1; i < shown.Count; i++)
            {
                Assert.True(shown[i - 1].Bounds.Right <= shown[i].Bounds.X + 0.5, $"At {width}: {shown[i - 1].Label} ends at {shown[i - 1].Bounds.Right}, over {shown[i].Label} at {shown[i].Bounds.X}.");
            }
        }

        Assert.True(folded, "No width folded an item to its icon.");
        window.Close();
    }

    [AvaloniaFact]
    public void A_long_status_message_trims_instead_of_pushing_the_caret_position_out_of_a_phone()
    {
        // On a 412-pixel phone a long message (or a long operation's text) was as long as it wanted, and the items
        // after it, the caret position among them, were laid out past the screen's edge.
        var message = new TextBlock
        {
            Text = "Restoring NuGet packages for HelloTablet, Greetings and HelloTablet.Tests from the cache",
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(message, 1);
        var status = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new Border { Width = 13, Height = 13 }, message } };
        AdaptiveBar.SetMinLength(status, 48);
        var caret = new AdaptiveBarItem { Label = "Ln 120, Col 45", IsPinned = true, Priority = 100 };
        var bar = new AdaptiveBar { Spacing = 6, Children = { status, new AdaptiveBarSpacer(), caret } };
        var window = new Window { Width = 300, Height = 40, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(AdaptiveMode.Full, bar.ModeOf(caret));
        Assert.True(caret.Bounds.Right <= 300.5, $"The caret position ends at {caret.Bounds.Right}, outside the bar's 300 pixels.");
        Assert.True(status.Bounds.Width >= 48);
        // Trimmed text is as wide as what it shows, which can be a little less than its room.
        Assert.True(
            message.DesiredSize.Width <= message.Bounds.Width + 0.5 && message.Bounds.Right <= status.Bounds.Width + 0.5,
            $"The message wants {message.DesiredSize.Width} pixels and is given {message.Bounds}, in a status of {status.Bounds.Width}.");
        Assert.True(status.Bounds.Width < 300 - caret.Bounds.Width);

        // With room, the whole message shows again.
        window.Width = 1400;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(AdaptiveMode.Full, bar.ModeOf(status));
        Assert.True(message.Bounds.Width > 400, $"The message is {message.Bounds.Width} pixels wide with room to spare.");
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
