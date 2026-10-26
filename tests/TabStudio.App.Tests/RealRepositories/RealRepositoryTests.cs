using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Tests.RealRepositories;

/// <summary>
/// Opens public .NET repositories in the real IDE, as a person would on the tablet, and builds them with
/// Ctrl+Shift+B. The repositories and their error baselines are in <c>build/ci/real-repos.json</c>, pinned to commits
/// that <c>build/ci/real-repos.py</c> clones into <c>TABSTUDIO_REAL_REPOS</c>. A build may only lower a baseline: more
/// errors fail the test, and fewer ask for the lower number to be recorded. Reports go to
/// <c>TABSTUDIO_REAL_REPOS_OUT</c> and screenshots to <c>TABSTUDIO_SCREENSHOTS</c>. Without the variables, nothing runs.
/// </summary>
public sealed class RealRepositoryTests
{
    [AvaloniaFact]
    public async Task Pinned_public_repositories_build_within_their_error_baselines()
    {
        var clones = Environment.GetEnvironmentVariable("TABSTUDIO_REAL_REPOS");
        if (string.IsNullOrEmpty(clones))
        {
            return;
        }

        var output = Environment.GetEnvironmentVariable("TABSTUDIO_REAL_REPOS_OUT") ?? Path.Combine(clones, "reports");
        Directory.CreateDirectory(output);
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "build", "ci", "real-repos.json")))!;
        var problems = new List<string>();
        var summary = new StringBuilder("| Repository | Errors | Baseline | Warnings | Build |\n| --- | --- | --- | --- | --- |\n");
        var results = new JsonObject();
        foreach (var repository in manifest["repositories"]!.AsArray())
        {
            var name = repository!["name"]!.GetValue<string>();
            var baseline = repository["maxErrors"]?.GetValue<int>();
            var solution = Path.Combine(clones, name, repository["solution"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
            var report = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"{name} at {repository["sha"]!.GetValue<string>()}");
            Outcome outcome;
            try
            {
                outcome = await OpenAndBuildAsync(solution, name, report);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"EXCEPTION: {ex}");
                outcome = new Outcome(-1, 0, TimeSpan.Zero);
            }

            await File.WriteAllTextAsync(Path.Combine(output, name + ".txt"), report.ToString());
            results[name] = new JsonObject { ["errors"] = outcome.Errors, ["warnings"] = outcome.Warnings, ["seconds"] = Math.Round(outcome.Duration.TotalSeconds, 1) };
            summary.Append(CultureInfo.InvariantCulture, $"| {name} | {outcome.Errors} | {baseline?.ToString(CultureInfo.InvariantCulture) ?? "none"} | {outcome.Warnings} | {outcome.Duration.TotalSeconds:F0} s |\n");
            if (outcome.Errors < 0)
            {
                problems.Add($"{name} did not open and build; see {name}.txt.");
            }
            else if (baseline is { } limit && outcome.Errors > limit)
            {
                problems.Add($"{name} has {outcome.Errors} errors, more than its baseline of {limit}; see {name}.txt.");
            }
            else if (baseline is { } previous && outcome.Errors < previous)
            {
                problems.Add($"{name} improved to {outcome.Errors} errors: lower its maxErrors in build/ci/real-repos.json from {previous} to {outcome.Errors}.");
            }
        }

        await File.WriteAllTextAsync(Path.Combine(output, "summary.md"), summary.ToString());
        await File.WriteAllTextAsync(Path.Combine(output, "results.json"), results.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Assert.True(problems.Count == 0, string.Join('\n', problems) + "\n\n" + summary);
    }

    private sealed record Outcome(int Errors, int Warnings, TimeSpan Duration);

    private static async Task<Outcome> OpenAndBuildAsync(string solution, string name, StringBuilder report)
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await workspace.LoadAsync(solution);
            await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(5), "the load");
            report.AppendLine(CultureInfo.InvariantCulture, $"Opened {workspace.Workspace?.Solution?.FilePath ?? solution} in {clock.Elapsed.TotalSeconds:F1} s");
            foreach (var error in workspace.Workspace?.LoadErrors ?? [])
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  load error: {error}");
            }

            report.AppendLine("Compatibility Doctor:");
            foreach (var row in workspace.Compatibility)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  {row.Project}: {row.Status} via {row.RunsVia} - {row.Explanation}");
            }

            UiTestHelpers.SaveScreenshot(window, $"real-{name}-1-opened");
            clock.Restart();
            await workspace.BuildCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(15), "the build");
            var duration = clock.Elapsed;
            var items = workspace.ErrorList.Items.ToList();
            var errors = items.Where(i => i.Diagnostic.Severity == DiagnosticSeverityLevel.Error).ToList();
            var warnings = items.Count(i => i.Diagnostic.Severity == DiagnosticSeverityLevel.Warning);
            report.AppendLine(CultureInfo.InvariantCulture, $"Built in {duration.TotalSeconds:F1} s: {errors.Count} errors, {warnings} warnings");
            foreach (var group in errors.GroupBy(e => e.Code).OrderByDescending(g => g.Count()))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  {group.Count()} x {group.Key}");
                foreach (var error in group.Take(5))
                {
                    report.AppendLine(CultureInfo.InvariantCulture, $"      [{error.Diagnostic.ProjectName}] {error.Diagnostic}");
                }
            }

            foreach (var warning in items.Where(i => i.Diagnostic.Id is "CS8032" or "CS8034" or "CS8784" or "CS8785" or "CS9057"))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  generator: [{warning.Diagnostic.ProjectName}] {warning.Diagnostic}");
            }

            UiTestHelpers.SaveScreenshot(window, $"real-{name}-2-built");
            report.AppendLine("Output:");
            var text = workspace.Output.Text;
            report.AppendLine(text.Length > 40000 ? text[^40000..] : text);
            return new Outcome(errors.Count, warnings, duration);
        }
        finally
        {
            window.Close();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TabStudio.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("The repository root, with TabStudio.slnx, was not found above the test.");
    }
}
