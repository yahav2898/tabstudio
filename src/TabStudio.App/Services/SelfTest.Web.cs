using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The self test's web part: ASP.NET Core restored, built and served on the device.</summary>
public static partial class SelfTest
{
    /// <summary>The web project the self test creates.</summary>
    public const string WebAppName = "SelfTestWeb";

    /// <summary>
    /// Creates a web app from the template, presses Ctrl+F5, asks the running app for its page and its API, and
    /// stops it: ASP.NET Core restored from NuGet, compiled and served on the device's own runtime.
    /// </summary>
    /// <returns>Whether the app answered, with a one-line summary.</returns>
    private static async Task<SelfTestResult> ServeWebAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.WebId, WebAppName));
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Web: the web app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        while (!workspace.WebPreview.IsRunning && !run.IsCompleted)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }

        if (workspace.WebPreview.AppAddress is not { } address || !workspace.WebPreview.IsRunning)
        {
            return new SelfTestResult(false, $"Web: the app did not start: {LastLines(workspace)}", clock.Elapsed);
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var page = await http.GetStringAsync(address, cancellationToken);
            var api = await http.GetStringAsync(new Uri(address, $"api/greeting?name={Name}"), cancellationToken);
            var served = page.Contains($"<h1>{WebAppName}</h1>", StringComparison.Ordinal)
                && api.Contains($"Hello, {Name}!", StringComparison.Ordinal);
            var message = served
                ? string.Create(CultureInfo.InvariantCulture, $"Web: served on {address.Authority} in {clock.Elapsed.TotalSeconds:0.0} s, the API said \"Hello, {Name}!\"")
                : $"Web: unexpected answers: {api}";
            if (served && !SkipWebScreenForExperiment)
            {
                var screen = await CheckWebScreenAsync(workspace, cancellationToken);
                return new SelfTestResult(screen.Success, $"{message} | {screen.Message}", clock.Elapsed);
            }

            return new SelfTestResult(served, message, clock.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            return new SelfTestResult(false, $"Web: {ex.Message} {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }

    // TEMPORARY: finds out whether the web screen part changes what desktop mode's part sees.
    private static readonly bool SkipWebScreenForExperiment = true;

    private static string LastLines(WorkspaceViewModel workspace) =>
        string.Join(" | ", workspace.Output.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(6));
}
