using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Tests.Trial;

/// <summary>
/// Opens real repositories from GitHub in the IDE as a person would (TABSTUDIO_TRIAL_REPOS, separated by ';', each a path or name=path),
/// builds them with Ctrl+Shift+B and writes what happened to TABSTUDIO_TRIAL_OUT. A report, not an assertion.
/// </summary>
public sealed class RepoTrialTests
{
    [AvaloniaFact]
    public async Task Open_and_build_real_repositories()
    {
        var repos = Environment.GetEnvironmentVariable("TABSTUDIO_TRIAL_REPOS");
        var output = Environment.GetEnvironmentVariable("TABSTUDIO_TRIAL_OUT");
        if (string.IsNullOrEmpty(repos) || string.IsNullOrEmpty(output))
        {
            return;
        }

        Directory.CreateDirectory(output);
        foreach (var repo in repos.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // "name=path" names the report; a bare path is named after its folder.
            var (name, path) = repo.Split('=', 2) is [var named, var at] ? (named, at) : (Path.GetFileName(repo), repo);
            var report = new StringBuilder();
            try
            {
                await TryAsync(path, name, report);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"EXCEPTION: {ex}");
            }

            await File.WriteAllTextAsync(Path.Combine(output, name + ".txt"), report.ToString());
        }
    }

    private static async Task TryAsync(string repo, string name, StringBuilder report)
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await workspace.LoadAsync(repo);
            await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(5), "the load");
            report.AppendLine(CultureInfo.InvariantCulture, $"Opened in {clock.Elapsed.TotalSeconds:F1} s");
            report.AppendLine(CultureInfo.InvariantCulture, $"Workspace: {workspace.Workspace?.Name}, solution: {workspace.Workspace?.Solution?.FilePath ?? "(none)"}");
            if (workspace.Workspace is { } loaded)
            {
                foreach (var error in loaded.LoadErrors)
                {
                    report.AppendLine(CultureInfo.InvariantCulture, $"  load error: {error}");
                }

                foreach (var project in loaded.Projects)
                {
                    report.AppendLine(CultureInfo.InvariantCulture, $"  project {project.Name} [{project.Language}] {project.FilePath}");
                }
            }

            report.AppendLine("Compatibility Doctor:");
            foreach (var row in workspace.Compatibility)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  {row.Project}: {row.Status} via {row.RunsVia} - {row.Explanation}");
            }

            UiTestHelpers.SaveScreenshot(window, $"trial-{name}-1-opened");

            clock.Restart();
            await workspace.BuildCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(10), "the build");
            report.AppendLine(CultureInfo.InvariantCulture, $"Built in {clock.Elapsed.TotalSeconds:F1} s");
            var errors = workspace.ErrorList.Items.Where(i => i.Diagnostic.Severity == DiagnosticSeverityLevel.Error).ToList();
            report.AppendLine(CultureInfo.InvariantCulture, $"Errors: {errors.Count}, warnings: {workspace.ErrorList.Items.Count(i => i.Diagnostic.Severity == DiagnosticSeverityLevel.Warning)}");
            foreach (var group in errors.GroupBy(e => e.Code).OrderByDescending(g => g.Count()).Take(15))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  {group.Count()} x {group.Key}: {group.First().Diagnostic}");
            }

            UiTestHelpers.SaveScreenshot(window, $"trial-{name}-2-built");
            report.AppendLine("Output:");
            report.AppendLine(workspace.Output.Text.Length > 12000 ? workspace.Output.Text[^12000..] : workspace.Output.Text);
        }
        finally
        {
            window.Close();
        }
    }
}
