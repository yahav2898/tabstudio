using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using TabStudio.Web.Templates;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Blazor, Razor Pages and MVC in the IDE, against the real nuget.org: F5 on each template serves it and shows it in
/// the web preview, the Server Counter counts over its circuit, saving a component restarts the app on the same
/// page, saving scoped CSS reloads it without a restart, and Razor errors are in the Error List and under a squiggle.
/// Where Chrome is installed, it draws the pages in the preview, for the screenshots.
/// </summary>
public sealed class BlazorAppTests : IClassFixture<SharedToolchain>, IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);

    private readonly SharedToolchain _toolchain;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly List<WorkspaceViewModel> _workspaces = [];
    private readonly List<HeadlessChrome> _browsers = [];

    public BlazorAppTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    [AvaloniaFact]
    public async Task F5_serves_the_Blazor_Web_App_beside_its_code_counts_restarts_on_save_and_stops()
    {
        var (workspace, webViews) = await CreateWorkspaceAsync();
        var solution = new ProjectTemplates(_toolchain.Paths).Create(WebTemplates.BlazorWebAppId, "HelloBlazor");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "HelloBlazor");
        await workspace.LoadAsync(solution);
        var window = Show(workspace);
        var counter = (await workspace.OpenDocumentAsync(Path.Combine(project, "Components", "Pages", "Counter.razor")))!;

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, StartTimeout, "the Blazor Web App to start");

        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        Assert.True(workspace.IsWebPreviewSelected);
        var address = workspace.WebPreview.AppAddress!;
        Assert.Contains("<h1>Hello, world!</h1>", await _http.GetStringAsync(address), StringComparison.Ordinal);
        using (var script = await _http.GetAsync(new Uri(address, "_framework/blazor.web.js")))
        {
            Assert.True(script.IsSuccessStatusCode);
            Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        }

        // The Counter page in the preview counts the clicks over the app's circuit.
        workspace.WebPreview.AddressText = "counter";
        workspace.WebPreview.NavigateCommand.Execute(null);
        if (webViews?.WebView is { } webView)
        {
            await WaitForAsync(webView.Rendering);
            var clicks = await ClickUntilCountedAsync(webView.Chrome);
            await webView.Chrome.ClickAsync("button.btn-primary");
            await webView.Chrome.ClickAsync("button.btn-primary");
            await webView.Chrome.WaitUntilAsync($"document.querySelector('p[role=status]').textContent === 'Current count: {clicks + 2}'", TimeSpan.FromSeconds(10));
            await WaitForAsync(webView.RefreshAsync());
        }

        UiTestHelpers.SaveScreenshot(window, "blazor-01-web-app-beside-code");

        // Saving a component rebuilds the app and starts it again, on the same page at the same address.
        Replace(counter, "Current count:", "Clicks so far:");
        await workspace.SaveCommand.ExecuteAsync(null);
        await WaitForPageAsync(new Uri(address, "counter"), "Clicks so far: 0");

        Assert.Equal(address, workspace.WebPreview.AppAddress);
        Assert.EndsWith("/counter", workspace.WebPreview.CurrentPage?.AbsolutePath, StringComparison.Ordinal);
        Assert.Single(Occurrences(workspace.Output.Text, "Restarting 'HelloBlazor' with the saved changes."));

        // Saving scoped CSS rebuilds the bundle and reloads the page, without a restart.
        var layout = (await workspace.OpenDocumentAsync(Path.Combine(project, "Components", "Layout", "MainLayout.razor.css")))!;
        Replace(layout, "background-color: #f7f7f7;", "background-color: #e8f0fe;");
        await workspace.SaveCommand.ExecuteAsync(null);
        await WaitForPageAsync(new Uri(address, "HelloBlazor.styles.css"), "background-color: #e8f0fe;");
        Assert.Single(Occurrences(workspace.Output.Text, "Restarting 'HelloBlazor' with the saved changes."));

        workspace.StopDebuggingCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(workspace.WebPreview.IsRunning);
        Assert.Contains("The program 'HelloBlazor' has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        await Assert.ThrowsAsync<HttpRequestException>(() => _http.GetStringAsync(address));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Razor_error_is_in_the_Error_List_and_under_a_squiggle_on_its_line()
    {
        var (workspace, _) = await CreateWorkspaceAsync(withBrowser: false);
        var solution = new ProjectTemplates(_toolchain.Paths).Create(WebTemplates.BlazorWebAppId, "Squiggles");
        await workspace.LoadAsync(solution);
        var window = Show(workspace);
        await workspace.BuildCommand.ExecuteAsync(null);
        Assert.Equal(0, workspace.ErrorList.ErrorCount);

        var path = Path.Combine(Path.GetDirectoryName(solution)!, "Squiggles", "Components", "Pages", "Counter.razor");
        var counter = (await workspace.OpenDocumentAsync(path))!;
        Assert.True(counter.HasLanguageService);
        Assert.False(counter.UsesSemanticColoring);
        Replace(counter, "currentCount++;", "currentCount += \"one\";");
        Replace(counter, "<h1>Counter</h1>", "<h1>Counter</h1>\n<Countr />");
        counter.NavigateTo(18, 9);
        await counter.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.ErrorCount > 0 && workspace.ErrorList.WarningCount > 0, because: "the Razor file's error and warning");

        var error = Assert.Single(counter.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal(("CS0029", 18, 9), (error.Id, error.Location!.Line, error.Location.Column));
        var warning = Assert.Single(counter.Diagnostics, d => d.Id == "RZ10012");
        Assert.Equal(7, warning.Location!.Line);
        Assert.Contains(workspace.ErrorList.Items, i => i.Code == "CS0029" && i.Diagnostic.Location?.FilePath == path);
        UiTestHelpers.SaveScreenshot(window, "blazor-02-razor-error");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Typing_a_tag_in_a_Razor_file_offers_the_components_it_can_use()
    {
        var (workspace, _) = await CreateWorkspaceAsync(withBrowser: false);
        var solution = new ProjectTemplates(_toolchain.Paths).Create(WebTemplates.BlazorWebAppId, "Typing");
        await workspace.LoadAsync(solution);
        var window = Show(workspace);
        await workspace.BuildCommand.ExecuteAsync(null);
        await workspace.OpenDocumentAsync(Path.Combine(Path.GetDirectoryName(solution)!, "Typing", "Components", "Pages", "Home.razor"));
        var view = Views.EditorHarness.ActiveEditor(window);
        var completion = view.Completion!;

        Views.EditorHarness.StartLineAfter(view.Editor, "Welcome to your new app.");
        Views.EditorHarness.Type(view.Editor, "<Cou");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.SelectedItem?.Entry.DisplayText == "Counter", because: "the components");

        UiTestHelpers.SaveScreenshot(window, "blazor-05-razor-completion");

        Assert.Equal("Typing.Components.Pages", completion.SelectedItem!.Entry.InlineDescription);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(WebTemplates.RazorPagesId, "", "<h1 class=\"display-4\">Welcome</h1>")]
    [InlineData(WebTemplates.MvcId, "Home/Privacy", "<h1>Privacy Policy</h1>")]
    [InlineData("blazor-auto", "counter", "Current count: 0")]
    [InlineData(WebTemplates.BlazorWebAssemblyId, "", "_framework/blazor.webassembly.js")]
    public async Task F5_shows_each_web_template_in_the_preview(string templateId, string page, string expected)
    {
        var (workspace, webViews) = await CreateWorkspaceAsync();
        var name = "Web" + templateId.Replace("-", string.Empty, StringComparison.Ordinal);
        await workspace.LoadAsync(new ProjectTemplates(_toolchain.Paths).Create(templateId, name));
        var window = Show(workspace);

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, StartTimeout, $"{name} to start");

        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        Assert.Equal(name, workspace.WebPreview.AppName);
        var address = workspace.WebPreview.AppAddress!;
        Assert.Contains(expected, await _http.GetStringAsync(new Uri(address, page)), StringComparison.Ordinal);

        if (templateId == WebTemplates.BlazorWebAssemblyId)
        {
            Assert.Contains("runs in the web preview on .NET for WebAssembly", workspace.Output.Text, StringComparison.Ordinal);
            Assert.Contains("a WebAssembly app runs in the web preview's browser", workspace.Output.Text, StringComparison.Ordinal);
            if (webViews?.WebView is { } webView)
            {
                // .NET for WebAssembly boots in the browser and renders the app's home page.
                await WaitForAsync(webView.Rendering);
                await webView.Chrome.WaitUntilAsync("document.querySelector('h1')?.textContent === 'Hello, world!'", TimeSpan.FromSeconds(90));
                await (await workspace.OpenDocumentAsync(Path.Combine(Path.GetDirectoryName(workspace.Workspace!.Projects[0].FilePath)!, "Pages", "Home.razor")))!.AnalyzeNowAsync();
                await WaitForAsync(webView.RefreshAsync());
                UiTestHelpers.SaveScreenshot(window, "blazor-04-webassembly-app");
            }
        }

        workspace.StopDebuggingCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(workspace.WebPreview.IsRunning);
        window.Close();
    }

    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        foreach (var browser in _browsers)
        {
            browser.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        _http.Dispose();
    }

    private static Window Show(WorkspaceViewModel workspace)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        return window;
    }

    private static void Replace(DocumentViewModel document, string oldText, string newText)
    {
        var offset = document.Document.Text.IndexOf(oldText, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"'{oldText}' is not in {document.FileName}.");
        document.Document.Replace(offset, oldText.Length, newText);
    }

    private static IEnumerable<int> Occurrences(string text, string value)
    {
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            yield return index;
        }
    }

    /// <summary>Waits for a task while the UI keeps running, as a test on the UI thread must.</summary>
    private static Task WaitForAsync(Task task) =>
        UiTestHelpers.WaitUntilAsync(() => task.IsCompleted, TimeSpan.FromMinutes(2), "the preview's browser");

    private static async Task<int> ClickUntilCountedAsync(HeadlessChrome chrome)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (waited.Elapsed < TimeSpan.FromSeconds(60))
        {
            await chrome.ClickAsync("button.btn-primary");
            await Task.Delay(250);
            var status = await chrome.TextOfAsync("p[role=status]") ?? string.Empty;
            if (int.TryParse(status.Replace("Current count: ", string.Empty, StringComparison.Ordinal), out var count) && count > 0)
            {
                return count;
            }
        }

        throw new TimeoutException("The Counter never counted: its circuit did not connect.");
    }

    private async Task<(WorkspaceViewModel Workspace, ChromeWebViewFactory? WebViews)> CreateWorkspaceAsync(bool withBrowser = true)
    {
        ChromeWebViewFactory? webViews = null;
        if (withBrowser && HeadlessChrome.ExecutablePath is not null)
        {
            var chrome = await HeadlessChrome.StartAsync();
            _browsers.Add(chrome);
            webViews = new ChromeWebViewFactory(chrome);
        }

        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var workspace = new WorkspaceViewModel(
            _toolchain.Compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths, webViews: webViews);
        _workspaces.Add(workspace);
        return (workspace, webViews);
    }

    /// <summary>Waits until a page answers with some text, while the app restarts.</summary>
    private async Task WaitForPageAsync(Uri page, string expected)
    {
        string? last = null;
        Task? fetching = null;
        await UiTestHelpers.WaitUntilAsync(
            () =>
            {
                if (last?.Contains(expected, StringComparison.Ordinal) == true)
                {
                    return true;
                }

                if (fetching is null || fetching.IsCompleted)
                {
                    fetching = Fetch();
                }

                return false;
            },
            StartTimeout,
            $"{page} to answer {expected}");

        async Task Fetch()
        {
            try
            {
                last = await _http.GetStringAsync(page);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // The app is restarting.
            }
        }
    }
}
