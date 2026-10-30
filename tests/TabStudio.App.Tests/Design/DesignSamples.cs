using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Controls;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Services.Search;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.ViewModels.Shell;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Tests.Design;

/// <summary>The design components filled with what the IDE would put in them, for screenshots, mockups and tests.</summary>
internal static class DesignSamples
{
    /// <summary>A command that does nothing, for buttons in a picture.</summary>
    public static IRelayCommand Nothing { get; } = new RelayCommand(() => { });

    /// <summary>An icon from the app's resources.</summary>
    public static Geometry Icon(string key) =>
        Application.Current!.TryGetResource(key, null, out var icon) && icon is Geometry geometry
            ? geometry
            : throw new InvalidOperationException($"There is no icon '{key}'.");

    /// <summary>A control theme from the app's resources.</summary>
    public static ControlTheme Theme(string key) =>
        Application.Current!.TryGetResource(key, null, out var theme) && theme is ControlTheme controlTheme
            ? controlTheme
            : throw new InvalidOperationException($"There is no control theme '{key}'.");

    /// <summary>The activity bar of a solution with tests, Git, CI, pull requests and the agent at work.</summary>
    public static ActivityBarViewModel ActivityBar()
    {
        var bar = new ActivityBarViewModel();
        bar.Add(new ActivityItem("SolutionExplorer", "Solution Explorer", Icon("Icon.Solution"), "Ctrl+Alt+L"));
        bar.Add(new ActivityItem("GitChanges", "Git Changes", Icon("Icon.Branch"))).ShowCount(3, ActivityTone.Neutral, "3 changed files");
        bar.Add(new ActivityItem("Actions", "GitHub Actions", Icon("Icon.Actions"))).ShowDot(ActivityTone.Error, "1 of 5 checks failed");
        bar.Add(new ActivityItem("PullRequests", "Pull Requests", Icon("Icon.PullRequest"))).ShowCount(2, ActivityTone.Accent, "2 reviews requested");
        bar.Add(new ActivityItem("TestExplorer", "Test Explorer", Icon("Icon.Flask"), "Ctrl+E, T")).ShowCount(1, ActivityTone.Error, "1 test failed");
        bar.Add(new ActivityItem("Agent", "Claude", Icon("Icon.Agent"))).ShowActivity("Claude is working");
        bar.Add(new ActivityItem("AppPreview", "App Preview", Icon("Icon.Window")));
        bar.Add(new ActivityItem("WebPreview", "Web Preview", Icon("Icon.Globe")) { IsAvailable = false });
        bar.Add(new ActivityItem("Extensions", "Extensions", Icon("Icon.Extensions"), "Ctrl+Shift+X") { IsAtBottom = true });
        bar.Add(new ActivityItem("Settings", "Settings", Icon("Icon.Gear")) { IsAtBottom = true });
        bar.Select("SolutionExplorer");
        return bar;
    }

    /// <summary>The standard toolbar as an adaptive bar, with the importance stage 2 gives each command.</summary>
    /// <param name="debugging">Whether the debugger's commands show, as while paused.</param>
    public static AdaptiveBar Toolbar(bool debugging = false)
    {
        var bar = new AdaptiveBar { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(Item("Icon.Back", "Back", "Alt+Left", 30, compact: true));
        bar.Children.Add(Item("Icon.Forward", "Forward", "Alt+Right", 20, compact: true));
        bar.Children.Add(new AdaptiveBarSeparator());
        bar.Children.Add(Item("Icon.Save", "Save", "Ctrl+S", 60, compact: true));
        bar.Children.Add(Item("Icon.SaveAll", "Save All", "Ctrl+Shift+S", 25, compact: true));
        bar.Children.Add(new AdaptiveBarSeparator());
        bar.Children.Add(Item("Icon.Undo", "Undo", "Ctrl+Z", 50, compact: true));
        bar.Children.Add(Item("Icon.Redo", "Redo", "Ctrl+Y", 40, compact: true));
        bar.Children.Add(new AdaptiveBarSeparator());
        bar.Children.Add(Configuration());
        bar.Children.Add(RunButton(debugging ? "Continue" : "HelloTablet"));
        if (debugging)
        {
            bar.Children.Add(Item("Icon.Pause", "Break All", "Ctrl+Alt+Break", 70, compact: true));
            bar.Children.Add(Item("Icon.Stop", "Stop", "Shift+F5", 95, compact: true));
            bar.Children.Add(Item("Icon.Restart", "Restart", "Ctrl+Shift+F5", 65, compact: true));
            bar.Children.Add(Item("Icon.StepOver", "Step Over", "F10", 90));
            bar.Children.Add(Item("Icon.StepInto", "Step Into", "F11", 85));
            bar.Children.Add(Item("Icon.StepOut", "Step Out", "Shift+F11", 80));
        }
        else
        {
            bar.Children.Add(Item("Icon.Play", "Start Without Debugging", "Ctrl+F5", 45, compact: true, filled: false));
        }

        bar.Children.Add(Item("Icon.Computer", "Run on In-process", null, 35));
        bar.Children.Add(new AdaptiveBarSeparator());
        bar.Children.Add(Item("Icon.Comment", "Comment", "Ctrl+/", 15, compact: true));
        bar.Children.Add(Item("Icon.Format", "Format Document", "Shift+Alt+F", 10, compact: true));
        bar.Children.Add(Item("Icon.Search", "Find", "Ctrl+F", 55, compact: true));
        return bar;
    }

    /// <summary>
    /// The status bar as one adaptive bar: the IDE's state on the left, a spacer, and the editor's on the right. The
    /// status message and the caret stay; the rest shrinks to icons or folds into More by importance.
    /// </summary>
    public static AdaptiveBar StatusBar()
    {
        var bar = new AdaptiveBar { Spacing = 6, ItemTheme = Theme("StatusBarButton"), VerticalAlignment = VerticalAlignment.Center };
        bar.MorePlacement = Avalonia.Controls.PlacementMode.TopEdgeAlignedRight;
        bar.Children.Add(Status(null, "Ready", 100, pinned: true));
        bar.Children.Add(Status("Icon.Branch", "main", 80));
        bar.Children.Add(Status("Icon.RunFailed", "1 check failed", 75));
        bar.Children.Add(Status("Icon.LiveTesting", "Live Unit Testing: 3 passed, 1 failed", 60));
        bar.Children.Add(Status("Icon.Link", "Pixel 9", 50));
        bar.Children.Add(Status("Icon.Terminal", "Starting Pyright…", 30));
        bar.Children.Add(Status("Icon.Thermometer", "Device is warm", 10));
        bar.Children.Add(new AdaptiveBarSpacer());
        bar.Children.Add(Status(null, "Ln 16, Col 9", 90, pinned: true));
        bar.Children.Add(Status(null, "Spaces: 4", 20));
        bar.Children.Add(Status(null, "UTF-8", 30));
        bar.Children.Add(Status(null, "LF", 25));
        bar.Children.Add(Status(null, "C#", 70));
        return bar;
    }

    /// <summary>The search over a small solution: files, commands from menus, settings, symbols, templates and recent solutions.</summary>
    /// <param name="paths">The app's folders, for the appearance setting.</param>
    public static UnifiedSearchViewModel Search(AppPaths paths)
    {
        var registry = new CommandRegistry();
        registry.AddMenu(MenuBar());
        var appearance = new AppearanceService(new AppearanceSettingsStore(paths));
        var history = new SearchHistory();
        ISearchProvider[] providers =
        [
            new FileSearchProvider(() => Files, _ => Task.CompletedTask),
            new CommandSearchProvider(registry),
            new SettingSearchProvider(SettingSearchProvider.Appearance(appearance)),
            new SymbolSearchProvider(new SampleSymbols(), _ => Task.CompletedTask),
            new TemplateSearchProvider(ProjectTemplates.All, _ => Task.CompletedTask),
            new WorkspaceSearchProvider(() => Workspaces, _ => Task.CompletedTask),
        ];
        return new UnifiedSearchViewModel(providers, history);
    }

    /// <summary>The files of the sample solution, and a few more.</summary>
    public static IReadOnlyList<SearchFile> Files { get; } =
    [
        new("/w/src/HelloTablet/Program.cs", "src/HelloTablet"),
        new("/w/src/HelloTablet/HelloTablet.csproj", "src/HelloTablet"),
        new("/w/src/HelloTablet/Tour.ipynb", "src/HelloTablet"),
        new("/w/src/Greetings/Greeter.cs", "src/Greetings"),
        new("/w/src/Greetings/Greetings.csproj", "src/Greetings"),
        new("/w/tests/Greetings.Tests/GreeterTests.cs", "tests/Greetings.Tests"),
        new("/w/README.md", string.Empty),
        new("/w/.github/workflows/build.yml", ".github/workflows"),
    ];

    /// <summary>Solutions opened recently.</summary>
    public static IReadOnlyList<RecentWorkspace> Workspaces { get; } =
    [
        new("/w/HelloTablet/HelloTablet.slnx", "HelloTablet", "2 projects", DateTimeOffset.Now),
        new("/w/Storefront/Storefront.slnx", "Storefront", "1 project", DateTimeOffset.Now.AddDays(-1)),
        new("/w/greeter", "greeter", "Folder", DateTimeOffset.Now.AddDays(-3)),
    ];

    /// <summary>A menu bar like the IDE's, for the command registry.</summary>
    public static Menu MenuBar() => new()
    {
        Items =
        {
            Submenu("_File", Command("_Save", "Ctrl+S"), Command("Save A_ll", "Ctrl+Shift+S"), Command("TabStudio _Link..."), Command("Close Sol_ution")),
            Submenu("_Edit", Command("_Undo", "Ctrl+Z"), Command("_Find", "Ctrl+F"), Command("Toggle Line _Comment (Ctrl+/)"),
                Command("F_ormat Document", "Shift+Alt+F"), Submenu("_IntelliSense", Command("_Quick Info (Ctrl+K, Ctrl+I)"), Command("Complete _Word", "Ctrl+Space"))),
            Submenu("_View", Command("_Solution Explorer", "Ctrl+Alt+L"), Command("_Git Changes"), Command("_Test Explorer (Ctrl+E, T)"), Command("_Error List")),
            Submenu("_Build", Command("_Build Solution", "Ctrl+Shift+B"), Command("_Rebuild Solution", enabled: false), Command("_Debug Configuration"), Command("_Release Configuration")),
            Submenu("_Debug", Command("_Start Debugging", "F5"), Command("Start Wit_hout Debugging", "Ctrl+F5"), Command("Toggle _Breakpoint", "F9"), Command("Step _Over", "F10")),
            Submenu("_Test", Command("Run _All Tests (Ctrl+R, A)"), Command("Run _Failed Tests (Ctrl+R, F)")),
        },
    };

    private static AdaptiveBarItem Item(string icon, string label, string? gesture, int priority, bool compact = false, bool filled = false) => new()
    {
        Icon = Icon(icon),
        Label = label,
        Gesture = gesture,
        Priority = priority,
        ShowsLabel = !compact,
        IsIconFilled = filled,
        Command = Nothing,
    };

    private static AdaptiveBarItem Status(string? icon, string label, int priority, bool pinned = false) => new()
    {
        Icon = icon is null ? null : Icon(icon),
        Label = label,
        Priority = priority,
        IsPinned = pinned,
        IconSize = 13,
        FontSize = 12,
        Command = Nothing,
    };

    private static AdaptiveBarItem Configuration()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuItem { Header = "Debug", Command = Nothing });
        menu.Items.Add(new MenuItem { Header = "Release", Command = Nothing });
        return new AdaptiveBarItem
        {
            Icon = Icon("Icon.Build"),
            Label = "Debug",
            Priority = 5,
            Flyout = menu,
            Theme = Theme("SecondaryButton"),
            Margin = new Thickness(2, 0),
        };
    }

    private static StackPanel RunButton(string label)
    {
        var run = new Button
        {
            Theme = Theme("AccentButton"),
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(8, 0, 0, 8),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { new StrokeIcon { Data = Icon("Icon.Play"), IsFilled = true, Size = 14 }, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } },
            },
        };
        var chooser = new Button
        {
            Theme = Theme("AccentButton"),
            Padding = default,
            CornerRadius = new CornerRadius(0, 8, 8, 0),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Content = new StrokeIcon { Data = Icon("Icon.ChevronDown"), Size = 14 },
        };
        ToolTip.SetTip(chooser, "Choose Startup Project");
        chooser.Bind(Button.BorderBrushProperty, chooser.GetResourceObservable("Brush.OnAccent.Divider"));
        var split = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 4, 0), Children = { run, chooser } };
        AdaptiveBar.SetIsPinned(split, true);
        return split;
    }

    private static MenuItem Submenu(string header, params MenuItem[] items)
    {
        var menu = new MenuItem { Header = header };
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        return menu;
    }

    private static MenuItem Command(string header, string? gesture = null, bool enabled = true) => new()
    {
        Header = header,
        InputGesture = gesture is null ? null : KeyGesture.Parse(gesture),
        Command = enabled ? Nothing : new RelayCommand(() => { }, () => false),
    };

    /// <summary>The symbols of the sample, as Roslyn would find them.</summary>
    private sealed class SampleSymbols : ISymbolSearch
    {
        private static readonly SymbolHit[] Symbols =
        [
            new("Greeter", "Class", "Greetings", SymbolGlyph.Class, "/w/src/Greetings/Greeter.cs", 6, 21),
            new("Greet", "Method", "Greeter", SymbolGlyph.Method, "/w/src/Greetings/Greeter.cs", 15, 19),
            new("GreeterTests", "Class", "Greetings.Tests", SymbolGlyph.Class, "/w/tests/Greetings.Tests/GreeterTests.cs", 5, 21),
            new("Language", "Record", null, SymbolGlyph.Class, "/w/src/HelloTablet/Program.cs", 35, 24),
        ];

        public Task<IReadOnlyList<SymbolHit>> FindAsync(string query, int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<SymbolHit> hits = [.. Symbols.Where(symbol => SearchMatcher.Match(query, symbol.Name) is not null).Take(limit)];
            return Task.FromResult(hits);
        }
    }
}
