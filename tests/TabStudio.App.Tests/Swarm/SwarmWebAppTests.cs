using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Agent;
using TabStudio.App.Swarm.Web;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// A web app on Swarm's screens, against the real nuget.org and a browser that answers as the page's screen script
/// does: the web preview is a screen while the app runs, its layout rows open the element's tag in the page's HTML,
/// the Device Matrix shows its pictures, and Fix with Agent sends Claude the HTML and a picture.
/// </summary>
public sealed class SwarmWebAppTests
{
    private const string Greet = "body/main[0]/form#form/button[0]";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task A_web_app_s_page_is_a_screen_whose_layout_rows_open_its_HTML_and_go_to_Claude_with_a_picture()
    {
        var browser = new PageBrowser();
        var claude = new FakeClaude().Text("The Greet button is 44 high now.");
        using var http = new HttpClient(claude);
        using var fixture = new WorkspaceFixture(agentHttp: http, webViews: new PageBrowsers(browser));
        new AgentSettingsStore(fixture.Paths).Save(AgentSettings.Default with { ApiKey = "sk-ant-test" });
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WebId, "Shop");
        var page = Path.Combine(Path.GetDirectoryName(solution)!, "Shop", "wwwroot", "index.html");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebScreen.IsRunning || run.IsCompleted, StartTimeout, "the page as a screen");

        Assert.True(workspace.WebScreen.IsRunning, workspace.Output.Text);
        Assert.Same(workspace.WebScreen, workspace.Screens.Screens[0]);
        Assert.Same(browser, workspace.WebScreen.WebView);
        Assert.Equal(workspace.WebPreview.CurrentPage, workspace.WebScreen.Page);
        Assert.Equal(new ScreenProfileSize(412, 800), new ScreenProfileSize(workspace.WebScreen.Profile.Width, workspace.WebScreen.Profile.Height));

        // The page's Greet button is too small for a finger: its row opens the button's tag in the page.
        await UiTestHelpers.WaitUntilAsync(() => Row(workspace) is not null, CheckTimeout, "the layout checks of the page");
        var row = Row(workspace)!;
        Assert.Equal(page, row.Diagnostic.Location!.FilePath);
        Assert.Equal(LineOf(page, "<button type=\"submit\">"), row.Diagnostic.Location.Line);
        Assert.Equal("Shop", row.Diagnostic.ProjectName);
        Assert.Equal("<button type=\"submit\">Greet</button>", row.Layout!.WebSource!.Snippet);
        Assert.Equal(WebAppScreen.WebPreviewName, Assert.Single(row.Layout.Finding.Screens).Profile.Name);

        // The Device Matrix shows the page as the pictures its browser takes.
        workspace.ShowDeviceMatrixCommand.Execute(null);
        var tile = workspace.DeviceMatrix!.Tiles.Single(t => ReferenceEquals(t.Screen, workspace.WebScreen));
        Assert.True(tile.IsPictured);
        await UiTestHelpers.WaitUntilAsync(() => tile.Snapshot is not null, CheckTimeout, "the page's picture");

        // Fix with Agent sends the problem, the button's HTML and a picture of the page.
        await UiTestHelpers.WaitUntilAsync(() => Row(workspace) is not null && workspace.LayoutChecks.Checking == 0, CheckTimeout, "the row after the checks");
        await workspace.FixWithAgentAsync(row.Diagnostic);
        var content = claude.Requests.Single()["messages"]!.AsArray()[0]!["content"]!.AsArray();
        Assert.Equal("image/png", content[0]!["source"]!["media_type"]!.GetValue<string>());
        var text = content[^1]!["text"]!.GetValue<string>();
        Assert.Contains("the web app Shop", text, StringComparison.Ordinal);
        Assert.Contains("```html\n<button type=\"submit\">Greet</button>\n```", text, StringComparison.Ordinal);
        Assert.Contains("wwwroot/index.html", text, StringComparison.Ordinal);
        Assert.Contains("min-width and min-height", text, StringComparison.Ordinal);

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(workspace.WebScreen.IsRunning);
        Assert.DoesNotContain(workspace.WebScreen, workspace.Screens.Screens);
        window.Close();
    }

    private static ErrorItemViewModel? Row(WorkspaceViewModel workspace) =>
        workspace.ErrorList.Items.FirstOrDefault(i => i.Code == "SWRM004" && i.Layout?.Finding.ElementPath == Greet);

    private static int LineOf(string file, string text) =>
        File.ReadAllLines(file).Select((line, index) => (line, index)).First(l => l.line.Contains(text, StringComparison.Ordinal)).index + 1;

    private sealed record ScreenProfileSize(double Width, double Height);

    private sealed class PageBrowsers(PageBrowser browser) : IWebViewFactory
    {
        public IWebView? Create() => browser;
    }

    /// <summary>A browser showing the web template's page, answering as its screen script does.</summary>
    private sealed class PageBrowser : IScriptableWebView
    {
        private const string Issues = """
            [{"kind":"SmallTouchTarget","severity":"Warning","path":"body/main[0]/form#form/button[0]",
              "message":"button \"Greet\" is 70 × 40 dips; on a 412-wide tablet, touch targets need at least 44 × 44 to be tapped reliably.",
              "bounds":{"x":252,"y":169,"width":70,"height":40}}]
            """;

        private Uri? _page;
        private bool _installed;
        private byte[]? _picture;

        public Control Control { get; } = new Border { Background = Brushes.Black };

        public event EventHandler<Uri>? Navigated
        {
            add { }
            remove { }
        }

        public void Navigate(Uri uri)
        {
            _page = uri;
            _installed = false;
        }

        public void Reload() => _installed = false;

        public void GoBack()
        {
        }

        public void GoForward()
        {
        }

        public Task<string?> EvaluateAsync(string script, CancellationToken cancellationToken)
        {
            if (_page is null)
            {
                return Task.FromResult<string?>(null);
            }

            if (script == WebScreenScript.Install)
            {
                _installed = true;
                return Task.FromResult<string?>("true");
            }

            string? answer = script switch
            {
                _ when script == WebScreenScript.State => _installed
                    ? $$"""{"ready":true,"url":"{{_page}}","width":412,"height":800,"scale":2,"dark":true,"rtl":false,"inputs":[]}"""
                    : "null",
                _ when script.Contains(".inspect(", StringComparison.Ordinal) => Issues,
                _ => "null",
            };
            return Task.FromResult<string?>(answer);
        }

        public Task<byte[]?> CaptureAsync(int maxPixels, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(_picture ??= Picture());

        public Task<bool> TouchAsync(Point pixel, TimeSpan hold, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> PressKeyAsync(Key key, KeyModifiers modifiers, CancellationToken cancellationToken) => Task.FromResult(true);

        /// <summary>The template's page, roughly: its dark background and its purple Greet button.</summary>
        private static byte[] Picture()
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(412, 800));
            using (var context = bitmap.CreateDrawingContext())
            {
                context.FillRectangle(new SolidColorBrush(Color.Parse("#1e1e2e")), new Rect(0, 0, 412, 800));
                context.FillRectangle(new SolidColorBrush(Color.Parse("#2a2a3c")), new Rect(20, 168, 222, 42));
                context.FillRectangle(new SolidColorBrush(Color.Parse("#7c6cf0")), new Rect(252, 169, 70, 40));
            }

            using var png = new MemoryStream();
            bitmap.Save(png);
            return png.ToArray();
        }
    }
}
