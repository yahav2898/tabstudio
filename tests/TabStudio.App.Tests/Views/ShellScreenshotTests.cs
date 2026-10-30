using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Controls;
using TabStudio.App.Services;
using TabStudio.App.Services.Search;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// The shell of docs/design/stage-2.md in the real IDE, rendered with Skia: the activity bar with every tool window,
/// the side bar as a drawer in portrait and on a phone, the adaptive toolbar and status bar, the search over the IDE,
/// a debugging session at every width, folding Properties and the touch bar that keeps clear of the caret. The tests
/// run in both themes on CI, so each screenshot comes in dark and light.
/// </summary>
public sealed class ShellScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public ShellScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Landscape_with_the_activity_bar_and_each_tool_window()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var shell = Shell(window);

        UiTestHelpers.SaveScreenshot(window, "shell-01-landscape");

        Assert.True(shell.IsSideBarDocked);
        Assert.False(shell.AreMenusFolded);
        Assert.Empty(Named<ActivityBar>(window, "ActivityBarView").Overflowed);
        Assert.False(Named<AdaptiveBar>(window, "Toolbar").ShowsMore);
        Assert.Equal("SolutionExplorer", workspace.Activity.SelectedItem?.Id);
        Assert.False(workspace.Properties.IsExpanded);
        AssertInWindow(window, Named<AdaptiveBar>(window, "Toolbar"), Named<AdaptiveBar>(window, "StatusBar"), Named<Button>(window, "SearchButton"));

        foreach (var (panel, name) in new[]
        {
            (SideBarPanel.GitChanges, "shell-02-git-changes"),
            (SideBarPanel.Actions, "shell-03-actions"),
            (SideBarPanel.PullRequests, "shell-04-pull-requests"),
            (SideBarPanel.TestExplorer, "shell-05-test-explorer"),
            (SideBarPanel.Agent, "shell-06-claude"),
            (SideBarPanel.Swarm, "shell-07-swarm"),
        })
        {
            // Through the activity bar where the tool window has an icon now, else as View's menu does.
            if (workspace.Activity.Find(panel.ToString()) is { IsAvailable: true } item)
            {
                workspace.Activity.ActivateCommand.Execute(item);
            }
            else
            {
                workspace.SelectSideBarPanelCommand.Execute(panel);
            }

            await UiTestHelpers.WaitUntilAsync(() => true);
            UiTestHelpers.SaveScreenshot(window, name);
            Assert.Equal(panel, workspace.SelectedSideBarPanel);
            Assert.Equal(panel.ToString(), workspace.Activity.SelectedItem?.Id);
        }

        // A tap on the shown tool window's icon folds the side bar away, giving the code the width; another brings it back.
        workspace.Activity.ActivateCommand.Execute(workspace.Activity.SelectedItem);
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-08-side-bar-folded");
        Assert.False(workspace.IsSideBarVisible);
        Assert.False(Named<Border>(window, "SideBarFrame").IsVisible);

        workspace.Activity.ActivateCommand.Execute(workspace.Activity.SelectedItem);
        Assert.True(workspace.IsSideBarVisible);
    }

    [AvaloniaFact]
    public async Task Portrait_opens_the_side_bar_as_a_drawer_over_the_editor()
    {
        var window = Show(800, 1280);
        var workspace = await OpenSampleAsync();
        var shell = Shell(window);

        UiTestHelpers.SaveScreenshot(window, "shell-10-portrait");

        Assert.False(shell.IsSideBarDocked);
        Assert.False(workspace.IsSideBarVisible);
        Assert.True(Named<ActivityBar>(window, "ActivityBarView").IsEffectivelyVisible);
        AssertInWindow(window, Named<AdaptiveBar>(window, "Toolbar"), Named<AdaptiveBar>(window, "StatusBar"), Named<Button>(window, "RunButton"));

        workspace.Activity.ActivateCommand.Execute(workspace.Activity.Find(nameof(SideBarPanel.SolutionExplorer)));
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-11-portrait-drawer");

        var drawer = Named<Border>(window, "SideBarFrame");
        Assert.True(drawer.IsEffectivelyVisible);
        Assert.Equal(330, drawer.Bounds.Width, 1);
        Assert.Equal(800 - ActivityBar.ItemSize - 330, drawer.Bounds.X, 1);

        workspace.Activity.ActivateCommand.Execute(workspace.Activity.Find(nameof(SideBarPanel.GitChanges)));
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-12-portrait-git-changes");
        Assert.True(workspace.IsGitChangesSelected);

        // A tap beside the drawer closes it.
        var dismiss = Named<Border>(window, "DrawerDismiss");
        var outside = dismiss.TranslatePoint(new Point(60, 300), window) ?? throw new InvalidOperationException("The editor is not in the window.");
        window.MouseDown(outside, MouseButton.Left);
        window.MouseUp(outside, MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsSideBarVisible, because: "the drawer to close");
        Assert.False(drawer.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_phone_folds_the_menus_and_the_bars_and_clips_nothing()
    {
        var window = Show(412, 915);
        var workspace = await OpenSampleAsync();
        var shell = Shell(window);

        UiTestHelpers.SaveScreenshot(window, "shell-20-phone");

        Assert.True(shell.AreMenusFolded);
        Assert.True(Named<Button>(window, "MenuButton").IsEffectivelyVisible);
        Assert.Contains("compact", Named<Button>(window, "SearchButton").Classes);
        var toolbar = Named<AdaptiveBar>(window, "Toolbar");
        var status = Named<AdaptiveBar>(window, "StatusBar");
        Assert.True(toolbar.ShowsMore);
        AssertInWindow(window, toolbar, status, toolbar.MoreButton, Named<Button>(window, "RunButton"), Named<Button>(window, "MenuButton"));
        var caret = status.Children.OfType<AdaptiveBarItem>().Single(item => item.Label == workspace.CaretText);
        Assert.NotEqual(AdaptiveMode.Overflow, status.ModeOf(caret));
        AssertInWindow(window, caret);

        // With Output selected, its picker leaves the tabs room: the selected tab and Hide stay on the screen.
        workspace.SelectPanelCommand.Execute(BottomPanel.Output);
        await UiTestHelpers.WaitUntilAsync(() => true);
        var tabs = Named<StackPanel>(window, "BottomTabs");
        var output = tabs.Children.OfType<Button>().Single(tab => tab.Classes.Contains("selected"));
        Assert.Equal("Output", output.Content);
        AssertInWindow(window, output, Named<Button>(window, "HideBottomPanelButton"));

        // The Error List keeps its Description on a phone: its fixed columns used to leave it no width at all.
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await UiTestHelpers.WaitUntilAsync(() => true);
        var header = Named<Grid>(window, "HeaderColumns");
        Assert.True(header.ColumnDefinitions[2].ActualWidth > 150, $"The Description column is {header.ColumnDefinitions[2].ActualWidth} pixels wide on a phone.");

        // The menus are all still there, in the Menu button, and the search still finds their commands.
        var menu = Assert.IsType<MenuFlyout>(Named<Button>(window, "MenuButton").Flyout);
        Assert.Contains(menu.Items.OfType<MenuItem>(), item => item.Header as string == "_Debug");
        Assert.Contains(workspace.Commands.Commands, command => command.Title == "Build Solution");

        workspace.Activity.ActivateCommand.Execute(workspace.Activity.Find(nameof(SideBarPanel.SolutionExplorer)));
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-21-phone-drawer");
        Assert.InRange(Named<Border>(window, "SideBarFrame").Bounds.Width, 240, 330);

        workspace.Search.OpenCommand.Execute(null);
        workspace.Search.Query = "gree";
        await workspace.Search.Completion;
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-22-phone-search");
        Assert.Contains(workspace.Search.Rows.OfType<SearchResultRow>(), row => row.Title == "Greeter.cs");
    }

    [AvaloniaFact]
    public async Task Debugging_on_a_tablet_in_both_orientations_and_on_a_phone()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        AnswerInput(workspace, "Ada");
        var program = workspace.ActiveDocument!;
        workspace.Breakpoints.Toggle(program.FilePath, 30);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForPauseAsync(workspace, 30);
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-30-debugging");

        // Continue and Stop never leave the toolbar.
        var toolbar = Named<AdaptiveBar>(window, "Toolbar");
        var stop = toolbar.Children.OfType<AdaptiveBarItem>().Single(item => item.Label == "Stop Debugging");
        Assert.NotEqual(AdaptiveMode.Overflow, toolbar.ModeOf(stop));
        AssertInWindow(window, Named<Button>(window, "RunButton"), stop);

        Resize(window, 800, 1280);
        UiTestHelpers.SaveScreenshot(window, "shell-31-debugging-portrait");
        Assert.NotEqual(AdaptiveMode.Overflow, toolbar.ModeOf(stop));
        AssertInWindow(window, toolbar, Named<Button>(window, "RunButton"), stop);

        Resize(window, 412, 915);
        UiTestHelpers.SaveScreenshot(window, "shell-32-debugging-phone");
        Assert.NotEqual(AdaptiveMode.Overflow, toolbar.ModeOf(stop));
        AssertInWindow(window, toolbar, Named<Button>(window, "RunButton"), stop);

        // On a phone Locals has the panel's width: the Call Stack beside it took a fixed 380 pixels and left it none.
        Assert.True(workspace.IsLocalsSelected);
        var locals = window.GetVisualDescendants().OfType<LocalsView>().Single(view => view.IsEffectivelyVisible);
        Assert.True(locals.Bounds.Width > 200, $"Locals is {locals.Bounds.Width} pixels wide on a phone.");

        workspace.StopDebuggingCommand.Execute(null);
        await DebuggingTests.WaitForExitAsync(workspace);
    }

    [AvaloniaFact]
    public async Task Ctrl_Q_searches_files_commands_and_symbols_over_the_IDE()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var search = workspace.Search;
        window.GetVisualDescendants().OfType<CodeEditorView>().First(editor => editor.IsVisible).Editor.TextArea.Focus();

        window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Control);
        await UiTestHelpers.WaitUntilAsync(() => search.IsOpen, because: "Ctrl+Q to open the search");
        search.Query = "gree";
        await search.Completion;
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-40-search");

        var rows = search.Rows.OfType<SearchResultRow>().ToList();
        Assert.Contains(rows, row => row.Title == "Greeter.cs" && row.Item.Kind == SearchKind.File);
        Assert.Contains(rows, row => row.Title == "Greeter" && row.Item.Kind == SearchKind.Symbol);

        search.Query = ">build sol";
        await search.Completion;
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-41-search-commands");
        var build = Assert.IsType<SearchResultRow>(search.Rows[0]);
        Assert.Equal(("Build Solution", "Build", "Ctrl+Shift+B"), (build.Title, build.Detail, build.Item.Gesture));

        // Every menu's commands are in the search, Tools > Theme's too.
        Assert.Contains(workspace.Commands.Commands, command => command.Title == "Light" && command.Path == "Tools > Theme");
        Assert.Contains(workspace.Commands.Commands, command => command.Title == "Go To Definition" && command.Gesture == "F12");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => !search.IsOpen, because: "Escape to close the search");
    }

    [AvaloniaFact]
    public async Task Properties_fold_into_their_header_and_keep_paths_on_one_line()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        workspace.Explorer.SelectedItem = workspace.Explorer.Rows[0];
        var properties = window.GetVisualDescendants().OfType<PropertiesView>().Single();
        await UiTestHelpers.WaitUntilAsync(() => true);

        Assert.False(workspace.Properties.IsExpanded);
        Assert.InRange(properties.Bounds.Height, 38, 44);
        Assert.False(Named<GridSplitter>(window, "PropertiesSplitter").IsVisible);

        Named<Button>(window, "HeaderButton").Command!.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => properties.Bounds.Height > 150, because: "Properties to open");
        UiTestHelpers.SaveScreenshot(window, "shell-60-properties");

        Assert.True(Named<GridSplitter>(window, "PropertiesSplitter").IsVisible);
        Assert.DoesNotContain(workspace.Properties.Rows, row => row.Value == "None");
        var path = properties.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.TextTrimming == TextTrimming.PathSegmentEllipsis && text.IsEffectivelyVisible);
        Assert.True(path.Bounds.Height < 24, $"The path takes {path.Bounds.Height} pixels: more than one line.");
    }

    [AvaloniaFact]
    public async Task The_touch_bar_keeps_clear_of_the_caret_and_fades_while_a_keyboard_types()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var shell = Shell(window);
        var editor = window.GetVisualDescendants().OfType<CodeEditorView>().First(view => view.IsVisible);
        var textView = editor.Editor.TextArea.TextView;
        var bar = Named<Panel>(window, "TouchBarHost");
        Assert.False(shell.IsTouchBarAtTop);

        // The caret on a line under the touch bar, as a tap there puts it, sends the bar to the top of the editor.
        var middle = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, bar.Bounds.Height / 2), textView)
            ?? throw new InvalidOperationException("The touch bar is not over the editor.");
        var covered = textView.GetVisualLineFromVisualTop(middle.Y + textView.ScrollOffset.Y)?.FirstDocumentLine
            ?? throw new InvalidOperationException("No line is under the touch bar.");
        editor.Editor.CaretOffset = covered.Offset;
        await UiTestHelpers.WaitUntilAsync(() => shell.IsTouchBarAtTop, because: "the touch bar to move out of the caret's way");
        UiTestHelpers.SaveScreenshot(window, "shell-50-touch-bar-clear-of-the-caret");

        editor.Editor.CaretOffset = 0;
        await UiTestHelpers.WaitUntilAsync(() => !shell.IsTouchBarAtTop, because: "the touch bar to come back down");

        // A hardware keyboard's key fades it away; touch input brings it back.
        editor.Editor.TextArea.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsTouchInput, because: "the touch bar to fade while a keyboard types");
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "shell-51-typing-on-a-keyboard");
        Assert.False(Named<Button>(window, "FoldTouchBarButton").IsEffectivelyVisible);

        workspace.IsTouchInput = true;
        await UiTestHelpers.WaitUntilAsync(() => Named<Button>(window, "FoldTouchBarButton").IsEffectivelyVisible, because: "the touch bar to come back");
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private static WorkspaceView Shell(Window window) => window.GetVisualDescendants().OfType<WorkspaceView>().Single();

    /// <summary>A control of the shell by its name, or of a view inside it when the shell has no control of that name.</summary>
    private static T Named<T>(Window window, string name)
        where T : Control =>
        Shell(window).FindControl<T>(name) ?? window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    /// <summary>Asserts that controls are whole inside the window: nothing is clipped at its edges.</summary>
    private static void AssertInWindow(Window window, params Control[] controls)
    {
        foreach (var control in controls)
        {
            var origin = control.TranslatePoint(default, window) ?? throw new InvalidOperationException($"{control.Name} is not in the window.");
            var bounds = new Rect(origin, control.Bounds.Size);
            Assert.True(control.IsEffectivelyVisible, $"{control.Name ?? control.GetType().Name} is hidden.");
            Assert.True(
                bounds.X >= -0.5 && bounds.Right <= window.Bounds.Width + 0.5,
                $"{control.Name ?? control.GetType().Name} is at {bounds}, outside the window's {window.Bounds.Width} pixels.");
        }
    }

    private static void Resize(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void AnswerInput(WorkspaceViewModel workspace, string line) =>
        workspace.Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = line;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        };

    private MainWindow Show(double width, double height)
    {
        // The desktop window's least size is a small laptop's; a phone's screen is narrower.
        var window = new MainWindow { DataContext = _main, Width = width, Height = height, MinWidth = 0, MinHeight = 0 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<WorkspaceViewModel> OpenSampleAsync()
    {
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return workspace;
    }
}
