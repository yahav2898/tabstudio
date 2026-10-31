using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Editing;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LiveTesting.Results;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Live Unit Testing in the real IDE window, rendered with Skia: the markers beside the code, a marker's popup, the
/// Test Explorer and status bar while it runs, and the Test menu.
/// </summary>
public sealed class LiveTestingScreenshotTests : IClassFixture<SharedToolchain>, IDisposable
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public LiveTestingScreenshotTests(SharedToolchain toolchain)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        _services = AppServices.Create(toolchain.Paths);
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task The_editor_shows_every_kind_of_marker_and_they_turn_stale_while_editing()
    {
        var (window, workspace, editor, pricing) = await OpenAsync();
        await StartAsync(workspace, pricing);

        // A breakpoint too: the markers have a margin of their own, between the breakpoints and the code.
        var breakpointLine = PricingSolution.LineOf("return price * quantity;");
        pricing.Debugger!.ToggleBreakpoint(pricing.FilePath, breakpointLine);
        Dispatcher.UIThread.RunJobs();
        UiTestHelpers.SaveScreenshot(window, "live-01-markers");

        var margin = editor.LiveTestingMargin!;
        Assert.True(margin.IsVisible);
        Assert.Equal(LiveTestingMargin.MarginWidth, margin.Bounds.Width);
        Assert.True(workspace.IsTestExplorerSelected);
        Assert.Equal(LineCoverageState.Covered, MarkerOn(pricing, breakpointLine).State);
        pricing.Debugger.ToggleBreakpoint(pricing.FilePath, breakpointLine);

        pricing.Document.Replace(pricing.Document.Text.IndexOf("20;", StringComparison.Ordinal), 2, "17");
        Dispatcher.UIThread.RunJobs();

        // Stale as soon as the line changes; the screenshot's checks take long enough for the next run to finish.
        var rateLine = PricingSolution.LineOf("var rate = 20;");
        Assert.True(MarkerOn(pricing, rateLine).IsStale);
        UiTestHelpers.SaveScreenshot(window, "live-03-editing");
        await UiTestHelpers.WaitUntilAsync(
            () => MarkerOn(pricing, rateLine) is { State: LineCoverageState.Covered, IsStale: false } && workspace.LiveTesting.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the fixed tax test to pass");
        UiTestHelpers.SaveScreenshot(window, "live-04-fixed");
    }

    [AvaloniaFact]
    public async Task Tapping_a_marker_lists_the_tests_that_run_its_line()
    {
        var (window, workspace, editor, pricing) = await OpenAsync();
        await StartAsync(workspace, pricing);
        var line = PricingSolution.LineOf("return total * 9 / 10;");
        editor.Editor.ScrollTo(line, 1);
        Dispatcher.UIThread.RunJobs();

        var point = MarginPoint(editor, window, line);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => editor.IsLiveTestPopupOpen, because: "the marker's popup");

        UiTestHelpers.SaveScreenshot(window, "live-02-popup");

        var popup = editor.LiveTestPopup!;
        Assert.Equal(line, popup.Line);
        Assert.Equal(2, popup.Tests.Count);

        // The failing line's popup puts its failing test first.
        Assert.True(editor.ShowLiveTestPopup(PricingSolution.LineOf("var rate = 20;")));
        Assert.Equal("Tax_is_seventeen_percent", editor.LiveTestPopup!.Tests[0].Name);
        Assert.Equal(TestState.Failed, editor.LiveTestPopup.Tests[0].State);
        Assert.Contains("Expected: 17", editor.LiveTestPopup.Tests[0].Failure, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(window, "live-02b-failing-popup");
    }

    [AvaloniaFact]
    public async Task The_Test_menu_starts_Live_Unit_Testing()
    {
        var (window, workspace, _, pricing) = await OpenAsync();
        var testMenu = window.GetVisualDescendants().OfType<MenuItem>().Single(m => m.Header as string == "_Test");

        testMenu.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        var liveMenu = testMenu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Live _Unit Testing");
        liveMenu.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        var start = liveMenu.Items.OfType<MenuItem>().Single(m => m.Header as string == "_Start");

        SaveWithPopups(window, "live-05-menu", liveMenu, start);

        Assert.True(start.IsEffectivelyEnabled);
        start.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        testMenu.Close();
        await StartedAsync(workspace, pricing);
        Assert.True(workspace.LiveTesting.IsActive);

        // Excluding the failing test from the Test Explorer: its row shows the struck-out flask, and the lines only
        // it ran are no longer covered.
        workspace.Tests.Rows.Single(r => r.Name == "Tax_is_seventeen_percent").ExcludeFromLiveTestingCommand.Execute(null);
        var rateLine = PricingSolution.LineOf("var rate = 20;");
        await UiTestHelpers.WaitUntilAsync(
            () => MarkerOn(pricing, rateLine).State == LineCoverageState.NotCovered
                && workspace.Tests.Rows.Single(r => r.Name == "Tax_is_seventeen_percent").IsExcludedFromLiveTesting,
            RunTimeout,
            "the excluded test's lines to lose it");

        // The summary names the excluded test, which the Test Explorer still shows as failed, instead of "0 failed".
        await UiTestHelpers.WaitUntilAsync(() => workspace.LiveTesting.StatusText.EndsWith(", 1 excluded", StringComparison.Ordinal), RunTimeout, "the summary to count the excluded test");
        UiTestHelpers.SaveScreenshot(window, "live-06-excluded");
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private static LiveTestMarkerLine MarkerOn(DocumentViewModel document, int line) =>
        new(Assert.Single(document.LiveTestMarkers!.Lines(document.Document), m => m.Line == line).Marker);

    /// <summary>The middle of a line in the Live Unit Testing margin, in window coordinates.</summary>
    private static Point MarginPoint(CodeEditorView editor, Window window, int line)
    {
        var margin = editor.LiveTestingMargin!;
        var textView = editor.Editor.TextArea.TextView;
        var visualLine = textView.GetVisualLine(line) ?? throw new InvalidOperationException($"Line {line} is not in view.");
        var y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.LineMiddle) - textView.VerticalOffset;
        return margin.TranslatePoint(new Point(margin.Bounds.Width / 2, y), window) ?? throw new InvalidOperationException("The margin is not in the window.");
    }

    /// <summary>
    /// Saves the window with the menus that are open over it. Menus open in popups of their own, which the window's
    /// frame does not include, so their frames are drawn on top of it where they are.
    /// </summary>
    private static void SaveWithPopups(Window window, string name, params Control[] insidePopups)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        // Each frame is disposed after drawing, which keeps it and the pixels Skia reads alive until then; see
        // UiTestHelpers.SaveFrame.
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        using var target = new RenderTargetBitmap(frame.PixelSize);
        using (var context = target.CreateDrawingContext())
        {
            context.DrawImage(frame, new Rect(frame.Size));
            var origin = window.PointToScreen(default);
            foreach (var control in insidePopups)
            {
                if (TopLevel.GetTopLevel(control) is { } popup && !ReferenceEquals(popup, window) && popup.CaptureRenderedFrame() is { } image)
                {
                    using (image)
                    {
                        var corner = popup.PointToScreen(default);
                        context.DrawImage(image, new Rect(new Point(corner.X - origin.X, corner.Y - origin.Y), image.Size));
                    }
                }
            }
        }

        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        target.Save(Path.Combine(folder, name + ".png"));
    }

    private static async Task StartAsync(WorkspaceViewModel workspace, DocumentViewModel pricing)
    {
        workspace.LiveTesting.StartCommand.Execute(null);
        await StartedAsync(workspace, pricing);
    }

    private static Task StartedAsync(WorkspaceViewModel workspace, DocumentViewModel pricing) =>
        UiTestHelpers.WaitUntilAsync(
            () => workspace.LiveTesting.State == LiveTestingState.UpToDate
                && pricing.LiveTestMarkers is { Count: 8 } markers
                && markers.Lines(pricing.Document).All(m => !m.Marker.IsStale),
            RunTimeout,
            "the first live run");

    private async Task<(Window Window, WorkspaceViewModel Workspace, CodeEditorView Editor, DocumentViewModel Pricing)> OpenAsync()
    {
        // Tall enough, with the bottom panel closed, to show the whole file under test.
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await _main.OpenWorkspaceAsync(PricingSolution.Write(_temp));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.TotalCount == 4, because: "the tests found in source");
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.TestExplorer);
        workspace.IsBottomPanelVisible = false;

        var pricing = (await workspace.OpenDocumentAsync(_temp.Resolve(PricingSolution.LibraryFile)))!;
        await pricing.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => pricing.Classifications.Count > 0, because: "syntax colors");
        var editor = window.GetVisualDescendants().OfType<CodeEditorView>().Single(v => ReferenceEquals(v.DataContext, pricing));
        return (window, workspace, editor, pricing);
    }

    /// <summary>A marker's state, for assertions.</summary>
    private readonly record struct LiveTestMarkerLine(LineCoverageState State, bool IsStale)
    {
        public LiveTestMarkerLine(LiveTestMarker marker)
            : this(marker.State, marker.IsStale)
        {
        }
    }
}
