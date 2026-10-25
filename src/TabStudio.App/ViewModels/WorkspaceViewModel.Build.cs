using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
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
        if (UsesLaunchTarget && CurrentLaunchTarget is { } target)
        {
            await LaunchAdapterAsync(target, DebugStart.Run, noDebug: true);
            return;
        }

        if (StartupProject is not { } project || _language is null)
        {
            return;
        }

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
        RunResult result;
        try
        {
            var packages = _language.GetPackageRuntimeAssemblies(project.FilePath);
            result = await _runner.RunAsync(entry, build.Dependencies, packages, [], Output, input, _run.Token);
        }
        finally
        {
            Output.EndInput();
            _run.Dispose();
            _run = null;
        }

        ReportRunResult(project, result);
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
        if (Workspace is { } workspace)
        {
            await BuildProjectsAsync(workspace.Projects.Where(p => p.Language == ProjectLanguage.CSharp).ToList(), showOutput: true);
        }
    }

    private bool CanRun() => (StartupProject is not null || UsesLaunchTarget) && State is IdeState.Ready or IdeState.Failed && !IsNotebookRunning;

    private bool CanBuild() => State is IdeState.Ready or IdeState.Failed;

    /// <summary>Builds projects one after another, reporting in the Output panel and the Error List.</summary>
    /// <param name="projects">The projects.</param>
    /// <param name="showOutput">Whether to clear the Output panel and bring it forward.</param>
    /// <param name="build">Builds one project, such as a debug build; the language service's normal build by default.</param>
    /// <param name="results">Receives the result of every project, in order, when given.</param>
    /// <returns>The result for the last project, or <see langword="null"/> when nothing was built.</returns>
    private async Task<BuildResult?> BuildProjectsAsync(
        List<ProjectInfo> projects,
        bool showOutput,
        Func<ProjectInfo, CancellationToken, Task<BuildResult>>? build = null,
        List<BuildResult>? results = null)
    {
        if (_language is not { } language || projects.Count == 0)
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

        if (showOutput)
        {
            Output.Clear();
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            Output.WriteLine($"Build started at {DateTime.Now.ToString("T", CultureInfo.CurrentCulture)}...", OutputKind.Info);
        }

        BuildResult? last = null;
        int succeeded = 0, failed = 0;
        for (var i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            var prefix = $"{(i + 1).ToString(CultureInfo.InvariantCulture)}>";
            StatusText = $"Building {project.Name}";
            Output.WriteLine($"{prefix}------ Build started: Project: {project.Name}, Configuration: {Configuration} ------", OutputKind.Info);

            last = await (build ?? language.BuildAsync)(project, CancellationToken.None);
            results?.Add(last);
            ErrorList.SetBuildResult(last);
            foreach (var diagnostic in last.Diagnostics.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning))
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

        var summary = $"========== Build: {succeeded.ToString(CultureInfo.InvariantCulture)} succeeded, {failed.ToString(CultureInfo.InvariantCulture)} failed ==========";
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
