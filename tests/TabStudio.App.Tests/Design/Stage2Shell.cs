using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Services.Search;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.ViewModels.Shell;
using TabStudio.App.Views;
using TabStudio.App.Views.Search;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// The shell as stage 2 of the design pass will build it, put together from the real IDE and the new components, for
/// the spec's mockups: the search box becomes a Search button that opens the unified search, the toolbar and the
/// status bar become adaptive bars, the tool window tabs become the activity bar, and Properties folds into its header.
/// Each change here is one listed edit of docs/design/stage-2.md, made in the live visual tree instead of the XAML.
/// </summary>
internal sealed class Stage2Shell
{
    private readonly WorkspaceView _view;
    private readonly Grid _root;
    private readonly Grid _mainArea;
    private readonly Grid _sideBar;

    private Stage2Shell(WorkspaceView view, WorkspaceViewModel workspace, AppPaths paths)
    {
        _view = view;
        _root = (Grid)view.Content!;
        _mainArea = view.FindControl<Grid>("MainArea")!;
        _sideBar = view.FindControl<Grid>("SideBar")!;
        Activity = DesignSamples.ActivityBar();
        Search = CreateSearch(view, workspace, paths);
        ActivityBar = new ActivityBar { Bar = Activity };
    }

    /// <summary>The activity bar's items.</summary>
    public ActivityBarViewModel Activity { get; }

    /// <summary>The activity bar.</summary>
    public ActivityBar ActivityBar { get; }

    /// <summary>The unified search over the open solution.</summary>
    public UnifiedSearchViewModel Search { get; }

    /// <summary>Rebuilds the shell of a live IDE as stage 2 will.</summary>
    /// <param name="window">The window the IDE is in.</param>
    /// <param name="workspace">The open workspace.</param>
    /// <param name="paths">The app's folders.</param>
    public static Stage2Shell Apply(Window window, WorkspaceViewModel workspace, AppPaths paths)
    {
        var view = window.GetVisualDescendants().OfType<WorkspaceView>().Single();
        var shell = new Stage2Shell(view, workspace, paths);
        shell.ReplaceSearchBox();
        shell.ReplaceToolbar(workspace);
        shell.AddActivityBar();
        shell.FoldProperties(workspace);
        shell.ReplaceStatusBar();
        shell.CompactTouchBar(window.Width < 900);
        var search = new UnifiedSearchView { DataContext = shell.Search };
        Grid.SetRowSpan(search, 4);
        shell._root.Children.Add(search);
        return shell;
    }

    /// <summary>Shows the side bar as a drawer over the editor, as a portrait screen does when an activity is tapped.</summary>
    public void OpenDrawer()
    {
        _mainArea.Children.Remove(_sideBar);
        var drawer = new Border
        {
            Width = 330,
            Margin = new Thickness(0, 0, 48, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _sideBar,
        };
        drawer.Bind(Border.BoxShadowProperty, drawer.GetResourceObservable("Shadow.Dialog"));
        drawer.Bind(Border.BorderBrushProperty, drawer.GetResourceObservable("Brush.Border"));
        _sideBar.IsVisible = true;
        Grid.SetRow(drawer, 2);
        _root.Children.Add(drawer);
    }

    private static UnifiedSearchViewModel CreateSearch(WorkspaceView view, WorkspaceViewModel workspace, AppPaths paths)
    {
        var registry = new CommandRegistry();
        registry.AddMenu(view.GetVisualDescendants().OfType<Menu>().First());
        registry.AddKeyBindings(view.KeyBindings);
        var root = workspace.Workspace!.RootDirectory;
        var files = workspace.Workspace.Tree.DescendantsAndSelf()
            .Where(node => node.IsOpenableFile)
            .Select(node => new SearchFile(node.FullPath!, FolderOf(root, node.FullPath!)))
            .ToList();
        var appearance = new AppearanceService(new AppearanceSettingsStore(paths));
        ISearchProvider[] providers =
        [
            new FileSearchProvider(() => files, workspace.OpenDocumentAsync),
            new CommandSearchProvider(registry),
            new SettingSearchProvider(SettingSearchProvider.Appearance(appearance)),
            new TemplateSearchProvider(ProjectTemplates.All, _ => Task.CompletedTask),
            new WorkspaceSearchProvider(() => DesignSamples.Workspaces, _ => Task.CompletedTask),
        ];
        return new UnifiedSearchViewModel(providers, new SearchHistory());
    }

    private static string FolderOf(string root, string path)
    {
        var folder = Path.GetRelativePath(root, Path.GetDirectoryName(path)!);
        return folder == "." ? string.Empty : folder.Replace('\\', '/');
    }

    /// <summary>The menu bar's search box becomes a button that opens the search, with its key gesture.</summary>
    private void ReplaceSearchBox()
    {
        var menuBar = (Grid)_root.Children.OfType<Border>().First(border => Grid.GetRow(border) == 0).Child!;
        var box = menuBar.Children.First(child => Grid.GetColumn(child) == 2);
        menuBar.Children.Remove(box);
        var button = new Button
        {
            Theme = DesignSamples.Theme("SecondaryButton"),
            MinHeight = 32,
            Height = 32,
            Width = 240,
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(10, 0, 6, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Command = Search.OpenCommand,
            Content = SearchButtonContent(),
        };
        ToolTip.SetTip(button, "Search (Ctrl+Q)");
        Grid.SetColumn(button, 2);
        menuBar.Children.Add(button);
    }

    private static Grid SearchButtonContent()
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var icon = new StrokeIcon { Data = DesignSamples.Icon("Icon.Search"), Size = 14 };
        var text = new TextBlock { Text = "Search", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5 };
        text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("Brush.TextMuted"));
        var key = new Border { Classes = { "keycap" }, Padding = new Thickness(6, 0), Height = 20, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
        key.Bind(Border.BorderBrushProperty, key.GetResourceObservable("Brush.BorderStrong"));
        key.Child = new TextBlock { Text = "Ctrl+Q", FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(key, 2);
        content.Children.Add(icon);
        content.Children.Add(text);
        content.Children.Add(key);
        return content;
    }

    /// <summary>The scrolling toolbar becomes an adaptive bar; the problems counter stays pinned on the right.</summary>
    private void ReplaceToolbar(WorkspaceViewModel workspace)
    {
        var toolbar = (Grid)_root.Children.OfType<Border>().First(border => Grid.GetRow(border) == 1).Child!;
        var scroller = toolbar.Children.OfType<ScrollViewer>().Single();
        toolbar.Children.Remove(scroller);
        toolbar.Children.Insert(0, DesignSamples.Toolbar(workspace.IsDebugging));
    }

    /// <summary>The tool window tabs give way to the activity bar on the side bar's outer edge.</summary>
    private void AddActivityBar()
    {
        _sideBar.Children.OfType<Border>().First(border => Grid.GetRow(border) == 3).IsVisible = false;
        _mainArea.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(48)));
        var edge = new Border { Child = ActivityBar, BorderThickness = new Thickness(1, 0, 0, 0) };
        edge.Bind(Border.BackgroundProperty, edge.GetResourceObservable("Brush.ActivityBar"));
        edge.Bind(Border.BorderBrushProperty, edge.GetResourceObservable("Brush.Border"));
        Grid.SetColumn(edge, 3);
        _mainArea.Children.Add(edge);
    }

    /// <summary>Properties folds into its header at the bottom of the side bar; a tap on the header opens it.</summary>
    private void FoldProperties(WorkspaceViewModel workspace)
    {
        _sideBar.RowDefinitions[2].Height = new GridLength(40);
        _sideBar.Children.OfType<PropertiesView>().Single().IsVisible = false;
        var expand = new AdaptiveBarItem { Theme = DesignSamples.Theme("ToolButton"), Icon = DesignSamples.Icon("Icon.ChevronUp"), Label = "Show Properties", ShowsLabel = false, IconSize = 16, MinWidth = 36, MinHeight = 36, Command = DesignSamples.Nothing };
        var header = new PanelHeader
        {
            Title = "Properties",
            Subtitle = workspace.ActiveDocument?.Title ?? workspace.Title,
            Icon = DesignSamples.Icon("Icon.Properties"),
            Actions = expand,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        Grid.SetRow(header, 2);
        _sideBar.Children.Add(header);
    }

    /// <summary>The status bar becomes one adaptive bar: the state and the caret stay, the rest folds into More.</summary>
    private void ReplaceStatusBar()
    {
        var status = _root.Children.OfType<Border>().First(border => Grid.GetRow(border) == 3 && Grid.GetRowSpan(border) == 1);
        var bar = DesignSamples.StatusBar();
        bar.Margin = new Thickness(6, 0);
        status.Child = bar;
    }

    /// <summary>The floating touch buttons become an adaptive bar too: on a narrow editor the least used fold to icons.</summary>
    private void CompactTouchBar(bool narrow)
    {
        var fold = _view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "FoldTouchBarButton");
        var bar = (Border)((StackPanel)fold.GetVisualParent()!).GetVisualParent()!;
        if (!narrow)
        {
            return;
        }

        var touch = new AdaptiveBar { Spacing = 2, MaxWidth = 420 };
        foreach (var (icon, label, gesture, priority) in new[]
        {
            ("Icon.Rename", "Rename", "F2", 30),
            ("Icon.Search", "Peek", "Alt+F12", 25),
            ("Icon.Comment", "Comment", "Ctrl+/", 20),
            ("Icon.Forward", "To Cursor", "Ctrl+F10", 15),
            ("Icon.Format", "Format", "Shift+Alt+F", 10),
        })
        {
            touch.Children.Add(new AdaptiveBarItem { Icon = DesignSamples.Icon(icon), Label = label, Gesture = gesture, Priority = priority, Height = 44, MinWidth = 44, Command = DesignSamples.Nothing });
        }

        var run = new AdaptiveBarItem { Icon = DesignSamples.Icon("Icon.Play"), IsIconFilled = true, IconSize = 14, Label = "Run", IsPinned = true, Priority = 100, Height = 44, Theme = DesignSamples.Theme("AccentButton"), Command = DesignSamples.Nothing };
        touch.Children.Add(run);
        bar.Child = touch;
    }
}
