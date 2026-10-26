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
                        return new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $"WPF: F5 rendered \"{WpfGreeting}\" on OpenSilver in {clock.Elapsed.TotalSeconds:0.0} s"), clock.Elapsed);
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
}
