using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.ViewModels.Shell;

namespace TabStudio.App.Tests.Design;

/// <summary>The activity bar: choosing a tool window, folding the side bar, badges, and fitting the height.</summary>
public sealed class ActivityBarTests
{
    [Fact]
    public void Tapping_an_item_shows_its_tool_window_and_tapping_it_again_folds_the_side_bar()
    {
        var bar = new ActivityBarViewModel();
        var explorer = bar.Add(new ActivityItem("SolutionExplorer", "Solution Explorer", null));
        var tests = bar.Add(new ActivityItem("TestExplorer", "Test Explorer", null, "Ctrl+E, T"));
        var shown = new List<string>();
        bar.Activated += (_, item) => shown.Add(item.Id);

        bar.ActivateCommand.Execute(tests);
        Assert.Same(tests, bar.SelectedItem);
        Assert.True(tests.IsSelected);
        Assert.False(explorer.IsSelected);
        Assert.True(bar.IsSideBarOpen);

        bar.ActivateCommand.Execute(tests);
        Assert.False(bar.IsSideBarOpen);

        bar.ActivateCommand.Execute(tests);
        Assert.True(bar.IsSideBarOpen);
        Assert.Equal(new[] { "TestExplorer", "TestExplorer" }, shown);
    }

    [Fact]
    public void An_unavailable_tool_window_cannot_be_chosen()
    {
        var bar = new ActivityBarViewModel();
        var preview = bar.Add(new ActivityItem("WebPreview", "Web Preview", null) { IsAvailable = false });

        bar.ActivateCommand.Execute(preview);

        Assert.Null(bar.SelectedItem);
        Assert.Throws<ArgumentException>(() => bar.Add(new ActivityItem("WebPreview", "Again", null)));
    }

    [Fact]
    public void Badges_count_up_to_99_and_say_what_they_mean()
    {
        var item = new ActivityItem("TestExplorer", "Test Explorer", null, "Ctrl+E, T");

        item.ShowCount(3, ActivityTone.Error, "3 tests failed");
        Assert.Equal(("3", "Test Explorer (Ctrl+E, T)\n3 tests failed"), (item.BadgeText, item.ToolTip));

        item.ShowCount(120, ActivityTone.Error, "120 tests failed");
        Assert.Equal("99+", item.BadgeText);

        item.ShowCount(0, ActivityTone.Error, "No failures");
        Assert.False(item.HasBadge);
        Assert.Equal("Test Explorer (Ctrl+E, T)", item.ToolTip);

        item.ShowActivity("Running 42 tests");
        Assert.Equal((ActivityBadgeKind.Activity, ActivityTone.Running), (item.BadgeKind, item.BadgeTone));
    }

    [AvaloniaFact]
    public void Items_that_do_not_fit_move_into_More_but_the_chosen_one_stays()
    {
        var bar = DesignSamples.ActivityBar();
        bar.Select("Agent");
        var activity = new ActivityBar { Bar = bar };
        var window = new Window { Width = 48, Height = 300, Content = activity };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 300 pixels hold six 48-pixel buttons: two at the bottom, then three at the top and More; Claude stays.
        Assert.Equal(new[] { "Actions", "PullRequests", "TestExplorer", "AppPreview" }, activity.Overflowed.Select(item => item.Id));
        Assert.True(activity.ShowsMore);

        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(activity.Overflowed);
        Assert.False(activity.ShowsMore);
        window.Close();
    }

    [AvaloniaFact]
    public void Items_come_and_go_with_the_tool_windows()
    {
        var bar = DesignSamples.ActivityBar();
        var activity = new ActivityBar { Bar = bar };
        var window = new Window { Width = 48, Height = 800, Content = activity };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var buttons = Shown(activity);

        bar.Find("WebPreview")!.IsAvailable = true;
        bar.Add(new ActivityItem("Swarm", "Swarm", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(buttons + 2, Shown(activity));
        window.Close();
    }

    /// <summary>How many buttons the bar shows: what it does not show is parked outside it.</summary>
    private static int Shown(ActivityBar activity) =>
        activity.GetVisualChildren().OfType<Button>().Count(button => button.Bounds.X >= 0 && button.Bounds.Y >= 0);
}
