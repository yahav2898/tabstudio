using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Previews;
using TabStudio.Testing;
using TabStudio.Wpf.Templates;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// A WPF window larger than the web preview: whole in Fit, the default, where a finger on its right-most button reaches
/// the app; at its size in 100%, where the page scrolls to the button and a tap reaches it too; the mode remembered for
/// the project. The browser is headless Chrome as a mobile browser, which zooms the page as Android's WebView does.
/// </summary>
public sealed class WpfPreviewFitTests : IClassFixture<SharedToolchain>, IDisposable
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 720;
    private const string ButtonText = "Far right";

    /// <summary>The innermost element showing the button's text, as JavaScript.</summary>
    private const string ButtonElement = """
        (() => {
            const text = 'Far right';
            const matches = [...document.querySelectorAll('body *')].filter(e => e.textContent.trim() === text && e.getClientRects().length > 0);
            return matches.find(e => ![...e.children].some(c => c.textContent.trim() === text)) ?? matches[0];
        })()
        """;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(6);

    private readonly SharedToolchain _toolchain;
    private readonly TempDirectory _temp = new();
    private readonly List<WorkspaceViewModel> _workspaces = [];
    private readonly List<HeadlessChrome> _browsers = [];

    public WpfPreviewFitTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    [AvaloniaFact]
    public async Task A_wpf_window_larger_than_the_pane_shows_whole_in_Fit_and_a_tap_on_its_right_most_button_reaches_the_app()
    {
        if (HeadlessChrome.ExecutablePath is null && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var chrome = await HeadlessChrome.StartAsync();
        _browsers.Add(chrome);
        var webViews = new ChromeWebViewFactory(chrome);
        var workspace = CreateWorkspace(webViews);
        var project = WriteWideApp();
        await workspace.LoadAsync(project);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, StartTimeout, "the WPF app to start");
        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        var webView = Assert.IsType<ChromeWebView>(webViews.WebView);

        // Fit is the default: the page is asked for it, the browser zooms pages, and the toolbar offers the modes.
        Assert.True(workspace.WebPreview.IsWpfApp);
        Assert.Equal(PreviewZoomMode.Fit, workspace.WebPreview.ZoomMode);
        Assert.True(webView.AllowsPageZoom);
        Assert.Contains("tabstudio-view=fit", webView.Page?.Query, StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(
            () => window.GetVisualDescendants().OfType<ComboBox>().Any(c => c.Name == "Zoom" && c.IsEffectivelyVisible), TimeSpan.FromSeconds(10), "the zoom choice in the toolbar");
        await WaitForAsync(webView.Rendering, "the preview's browser");
        await WaitForAsync(chrome.WaitUntilAsync("document.body.innerText.includes('Not clicked')", TimeSpan.FromSeconds(150)), "the wide window");

        // The page lays the window out exactly as the calculator says, for the pane it is in, and all of it is in view.
        // The browser first loaded the page before the side bar was laid out; drawn again, it has the pane's size.
        await WaitForAsync(webView.RefreshAsync(), "the preview's browser");
        var pane = webView.Control.Bounds.Size;
        var fit = await SettledLayoutAsync(chrome, "fit", pane.Width);
        Assert.True(WindowWidth > pane.Width, $"The window ({WindowWidth} wide) should be larger than the pane ({pane.Width}).");
        Assert.InRange(Number(fit, "paneWidth"), pane.Width - 1, pane.Width + 1);
        var expected = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, Number(fit, "paneWidth"), Number(fit, "paneHeight"), WindowWidth, WindowHeight);
        Assert.Equal(expected.Scale, Number(fit, "scale"), 3);
        Assert.Equal(expected.Left, Number(fit, "left"));
        Assert.Equal(expected.Top, Number(fit, "top"));
        Assert.True(expected.Scale < 1);
        Assert.Equal(expected.Scale, Number(fit, "pageScale"), 2);
        var frame = await PaneRectAsync(chrome, "document.getElementById('tabstudio-wpf-window')");
        Assert.True(frame.Left >= -0.5 && frame.Top >= -0.5, $"The window starts at {frame.Left}, {frame.Top}.");
        Assert.True(frame.Right <= pane.Width + 0.5 && frame.Bottom <= pane.Height + 0.5, $"The window ends at {frame.Right}, {frame.Bottom}, outside the {pane.Width} x {pane.Height} pane.");
        Assert.InRange(frame.Right - frame.Left, (WindowWidth * expected.Scale) - 1, (WindowWidth * expected.Scale) + 1);

        // A finger on the right-most button, where the pane shows it: the browser hit-tests the zoomed page, and the app
        // sees its click at the button.
        var (x, y) = await ButtonInWindowAsync(chrome);
        Assert.True(x > WindowWidth - 150, $"The button's center is {x} from the window's left.");
        var (paneX, paneY) = expected.ToPane(x, y);
        await TapPaneAsync(chrome, paneX, paneY);
        await WaitForAsync(chrome.WaitUntilAsync("document.body.innerText.includes('Far right clicked 1')", TimeSpan.FromSeconds(20)), "the tap in Fit");
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.Output.Text.Contains("Far right clicked 1", StringComparison.Ordinal), TimeSpan.FromSeconds(30), "the click's Console output");
        await WaitForAsync(webView.RefreshAsync(), "the preview's browser");
        UiTestHelpers.SaveScreenshot(window, "wpf-fit-02-after-fit");

        // 100%: the window at its size, clipped by the pane, which scrolls; remembered for the project.
        workspace.WebPreview.ZoomMode = PreviewZoomMode.ActualSize;
        Assert.Equal(PreviewZoomMode.ActualSize, new PreviewZoomStore(_toolchain.Paths).Load(project));
        Assert.Contains("tabstudio-view=actual", webView.Page?.Query, StringComparison.Ordinal);
        await WaitForAsync(webView.Rendering, "the preview's browser");
        await WaitForAsync(chrome.WaitUntilAsync("document.body.innerText.includes('Not clicked')", TimeSpan.FromSeconds(150)), "the wide window again");
        var actual = await SettledLayoutAsync(chrome, "actual", pane.Width);
        Assert.Equal(1, Number(actual, "scale"));
        Assert.Equal(1, Number(actual, "pageScale"), 2);
        Assert.True(
            (await chrome.EvaluateAsync("document.documentElement.scrollWidth > document.documentElement.clientWidth + 1"))?.GetValue<bool>() == true,
            "At 100% the page scrolls sideways to the rest of the window.");
        await WaitForAsync(webView.RefreshAsync(), "the preview's browser");
        UiTestHelpers.SaveScreenshot(window, "wpf-fit-01-before-actual-size");

        // Scrolled to the button, a tap at 100% reaches it as well.
        await chrome.EvaluateAsync("window.scrollTo(document.documentElement.scrollWidth, document.documentElement.scrollHeight)");
        var button = await PaneRectAsync(chrome, ButtonElement);
        await TapPaneAsync(chrome, (button.Left + button.Right) / 2, (button.Top + button.Bottom) / 2);
        await WaitForAsync(chrome.WaitUntilAsync("document.body.innerText.includes('Far right clicked 1')", TimeSpan.FromSeconds(20)), "the tap at 100%");

        workspace.StopCommand.Execute(null);
        await WaitForAsync(run, "the run to stop");
        window.Close();
    }

    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        // Off the UI thread, which this blocks: the browser's shutdown continues on the thread pool.
        foreach (var browser in _browsers)
        {
            Task.Run(() => browser.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }

        _temp.Dispose();
    }

    /// <summary>The template with a window wider and taller than the side bar, and a button at its right edge.</summary>
    private string WriteWideApp()
    {
        var project = WpfTemplates.Create(_temp.Path, "WideWpf");
        var directory = Path.GetDirectoryName(project)!;
        File.WriteAllText(Path.Combine(directory, "MainWindow.xaml"), $$"""
            <Window x:Class="WideWpf.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Wide window" Width="{{WindowWidth}}" Height="{{WindowHeight}}">
                <Grid Background="#FFF3EFE6">
                    <TextBlock Text="Left edge" Margin="16" FontSize="28" HorizontalAlignment="Left" VerticalAlignment="Top" />
                    <TextBlock x:Name="Status" Text="Not clicked" FontSize="40" HorizontalAlignment="Center" VerticalAlignment="Center" />
                    <Button Content="{{ButtonText}}" Click="FarRight_Click" FontSize="24" Padding="20,10" Margin="0,0,12,12"
                            HorizontalAlignment="Right" VerticalAlignment="Bottom" />
                </Grid>
            </Window>
            """);
        File.WriteAllText(Path.Combine(directory, "MainWindow.xaml.cs"), """
            using System;
            using System.Windows;

            namespace WideWpf
            {
                public partial class MainWindow : Window
                {
                    private int _clicks;

                    public MainWindow()
                    {
                        InitializeComponent();
                    }

                    private void FarRight_Click(object sender, RoutedEventArgs e)
                    {
                        _clicks++;
                        Status.Text = "Far right clicked " + _clicks;
                        Console.WriteLine(Status.Text);
                    }
                }
            }
            """);
        return project;
    }

    /// <summary>
    /// Waits until the page applied a mode for the window's declared size in a pane of the width given and the browser
    /// zoomed to it, and returns the layout with the browser's zoom as <c>pageScale</c>.
    /// </summary>
    private static async Task<JsonNode> SettledLayoutAsync(HeadlessChrome chrome, string mode, double paneWidth)
    {
        var condition = $$"""
            (() => {
                const layout = window.tabStudioWpf && window.tabStudioWpf.layout;
                const meta = document.getElementById('tabstudio-wpf-viewport');
                return !!layout && layout.mode === '{{mode}}' && !layout.fills && meta.content.includes('user-scalable')
                    && Math.abs(layout.paneWidth - {{paneWidth.ToString(CultureInfo.InvariantCulture)}}) < 1.5
                    && Math.abs(window.visualViewport.scale - layout.scale) < 0.002;
            })()
            """;
        await WaitForAsync(chrome.WaitUntilAsync(condition, TimeSpan.FromSeconds(20)), $"the page to show the window in {mode}");
        var layout = await chrome.EvaluateAsync("JSON.stringify(Object.assign({ pageScale: window.visualViewport.scale }, window.tabStudioWpf.layout))");
        return JsonNode.Parse(layout!.GetValue<string>())!;
    }

    /// <summary>Where an element shows in the pane, in the pane's pixels: the page's layout, zoomed and scrolled.</summary>
    private static async Task<(double Left, double Top, double Right, double Bottom)> PaneRectAsync(HeadlessChrome chrome, string element)
    {
        var rect = await chrome.EvaluateAsync($$"""
            (() => {
                const box = ({{element}}).getBoundingClientRect();
                const viewport = window.visualViewport;
                return [(box.left - viewport.offsetLeft) * viewport.scale, (box.top - viewport.offsetTop) * viewport.scale,
                        (box.right - viewport.offsetLeft) * viewport.scale, (box.bottom - viewport.offsetTop) * viewport.scale];
            })()
            """);
        return (rect![0]!.GetValue<double>(), rect[1]!.GetValue<double>(), rect[2]!.GetValue<double>(), rect[3]!.GetValue<double>());
    }

    /// <summary>The button's center in the window's own pixels, from its top left corner, which no zoom changes.</summary>
    private static async Task<(double X, double Y)> ButtonInWindowAsync(HeadlessChrome chrome)
    {
        var point = await chrome.EvaluateAsync($$"""
            (() => {
                const box = ({{ButtonElement}}).getBoundingClientRect();
                const frame = document.getElementById('tabstudio-wpf-window').getBoundingClientRect();
                return [box.left + (box.width / 2) - frame.left, box.top + (box.height / 2) - frame.top];
            })()
            """);
        return (point![0]!.GetValue<double>(), point[1]!.GetValue<double>());
    }

    /// <summary>
    /// A finger at a pixel of the pane. Chrome's touch input takes points in the page's CSS pixels of the visible area,
    /// so the pixel is divided by the browser's zoom, as Android's WebView does with a finger.
    /// </summary>
    private static async Task TapPaneAsync(HeadlessChrome chrome, double x, double y)
    {
        var scale = (await chrome.EvaluateAsync("window.visualViewport.scale"))!.GetValue<double>();
        await chrome.TapAsync(x / scale, y / scale);
    }

    private static double Number(JsonNode layout, string name) =>
        double.Parse(layout[name]!.ToJsonString(), CultureInfo.InvariantCulture);

    /// <summary>Waits for a task while the UI keeps running, as a test on the UI thread must.</summary>
    private static async Task WaitForAsync(Task task, string because)
    {
        await UiTestHelpers.WaitUntilAsync(() => task.IsCompleted, TimeSpan.FromMinutes(3), because);
        await task;
    }

    private WorkspaceViewModel CreateWorkspace(IWebViewFactory webViews)
    {
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var workspace = new WorkspaceViewModel(
            _toolchain.Compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths, webViews: webViews);
        _workspaces.Add(workspace);
        return workspace;
    }
}
