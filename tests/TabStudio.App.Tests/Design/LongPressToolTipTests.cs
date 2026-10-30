using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Controls;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// A long press names a button by touch, as a hover does with a mouse, and lifting the finger does not run it: a long
/// press on a notebook's trash icon, to learn what it was, deleted the cell.
/// </summary>
public sealed class LongPressToolTipTests
{
    [AvaloniaFact]
    public void A_long_press_shows_the_buttons_name_and_the_release_does_not_click()
    {
        TouchToolTips.Start();
        var clicks = 0;
        var button = new Button { Width = 44, Height = 44, Content = "Del", Command = new RelayCommand(() => clicks++) };
        ToolTip.SetTip(button, "Delete Cell");
        var window = new Window { Width = 200, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var center = button.TranslatePoint(new Point(22, 22), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        button.RaiseEvent(new HoldingRoutedEventArgs(HoldingState.Started, new Point(22, 22), PointerType.Touch));
        Assert.True(ToolTip.GetIsOpen(button));
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, clicks);
        Assert.False(ToolTip.GetIsOpen(button));

        // A tap still runs it.
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Assert.Equal(1, clicks);

        // A button whose long press opens its own menu keeps that.
        button.ContextFlyout = new MenuFlyout();
        Assert.False(TouchToolTips.Names(button));
        window.Close();
    }
}
