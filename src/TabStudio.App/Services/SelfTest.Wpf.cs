using System.Diagnostics;
using System.Globalization;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The WPF self test: the template built on OpenSilver and its rendered text read from the WebView.</summary>
public static partial class SelfTest
{
    /// <summary>The WPF project the self test creates.</summary>
    public const string WpfAppName = "SelfTestWpf";

    /// <summary>The text that proves the compiled XAML rendered, rather than only the browser host's page loading.</summary>
    private const string WpfGreeting = "Hello from WPF on the tablet";

    /// <summary>Creates the WPF template, adds a greeting, presses F5 and waits for the actual page's text.</summary>
    private static async Task<SelfTestResult> ShowWpfAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var solution = templates.Create(ProjectTemplates.WpfId, WpfAppName);
        var xamlPath = Path.Combine(Path.GetDirectoryName(solution)!, WpfAppName, "MainWindow.xaml");
        var xaml = await File.ReadAllTextAsync(xamlPath, cancellationToken);
        await File.WriteAllTextAsync(xamlPath, xaml.Replace("<Grid>", $"<Grid><TextBlock Text=\"{WpfGreeting}\" />", StringComparison.Ordinal), cancellationToken);
        await main.OpenWorkspaceAsync(solution);
        if (main.Workspace is not { } workspace || !workspace.StartDebuggingCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "WPF: the template did not open.", clock.Elapsed);
        }

        if (workspace.WebPreview.WebViews is NoWebViewFactory)
        {
            return new SelfTestResult(true, "WPF: no embedded browser on this platform", clock.Elapsed);
        }

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.WebPreview.IsRunning && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (!workspace.WebPreview.IsRunning)
            {
                return new SelfTestResult(false, $"WPF: the app did not start: {LastLines(workspace)}", clock.Elapsed);
            }

            var deadline = clock.Elapsed + WebScreenTimeout;
            string? page = null;
            while (clock.Elapsed < deadline && !run.IsCompleted)
            {
                if (workspace.WebPreview.WebView is IScriptableWebView browser)
                {
                    page = await browser.EvaluateAsync("document.body && document.body.innerText", cancellationToken);
                    if (page?.Contains(WpfGreeting, StringComparison.Ordinal) == true)
                    {
                        var fit = await WpfWindowFitsAsync(browser, cancellationToken);
                        return fit.Fits
                            ? new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $"WPF: F5 rendered \"{WpfGreeting}\" on OpenSilver, the whole window in Fit ({fit.Detail}), in {clock.Elapsed.TotalSeconds:0.0} s"), clock.Elapsed)
                            : new SelfTestResult(false, $"WPF: the window is not whole in Fit: {fit.Detail}", clock.Elapsed);
                    }
                }

                await Task.Delay(PollInterval, cancellationToken);
            }

            return new SelfTestResult(false, $"WPF: the WebView did not render the greeting: {page ?? "no page text"} | {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Whether the template's window (800 by 450, wider than the side bar) shows whole in Fit, the default: the page
    /// zoomed out to the scale it chose, and the window's right and bottom edges inside the pane. Waits up to two seconds
    /// for the WebView to apply the zoom.
    /// </summary>
    private static async Task<(bool Fits, string Detail)> WpfWindowFitsAsync(IScriptableWebView browser, CancellationToken cancellationToken)
    {
        const string Measure = """
            (() => {
                const layout = window.tabStudioWpf && window.tabStudioWpf.layout;
                const frame = document.getElementById('tabstudio-wpf-window');
                const viewport = window.visualViewport;
                if (!layout || !frame || !viewport) { return 'no layout'; }
                const box = frame.getBoundingClientRect();
                const right = (box.right - viewport.offsetLeft) * viewport.scale;
                const bottom = (box.bottom - viewport.offsetTop) * viewport.scale;
                const fits = layout.mode === 'fit' && !layout.fills && Math.abs(viewport.scale - layout.scale) < 0.01
                    && box.left >= viewport.offsetLeft - 0.5 && right <= layout.paneWidth + 1 && bottom <= layout.paneHeight + 1;
                return (fits ? 'fits' : 'clipped') + ': scale ' + viewport.scale.toFixed(3) + ' of ' + layout.scale + ', window '
                    + layout.windowWidth + 'x' + layout.windowHeight + ' ends at ' + Math.round(right) + ',' + Math.round(bottom)
                    + ' in a ' + Math.round(layout.paneWidth) + 'x' + Math.round(layout.paneHeight) + ' pane';
            })()
            """;
        var detail = "no answer";
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(2))
        {
            detail = (await browser.EvaluateAsync(Measure, cancellationToken))?.Trim('"') ?? "no page";
            if (detail.StartsWith("fits", StringComparison.Ordinal))
            {
                return (true, detail);
            }

            await Task.Delay(200, cancellationToken);
        }

        return (false, detail);
    }
}
