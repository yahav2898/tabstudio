using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.LanguageServers;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Web.Serving;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Static web sites: the template opens as one, F5 serves it beside its code, saving reloads the page or swaps the
/// style sheet in place, and HTML and CSS get squiggles from their language servers.
/// </summary>
public sealed class StaticSiteTests
{
    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(60);

    [AvaloniaFact]
    public async Task F5_serves_the_site_beside_its_code_and_saving_reloads_it_or_swaps_its_styles()
    {
        await using var chrome = HeadlessChrome.ExecutablePath is null ? null : await HeadlessChrome.StartAsync();
        var webViews = chrome is null ? null : new ChromeWebViewFactory(chrome);
        using var fixture = new WorkspaceFixture(webViews: webViews);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.StaticSiteId, "Portfolio");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FileName == "index.html", because: "index.html to open");

        Assert.Equal(ProjectKind.StaticWebSite, workspace.StartupProject?.Kind);
        Assert.Equal("Portfolio", workspace.RunLabel);
        Assert.Equal("Web preview", workspace.RunHostText);
        var row = Assert.Single(workspace.Compatibility, r => r.Report.Kind == ProjectKind.StaticWebSite);
        Assert.Equal(CompatibilityStatus.Ready, row.Report.Status);
        Assert.Contains(workspace.Stacks, s => s.Name == "Static web site" && s.Evidence == "index.html");

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, PageTimeout, "the site to be served");
        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        Assert.True(workspace.IsWebPreviewSelected);
        var address = workspace.WebPreview.AppAddress!;
        Assert.Equal(workspace.StaticSiteAddress, address);
        var page = await http.GetStringAsync(address);
        Assert.Contains("<h1>Portfolio</h1>", page, StringComparison.Ordinal);
        Assert.Contains(StaticSiteServer.ScriptPath, page, StringComparison.Ordinal);
        using (var stylesResponse = await http.GetAsync(new Uri(address, "styles.css")))
        {
            Assert.Equal("text/css", stylesResponse.Content.Headers.ContentType?.MediaType);
        }

        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("GET /styles.css 200", StringComparison.Ordinal), because: "the request in the Output");

        // A saved style sheet is swapped in place: the page keeps its state. Saved HTML reloads the page.
        var styles = (await workspace.OpenDocumentAsync(Path.Combine(folder, "styles.css")))!;
        if (webViews?.WebView is { } webView)
        {
            await WaitForAsync(webView.Rendering);
            await chrome!.WaitUntilAsync("document.querySelector('h1') !== null && window.__tabStudioLiveReload === true", PageTimeout);
            await chrome.EvaluateAsync("window.__kept = 'yes'");
        }

        Replace(styles, "--accent: #0e639c;", "--accent: #c50f1f;");
        await workspace.SaveCommand.ExecuteAsync(null);
        Assert.Contains("Saved /styles.css: applying the style sheet", workspace.Output.Text, StringComparison.Ordinal);
        if (chrome is not null)
        {
            await chrome.WaitUntilAsync("getComputedStyle(document.querySelector('h1')).color === 'rgb(197, 15, 31)' && window.__kept === 'yes'", PageTimeout);
        }

        var index = workspace.Documents.Single(d => d.FileName == "index.html");
        workspace.ActiveDocument = index;
        Replace(index, "<h1>Portfolio</h1>", "<h1>My portfolio</h1>");
        await workspace.SaveCommand.ExecuteAsync(null);
        Assert.Contains("Saved /index.html: reloading the page", workspace.Output.Text, StringComparison.Ordinal);
        if (chrome is not null && webViews?.WebView is { } reloaded)
        {
            await chrome.WaitUntilAsync("document.querySelector('h1') !== null && document.querySelector('h1').textContent === 'My portfolio' && window.__kept === undefined", PageTimeout);
            await WaitForAsync(reloaded.RefreshAsync());
        }

        UiTestHelpers.SaveScreenshot(window, "open-any-repo-03-static-site-beside-code");

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(workspace.WebPreview.IsRunning);
        Assert.Null(workspace.StaticSiteAddress);
        Assert.Contains("Portfolio stopped.", workspace.Output.Text, StringComparison.Ordinal);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => http.GetStringAsync(address));
        window.Close();
    }

    [RealServerAvaloniaFact("vscode-html-language-server", "vscode-css-language-server")]
    public async Task HTML_and_CSS_get_squiggles_from_their_language_servers()
    {
        using var temp = new TempDirectory();
        using var services = AppServices.Create(
            new AppPaths(temp.Resolve("app")),
            services => services.AddSingleton(new LanguageServerServices(new ToolchainServerLauncher(new HostRuntime())) { TraceMessages = true }));
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        await main.OpenWorkspaceAsync(services.GetRequiredService<ProjectTemplates>().Create(ProjectTemplates.StaticSiteId, "Portfolio"));
        var workspace = main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FileName == "index.html", because: "index.html to open");
        var html = workspace.ActiveDocument!;
        await UiTestHelpers.WaitUntilAsync(() => html.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(90), "the HTML server to start");
        Assert.Equal(LanguageServerState.Running, html.LanguageServer!.ServerState);

        // A style in the page with a property and no value: the HTML server's embedded CSS says so.
        Replace(html, "</head>", "  <style>\n    .card { color: ; }\n  </style>\n</head>");
        await UiTestHelpers.WaitUntilAsync(() => html.Diagnostics.Any(d => d.Message.Contains("expected", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromSeconds(60), "the HTML server's squiggle");
        var squiggle = html.Diagnostics.First(d => d.Message.Contains("expected", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(LineOf(html, ".card { color: ; }"), squiggle.Location?.Line);
        UiTestHelpers.SaveScreenshot(window, "open-any-repo-04-html-squiggle");

        var css = (await workspace.OpenDocumentAsync(Path.Combine(workspace.Workspace!.RootDirectory, "styles.css")))!;
        await UiTestHelpers.WaitUntilAsync(() => css.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(90), "the CSS server to start");
        Assert.Equal(LanguageServerState.Running, css.LanguageServer!.ServerState);
        css.Document.Insert(css.Document.TextLength, "footer { margin: 0\n");
        await UiTestHelpers.WaitUntilAsync(() => css.Diagnostics.Any(d => d.Message.Contains("expected", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromSeconds(60), "the CSS server's squiggle");
        Assert.Null(workspace.ServerInstallOffer);
        workspace.Dispose();
    }

    private static Task WaitForAsync(Task task) =>
        UiTestHelpers.WaitUntilAsync(() => task.IsCompleted, TimeSpan.FromMinutes(2), "the preview's browser");

    private static int LineOf(DocumentViewModel document, string text) =>
        document.Document.GetLineByOffset(document.Document.Text.IndexOf(text, StringComparison.Ordinal)).LineNumber;

    private static void Replace(DocumentViewModel document, string oldText, string newText)
    {
        var offset = document.Document.Text.IndexOf(oldText, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"'{oldText}' is not in {document.FileName}.");
        document.Document.Replace(offset, oldText.Length, newText);
    }
}
