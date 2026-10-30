using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Progress;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>Building, running and stopping programs.</summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, CancellationTokenSource> _projectAnalysis = new(StringComparer.Ordinal);

    /// <summary>
    /// Ctrl+F5: saves, builds the startup project and the projects it references, and runs it with its console in
    /// the Output panel. A web app is served on the device and shown in the web preview.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (IsRemoteWorkspace) { await RunRemoteAsync(buildOnly: false); return; }
        if (UsesLaunchTarget && CurrentLaunchTarget is { } target)
        {
            await LaunchAdapterAsync(target, DebugStart.Run, noDebug: true);
            return;
        }

        if (StartupProject is not { } project || _language is null)
        {
            return;
        }

        if (IsRunOnSwarm)
        {
            StartOnSwarm(project, debug: false, pauseAtStart: false);
            return;
        }

        if (IsStaticSite(project))
        {
            await ServeStaticSiteAsync(project);
            return;
        }

        if (await TryRunOnWindowsBridgeAsync(project))
        {
            return;
        }

        if (!await PrepareServicesAsync(project)) return;

        if (IsToolchainProject(project))
        {
            await RunToolchainProjectAsync(project);
            return;
        }

        if (IsLiveApp(project))
        {
            await RunLiveProjectAsync(project, fromDebugger: false);
            return;
        }

        var build = await BuildProjectsAsync([project], showOutput: true);
        if (build is not { Success: true } || build.EntryAssembly is not { } entry)
        {
            return;
        }

        State = IdeState.Running;
        StatusText = $"Running {project.Name}";
        _run = new CancellationTokenSource();
        var input = Output.BeginInput();
        var listening = BeginServerRun();
        var runLifetime = _run;
        RunResult result;
        void Loaded(Assembly assembly) => _ui.Post(() =>
        {
            if (ReferenceEquals(_run, runLifetime) && !runLifetime.IsCancellationRequested) BeginHotReload(project, build, assembly);
        });
        _runner.Loaded += Loaded;
        try
        {
            var packages = _language.GetPackageRuntimeAssemblies(project.FilePath);
            result = await _runner.RunAsync(entry, build.Dependencies, packages, [], new ServerConsoleSink(Output, listening), input, _run.Token, _serviceEnvironment);
        }
        finally
        {
            _runner.Loaded -= Loaded;
            EndHotReload();
            Output.EndInput();
            _run.Dispose();
            _run = null;
            EndServerRun();
        }

        ReportRunResult(project, result);
        if (_restartConsoleForHotReload && !_disposed)
        {
            _restartConsoleForHotReload = false;
            await RunAsync();
        }
    }

    /// <summary>Ends a run: the exit line in the Output panel, as Visual Studio writes it, and the status bar.</summary>
    private void ReportRunResult(ProjectInfo project, RunResult result)
    {
        Output.WriteLine();
        switch (result.Outcome)
        {
            case RunOutcome.Completed:
                Output.WriteLine($"The program '{project.Name}' has exited with code {result.ExitCode.ToString(CultureInfo.InvariantCulture)} (0x{result.ExitCode:x}).", OutputKind.Info);
                break;
            case RunOutcome.Crashed:
                Output.WriteLine($"The program '{project.Name}' stopped because of an unhandled exception.", OutputKind.Failure);
                ExplainSqlServerCrash(project, result);
                break;
            case RunOutcome.Stopped:
                Output.WriteLine($"The program '{project.Name}' was stopped.", OutputKind.Info);
                break;
            case RunOutcome.NoEntryPoint:
                Output.WriteLine($"'{project.Name}' has no Main method or top-level statements to run.", OutputKind.Failure);
                break;
        }

        State = result.Outcome == RunOutcome.Crashed ? IdeState.Failed : IdeState.Ready;
        StatusText = result.Outcome switch
        {
            RunOutcome.Completed => "Ready",
            RunOutcome.Stopped => "Stopped",
            _ => "The program stopped with an error",
        };
    }

    /// <summary>Shift+F5: stops the running program.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _run?.Cancel();

    /// <summary>Ctrl+Shift+B: builds every project in the solution without running anything.</summary>
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildAsync()
    {
        if (IsRemoteWorkspace) { await RunRemoteAsync(buildOnly: true); return; }
        if (MobileRoot() is not null)
        {
            await BuildAndroidCoreAsync(install: false);
            return;
        }

        if (CurrentToolchainProject() is { } native && TabStudio.Toolchains.Embedded.Projects.EmbeddedProjectDetector.Read(native.Project.Directory) is { } embedded)
        {
            try { await BuildEmbeddedCoreAsync(embedded); }
            catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
            return;
        }

        if (Workspace is { } workspace)
        {
            var csharp = workspace.Projects.Where(p => p.IsRoslynLanguage).ToList();
            var excluded = BuildExclusions.Of(workspace.Projects);
            var skipped = csharp.Where(p => IsNotBuiltHere(p) || excluded.ContainsKey(p.FilePath)).ToList();
            AddProjectsThatReferenceSkipped(csharp, skipped);
            await BuildProjectsAsync(InDependencyOrder(csharp.Except(skipped).ToList()), showOutput: true, skipped: skipped);
            foreach (var item in _toolchainProjects.Where(t => TabStudio.Toolchains.Microchip.MicrochipInstallation.IsCompiler(t.Toolchain.Id)))
            {
                await RunToolchainProjectAsync(item.Info);
                if (State == IdeState.Failed) break;
            }

            // After the solution's own projects, unless they failed: a state left from an earlier build does not count.
            if (csharp.Count == 0 || State != IdeState.Failed)
            {
                await BuildToolchainProjectsAsync(clearOutput: csharp.Count == 0);
            }
        }

        // A Rust, Go, Java, CMake or Make startup project is compiled as F5 would compile it, without running it.
        if (State != IdeState.Failed && StartupProject is { } chosen && FindToolchainProject(chosen) is { } startup
            && !TabStudio.Toolchains.Microchip.MicrochipInstallation.IsCompiler(startup.Toolchain.Id)
            && startup.Toolchain.Id is not ("android" or "flutter"))
        {
            var project = startup.Project;
            await RunToolchainPlanAsync(project, () => PlanToolchainProject(project, ActiveDocument?.FilePath), buildOnly: true);
        }
    }

    /// <summary>
    /// The projects of a solution build in dependency order, as Visual Studio builds them: every project after the
    /// projects it references, otherwise in solution order.
    /// </summary>
    private static List<ProjectInfo> InDependencyOrder(List<ProjectInfo> projects)
    {
        var byPath = projects.ToDictionary(p => p.FilePath, StringComparer.Ordinal);
        var ordered = new List<ProjectInfo>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(ProjectInfo project)
        {
            if (!visited.Add(project.FilePath))
            {
                return;
            }

            foreach (var reference in project.ProjectReferences)
            {
                if (byPath.TryGetValue(reference, out var dependency))
                {
                    Visit(dependency);
                }
            }

            ordered.Add(project);
        }

        foreach (var project in projects)
        {
            Visit(project);
        }

        return ordered;
    }

    /// <summary>
    /// Whether a project of the solution is not built on this device: an Aspire AppHost, a .NET Framework program that
    /// needs Windows, or a project that needs a platform workload, such as a .NET MAUI app.
    /// </summary>
    private static bool IsNotBuiltHere(ProjectInfo project) =>
        project.Kind == ProjectKind.AspireAppHost || NetFrameworkCompatibility.NeedsWindows(project) || ProjectClassifier.NeedsPlatformWorkload(project);

    /// <summary>
    /// Adds to the skipped projects those that reference one of them, directly or through others, such as an AppHost's
    /// tests: compiled without the project they reference, they would only report its types as missing.
    /// </summary>
    private static void AddProjectsThatReferenceSkipped(List<ProjectInfo> projects, List<ProjectInfo> skipped)
    {
        var added = true;
        while (added)
        {
            added = false;
            foreach (var project in projects.Except(skipped).ToList())
            {
                if (SkippedReference(project, skipped) is not null)
                {
                    skipped.Add(project);
                    added = true;
                }
            }
        }
    }

    /// <summary>
    /// The first skipped project a project compiles against, or <see langword="null"/> when it compiles against none. A
    /// reference for the build order only (<c>ReferenceOutputAssembly="false"</c>) does not count.
    /// </summary>
    private static ProjectInfo? SkippedReference(ProjectInfo project, List<ProjectInfo> skipped) =>
        project.ProjectReferences
            .Where(reference => project.GetProjectReference(reference).IsCompiledAgainst)
            .Select(reference => skipped.FirstOrDefault(s => string.Equals(s.FilePath, reference, StringComparison.Ordinal)))
            .FirstOrDefault(s => s is not null);

    /// <summary>Why a project of the solution is not built on this device, for the Output panel.</summary>
    /// <param name="project">The skipped project.</param>
    /// <param name="skipped">Every skipped project, for one that is skipped because it references another.</param>
    private static string SkipReason(ProjectInfo project, List<ProjectInfo> skipped) =>
        project.Kind == ProjectKind.AspireAppHost
            ? "an Aspire AppHost runs on a computer, where Aspire's developer control plane starts the app's projects and containers"
            : ProjectClassifier.PlatformWorkloadReason(project) is { } workload
                ? $"it {workload}"
                : NetFrameworkCompatibility.NeedsWindows(project)
                    ? $"a .NET Framework project that needs Windows: it {NetFrameworkCompatibility.WhyWindows(project)}"
                    : SkippedReference(project, skipped) is { } reference
                        ? $"it references {reference.Name}, which is not built on this device"
                        : !project.IsBuiltBySolution
                            ? "the solution's Debug configuration does not build it"
                            : "it is not built on this device";

    private bool CanRun() => !_preparingServices && (IsRemoteWorkspace ? _remoteWorkspace?.Connection.IsConnected == true : StartupProject is not null || UsesLaunchTarget) && State is IdeState.Ready or IdeState.Failed && !IsNotebookRunning && !IsLiveGuest;

    private bool CanBuild() => !_preparingServices && (!IsRemoteWorkspace || _remoteWorkspace?.Connection.IsConnected == true) && State is IdeState.Ready or IdeState.Failed && !IsLiveGuest;

    /// <summary>Builds projects one after another, reporting in the Output panel and the Error List.</summary>
    /// <param name="projects">The projects.</param>
    /// <param name="showOutput">Whether to clear the Output panel and bring it forward.</param>
    /// <param name="build">Builds one project, such as a debug build; the language service's normal build by default.</param>
    /// <param name="results">Receives the result of every project, in order, when given.</param>
    /// <param name="skipped">Projects of the solution that are not built on this device, each named with the reason.</param>
    /// <returns>The result for the last project, or <see langword="null"/> when nothing was built.</returns>
    /// <remarks>
    /// A project's build compiles the projects it references too, so their diagnostics come back with every project
    /// that references them. Each one is reported once, under the project it belongs to, as Visual Studio does.
    /// </remarks>
    private async Task<BuildResult?> BuildProjectsAsync(
        List<ProjectInfo> projects,
        bool showOutput,
        Func<ProjectInfo, CancellationToken, Task<BuildResult>>? build = null,
        List<BuildResult>? results = null,
        List<ProjectInfo>? skipped = null)
    {
        skipped ??= [];
        if (_language is not { } language || projects.Count + skipped.Count == 0)
        {
            return null;
        }

        State = IdeState.Busy;
        await SaveAllCoreAsync();
        await WaitForPackagesAsync();
        foreach (var document in Documents)
        {
            document.SyncText();
        }

        // Background analyses may still describe the text from before Save All. The build below is now the
        // authoritative result; an older analysis must not clear its Error List while F5 reports a failure.
        _solutionAnalysis?.Cancel();
        Operations.Find(AnalysisOperation)?.Complete(OperationOutcome.Cancelled);
        foreach (var analysis in _projectAnalysis.Values)
        {
            analysis.Cancel();
            analysis.Dispose();
        }
        _projectAnalysis.Clear();

        if (showOutput)
        {
            Output.Clear();
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            Output.WriteLine($"Build started at {DateTime.Now.ToString("T", CultureInfo.CurrentCulture)}...", OutputKind.Info);
            WriteRunNotes();
        }

        BuildResult? last = null;
        int succeeded = 0, failed = 0;
        var reported = new List<BuildDiagnostic>();
        var seen = new HashSet<BuildDiagnostic>();
        using var operation = new CompletingOperation(Operations.Begin("build", "Building"));
        for (var i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            var prefix = $"{(i + 1).ToString(CultureInfo.InvariantCulture)}>";
            StatusText = $"Building {project.Name}";
            operation.Operation.Report(projects.Count > 1
                ? new OperationProgress("Building", i, projects.Count, ProgressUnit.Projects)
                : new OperationProgress($"Building {project.Name}"));
            operation.Operation.Log($"Building {project.Name}");
            Output.WriteLine($"{prefix}------ Build started: Project: {project.Name}, Configuration: {Configuration} ------", OutputKind.Info);
            if (RestoreFailed(project))
            {
                // As before, a failed restore does not stop the build by itself: its errors are in the Error List, and
                // the build warns (TS2003) for the packages that are missing. The project compiles against the rest.
                Output.WriteLine($"{prefix}warning: The NuGet restore of {project.Name} reported errors, so it is built without the packages that could not be restored. The Error List shows the restore's errors.", OutputKind.Warning);
            }

            last = await (build ?? language.BuildAsync)(project, CancellationToken.None);
            results?.Add(last);
            var fresh = last.Diagnostics.Where(seen.Add).ToList();
            reported.AddRange(fresh);

            // The build replaces what live analysis last said about the project's files, which may be from before its
            // generators were loaded.
            ErrorList.SetProjectDiagnostics(project.DirectoryPath, [.. last.Diagnostics.Where(d => d.ProjectName == project.Name)]);
            ErrorList.SetBuildResult(last with { Diagnostics = reported.OrderByDescending(d => d.Severity).ToList() });
            foreach (var diagnostic in fresh.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning))
            {
                Output.WriteLine(prefix + diagnostic, diagnostic.Severity == DiagnosticSeverityLevel.Error ? OutputKind.Failure : OutputKind.Program);
            }

            if (last.Success)
            {
                succeeded++;
                var size = (last.EntryAssembly!.Image.Length / 1024.0).ToString("0.#", CultureInfo.InvariantCulture);
                var time = last.Duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
                Output.WriteLine($"{prefix}{project.Name} -> compiled in memory ({size} KB, {time} ms)", OutputKind.Info);
            }
            else
            {
                failed++;
            }
        }

        for (var i = 0; i < skipped.Count; i++)
        {
            var prefix = $"{(projects.Count + i + 1).ToString(CultureInfo.InvariantCulture)}>";
            Output.WriteLine($"{prefix}------ Skipped: Project: {skipped[i].Name}: {SkipReason(skipped[i], skipped)} ------", OutputKind.Info);
        }

        operation.Outcome = failed == 0 ? OperationOutcome.Succeeded : OperationOutcome.Failed;
        var skippedText = skipped.Count == 0 ? string.Empty : $", {skipped.Count.ToString(CultureInfo.InvariantCulture)} skipped";
        var summary = $"========== Build: {succeeded.ToString(CultureInfo.InvariantCulture)} succeeded, {failed.ToString(CultureInfo.InvariantCulture)} failed{skippedText} ==========";
        Output.WriteLine(summary, failed == 0 ? OutputKind.Success : OutputKind.Failure);

        if (failed > 0)
        {
            State = IdeState.Failed;
            StatusText = "Build failed";
            SelectedPanel = BottomPanel.ErrorList;
        }
        else
        {
            State = IdeState.Ready;
            StatusText = "Build succeeded";
        }

        return last;
    }

    /// <summary>
    /// After an edit, re-analyzes the rest of the edited file's project in the background, so errors the edit
    /// caused in other files appear in the Error List too.
    /// </summary>
    private void ScheduleProjectAnalysis(DocumentViewModel document)
    {
        if (!_profile.AnalyzeWholeSolution || _language is not { } language || Workspace?.FindProjectForFile(document.FilePath) is not { } project)
        {
            return;
        }

        if (_projectAnalysis.Remove(project.FilePath, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_background?.Token ?? CancellationToken.None);
        _projectAnalysis[project.FilePath] = cancellation;
        _ = AnalyzeProjectLaterAsync(language, project, cancellation.Token);
    }

    /// <summary>
    /// Typing stops the project's background analysis: it compiled the whole project on every core two seconds after
    /// each pause and kept going when typing resumed. The edit's own analysis schedules a fresh one.
    /// </summary>
    private void CancelProjectAnalysis(DocumentViewModel document)
    {
        if (_projectAnalysis.Count == 0 || Workspace?.FindProjectForFile(document.FilePath) is not { } project
            || !_projectAnalysis.Remove(project.FilePath, out var running))
        {
            return;
        }

        running.Cancel();
        running.Dispose();
    }

    /// <summary>Whether a project's background analysis is waiting or running, for tests.</summary>
    /// <param name="projectPath">The project file.</param>
    internal bool IsProjectAnalysisPending(string projectPath) => _projectAnalysis.ContainsKey(projectPath);

    private async Task AnalyzeProjectLaterAsync(ILanguageService language, ProjectInfo project, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            if (_device.Thermal == ThermalState.Hot)
            {
                return;
            }

            var diagnostics = await language.GetProjectDiagnosticsAsync(project.FilePath, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                ApplyProjectDiagnostics(project, diagnostics);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer edit scheduled a fresh analysis.
        }
    }
}
