using System.Diagnostics;
using System.Globalization;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels;
using TabStudio.Core.Workspaces;
using TabStudio.Web.Serving;

namespace TabStudio.App.Services;

/// <summary>
/// The static web site step: the Static Web Site template opens as a static site, F5 serves it from the device,
/// the web preview shows its page, and a saved style sheet applies without a reload.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The name of the static web site the self test makes.</summary>
    public const string StaticSiteName = "SelfTestSite";

    private static readonly TimeSpan StaticSiteTimeout = TimeSpan.FromSeconds(60);

    private static async Task<SelfTestResult> ShowStaticSiteAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var folder = templates.Create(ProjectTemplates.StaticSiteId, StaticSiteName);
        await main.OpenWorkspaceAsync(folder);
        if (main.Workspace is not { } workspace || workspace.StartupProject?.Kind != ProjectKind.StaticWebSite || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Static site: the template did not open as a static web site.", clock.Elapsed);
        }

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.WebPreview.IsRunning && !run.IsCompleted && clock.Elapsed < StaticSiteTimeout)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.WebPreview.AppAddress is not { } address || !workspace.WebPreview.IsRunning)
            {
                return new SelfTestResult(false, $"Static site: it was not served: {LastLines(workspace)}", clock.Elapsed);
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var page = await http.GetStringAsync(address, cancellationToken);
            if (!page.Contains($"<h1>{StaticSiteName}</h1>", StringComparison.Ordinal) || !page.Contains(StaticSiteServer.ScriptPath, StringComparison.Ordinal))
            {
                return new SelfTestResult(false, "Static site: the page came without its heading or live reload.", clock.Elapsed);
            }

            var served = string.Create(CultureInfo.InvariantCulture, $"Static site: served on {address.Authority} in {clock.Elapsed.TotalSeconds:0.0} s");
            var preview = await CheckStaticSitePreviewAsync(workspace, Path.Combine(folder, "styles.css"), cancellationToken);
            return new SelfTestResult(preview.Success, $"{served} | {preview.Message}", clock.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            return new SelfTestResult(false, $"Static site: {ex.Message} {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }

    /// <summary>The page in the web preview, and a saved style applied to it in place.</summary>
    private static async Task<SelfTestResult> CheckStaticSitePreviewAsync(WorkspaceViewModel workspace, string styles, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (workspace.WebPreview.WebViews is NoWebViewFactory)
        {
            return new SelfTestResult(true, "Static site preview: no embedded browser on this platform", clock.Elapsed);
        }

        IScriptableWebView? browser = null;
        while (clock.Elapsed < StaticSiteTimeout)
        {
            if (workspace.WebPreview.WebView is IScriptableWebView view
                && (await view.EvaluateAsync("window.__tabStudioLiveReloadReady && document.querySelector('h1') ? document.querySelector('h1').textContent : ''", cancellationToken))?.Contains(StaticSiteName, StringComparison.Ordinal) == true)
            {
                browser = view;
                break;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        if (browser is null)
        {
            return new SelfTestResult(false, "Static site preview: the web preview did not show the page.", clock.Elapsed);
        }

        // A mark on the page survives a style sheet swapped in place, and not a reload.
        await browser.EvaluateAsync("window.__tabStudioSelfTest = 'kept'", cancellationToken);
        var shown = clock.Elapsed;
        var css = await workspace.OpenDocumentAsync(styles);
        var text = css?.Document.Text ?? string.Empty;
        var at = text.IndexOf("--accent: #0e639c;", StringComparison.Ordinal);
        if (css is null || at < 0)
        {
            return new SelfTestResult(false, "Static site preview: styles.css did not open with its accent color.", clock.Elapsed);
        }

        css.Document.Replace(at, "--accent: #0e639c;".Length, "--accent: #c50f1f;");
        await workspace.SaveCommand.ExecuteAsync(null);
        var saved = clock.Elapsed;
        string? state = null;
        while (clock.Elapsed - saved < TimeSpan.FromSeconds(20))
        {
            state = await browser.EvaluateAsync("getComputedStyle(document.querySelector('h1')).color + ' ' + window.__tabStudioSelfTest", cancellationToken);
            if (state?.Contains("197, 15, 31", StringComparison.Ordinal) == true)
            {
                break;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        if (state?.Contains("197, 15, 31", StringComparison.Ordinal) != true || !state.Contains("kept", StringComparison.Ordinal))
        {
            return new SelfTestResult(false, $"Static site preview: the saved style did not apply in place ({state}).", clock.Elapsed);
        }

        var index = await workspace.OpenDocumentAsync(Path.Combine(Path.GetDirectoryName(styles)!, "index.html"));
        if (index is null)
        {
            return new SelfTestResult(false, "Static site preview: index.html could not be opened.", clock.Elapsed);
        }

        var heading = $"<h1>{StaticSiteName}</h1>";
        index.Document.Replace(index.Document.Text.IndexOf(heading, StringComparison.Ordinal), heading.Length, $"<h1>{StaticSiteName} reloaded</h1>");
        await workspace.SaveCommand.ExecuteAsync(null);
        var reload = Stopwatch.StartNew();
        while (reload.Elapsed < StaticSiteTimeout)
        {
            state = await browser.EvaluateAsync("document.querySelector('h1').textContent + ' ' + typeof window.__tabStudioSelfTest", cancellationToken);
            if (state?.Contains($"{StaticSiteName} reloaded undefined", StringComparison.Ordinal) == true)
            {
                break;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        if (state?.Contains($"{StaticSiteName} reloaded undefined", StringComparison.Ordinal) != true)
        {
            return new SelfTestResult(false, $"Static site preview: saved HTML did not reload ({state}).", clock.Elapsed);
        }

        return new SelfTestResult(
            true,
            string.Create(CultureInfo.InvariantCulture, $"Static site preview: the page showed in {shown.TotalSeconds:0.0} s, CSS applied without a reload and saved HTML reloaded in {(clock.Elapsed - saved).TotalSeconds:0.0} s"),
            clock.Elapsed);
    }
}
