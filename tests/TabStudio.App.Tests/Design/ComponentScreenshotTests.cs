using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TabStudio.App.Controls;
using TabStudio.App.Services;
using TabStudio.App.Services.Search;
using TabStudio.App.Tests.Theming;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.Views.Search;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// Each new shell piece alone in a window, in the dark and the light theme, for the design spec (docs/design/) and for
/// looking at: the activity bar, the search, the adaptive toolbar and status bar, and the panel states.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class ComponentScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [AvaloniaFact]
    public void The_activity_bar_beside_its_side_bar()
    {
        var bar = DesignSamples.ActivityBar();
        var activity = new ActivityBar { Bar = bar };
        var window = Show(SideBarWithActivityBar(activity, "Solution Explorer"), 420, 720);

        Shoot(window, "design-01-activity-bar");

        var selected = bar.Find("SolutionExplorer")!;
        Assert.True(selected.IsSelected);
        Assert.Empty(activity.Overflowed);
        Assert.Equal("Test Explorer (Ctrl+E, T)\n1 test failed", bar.Find("TestExplorer")!.ToolTip);
    }

    [AvaloniaFact]
    public void A_short_activity_bar_moves_what_does_not_fit_into_More()
    {
        var bar = DesignSamples.ActivityBar();
        bar.Select("Agent");
        var activity = new ActivityBar { Bar = bar };
        var window = Show(SideBarWithActivityBar(activity, "Claude"), 420, 360);

        Shoot(window, "design-02-activity-bar-overflow");

        Assert.NotEmpty(activity.Overflowed);
        Assert.DoesNotContain(bar.Find("Agent")!, activity.Overflowed);
        Assert.True(activity.ShowsMore);
    }

    [AvaloniaFact]
    public async Task The_search_finds_everything_and_shows_the_best_match_first()
    {
        var search = DesignSamples.Search(new AppPaths(_temp.Path));
        var window = Show(SearchHost(search), 960, 720);
        search.OpenCommand.Execute(null);
        search.Query = "greet";
        await search.Completion;

        Shoot(window, "design-03-search");

        var best = Assert.IsType<SearchResultRow>(search.Rows[0]);
        Assert.True(best.IsBestMatch);
        Assert.Contains(search.Rows.OfType<SearchResultRow>(), row => row.Item.Kind == SearchKind.File && row.Title == "Greeter.cs");
    }

    [AvaloniaFact]
    public async Task The_search_shows_commands_with_their_key_gestures()
    {
        var search = DesignSamples.Search(new AppPaths(_temp.Path));
        var window = Show(SearchHost(search), 960, 720);
        search.OpenCommand.Execute(null);
        search.Query = ">b";
        await search.Completion;

        Shoot(window, "design-04-search-commands");

        var build = search.Rows.OfType<SearchResultRow>().First(row => row.Title == "Build Solution");
        Assert.Equal(new[] { "Ctrl", "Shift", "B" }, build.Keys);
    }

    [AvaloniaFact]
    public async Task The_search_says_when_nothing_matches()
    {
        var search = DesignSamples.Search(new AppPaths(_temp.Path));
        var window = Show(SearchHost(search), 960, 520);
        search.OpenCommand.Execute(null);
        search.Query = "qqqz";
        await search.Completion;

        Shoot(window, "design-05-search-empty");

        Assert.True(search.IsEmpty);
        Assert.Equal("No results for “qqqz”", search.EmptyTitle);
    }

    [AvaloniaFact]
    public void The_toolbar_shrinks_to_icons_and_then_into_More()
    {
        var widths = new[] { 1280.0, 900, 640, 420 };
        var bars = widths.Select(_ => DesignSamples.Toolbar()).ToList();
        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(16) };
        for (var i = 0; i < widths.Length; i++)
        {
            stack.Children.Add(Labeled($"{widths[i]} pixels", Strip(bars[i], widths[i] - 32, "Brush.Toolbar", 46)));
        }

        var window = Show(stack, 1280, 330);
        Shoot(window, "design-06-adaptive-toolbar");

        Assert.Empty(bars[0].Overflowed);
        Assert.NotEmpty(bars[3].Overflowed);
        var run = bars[3].Children.OfType<StackPanel>().Single();
        Assert.Equal(AdaptiveMode.Full, bars[3].ModeOf(run));
    }

    [AvaloniaFact]
    public void The_status_bar_keeps_the_state_and_folds_the_rest()
    {
        var widths = new[] { 1280.0, 800, 480 };
        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(16) };
        var bars = new List<AdaptiveBar>();
        foreach (var width in widths)
        {
            var bar = DesignSamples.StatusBar();
            bars.Add(bar);
            stack.Children.Add(Labeled($"{width} pixels", Strip(bar, width - 32, "Brush.Accent", 28)));
        }

        var window = Show(stack, 1280, 260);
        Shoot(window, "design-07-adaptive-status-bar");

        Assert.Empty(bars[0].Overflowed);
        Assert.NotEmpty(bars[2].Overflowed);
        var caret = bars[2].Children.OfType<AdaptiveBarItem>().Single(item => item.Label == "Ln 16, Col 9");
        Assert.Equal((AdaptiveMode.Full, AdaptiveMode.Full), (bars[2].ModeOf(bars[2].Children[0]), bars[2].ModeOf(caret)));
        Assert.True(caret.Bounds.X > bars[2].Bounds.Width / 2, "The caret sits at the bar's end.");
    }

    [AvaloniaFact]
    public void Every_panel_has_the_same_header_and_states()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), Margin = new Thickness(16) };
        grid.Children.Add(Panel(0, "Test Explorer", "HelloTablet", "Icon.Flask", new EmptyState
        {
            Icon = DesignSamples.Icon("Icon.Flask"),
            Title = "No tests yet",
            Message = "Tests show here once the solution builds. Build it, or add a test project.",
            ActionText = "Build Solution",
            ActionCommand = DesignSamples.Nothing,
        }));
        grid.Children.Add(Panel(1, "Pull Requests", "tester/hello", "Icon.PullRequest", new LoadingState { Message = "Loading pull requests…", Detail = "tester/hello on GitHub" }));
        grid.Children.Add(Panel(2, "GitHub Actions", "tester/hello", "Icon.Actions", new ErrorState
        {
            Title = "GitHub did not answer",
            Message = "Check the connection, then try again. Runs from before are still in the log.",
            Details = "HttpRequestException: The SSL connection could not be established (api.github.com:443).",
            RetryCommand = DesignSamples.Nothing,
            FixText = "Sign In Again",
            FixCommand = DesignSamples.Nothing,
        }));
        var list = new StackPanel();
        foreach (var (name, detail) in new[] { ("Greeter.cs", "src/Greetings"), ("Program.cs", "src/HelloTablet"), ("README.md", string.Empty) })
        {
            list.Children.Add(new StackPanel
            {
                Margin = new Thickness(12, 6),
                Children =
                {
                    new TextBlock { Text = name, FontSize = 13 },
                    new TextBlock { Text = detail, FontSize = 12, Classes = { "muted" } },
                },
            });
        }

        list.Children.Add(new LoadingState { IsInline = true, Message = "Loading history…" });
        grid.Children.Add(Panel(3, "Git Changes", "main", "Icon.Branch", list));

        var window = Show(grid, 1280, 460);
        Shoot(window, "design-08-panel-states");

        Assert.Equal(4, grid.Children.Count);
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>A side bar with a header and a few rows, and the activity bar on its outer edge.</summary>
    internal static Control SideBarWithActivityBar(ActivityBar activity, string title)
    {
        var panel = new DockPanel();
        var header = new PanelHeader
        {
            Title = title,
            Subtitle = "HelloTablet",
            Actions = HeaderActions(),
        };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        var rows = new StackPanel { Margin = new Thickness(0, 6) };
        foreach (var (indent, icon, name) in new[] { (0, "Icon.Solution", "HelloTablet"), (1, "Icon.Folder", "src"), (2, "Icon.Globe", "HelloTablet"), (3, "Icon.File", "Program.cs"), (2, "Icon.Cube", "Greetings"), (3, "Icon.File", "Greeter.cs") })
        {
            rows.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Height = 32,
                Margin = new Thickness(12 + (indent * 16), 0, 0, 0),
                Children =
                {
                    new StrokeIcon { Data = DesignSamples.Icon(icon), Size = 16 },
                    new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center },
                },
            });
        }

        panel.Children.Add(rows);
        var sideBar = new Border { Child = panel };
        sideBar.Bind(Border.BackgroundProperty, sideBar.GetResourceObservable("Brush.Panel"));
        var edge = new Border { Child = activity, BorderThickness = new Thickness(1, 0, 0, 0) };
        edge.Bind(Border.BackgroundProperty, edge.GetResourceObservable("Brush.ActivityBar"));
        edge.Bind(Border.BorderBrushProperty, edge.GetResourceObservable("Brush.Border"));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,48") };
        grid.Children.Add(sideBar);
        Grid.SetColumn(edge, 1);
        grid.Children.Add(edge);
        return grid;
    }

    /// <summary>A header's actions: icons that fold into More when the panel is narrow.</summary>
    internal static AdaptiveBar HeaderActions()
    {
        var actions = new AdaptiveBar { Spacing = 0 };
        foreach (var (icon, label) in new[] { ("Icon.Refresh", "Refresh"), ("Icon.Collapse", "Collapse All"), ("Icon.Gear", "Settings") })
        {
            actions.Children.Add(new AdaptiveBarItem { Icon = DesignSamples.Icon(icon), Label = label, ShowsLabel = false, IconSize = 16, Command = DesignSamples.Nothing });
        }

        return actions;
    }

    /// <summary>A window's content with the search open over a quiet background.</summary>
    private static Panel SearchHost(UnifiedSearchViewModel search)
    {
        var host = new Panel();
        host.Children.Add(new UnifiedSearchView { DataContext = search });
        return host;
    }

    private static StackPanel Labeled(string label, Control content) => new()
    {
        Spacing = 4,
        Children = { new TextBlock { Text = label, FontSize = 11.5, Classes = { "muted" } }, content },
    };

    private static Border Strip(Control content, double width, string background, double height)
    {
        var strip = new Border { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 0), Child = content };
        strip.Bind(Border.BackgroundProperty, strip.GetResourceObservable(background));
        return strip;
    }

    private static Border Panel(int column, string title, string subtitle, string icon, Control content)
    {
        var dock = new DockPanel();
        var header = new PanelHeader { Title = title, Subtitle = subtitle, Icon = DesignSamples.Icon(icon), Actions = HeaderActions() };
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(content);
        var panel = new Border { Child = dock, Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0), CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderThickness = new Thickness(1) };
        panel.Bind(Border.BackgroundProperty, panel.GetResourceObservable("Brush.Panel"));
        panel.Bind(Border.BorderBrushProperty, panel.GetResourceObservable("Brush.Border"));
        Grid.SetColumn(panel, column);
        return panel;
    }

    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("Brush.Window"));
        window.Bind(Window.ForegroundProperty, window.GetResourceObservable("Brush.Text"));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Saves the window in the dark theme and then the light one, and checks that its text is centered.</summary>
    private static void Shoot(Window window, string name)
    {
        UiTestHelpers.SaveScreenshot(window, name + "-dark");
        using (ThemeScope.Light())
        {
            UiTestHelpers.SaveScreenshot(window, name + "-light");
        }

        Dispatcher.UIThread.RunJobs();
        StretchedText.AssertNone(window);
    }
}
