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
/// <c>TABSTUDIO_REAL_REPOS_OUT</c> and screenshots to <c>TABSTUDIO_SCREENSHOTS</c>. Without the variables, nothing runs;
/// <c>TABSTUDIO_REAL_REPOS_ONLY</c> (comma-separated names) limits a run to some of the repositories.
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
        var only = Environment.GetEnvironmentVariable("TABSTUDIO_REAL_REPOS_ONLY")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var repository in manifest["repositories"]!.AsArray().Where(r => InThisJob(r!) && (only is null || only.Contains(r!["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase))))
        {
            // Python, Java, Rust, Node.js, PHP, Kotlin and Swift repositories run in the test below.
            if (repository!["toolchain"] is not null)
            {
                continue;
            }

            var name = repository!["name"]!.GetValue<string>();
            var baseline = repository["maxErrors"]?.GetValue<int>();
            var solution = Path.Combine(clones, name, repository["solution"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
            var report = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"{name} at {repository["sha"]!.GetValue<string>()}");
            Outcome outcome;
            try
            {
                outcome = await OpenAndBuildAsync(solution, name, repository["startup"]?.GetValue<string>(), repository["serve"], report);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"EXCEPTION: {ex}");
                outcome = new Outcome(-1, 0, TimeSpan.Zero);
            }

            await File.WriteAllTextAsync(Path.Combine(output, name + ".txt"), report.ToString());
            results[name] = new JsonObject { ["errors"] = outcome.Errors, ["warnings"] = outcome.Warnings, ["seconds"] = Math.Round(outcome.Duration.TotalSeconds, 1), ["served"] = outcome.Served };
            var served = outcome.Served switch { true => ", served", false => ", NOT served", null => string.Empty };
            summary.Append(CultureInfo.InvariantCulture, $"| {name} | {outcome.Errors} | {baseline?.ToString(CultureInfo.InvariantCulture) ?? "none"} | {outcome.Warnings} | {outcome.Duration.TotalSeconds:F0} s{served} |\n");
            if (outcome.Startup is { } wrongStartup)
            {
                problems.Add($"{name}: {wrongStartup}; see {name}.txt.");
            }

            if (outcome.Served == false)
            {
                problems.Add($"{name} built, but its first page was not served as expected; see {name}.txt.");
            }

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

    /// <summary>
    /// Opens the pinned Python, Java, Rust, Node.js, PHP, Kotlin and Swift repositories as a person would: the project setup installs their
    /// toolchains' packages, and Ctrl+F5 builds and runs the startup project. A web app must answer its first page
    /// in the web preview; a command-line program must exit with code 0. Each repository's report has the setup
    /// log, the Output panel and screenshots.
    /// </summary>
    [AvaloniaFact]
    public async Task Pinned_Python_Java_Rust_and_Node_repositories_set_up_build_and_start_their_programs()
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
        var summary = new StringBuilder("| Repository | Toolchain | Startup project | Program | Result | Time |\n| --- | --- | --- | --- | --- | --- |\n");
        var only = Environment.GetEnvironmentVariable("TABSTUDIO_REAL_REPOS_ONLY")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var repository in manifest["repositories"]!.AsArray().Where(r => InThisJob(r!) && (only is null || only.Contains(r!["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase))))
        {
            if (repository!["toolchain"]?.GetValue<string>() is not { } toolchain)
            {
                continue;
            }

            var name = repository["name"]!.GetValue<string>();
            var expected = new ProgramExpectation(
                toolchain,
                repository["program"]?.GetValue<string>(),
                repository["web"]?.GetValue<string>(),
                repository["expect"]?.GetValue<string>(),
                repository["tests"]?.GetValue<int>());
            var report = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"{name} at {repository["sha"]!.GetValue<string>()}");

            // The app's folder in the repository, such as next-learn's dashboard/final-example.
            var folder = Path.Combine(clones, name, (repository["folder"]?.GetValue<string>() ?? ".").Replace('/', Path.DirectorySeparatorChar));
            if (repository["services"]?.GetValue<string>() == "job")
            {
                // The job's service containers stand in for the repository's Compose services, which run natively on a
                // tablet: its Compose file is set aside, so the run uses the .env below.
                foreach (var compose in new[] { "docker-compose.yml", "docker-compose.yaml", "compose.yml", "compose.yaml" }.Select(f => Path.Combine(folder, f)).Where(File.Exists))
                {
                    File.Move(compose, compose + ".job", overwrite: true);
                    report.AppendLine(CultureInfo.InvariantCulture, $"Set aside {Path.GetFileName(compose)}: the job's containers serve its databases.");
                }
            }

            if (repository["dotenv"] is JsonObject dotenv)
            {
                // The variables the repository's README asks a person to put in .env.
                var lines = dotenv.Select(pair => $"{pair.Key}={pair.Value!.GetValue<string>()}");
                await File.WriteAllLinesAsync(Path.Combine(folder, ".env"), lines);
                report.AppendLine(CultureInfo.InvariantCulture, $".env: {string.Join(", ", dotenv.Select(pair => pair.Key))}");
            }

            RunOutcome outcome;
            try
            {
                outcome = await OpenAndRunAsync(folder, name, expected, report);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"EXCEPTION: {ex}");
                outcome = new RunOutcome(null, null, ex.GetType().Name + ": " + ex.Message, TimeSpan.Zero);
            }

            await File.WriteAllTextAsync(Path.Combine(output, name + ".txt"), report.ToString());
            summary.Append(CultureInfo.InvariantCulture, $"| {name} | {toolchain} | {outcome.Startup ?? "none"} | {outcome.Program ?? "none"} | {outcome.Problem ?? "started"} | {outcome.Duration.TotalSeconds:F0} s |\n");
            if (outcome.Problem is { } problem)
            {
                problems.Add($"{name}: {problem}; see {name}.txt.");
            }
        }

        await File.WriteAllTextAsync(Path.Combine(output, "summary-polyglot.md"), summary.ToString());
        Assert.True(problems.Count == 0, string.Join('\n', problems) + "\n\n" + summary);
    }

    /// <param name="Tests">How many tests Test Explorer's Run All must pass, at least, after the run; <see langword="null"/> to run none.</param>
    /// <param name="Program">The program Ctrl+F5 runs; <see langword="null"/> for a library, whose tests are its check.</param>
    private sealed record ProgramExpectation(string Toolchain, string? Program, string? Web, string? Text, int? Tests = null);

    /// <summary>
    /// Whether a repository belongs to this run of the job: those with no <c>job</c> to the Real repositories job, and
    /// those whose <c>job</c> is <c>TABSTUDIO_REAL_REPOS_JOB</c> to that job (<c>sweep</c>: the repositories of the
    /// 0.20.5 sweep, in a job of their own so that neither job takes much longer).
    /// </summary>
    private static bool InThisJob(JsonNode repository) =>
        string.Equals(repository["job"]?.GetValue<string>(), Environment.GetEnvironmentVariable("TABSTUDIO_REAL_REPOS_JOB") is { Length: > 0 } job ? job : null, StringComparison.Ordinal);

    private sealed record RunOutcome(string? Startup, string? Program, string? Problem, TimeSpan Duration);

    private static async Task<RunOutcome> OpenAndRunAsync(string folder, string name, ProgramExpectation expected, StringBuilder report)
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Task? run = null;
        try
        {
            await workspace.LoadAsync(folder);
            await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(5), "the load");
            report.AppendLine("Toolchain projects:");
            foreach (var project in workspace.ToolchainProjects)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  {project.Toolchain.Id}: {project.Project.MarkerPath ?? project.Project.Directory}");
            }

            var startup = workspace.ToolchainProjects.FirstOrDefault(p => p.Info.FilePath == workspace.StartupProject?.FilePath);
            report.AppendLine(CultureInfo.InvariantCulture, $"Startup project: {startup?.Toolchain.Id} {workspace.StartupProject?.FilePath}");
            if (startup?.Toolchain.Id != expected.Toolchain)
            {
                return new RunOutcome(startup?.Toolchain.Id, null, $"the startup project is {startup?.Toolchain.Id ?? "none"}, not {expected.Toolchain}", clock.Elapsed);
            }

            await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted && !workspace.IsSettingUpProjects, TimeSpan.FromMinutes(20), "the project setup");
            report.AppendLine(CultureInfo.InvariantCulture, $"Set up in {clock.Elapsed.TotalSeconds:F1} s");
            UiTestHelpers.SaveScreenshot(window, $"real-{name}-1-set-up");
            if (workspace.ToolchainNotice is { } notice)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"Notice: {notice.Title}: {notice.Message}");
            }

            clock.Restart();
            if (expected.Program is null)
            {
                // A library: Test Explorer builds it and runs its tests.
                var tested = await RunAllTestsAsync(workspace, expected.Tests ?? 1, report);
                UiTestHelpers.SaveScreenshot(window, $"real-{name}-3-tests");
                AppendTail(report, "Project setup", workspace.ProjectSetupOutput.Text);
                AppendTail(report, "Output", workspace.Output.Text);
                return new RunOutcome(startup!.Toolchain.Id, null, tested, clock.Elapsed);
            }

            run = workspace.RunCommand.ExecuteAsync(null);
            string? problem = null;
            if (expected.Web is { } path)
            {
                await UiTestHelpers.WaitUntilAsync(() => workspace.ServerNotice is not null || run.IsCompleted, TimeSpan.FromMinutes(25), "the program to listen");
                if (workspace.ServerNotice is not { } server)
                {
                    problem = "the program did not start listening";
                }
                else
                {
                    server.OpenPreview.Execute(null);
                    Assert.True(workspace.WebPreview.IsRunning);
                    report.AppendLine(CultureInfo.InvariantCulture, $"Web preview: {workspace.WebPreview.AppAddress}");
                    problem = await CheckPageAsync(new Uri(workspace.WebPreview.AppAddress!, path), expected.Text, report);
                    UiTestHelpers.SaveScreenshot(window, $"real-{name}-2-running");
                }

                if (workspace.StopCommand.CanExecute(null))
                {
                    workspace.StopCommand.Execute(null);
                }
            }

            await run.WaitAsync(TimeSpan.FromMinutes(25));
            var text = workspace.Output.Text;
            if (expected.Web is null)
            {
                UiTestHelpers.SaveScreenshot(window, $"real-{name}-2-ran");
                problem = text.Contains($"The program '{expected.Program}' has exited with code 0", StringComparison.Ordinal) ? null
                    : $"the program '{expected.Program}' did not exit with code 0";
                if (problem is null && expected.Text is { } output && !text.Contains(output, StringComparison.Ordinal))
                {
                    problem = $"the program did not print \"{output}\"";
                }
            }
            else if (!text.Contains($"Running {expected.Program} with", StringComparison.Ordinal))
            {
                problem ??= $"the program was not called {expected.Program}";
            }

            report.AppendLine(CultureInfo.InvariantCulture, $"Ran in {clock.Elapsed.TotalSeconds:F1} s");
            if (problem is null && expected.Tests is { } minimum)
            {
                problem = await RunAllTestsAsync(workspace, minimum, report);
                UiTestHelpers.SaveScreenshot(window, $"real-{name}-3-tests");
            }

            AppendTail(report, "Project setup", workspace.ProjectSetupOutput.Text);
            AppendTail(report, "Output", text);
            return new RunOutcome(startup!.Toolchain.Id, expected.Program, problem, clock.Elapsed);
        }
        finally
        {
            if (run is { IsCompleted: false } && workspace.StopCommand.CanExecute(null))
            {
                workspace.StopCommand.Execute(null);
            }

            window.Close();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Runs every test with Test Explorer's Run All, as a person would after opening the repository; <see langword="null"/>
    /// when at least <paramref name="minimum"/> passed.
    /// </summary>
    private static async Task<string?> RunAllTestsAsync(WorkspaceViewModel workspace, int minimum, StringBuilder report)
    {
        var explorer = workspace.Tests;
        await UiTestHelpers.WaitUntilAsync(() => explorer.HasTestProjects && explorer.RunAllCommand.CanExecute(null), TimeSpan.FromMinutes(2), "Test Explorer's tests");
        await explorer.RunAllCommand.ExecuteAsync(null);
        report.AppendLine(CultureInfo.InvariantCulture, $"Tests: {explorer.PassedCount} passed, {explorer.FailedCount} failed, {explorer.SkippedCount} skipped of {explorer.TotalCount}: {explorer.RunStatusText}");
        return explorer.PassedCount >= minimum ? null : $"Test Explorer's Run All passed {explorer.PassedCount} tests, fewer than {minimum} ({explorer.FailedCount} failed)";
    }

    /// <summary>Requests a page of the running app, as the web preview shows it; <see langword="null"/> when it answers as expected.</summary>
    private static async Task<string?> CheckPageAsync(Uri address, string? expected, StringBuilder report)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(address);
                var body = await response.Content.ReadAsStringAsync();
                report.AppendLine(CultureInfo.InvariantCulture, $"GET {address}: {(int)response.StatusCode} {response.ReasonPhrase}, {body.Length} characters");
                if ((int)response.StatusCode >= 500)
                {
                    // The error page's title and start, such as a Laravel exception's message, for the report.
                    var title = System.Text.RegularExpressions.Regex.Match(body, "<title>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value.Trim();
                    report.AppendLine(CultureInfo.InvariantCulture, $"Error page: {title}\n{(body.Length > 2000 ? body[..2000] : body)}");
                    return $"{address} answered {(int)response.StatusCode}";
                }

                return expected is null || body.Contains(expected, StringComparison.Ordinal) ? null : $"{address} did not show \"{expected}\"";
            }
            catch (HttpRequestException ex) when (attempt < 10)
            {
                // The port can open a moment before the app answers.
                report.AppendLine(CultureInfo.InvariantCulture, $"GET {address}: {ex.Message}; retrying");
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    private static void AppendTail(StringBuilder report, string title, string text)
    {
        report.AppendLine(title + ":");
        report.AppendLine(text.Length > 40000 ? text[^40000..] : text);
    }

    /// <param name="Served">Whether the first page was served as the repository's <c>serve</c> entry expects; <see langword="null"/> without one.</param>
    /// <param name="Startup">What is wrong with the startup project TabStudio chose, when the entry names the one it expects.</param>
    private sealed record Outcome(int Errors, int Warnings, TimeSpan Duration, bool? Served = null, string? Startup = null);

    private static async Task<Outcome> OpenAndBuildAsync(string solution, string name, string? expectedStartup, JsonNode? serve, StringBuilder report)
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

            // The startup project TabStudio chose by itself, before the serve check picks one.
            report.AppendLine(CultureInfo.InvariantCulture, $"Startup project: {workspace.StartupProject?.Name ?? "none"}");
            var startupProblem = expectedStartup is null || workspace.StartupProject?.Name == expectedStartup
                ? null
                : $"the startup project is {workspace.StartupProject?.Name ?? "none"}, not {expectedStartup}";
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
                foreach (var error in group.Take(25))
                {
                    report.AppendLine(CultureInfo.InvariantCulture, $"      [{error.Diagnostic.ProjectName}] {error.Diagnostic}");
                }
            }

            foreach (var warning in items.Where(i => IsGeneratorWarning(i.Diagnostic.Id)))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  generator: [{warning.Diagnostic.ProjectName}] {warning.Diagnostic}");
            }

            AppendGeneratorPackages(report, fixture.Paths.Packages);

            UiTestHelpers.SaveScreenshot(window, $"real-{name}-2-built");
            bool? served = serve is null ? null : await ServeFirstPageAsync(workspace, window, name, serve, report);
            report.AppendLine("Output:");
            var text = workspace.Output.Text;
            report.AppendLine(text.Length > 40000 ? text[^40000..] : text);
            return new Outcome(errors.Count, warnings, duration, served, startupProblem);
        }
        finally
        {
            window.Close();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// The <c>serve</c> check: F5 on the named web project, with each connection string in <c>connections</c> taken
    /// from the environment variable it names and saved as the Database Explorer connection the run uses (as Connect to
    /// SQL Server does), and each variable in <c>environment</c> set for the run, as a person sets one the app's
    /// README asks for (<c>$(RandomBase64Key)</c> is a new random 32-byte key, <c>$(TempDirectory)</c> a new folder),
    /// then a GET of <c>path</c>, which must answer <c>status</c> with <c>contains</c> in its body.
    /// </summary>
    private static async Task<bool> ServeFirstPageAsync(WorkspaceViewModel workspace, Window window, string name, JsonNode serve, StringBuilder report)
    {
        var projectName = serve["project"]!.GetValue<string>();
        var path = serve["path"]?.GetValue<string>() ?? "/";
        var status = serve["status"]?.GetValue<int>() ?? 200;
        var contains = serve["contains"]?.GetValue<string>();
        if (workspace.Workspace?.Projects.FirstOrDefault(p => p.Name == projectName) is not { } project)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"Serve: the project {projectName} is not in the solution.");
            return false;
        }

        foreach (var (connectionName, variable) in serve["connections"]?.AsObject() ?? new JsonObject())
        {
            var value = Environment.GetEnvironmentVariable(variable!.GetValue<string>());
            if (string.IsNullOrEmpty(value))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"Serve: {variable} is not set, so '{connectionName}' has no database to connect to.");
                return false;
            }

            await workspace.DatabaseExplorer.SaveRunConnectionAsync(new TabStudio.Database.DatabaseConnection(
                Guid.NewGuid(), WorkspaceViewModel.RunConnectionName(projectName, connectionName), TabStudio.Database.DatabaseProvider.SqlServer, value));
        }

        var previous = new List<(string Name, string? Value)>();
        var temporary = Path.Combine(Path.GetTempPath(), "tabstudio-serve-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var (variable, template) in serve["environment"]?.AsObject() ?? new JsonObject())
        {
            var value = template!.GetValue<string>()
                .Replace("$(RandomBase64Key)", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), StringComparison.Ordinal)
                .Replace("$(TempDirectory)", temporary, StringComparison.Ordinal);
            Directory.CreateDirectory(temporary);
            previous.Add((variable, Environment.GetEnvironmentVariable(variable)));
            Environment.SetEnvironmentVariable(variable, value);
            report.AppendLine(CultureInfo.InvariantCulture, $"Serve: {variable} set for the run.");
        }

        workspace.SetStartupProject(project);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, TimeSpan.FromMinutes(6), "the web app to start");
            if (!workspace.WebPreview.IsRunning)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"Serve: {projectName} did not start; see the Output below.");
                return false;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var response = await http.GetAsync(new Uri(workspace.WebPreview.AppAddress!, path));
            var body = await response.Content.ReadAsStringAsync();
            var ok = (int)response.StatusCode == status && (contains is null || body.Contains(contains, StringComparison.Ordinal));
            report.AppendLine(CultureInfo.InvariantCulture, $"Serve: GET {path} answered {(int)response.StatusCode} in {clock.Elapsed.TotalSeconds:F1} s after F5, {body.Length} characters; expected {status}{(contains is null ? string.Empty : $" with '{contains}'")}: {(ok ? "OK" : "FAILED")}");
            if (!ok)
            {
                report.AppendLine(body.Length > 4000 ? body[..4000] : body);
            }

            UiTestHelpers.SaveScreenshot(window, $"real-{name}-3-served");
            return ok;
        }
        finally
        {
            if (workspace.StopCommand.CanExecute(null))
            {
                workspace.StopCommand.Execute(null);
            }

            await run.WaitAsync(TimeSpan.FromMinutes(1));
            foreach (var (variable, value) in previous)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
    }

    /// <summary>The warnings that say a generator's code is missing: it did not load, threw, or needs a newer compiler.</summary>
    private static bool IsGeneratorWarning(string id) => id is "CS8032" or "CS8034" or "CS8784" or "CS8785" or "CS9057";

    /// <summary>
    /// Lists the restored packages that bring analyzers or MSBuild files, with those files, so a missing generator's
    /// package layout is in the report. With <c>TABSTUDIO_REAL_REPOS_DUMP_BUILD_FILES=1</c>, their props and targets
    /// files are copied into the report too.
    /// </summary>
    private static void AppendGeneratorPackages(StringBuilder report, string packagesFolder)
    {
        if (!Directory.Exists(packagesFolder))
        {
            return;
        }

        var dump = Environment.GetEnvironmentVariable("TABSTUDIO_REAL_REPOS_DUMP_BUILD_FILES") == "1";
        report.AppendLine("Packages with analyzers or MSBuild files:");
        foreach (var versionFolder in Directory.EnumerateDirectories(packagesFolder).SelectMany(Directory.EnumerateDirectories).Order(StringComparer.Ordinal))
        {
            var files = new[] { "analyzers", "build", "buildTransitive", "buildMultiTargeting" }
                .Select(folder => Path.Combine(versionFolder, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                .Select(file => Path.GetRelativePath(versionFolder, file).Replace('\\', '/'))
                .Where(file => !file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                continue;
            }

            report.AppendLine(CultureInfo.InvariantCulture, $"  {Path.GetFileName(Path.GetDirectoryName(versionFolder))} {Path.GetFileName(versionFolder)}");
            foreach (var file in files)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"      {file}");
                if (dump && (file.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
                    && new FileInfo(Path.Combine(versionFolder, file)) is { Length: < 24_000 } info)
                {
                    report.AppendLine(File.ReadAllText(info.FullName));
                }
            }
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
