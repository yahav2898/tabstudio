using System.Diagnostics;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging;
using TabStudio.Debugging.Building;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Testing;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Debug Tests, as in Visual Studio's Test Explorer: the selected tests run under the debugger, so breakpoints in the
/// tests and in the code they call stop, with stepping, Locals, Watch, the Call Stack and the exception helper, and
/// the results land in Test Explorer as they do for a run.
/// </summary>
/// <remarks>
/// <para>
/// C# and Visual Basic tests (xUnit, NUnit and MSTest) are built for debugging, as F5 builds a program, and the test
/// runner loads that build under a debug session, without the per-test timeout a run has. An exception that leaves
/// a test pauses there, as one Visual Studio calls unhandled by user code; Continue lets the test fail.
/// </para>
/// <para>
/// Toolchain tests run under their language's debug adapter (see <see cref="ToolchainTestPlanner.PlanDebug"/>):
/// pytest under debugpy, Vitest, Jest, Mocha and <c>node --test</c> under js-debug, PHPUnit and Pest under Xdebug,
/// and a Go package's tests under Delve. Runners TabStudio cannot debug yet run without the debugger, and say so.
/// </para>
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    private TaskCompletionSource? _adapterEnd;

    /// <inheritdoc />
    async Task ITestRunHost.DebugTestsAsync(TestRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Workspace is not { } workspace || _testRun is not null || IsDebugging)
        {
            return;
        }

        request = await DebugToolchainTestSelectionsAsync(request);
        if (request.Projects.Count == 0 || _language is not { } language || IsDebugging)
        {
            return;
        }

        var selections = request.Projects
            .Select(s => (Project: workspace.Projects.FirstOrDefault(p => p.FilePath == s.ProjectFilePath), s.Names))
            .Where(s => s.Project is not null)
            .Select(s => (Project: s.Project!, s.Names))
            .ToList();
        if (selections.Count == 0)
        {
            return;
        }

        SelectSideBarPanel(SideBarPanel.TestExplorer);
        var summaries = new List<TestRunSummary>();
        using (var run = new CancellationTokenSource())
        {
            _testRun = run;
            try
            {
                foreach (var (project, names) in selections)
                {
                    if (run.IsCancellationRequested || await DebugTestProjectAsync(project, names, language, run.Token) is not { } summary)
                    {
                        break;
                    }

                    summaries.Add(summary);
                }

                if (summaries.Count > 0)
                {
                    ReportTestRun(summaries, run.IsCancellationRequested);
                }
            }
            finally
            {
                _testRun = null;
            }
        }

        // Restart (Ctrl+Shift+F5) while tests are debugged debugs them again, built from the code as it is now.
        if (_restartRequested)
        {
            _restartRequested = false;
            await ((ITestRunHost)this).DebugTestsAsync(request);
        }
    }

    /// <summary>Builds one C# or Visual Basic test project for debugging and runs its selected tests under a debug session.</summary>
    /// <returns>The run's totals, or <see langword="null"/> when it did not build or could not run.</returns>
    private async Task<TestRunSummary?> DebugTestProjectAsync(ProjectInfo project, IReadOnlyList<string>? names, ILanguageService language, CancellationToken cancellationToken)
    {
        DebugBuild? debugBuild = null;
        var built = await BuildProjectsAsync([project], showOutput: true, async (target, token) =>
        {
            debugBuild = await language.BuildForDebuggingAsync(target, token);
            return debugBuild.Result;
        });
        if (built is not { Success: true } || debugBuild is not { Success: true } build)
        {
            Tests.ReportBuildFailed();
            return null;
        }

        var session = new DebugSession(build) { BreakOnUnhandledExceptions = BreakOnUnhandledExceptions };
        session.Paused += OnSessionPaused;
        session.Resumed += OnSessionResumed;
        _session = session;
        _debuggedProject = project;
        Breakpoints.Bind(session);
        State = IdeState.Debugging;
        StatusText = $"Debugging the tests of {project.Name}";
        Output.WriteLine("========== Starting test run under the debugger ==========", OutputKind.Info);
        List<CompiledAssembly> assemblies = [build.Result.EntryAssembly!, .. build.Result.Dependencies];
        var packages = language.GetPackageRuntimeAssemblies(project.FilePath);

        // Test Explorer's Cancel stops the debugger, which ends the run.
        using var cancel = cancellationToken.Register(session.Stop);
        try
        {
            return await session.RunHostedAsync(async stop =>
            {
                // A test may stay paused at a breakpoint for as long as the user looks at it: no timeout.
                using var tests = await TestSession.LoadAsync(assemblies, packages, new TestRunOptions { DefaultTimeout = null }, stop);
                return await InvokeOnUiAsync(() => RunDebuggedTestsAsync(project, tests, names, stop));
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Output.WriteLine("========== Test run cancelled ==========", OutputKind.Info);
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Output.WriteLine($"The tests of {project.Name} could not be debugged: {ex.Message}", OutputKind.Failure);
            return null;
        }
        finally
        {
            EndTestDebugging(session);
        }
    }

    private async Task<TestRunSummary> RunDebuggedTestsAsync(ProjectInfo project, TestSession tests, IReadOnlyList<string>? names, CancellationToken stop)
    {
        foreach (var message in tests.DiscoveryMessages)
        {
            Output.WriteLine($"{project.Name}: {message}", OutputKind.Failure);
        }

        Tests.SetBuiltTests(project, tests.Tests);
        var selected = names is null ? tests.Tests.Where(t => !t.IsExplicit).ToList() : tests.FindTests(names);
        return await RunTestSessionAsync(project, tests, selected, stop);
    }

    /// <summary>After tests were debugged: the debugger's panels empty and the IDE is ready again.</summary>
    private void EndTestDebugging(DebugSession session)
    {
        session.Paused -= OnSessionPaused;
        session.Resumed -= OnSessionResumed;
        session.Dispose();
        if (!ReferenceEquals(_session, session))
        {
            return;
        }

        _session = null;
        _debuggedProject = null;
        _pause = null;
        _exceptionHelper = null;
        Breakpoints.Unbind();
        CallStack.Clear();
        Locals.Clear();
        _ = Watch.EvaluateAsync(null);
        ShowExecutionPoints();
        State = IdeState.Ready;
        StatusText = "Ready";
        if (SelectedPanel is BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack)
        {
            SelectedPanel = BottomPanel.Output;
        }
    }

    /// <summary>
    /// Debugs the selected tests of a toolchain project, and returns the rest of the request, which the C# debugger
    /// runs. The debugger debugs one toolchain project's tests at a time.
    /// </summary>
    private async Task<TestRunRequest> DebugToolchainTestSelectionsAsync(TestRunRequest request)
    {
        var remaining = new List<TestProjectSelection>();
        var debugged = false;
        foreach (var selection in request.Projects)
        {
            if (!_toolchainTests.TryGetValue(selection.ProjectFilePath, out var target)
                || _toolchainProjects.FirstOrDefault(t => t.Info.FilePath == selection.ProjectFilePath) is not { } item)
            {
                remaining.Add(selection);
                continue;
            }

            if (debugged)
            {
                Output.WriteLine($"The debugger runs one project's tests at a time: debug the tests of {item.Info.Name} on their own.", OutputKind.Info);
                continue;
            }

            debugged = true;
            await DebugToolchainTestsAsync(item.Info, target.Project, target.Framework, SelectorsOf(selection));
        }

        return request with { Projects = remaining };
    }

    private async Task DebugToolchainTestsAsync(ProjectInfo info, ToolchainProject project, ToolchainTestFramework framework, List<ToolchainTestSelector>? selection)
    {
        var toolchain = project.Toolchain;
        if (toolchain.DebugAdapter?.Type is not { } type || !ToolchainLaunch.Supports(type) || _languageServers?.Configuration.Registry.FindAdapter(type) is null)
        {
            Output.WriteLine($"{toolchain.Name} has no debugger TabStudio can start, so {project.Name}'s tests run without one.", OutputKind.Info);
            await RunToolchainTestsAsync(info, project, framework, selection);
            return;
        }

        if (BlockLocalToolchainActionForRemote() || !await SaveAllCoreAsync()
            || !await EnsureToolchainReadyAsync(toolchain, $"{project.Name}'s tests run with {toolchain.Name}, which is not installed yet."))
        {
            Tests.ReportBuildFailed();
            return;
        }

        SelectSideBarPanel(SideBarPanel.TestExplorer);
        var reports = Path.Combine(project.Directory, ".tabstudio", "test-results", Guid.NewGuid().ToString("N"));
        var cancellation = new CancellationTokenSource();
        _testRun = cancellation;
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

            if (ToolchainTestPlanner.PlanDebug(project, framework, selection, reports) is not { } debug
                || ToolchainLaunch.Create(type, new RunPlan(null, debug.Debuggee, debug.Debuggee.Program)) is not { } configuration)
            {
                TryDeleteDirectory(reports);
                ReleaseTestRun(cancellation);
                Output.WriteLine($"TabStudio cannot debug {framework} tests yet, so the tests of {project.Name} run without the debugger.", OutputKind.Info);
                await RunToolchainTestsAsync(info, project, framework, selection);
                return;
            }

            Output.Clear();
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            if (debug.Prepare is { } prepare)
            {
                State = IdeState.Running;
                StatusText = $"Preparing the tests of {project.Name}";
                Output.WriteLine("> " + prepare.CommandLine, OutputKind.Info);
                var prepared = await OutputProcess.RunAsync(_toolchains.Runtime, prepare, Output, null, cancellation.Token, toolchain: toolchain.Id);
                State = IdeState.Ready;
                if (!prepared.Succeeded)
                {
                    if (!cancellation.IsCancellationRequested)
                    {
                        ErrorList.SetRunErrors(project.Directory, [.. Matcher(toolchain).FindErrors(prepared.Lines, IsWorkspaceFile).Select(error => ToDiagnostic(project, error))]);
                        Output.WriteLine($"The tests of {project.Name} could not be prepared, so they cannot be debugged: {prepared.StartError ?? "see the Error List and the output above"}.", OutputKind.Failure);
                        State = IdeState.Failed;
                    }

                    Tests.ReportBuildFailed();
                    return;
                }
            }

            if (type == ToolchainLaunch.PhpType)
            {
                configuration = PreparePhpDebug(project, configuration);
            }

            var target = new LaunchTarget($"{project.Name} tests", configuration, IsCurrentFile: false);
            _programEnvironments.AddOrUpdate(target, WithServiceEnvironment(debug.Debuggee).Environment);
            var started = DateTime.UtcNow;
            var clock = Stopwatch.GetTimestamp();
            _pendingAdapterRelaunch = () => DebugToolchainTestsAsync(info, project, framework, selection);
            await LaunchAdapterAsync(target, DebugStart.Run, noDebug: false);
            _pendingAdapterRelaunch = null;
            if (_adapterEnd is { } end)
            {
                // Test Explorer's Cancel stops the debugger, which ends the run.
                using (cancellation.Token.Register(() => _ui.Post(() => _ = StopAdapterAsync())))
                {
                    await end.Task;
                }
            }

            if (debug.OutputReport is { } report)
            {
                // The runner's results are its output, which the debugger showed in the Output panel.
                Directory.CreateDirectory(Path.GetDirectoryName(report)!);
                await File.WriteAllTextAsync(report, Output.Text + "\n" + debug.OutputFooter + "\n");
            }

            var results = await Task.Run(() => ToolchainTestPlanner.ReadResults(project, debug.Results, started));
            Tests.ReportToolchainResults(info, results, selection is null, Stopwatch.GetElapsedTime(clock));
            var failed = results.Count(r => r.Outcome is ToolchainTestOutcome.Failed or ToolchainTestOutcome.Errored);
            StatusText = results.Count == 0
                ? $"The tests of {project.Name} were debugged; the runner reported no results"
                : $"{project.Name}: {results.Count(r => r.Outcome == ToolchainTestOutcome.Passed)} passed, {failed} failed";
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "Test run stopped";
        }
        finally
        {
            _pendingAdapterRelaunch = null;
            TryDeleteDirectory(reports);
            ReleaseTestRun(cancellation);
        }
    }

    /// <summary>Ends a toolchain test run's hold on Test Explorer, unless a newer run (a Restart) holds it now.</summary>
    private void ReleaseTestRun(CancellationTokenSource run)
    {
        if (ReferenceEquals(_testRun, run))
        {
            _testRun = null;
        }

        run.Dispose();
    }
}
