using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Progress;

namespace TabStudio.App.Tests.Trial;

/// <summary>
/// Opens real repositories in the IDE as a person would and writes down what happened: the repositories of one area of
/// <c>build/ci/trial-sweep.json</c> (TABSTUDIO_TRIAL_AREA), cloned under TABSTUDIO_TRIAL_ROOT, with reports, a summary
/// and results.json in TABSTUDIO_TRIAL_OUT. For each one: the setup and restore, the Error List once analysis is stable
/// and how long that took, the build, the startup project F5 picks, whether the app runs (a web app answers its first
/// route, a console app exits, a GUI app shows a window), what the Compatibility Doctor says, and every stage the
/// long-operation progress UI showed. A report, not an assertion.
/// </summary>
public sealed partial class RepoTrialTests
{
    private static readonly TimeSpan StableFor = TimeSpan.FromSeconds(5);

    [AvaloniaFact]
    public async Task Open_set_up_build_and_run_real_repositories()
    {
        var root = Environment.GetEnvironmentVariable("TABSTUDIO_TRIAL_ROOT");
        var output = Environment.GetEnvironmentVariable("TABSTUDIO_TRIAL_OUT");
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(output))
        {
            return;
        }

        var area = Environment.GetEnvironmentVariable("TABSTUDIO_TRIAL_AREA");
        Directory.CreateDirectory(output);
        var plan = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "build", "ci", "trial-sweep.json")))!;
        var results = new JsonArray();
        var summary = new StringBuilder("| Repository | Opened | F5 startup | Setup | Analysis stable | Errors | Build | Run | Doctor |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var entry in plan["repositories"]!.AsArray().Where(e => string.IsNullOrEmpty(area) || e!["area"]!.GetValue<string>() == area))
        {
            var name = entry!["name"]!.GetValue<string>();
            var folder = Path.GetFullPath(Path.Combine(root, name, entry["open"]!.GetValue<string>()));
            var result = new JsonObject
            {
                ["name"] = name,
                ["area"] = entry["area"]!.GetValue<string>(),
                ["sha"] = entry["sha"]!.GetValue<string>(),
                ["expect"] = entry["expect"]!.GetValue<string>(),
            };
            var report = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"{name} at {result["sha"]}, opening {folder}");
            try
            {
                // What the README asks a person to create first: its .env, and settings files copied from examples.
                if (entry["dotenv"] is JsonObject dotenv)
                {
                    await File.WriteAllLinesAsync(Path.Combine(folder, ".env"), dotenv.Select(pair => $"{pair.Key}={pair.Value!.GetValue<string>()}"));
                }

                foreach (var (file, content) in entry["files"]?.AsObject() ?? new JsonObject())
                {
                    var path = Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, content!.GetValue<string>());
                }

                var route = entry["route"]?.GetValue<string>() ?? "/";
                await TryAsync(folder, name, result["expect"]!.GetValue<string>(), route, report, result);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"EXCEPTION: {ex}");
                result["exception"] = $"{ex.GetType().Name}: {ex.Message}";
            }

            await File.WriteAllTextAsync(Path.Combine(output, name + ".txt"), report.ToString());
            results.Add(result);
            summary.Append(CultureInfo.InvariantCulture,
                $"| {name} | {Cell(result, "opened")} | {Cell(result, "startup")} | {Cell(result, "setup")} | {Cell(result, "analysis")} | {Cell(result, "errors")} | {Cell(result, "build")} | {Cell(result, "run")} | {Cell(result, "doctorSummary")} |\n");
        }

        await File.WriteAllTextAsync(Path.Combine(output, "summary.md"), summary.ToString());
        await File.WriteAllTextAsync(Path.Combine(output, "results.json"), results.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Cell(JsonObject result, string key) =>
        result["exception"] is { } exception && result[key] is null ? "exception: " + exception.GetValue<string>().Replace('|', '/').ReplaceLineEndings(" ")
        : (result[key]?.ToString() ?? "-").Replace('|', '/').ReplaceLineEndings(" ");

    private static async Task TryAsync(string folder, string name, string expect, string route, StringBuilder report, JsonObject result)
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;

        // Every stage the long-operation progress UI was given, in order, and how each operation ended.
        var stages = new List<string>();
        void OnChanged(object? sender, TrackedOperation operation)
        {
            lock (stages)
            {
                var line = operation.IsCompleted
                    ? $"{operation.Title}: {operation.Outcome} after {operation.Elapsed.TotalSeconds:F1} s"
                    : $"{operation.Title}: {operation.Progress.Text}";
                if (stages.Count == 0 || stages[^1] != line)
                {
                    stages.Add(line);
                }
            }
        }

        workspace.Operations.Tracker.Changed += OnChanged;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        Task? run = null;
        try
        {
            // Open.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await workspace.LoadAsync(folder);
            var opened = await WaitAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(10));
            result["opened"] = opened ? $"{clock.Elapsed.TotalSeconds:F1} s" : $"still busy after {clock.Elapsed.TotalSeconds:F0} s";
            report.AppendLine(CultureInfo.InvariantCulture, $"Opened in {clock.Elapsed.TotalSeconds:F1} s: {workspace.Workspace?.Name}, solution {workspace.Workspace?.Solution?.FilePath ?? "(none)"}");
            var loadErrors = workspace.Workspace?.LoadErrors.Select(e => e.ToString()).ToList() ?? [];
            result["loadErrors"] = Strings(loadErrors);
            foreach (var error in loadErrors)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  load error: {error}");
            }

            foreach (var project in workspace.Workspace?.Projects ?? [])
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  project {project.Name} [{project.Language}] {project.FilePath}");
            }

            foreach (var project in workspace.ToolchainProjects)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  toolchain project {project.Toolchain.Id}: {project.Project.MarkerPath ?? project.Project.Directory}");
            }

            // The Compatibility Doctor.
            var doctor = workspace.Compatibility.Select(row => $"{row.Project}: {row.Status} via {row.RunsVia} - {row.Explanation}").ToList();
            result["doctor"] = Strings(doctor);
            result["doctorSummary"] = string.Join(", ", workspace.Compatibility.GroupBy(row => row.Status.ToString()).Select(g => $"{g.Count()} {g.Key}"));
            report.AppendLine("Compatibility Doctor:");
            foreach (var row in doctor)
            {
                report.AppendLine("  " + row);
            }

            // The startup project F5 picks by itself.
            var toolchainStartup = workspace.ToolchainProjects.FirstOrDefault(p => p.Info.FilePath == workspace.StartupProject?.FilePath);
            result["startup"] = workspace.StartupProject is { } startup
                ? toolchainStartup is null ? startup.Name : $"{startup.Name} ({toolchainStartup.Toolchain.Id})"
                : "none";
            report.AppendLine(CultureInfo.InvariantCulture, $"F5 startup project: {result["startup"]} {workspace.StartupProject?.FilePath}");
            UiTestHelpers.SaveScreenshot(window, $"trial-{name}-1-opened");

            // Setup (toolchain projects) and restore (NuGet), then the Error List once analysis is stable.
            clock.Restart();
            if (workspace.ToolchainProjects.Any())
            {
                var setUp = await WaitAsync(() => workspace.ProjectSetupTask.IsCompleted && !workspace.IsSettingUpProjects, TimeSpan.FromMinutes(25));
                result["setup"] = !setUp ? $"not done after {clock.Elapsed.TotalSeconds:F0} s"
                    : workspace.ProjectSetupTask.IsFaulted ? "failed: " + workspace.ProjectSetupTask.Exception?.GetBaseException().Message
                    : workspace.ToolchainNotice is { } notice ? $"notice: {notice.Title}: {notice.Message}"
                    : $"ok in {clock.Elapsed.TotalSeconds:F0} s";
            }

            var stable = await WaitForStableErrorListAsync(workspace, TimeSpan.FromMinutes(20));
            result["analysis"] = stable ? $"{clock.Elapsed.TotalSeconds:F1} s" : $"not stable after {clock.Elapsed.TotalSeconds:F0} s";
            result["setup"] ??= Ended(stages, "Restor") ?? "nothing to set up";
            var analyzed = Count(workspace, report, "Error List after analysis");
            result["errors"] = $"{analyzed.Errors} errors, {analyzed.Warnings} warnings";
            result["errorCodes"] = analyzed.Codes;
            UiTestHelpers.SaveScreenshot(window, $"trial-{name}-2-analyzed");

            // Build.
            clock.Restart();
            await workspace.BuildCommand.ExecuteAsync(null);
            var builtInTime = await WaitAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(25));
            var built = Count(workspace, report, "Error List after the build");
            result["build"] = builtInTime ? $"{built.Errors} errors in {clock.Elapsed.TotalSeconds:F0} s" : $"still building after {clock.Elapsed.TotalSeconds:F0} s";
            result["buildErrorCodes"] = built.Codes;
            UiTestHelpers.SaveScreenshot(window, $"trial-{name}-3-built");

            // Run.
            if (expect is "build" or "explained")
            {
                result["run"] = expect == "explained" ? "not run: the Doctor should explain" : "not run: build only";
            }
            else
            {
                clock.Restart();
                run = workspace.RunCommand.ExecuteAsync(null);
                result["run"] = expect switch
                {
                    "web-first-route" => await FirstRouteAsync(workspace, run, route, report),
                    "gui-window" => await WindowAsync(workspace, run),
                    _ => await ExitAsync(workspace, run),
                };
                report.AppendLine(CultureInfo.InvariantCulture, $"Run: {result["run"]} ({clock.Elapsed.TotalSeconds:F0} s after F5)");
                UiTestHelpers.SaveScreenshot(window, $"trial-{name}-4-run");
            }

            lock (stages)
            {
                result["stages"] = Strings(stages);
                report.AppendLine("Long-operation progress:");
                foreach (var stage in stages)
                {
                    report.AppendLine("  " + stage);
                }
            }

            Tail(report, "Project setup", workspace.ProjectSetupOutput.Text);
            Tail(report, "Output", workspace.Output.Text);
        }
        finally
        {
            workspace.Operations.Tracker.Changed -= OnChanged;
            if (workspace.StopCommand.CanExecute(null))
            {
                workspace.StopCommand.Execute(null);
            }

            if (run is not null)
            {
                await Task.WhenAny(run, Task.Delay(TimeSpan.FromMinutes(1)));
            }

            window.Close();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Processes UI work until a condition holds; <see langword="false"/> when it did not within the time.</summary>
    private static async Task<bool> WaitAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(50);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return true;
    }

    private static JsonArray Strings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    /// <summary>Waits until nothing is restoring or analyzing and the Error List has kept the same items for a few seconds.</summary>
    private static async Task<bool> WaitForStableErrorListAsync(WorkspaceViewModel workspace, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var count = -1;
        var since = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var busy = workspace.ErrorList.IsPending || workspace.Operations.HasOperations || workspace.State == IdeState.Busy;
            var now = workspace.ErrorList.Items.Count;
            if (busy || now != count)
            {
                count = busy ? -1 : now;
                since = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - since >= StableFor)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private sealed record Counts(int Errors, int Warnings, JsonArray Codes);

    private static Counts Count(WorkspaceViewModel workspace, StringBuilder report, string title)
    {
        var items = workspace.ErrorList.Items.ToList();
        var errors = items.Where(i => i.Diagnostic.Severity == DiagnosticSeverityLevel.Error).ToList();
        var warnings = items.Count(i => i.Diagnostic.Severity == DiagnosticSeverityLevel.Warning);
        report.AppendLine(CultureInfo.InvariantCulture, $"{title}: {errors.Count} errors, {warnings} warnings");
        var codes = new JsonArray();
        foreach (var group in errors.GroupBy(e => e.Code).OrderByDescending(g => g.Count()))
        {
            codes.Add($"{group.Count()} x {group.Key}: {group.First().Diagnostic}");
            report.AppendLine(CultureInfo.InvariantCulture, $"  {group.Count()} x {group.Key}");
            foreach (var error in group.Take(10))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"      [{error.Diagnostic.ProjectName}] {error.Diagnostic}");
            }
        }

        return new Counts(errors.Count, warnings, codes);
    }

    private static async Task<string> FirstRouteAsync(WorkspaceViewModel workspace, Task run, string route, StringBuilder report)
    {
        await WaitAsync(() => workspace.WebPreview.IsRunning || workspace.ServerNotice is not null || run.IsCompleted, TimeSpan.FromMinutes(15));
        if (!workspace.WebPreview.IsRunning && workspace.ServerNotice is { } server)
        {
            server.OpenPreview.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        if (!workspace.WebPreview.IsRunning || workspace.WebPreview.AppAddress is not { } preview)
        {
            return run.IsCompleted ? $"exited before listening{ExitCode(workspace.Output.Text)}" : "did not listen";
        }

        var address = new Uri(preview, route);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(address);
                var body = await response.Content.ReadAsStringAsync();
                report.AppendLine(CultureInfo.InvariantCulture, $"GET {address}: {(int)response.StatusCode}, {body.Length} characters");
                return $"GET {route} {(int)response.StatusCode}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return run.IsCompleted ? $"listened, then exited{ExitCode(workspace.Output.Text)}" : $"listened, but {route} did not answer: {ex.Message}";
            }
        }
    }

    private static async Task<string> WindowAsync(WorkspaceViewModel workspace, Task run)
    {
        await WaitAsync(() => (workspace.AppPreview.IsRunning && workspace.AppPreview.View is not null) || run.IsCompleted, TimeSpan.FromMinutes(10));
        return workspace.AppPreview is { IsRunning: true, View: not null } preview ? $"window shown: {preview.AppName}"
            : $"no window{ExitCode(workspace.Output.Text)}";
    }

    private static async Task<string> ExitAsync(WorkspaceViewModel workspace, Task run)
    {
        await WaitAsync(() => run.IsCompleted || workspace.WebPreview.IsRunning, TimeSpan.FromMinutes(5));
        return run.IsCompleted ? $"exited{ExitCode(workspace.Output.Text)}"
            : workspace.WebPreview.IsRunning ? "listening (a web app)"
            : "still running after 5 min (interactive?)";
    }

    private static string ExitCode(string output) =>
        ExitCodePattern().Matches(output) is { Count: > 0 } matches ? " with code " + matches[^1].Groups[1].Value : string.Empty;

    [GeneratedRegex(@"has exited with code (-?\d+)")]
    private static partial Regex ExitCodePattern();

    /// <summary>How the last operation whose title starts with a word ended, if one ran.</summary>
    private static string? Ended(List<string> stages, string title)
    {
        lock (stages)
        {
            return stages.LastOrDefault(s => s.StartsWith(title, StringComparison.Ordinal) && s.Contains(" after ", StringComparison.Ordinal));
        }
    }

    private static void Tail(StringBuilder report, string title, string text)
    {
        report.AppendLine(title + ":");
        report.AppendLine(text.Length > 20000 ? text[^20000..] : text);
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
