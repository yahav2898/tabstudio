using System.Diagnostics;
using TabStudio.Core.Progress;
using TabStudio.Core.Workspaces;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Testing;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Test Explorer for toolchain projects: PHPUnit and Pest in PHP projects, JUnit in Gradle and Maven builds of Kotlin
/// and Java, XCTest and Swift Testing in Swift packages, pytest in Python projects, <c>go test</c> in Go modules,
/// <c>cargo test</c> in Rust packages and <c>dart test</c> or <c>flutter test</c> in Dart and Flutter packages. Their tests
/// are found in source when the workspace opens, and a run starts the project's own runner, whose reports or output
/// become the results.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, (ToolchainProject Project, ToolchainTestFramework Framework)> _toolchainTests = new(StringComparer.Ordinal);

    /// <summary>The toolchain projects with tests, as Test Explorer lists them.</summary>
    private IEnumerable<ProjectInfo> ToolchainTestProjects() =>
        _toolchainProjects.Where(item => _toolchainTests.ContainsKey(item.Info.FilePath)).Select(item => item.Info);

    /// <summary>After the toolchain projects were found: those with tests join Test Explorer, with the tests in their source.</summary>
    private void OnToolchainTestProjectsChanged()
    {
        _toolchainTests.Clear();
        foreach (var item in _toolchainProjects)
        {
            if (ToolchainTestPlanner.Detect(item.Project) is { } framework)
            {
                _toolchainTests[item.Info.FilePath] = (item.Project, framework);
            }
        }

        Tests.SetProjects([.. TestProjectsOf(Workspace), .. ToolchainTestProjects()]);
        _ = DiscoverToolchainTestsAsync([.. _toolchainTests.Values], _background?.Token ?? CancellationToken.None);
    }

    private async Task DiscoverToolchainTestsAsync(List<(ToolchainProject Project, ToolchainTestFramework Framework)> projects, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var (project, framework) in projects)
            {
                var tests = await Task.Run(() => ToolchainTestDiscoverer.Discover(project.Directory, framework, cancellationToken), cancellationToken);
                if (!_disposed && _toolchainProjects.FirstOrDefault(t => ReferenceEquals(t.Project, project)) is { } item)
                {
                    Tests.SetToolchainSourceTests(item.Info, tests);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Output.WriteLine($"Finding tests stopped: {ex.Message}", OutputKind.Failure);
        }
    }

    /// <summary>
    /// Runs the selected tests of toolchain projects with their own runners, and returns the rest of the request,
    /// which the C# test runner runs.
    /// </summary>
    private async Task<TestRunRequest> RunToolchainTestSelectionsAsync(TestRunRequest request)
    {
        var remaining = new List<TestProjectSelection>();
        foreach (var selection in request.Projects)
        {
            if (!_toolchainTests.TryGetValue(selection.ProjectFilePath, out var target)
                || _toolchainProjects.FirstOrDefault(t => t.Info.FilePath == selection.ProjectFilePath) is not { } item)
            {
                remaining.Add(selection);
                continue;
            }

            await RunToolchainTestsAsync(item.Info, target.Project, target.Framework, SelectorsOf(selection));
        }

        return request with { Projects = remaining };
    }

    /// <summary>The classes and methods a selection names: every test of the project, or those of the entries it names.</summary>
    private List<ToolchainTestSelector>? SelectorsOf(TestProjectSelection selection)
    {
        if (selection.Names is null)
        {
            return null;
        }

        var names = selection.Names.ToHashSet(StringComparer.Ordinal);
        var entries = Tests.Entries.Where(e => e.ProjectFilePath == selection.ProjectFilePath).ToList();
        var selectors = new List<ToolchainTestSelector>();
        foreach (var entry in entries.Where(e => names.Contains(e.Id) || names.Contains(e.MethodFullName)))
        {
            selectors.Add(new ToolchainTestSelector(entry.TypeFullName, entry.MethodName));
        }

        // A class or a namespace runs every test in it.
        foreach (var type in entries.Select(e => e.TypeFullName).Distinct(StringComparer.Ordinal)
            .Where(t => names.Contains(t) || names.Any(n => t.StartsWith(n + ".", StringComparison.Ordinal))))
        {
            selectors.RemoveAll(s => s.ClassName == type);
            selectors.Add(new ToolchainTestSelector(type, null));
        }

        return [.. selectors.Distinct()];
    }

    private async Task RunToolchainTestsAsync(ProjectInfo info, ToolchainProject project, ToolchainTestFramework framework, List<ToolchainTestSelector>? selection)
    {
        if (BlockLocalToolchainActionForRemote() || !await SaveAllCoreAsync()
            || !await EnsureToolchainReadyAsync(project.Toolchain, $"{project.Name}'s tests run with {project.Toolchain.Name}, which is not installed yet."))
        {
            Tests.ReportBuildFailed();
            return;
        }

        SelectSideBarPanel(SideBarPanel.TestExplorer);
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        using var cancellation = new CancellationTokenSource();
        _run = cancellation;
        _testRun = cancellation;
        _runDirectory = project.Directory;
        State = IdeState.Running;
        StatusText = $"Running the tests of {project.Name}";
        try
        {
            await _toolchains.Manager.PrepareRuntimeAsync(cancellationToken: cancellation.Token);
            if (RunPlanner.DependenciesOf(project) is { } dependency && RunPlanner.IsPending(project, dependency)
                && await RunDependencyInstallAsync(project, dependency, cancellation.Token) is { } failedInstall)
            {
                Output.WriteLine($"{project.Name}'s packages could not be installed, so its tests cannot run: {failedInstall.StartError ?? "see the output above"}.", OutputKind.Failure);
                Tests.ReportBuildFailed();
                State = IdeState.Failed;
                return;
            }

            var reports = Path.Combine(project.Directory, ".tabstudio", "test-results", Guid.NewGuid().ToString("N"));
            var plan = ToolchainTestPlanner.Plan(project, framework, selection, reports);
            Output.WriteLine($"========== Running {project.Name}'s tests with {framework} ==========", OutputKind.Info);
            Output.WriteLine("> " + plan.Command.CommandLine, OutputKind.Info);
            var started = DateTime.UtcNow;
            var clock = Stopwatch.GetTimestamp();
            OutputProcessResult result;
            using (var operation = new CompletingOperation(Operations.Begin("tests:" + project.Directory, $"Testing {project.Name}", new OperationProgress($"Running {project.Name}'s tests"), cancellation.Cancel)))
            using (FollowOutput(operation.Operation))
            {
                result = await OutputProcess.RunAsync(_toolchains.Runtime, plan.Command, Output, null, cancellation.Token, toolchain: project.Toolchain.Id);
                operation.Outcome = result.Succeeded ? OperationOutcome.Succeeded
                    : cancellation.IsCancellationRequested ? OperationOutcome.Cancelled : OperationOutcome.Failed;
            }

            var results = await Task.Run(() => ToolchainTestPlanner.ReadResults(project, plan, started));
            TryDeleteDirectory(reports);
            if (results.Count == 0 && !cancellation.IsCancellationRequested)
            {
                ErrorList.SetRunErrors(project.Directory, [.. Matcher(project.Toolchain).FindErrors(result.Lines, IsWorkspaceFile).Select(error => ToDiagnostic(project, error))]);
                Output.WriteLine(result.Succeeded
                    ? $"{project.Name}'s test runner found no tests{(selection is null ? string.Empty : " matching the selection")}."
                    : $"{project.Name}'s tests did not run: the build or the test runner failed. See the Error List and the output above.", result.Succeeded ? OutputKind.Info : OutputKind.Failure);
                if (!result.Succeeded)
                {
                    Tests.ReportBuildFailed();
                }
            }

            Tests.ReportToolchainResults(info, results, selection is null, Stopwatch.GetElapsedTime(clock));
            var failed = results.Count(r => r.Outcome is ToolchainTestOutcome.Failed or ToolchainTestOutcome.Errored);
            State = failed > 0 || (results.Count == 0 && !result.Succeeded) ? IdeState.Failed : IdeState.Ready;
            StatusText = $"{project.Name}: {results.Count(r => r.Outcome == ToolchainTestOutcome.Passed)} passed, {failed} failed";
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "Test run stopped";
        }
        finally
        {
            _run = null;
            _testRun = null;
            if (State == IdeState.Running)
            {
                State = IdeState.Ready;
            }
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next run; reports are read by their write time.
        }
    }
}
