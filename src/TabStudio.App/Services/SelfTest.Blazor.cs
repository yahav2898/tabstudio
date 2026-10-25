using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels;
using TabStudio.Web.Serving;
using TabStudio.Web.Templates;
using TabStudio.Web.WebAssembly;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's Blazor part: a Blazor WebAssembly app served to the web preview with .NET for WebAssembly, then a
/// Blazor Web App served on the device, left running beside its code.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The Blazor WebAssembly app the self test creates.</summary>
    public const string BlazorWebAssemblyAppName = "SelfTestWasm";

    /// <summary>The Blazor Web App the self test creates.</summary>
    public const string BlazorWebAppName = "SelfTestBlazor";

    /// <summary>The oldest Chrome, and so Android System WebView, that runs .NET 10 WebAssembly: it needs SIMD and exception handling.</summary>
    private const int WebAssemblyChromeVersion = 95;

    /// <summary>How long the preview's browser may take to download .NET for WebAssembly and start the app.</summary>
    private static readonly TimeSpan WebAssemblyBootTimeout = TimeSpan.FromSeconds(120);

    /// <summary>Serves both Blazor apps; the Blazor Web App stays running when everything worked.</summary>
    private static async Task<SelfTestResult> ServeBlazorAppsAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var webAssembly = await ServeBlazorWebAssemblyAppAsync(main, templates, cancellationToken);
        if (!webAssembly.Success)
        {
            return webAssembly;
        }

        var webApp = await ServeBlazorWebAppAsync(main, templates, cancellationToken);
        return new SelfTestResult(webApp.Success, $"{webAssembly.Message} | {webApp.Message}", clock.Elapsed);
    }

    /// <summary>
    /// Creates a Blazor WebAssembly app from the template, presses Ctrl+F5 and asks the host for its boot files. When
    /// the web preview has a browser recent enough for .NET 10 WebAssembly, opens the app's Weather page in it and
    /// waits until the app, running in the browser, asks for its forecast. Then stops it.
    /// </summary>
    private static async Task<SelfTestResult> ServeBlazorWebAssemblyAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(WebTemplates.BlazorWebAssemblyId, BlazorWebAssemblyAppName));
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "WebAssembly: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.WebPreview.IsRunning && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.WebPreview.AppAddress is not { } address || !workspace.WebPreview.IsRunning || workspace.RunningWebAssemblyApp is not { } host)
            {
                return new SelfTestResult(false, $"WebAssembly: the app was not served: {LastLines(workspace)}", clock.Elapsed);
            }

            var served = clock.Elapsed;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var page = await http.GetStringAsync(address, cancellationToken);
            var loader = await http.GetStringAsync(new Uri(address, "_framework/dotnet.js"), cancellationToken);
            using var runtime = await http.GetAsync(new Uri(address, "_framework/dotnet.native.wasm"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            using var assembly = await http.GetAsync(new Uri(address, $"_framework/{BlazorWebAssemblyAppName}.dll"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!page.Contains("_framework/blazor.webassembly.js", StringComparison.Ordinal)
                || !loader.Contains($"\"mainAssemblyName\": \"{BlazorWebAssemblyAppName}\"", StringComparison.Ordinal)
                || runtime.Content.Headers.ContentType?.MediaType != "application/wasm"
                || !assembly.IsSuccessStatusCode)
            {
                return new SelfTestResult(false, $"WebAssembly: unexpected boot files: {runtime.Content.Headers.ContentType}, {assembly.StatusCode}", clock.Elapsed);
            }

            var message = string.Create(CultureInfo.InvariantCulture, $"WebAssembly: served on {address.Authority} in {served.TotalSeconds:0.0} s with its boot files");
            var ran = await RunInPreviewAsync(workspace, host, cancellationToken);
            return new SelfTestResult(ran.Success, $"{message}{ran.Message}", clock.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            return new SelfTestResult(false, $"WebAssembly: {ex.Message} {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Opens the Weather page in the preview's browser and waits until the app asks for its forecast: proof that the
    /// browser started .NET for WebAssembly, ran the app and rendered the page. Skipped without a browser, and noted
    /// with one older than .NET 10 WebAssembly needs.
    /// </summary>
    private static async Task<SelfTestResult> RunInPreviewAsync(WorkspaceViewModel workspace, WebAssemblyAppHost host, CancellationToken cancellationToken)
    {
        if (workspace.WebPreview.WebViews is NoWebViewFactory)
        {
            return new SelfTestResult(true, string.Empty, TimeSpan.Zero);
        }

        var clock = Stopwatch.StartNew();
        string? browser = null;
        void Watch(object? sender, ServedRequest request) => browser ??= request.UserAgent;
        host.RequestServed += Watch;
        try
        {
            workspace.WebPreview.AddressText = "weather";
            workspace.WebPreview.NavigateCommand.Execute(null);
            while (!host.HasServed("sample-data/weather.json") && clock.Elapsed < WebAssemblyBootTimeout)
            {
                if (ChromeVersion(browser) is { } old && old < WebAssemblyChromeVersion)
                {
                    return new SelfTestResult(true, $", not run in the preview: its WebView is Chrome {old}, and .NET 10 WebAssembly needs {WebAssemblyChromeVersion} or later", clock.Elapsed);
                }

                await Task.Delay(PollInterval, cancellationToken);
            }
        }
        finally
        {
            host.RequestServed -= Watch;
        }

        return host.HasServed("sample-data/weather.json")
            ? new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $", run in the preview in {clock.Elapsed.TotalSeconds:0.0} s: its Weather page fetched its forecast"), clock.Elapsed)
            : new SelfTestResult(false, $", but the preview did not run it ({browser ?? "no request from the WebView"})", clock.Elapsed);
    }

    /// <summary>
    /// Creates a Blazor Web App from the template, presses Ctrl+F5, asks it for its pages and Blazor's script, and
    /// leaves it running, with its Counter page in the editor beside the preview.
    /// </summary>
    private static async Task<SelfTestResult> ServeBlazorWebAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var solution = templates.Create(WebTemplates.BlazorWebAppId, BlazorWebAppName);
        await main.OpenWorkspaceAsync(solution);
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Blazor: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        var keepRunning = false;
        try
        {
            while (!workspace.WebPreview.IsRunning && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.WebPreview.AppAddress is not { } address || !workspace.WebPreview.IsRunning)
            {
                return new SelfTestResult(false, $"Blazor: the app did not start: {LastLines(workspace)}", clock.Elapsed);
            }

            var started = clock.Elapsed;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var home = await http.GetStringAsync(address, cancellationToken);
            var counter = await http.GetStringAsync(new Uri(address, "counter"), cancellationToken);
            using var script = await http.GetAsync(new Uri(address, "_framework/blazor.web.js"), cancellationToken);
            var scriptLength = (await script.Content.ReadAsByteArrayAsync(cancellationToken)).Length;
            if (!home.Contains("Hello, world!", StringComparison.Ordinal)
                || !home.Contains("_framework/blazor.web.js", StringComparison.Ordinal)
                || !counter.Contains("Current count: 0", StringComparison.Ordinal)
                || script.Content.Headers.ContentType?.MediaType != "text/javascript"
                || scriptLength < 100_000)
            {
                return new SelfTestResult(false, $"Blazor: unexpected answers: {script.StatusCode} {scriptLength} bytes, {Excerpt(home)}", clock.Elapsed);
            }

            // For the screenshot: the Counter page's code beside the running page.
            var project = Path.Combine(Path.GetDirectoryName(solution)!, BlazorWebAppName);
            await workspace.OpenDocumentAsync(Path.Combine(project, "Components", "Pages", "Counter.razor"));
            workspace.WebPreview.AddressText = "counter";
            workspace.WebPreview.NavigateCommand.Execute(null);
            keepRunning = true;
            return new SelfTestResult(
                true,
                string.Create(CultureInfo.InvariantCulture, $"Blazor: served on {address.Authority} in {started.TotalSeconds:0.0} s with blazor.web.js ({scriptLength / 1024} KB), left running"),
                clock.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            return new SelfTestResult(false, $"Blazor: {ex.Message} {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            if (!keepRunning)
            {
                workspace.StopCommand.Execute(null);
                await run.WaitAsync(cancellationToken);
            }
        }
    }

    /// <summary>The major version of Chrome in a user agent, such as 74 in <c>Chrome/74.0.3729.186</c>.</summary>
    private static int? ChromeVersion(string? userAgent) =>
        userAgent is not null && ChromeVersionPattern().Match(userAgent) is { Success: true } match
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : null;

    private static string Excerpt(string html) =>
        html.Length <= 200 ? html.ReplaceLineEndings(" ") : html[..200].ReplaceLineEndings(" ") + "…";

    [GeneratedRegex(@"Chrome/(\d+)")]
    private static partial Regex ChromeVersionPattern();
}
