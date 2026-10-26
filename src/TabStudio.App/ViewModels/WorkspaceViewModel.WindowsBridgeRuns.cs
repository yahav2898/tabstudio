using System.Globalization;
using System.Text.Json.Nodes;
using TabStudio.Compilation;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Link;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Building, running and debugging on the Windows Bridge: the workspace goes to the PC (only the files that changed),
/// the PC builds with its own .NET SDK or MSBuild, its output streams into the Output panel and its errors land in the
/// Error List on this device's files, and the program runs on the PC with its windows in the Bridge pane. Several
/// programs can run at once; a save rebuilds and restarts the programs it changed, building only the projects that
/// changed; .NET programs debug under netcoredbg on the PC, driven by this device's debugger.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>How long a restart waits for more saves, as Save All makes several in a row.</summary>
    private static readonly TimeSpan BridgeRestartDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>The programs this workspace runs on the PC, by the bridge's number for them.</summary>
    private readonly Dictionary<int, BridgeRun> _bridgeRuns = [];

    private DebugAdapterSession? _bridgeDebugSession;
    private ProjectInfo? _bridgeDebugProject;

    /// <summary>Whether programs this workspace started run on the PC, so Stop can stop one.</summary>
    private bool HasWindowsBridgeApps => _bridgeRuns.Count > 0;

    /// <summary>
    /// Run (Ctrl+F5) for a project the bridge runs: builds it on the PC and starts it there. A Visual C++ program
    /// without a PC is told where to pair one.
    /// </summary>
    /// <returns>Whether the bridge took the project.</returns>
    private async Task<bool> TryRunOnWindowsBridgeAsync(ProjectInfo project)
    {
        if (!RunsOnWindowsBridge(project))
        {
            return ExplainNativeWithoutPc(project);
        }

        await RunOnWindowsBridgeAsync(project, showOutput: true, skipReferences: false, replacing: null);
        return true;
    }

    /// <summary>
    /// Start Debugging (F5) for a project the bridge runs: a .NET program debugs under netcoredbg on the PC; a Visual
    /// C++ program runs without the debugger, which debugs .NET code only.
    /// </summary>
    /// <returns>Whether the bridge took the project.</returns>
    private async Task<bool> TryDebugOnWindowsBridgeAsync(ProjectInfo project, DebugStart start)
    {
        if (!RunsOnWindowsBridge(project))
        {
            return ExplainNativeWithoutPc(project);
        }

        if (WindowsBridgeCompatibility.IsNativeApplication(project))
        {
            await RunOnWindowsBridgeAsync(project, showOutput: true, skipReferences: false, replacing: null);
            Output.WriteLine($"Started {project.Name} without the debugger: netcoredbg debugs .NET programs, and {project.Name} is a Visual C++ program.", OutputKind.Info);
            return true;
        }

        if (_adapter is null)
        {
            await DebugOnWindowsBridgeAsync(project, start);
        }

        return true;
    }

    private bool ExplainNativeWithoutPc(ProjectInfo project)
    {
        if (_windowsBridge is null || !WindowsBridgeCompatibility.IsNativeApplication(project))
        {
            return false;
        }

        Output.Clear();
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine($"{project.Name} is a Visual C++ program: it builds with MSBuild and runs on Windows. Pair a Windows PC in File > TabStudio Link > Windows PC, and it builds and runs there.", OutputKind.Failure);
        StatusText = "Visual C++ programs run on a Windows PC";
        State = IdeState.Failed;
        return true;
    }

    /// <summary>Builds a project on the PC and starts it there, in place of the run it replaces, if any.</summary>
    private async Task RunOnWindowsBridgeAsync(ProjectInfo project, bool showOutput, bool skipReferences, BridgeRun? replacing)
    {
        if (replacing is not null)
        {
            // Windows does not let a build overwrite the files of a program that runs, so the run it replaces ends before
            // the build starts, as it does in Visual Studio.
            await StopBridgeRunAsync(replacing, restarting: true);
            await WaitForBridgeRunToEndAsync(replacing);
        }

        if (await BuildOnWindowsBridgeAsync(project, showOutput, skipReferences) is not { Success: true, Program: { } program } || _windowsBridge is not { } bridge)
        {
            return;
        }

        var pc = bridge.PcName ?? "the PC";
        try
        {
            var started = await bridge.Client.RunAsync(project.FilePath, program);
            _bridgeRuns[started.AppId] = new BridgeRun(project, started.AppId);
            Output.WriteLine($"{started.Name} started on {pc} (process {started.ProcessId.ToString(CultureInfo.InvariantCulture)}). Its windows are in the Bridge pane.", OutputKind.Info);
            WindowsBridgePane?.Show(started);
            SelectedSideBarPanel = SideBarPanel.WindowsBridge;
            IsSideBarVisible = true;
            State = IdeState.Ready;
            StatusText = $"{project.Name} runs on {pc}";
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException or TimeoutException)
        {
            Output.WriteLine($"{project.Name} did not start on {pc}: {ex.Message}", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = $"{project.Name} did not start on {pc}";
        }

        StopDebuggingCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Sends the workspace's changed files to the PC and builds a project there, with its output in the Output panel
    /// and its errors and warnings in the Error List, on this device's files.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="showOutput">Whether to clear the Output panel and bring it forward.</param>
    /// <param name="skipReferences">Whether to build only the project, because the projects it references did not change.</param>
    /// <returns>What the build made, or <see langword="null"/> when the PC could not build.</returns>
    private async Task<BuildOutcome?> BuildOnWindowsBridgeAsync(ProjectInfo project, bool showOutput, bool skipReferences)
    {
        if (_windowsBridge is not { } bridge || Workspace is not { } workspace)
        {
            return null;
        }

        State = IdeState.Busy;
        await SaveAllCoreAsync();
        if (showOutput)
        {
            Output.Clear();
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
        }

        var pc = bridge.PcName ?? "the PC";
        StatusText = $"Connecting to {pc}";
        if (await bridge.EnsureConnectedAsync() is not { } client)
        {
            Output.WriteLine($"{pc} is not answering, so {project.Name} cannot build there now. Keep working: run it again when the PC is awake and on the same Wi-Fi.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = $"{pc} is away";
            return null;
        }

        try
        {
            Output.WriteLine($"Build started on {pc} at {DateTime.Now.ToString("T", CultureInfo.CurrentCulture)}...", OutputKind.Info);
            StatusText = $"Sending {workspace.Name} to {pc}";
            var sync = await client.SyncWorkspaceAsync(workspace, FileSystemTextProvider.Instance);
            Output.WriteLine(
                sync.SentFiles.Count == 0 ? $"{pc} has every file already." : $"Sent {sync.SentFiles.Count.ToString(CultureInfo.InvariantCulture)} changed files to {pc}.",
                OutputKind.Info);

            StatusText = $"Building {project.Name} on {pc}";
            Output.WriteLine($"1>------ Build started: Project: {project.Name}, Configuration: {Configuration}, on {pc} ------", OutputKind.Info);
            var configuration = FindToolchainProject(project) is { } toolchain && TabStudio.Toolchains.Microchip.MicrochipInstallation.IsCompiler(toolchain.Toolchain.Id)
                ? TabStudio.Toolchains.Microchip.MplabProject.Load(project.FilePath).SelectedConfiguration().Name : Configuration;
            var outcome = await client.BuildAsync(project.FilePath, configuration, skipReferences, line => Output.WriteLine("1>" + line, OutputKindOf(line)));
            ErrorList.SetWindowsBridgeDiagnostics(project.FilePath, pc, outcome.Diagnostics);
            var time = outcome.Duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            Output.WriteLine(
                outcome.Success
                    ? $"========== Build: 1 succeeded, 0 failed, on {pc} with {outcome.Tool} in {time} s =========="
                    : $"========== Build: 0 succeeded, 1 failed, on {pc} with {outcome.Tool} in {time} s ==========",
                outcome.Success ? OutputKind.Success : OutputKind.Failure);
            if (!outcome.Success)
            {
                State = IdeState.Failed;
                StatusText = "Build failed";
                SelectedPanel = BottomPanel.ErrorList;
                IsBottomPanelVisible = true;
                return outcome;
            }

            State = IdeState.Ready;
            StatusText = "Build succeeded";
            return outcome;
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException or TimeoutException)
        {
            Output.WriteLine($"The build on {pc} did not finish: {ex.Message}", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = $"The build on {pc} did not finish";
            return null;
        }
    }

    /// <summary>Errors in red and warnings as the program's text, as the Output panel shows a local build.</summary>
    private static OutputKind OutputKindOf(string line) =>
        MsBuildMessage.Parse(line) is { Severity: DiagnosticSeverityLevel.Error } ? OutputKind.Failure : OutputKind.Program;

    /// <summary>Restart in the Bridge pane: builds the program's project again and starts it over.</summary>
    private async Task RestartBridgeAppAsync(int appId)
    {
        if (_bridgeRuns.TryGetValue(appId, out var run) && !run.IsRestarting)
        {
            run.IsRestarting = true;
            await RunOnWindowsBridgeAsync(run.Project, showOutput: false, skipReferences: false, replacing: run);
            run.IsRestarting = false;
        }
    }

    /// <summary>
    /// A save restarts the programs on the PC that it changed, after a moment for the saves that follow it: their
    /// project builds again, and the projects it references only when one of them changed.
    /// </summary>
    private void OnDocumentSavedForWindowsBridge(DocumentViewModel document)
    {
        if (_bridgeRuns.Count == 0 || Workspace?.FindProjectForFile(document.FilePath) is not { } saved)
        {
            return;
        }

        foreach (var run in _bridgeRuns.Values.Where(r => !r.IsRestarting && DependsOn(r.Project, saved.FilePath)).ToList())
        {
            run.ChangedProjects.Add(saved.FilePath);
            if (run.ChangedProjects.Count == 1)
            {
                _ = RestartAfterSavesAsync(run);
            }
        }
    }

    private async Task RestartAfterSavesAsync(BridgeRun run)
    {
        await Task.Delay(BridgeRestartDelay);
        if (_disposed || !_bridgeRuns.ContainsKey(run.AppId) || run.IsRestarting)
        {
            return;
        }

        var onlyItself = run.ChangedProjects.All(p => p == run.Project.FilePath);
        run.ChangedProjects.Clear();
        run.IsRestarting = true;
        Output.WriteLine();
        Output.WriteLine(
            onlyItself
                ? $"Restarting '{run.Project.Name}' with the saved changes, building {run.Project.Name} only."
                : $"Restarting '{run.Project.Name}' with the saved changes, building the projects that changed.",
            OutputKind.Info);
        await RunOnWindowsBridgeAsync(run.Project, showOutput: false, skipReferences: onlyItself, replacing: run);
        run.IsRestarting = false;
    }

    /// <summary>Whether a project is, or references directly or through others, the project at a path.</summary>
    private bool DependsOn(ProjectInfo project, string projectPath)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([project.FilePath]);
        while (pending.TryPop(out var path))
        {
            if (string.Equals(path, projectPath, StringComparison.Ordinal))
            {
                return true;
            }

            if (seen.Add(path) && Workspace?.Projects.FirstOrDefault(p => p.FilePath == path) is { } current)
            {
                foreach (var reference in current.ProjectReferences)
                {
                    pending.Push(reference);
                }
            }
        }

        return false;
    }

    /// <summary>Stop (Shift+F5) while programs run on the PC and nothing is debugged: stops the one the Bridge pane shows.</summary>
    private void StopWindowsBridgeApp()
    {
        if (IsDebugging || _bridgeRuns.Count == 0)
        {
            return;
        }

        var shown = WindowsBridgePane?.SelectedApp?.AppId;
        var run = shown is { } id && _bridgeRuns.TryGetValue(id, out var selected) ? selected : _bridgeRuns.Values.Last();
        _ = StopBridgeRunAsync(run, restarting: false);
    }

    /// <summary>Waits, for a few seconds at most, until the PC has reported that a stopped program ended and let go of its files.</summary>
    private async Task WaitForBridgeRunToEndAsync(BridgeRun run)
    {
        for (var i = 0; i < 100 && !_disposed && _bridgeRuns.ContainsKey(run.AppId); i++)
        {
            await Task.Delay(50);
        }
    }

    private async Task StopBridgeRunAsync(BridgeRun run, bool restarting)
    {
        run.IsReplaced = restarting;
        if (_windowsBridge is not { IsConnected: true } bridge)
        {
            return;
        }

        try
        {
            await bridge.Client.StopAsync(run.AppId);
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException)
        {
            // It had ended, or the PC left: its exit is reported either way.
        }
    }

    private void OnBridgeAppStarted(object? sender, AppStarted started) => _ui.Post(() =>
    {
        // The debugger started the program: show its windows beside the code.
        if (!_disposed && started.IsDebugging && _bridgeDebugProject is { } project && started.ProjectPath == project.FilePath)
        {
            WindowsBridgePane?.Show(started);
            SelectedSideBarPanel = SideBarPanel.WindowsBridge;
            IsSideBarVisible = true;
        }
    });

    private void OnBridgeAppOutput(object? sender, AppOutput output) => _ui.Post(() =>
    {
        if (!_disposed && _bridgeRuns.ContainsKey(output.AppId))
        {
            Output.WriteLine(output.Text, output.IsError ? OutputKind.Error : OutputKind.Program);
        }
    });

    private void OnBridgeAppExited(object? sender, AppExited exited) => _ui.Post(() =>
    {
        if (_disposed || !_bridgeRuns.Remove(exited.AppId, out var run))
        {
            return;
        }

        StopDebuggingCommand.NotifyCanExecuteChanged();
        if (run.IsReplaced)
        {
            return;
        }

        Output.WriteLine();
        Output.WriteLine(
            exited.ExitCode is { } code
                ? $"The program '{run.Project.Name}' has exited with code {code.ToString(CultureInfo.InvariantCulture)} (0x{code:x}) on {_windowsBridge?.PcName ?? "the PC"}."
                : $"The program '{run.Project.Name}' has stopped on {_windowsBridge?.PcName ?? "the PC"}.",
            exited.ExitCode is null or 0 ? OutputKind.Info : OutputKind.Failure);
        if (State is IdeState.Ready or IdeState.Failed && _bridgeRuns.Count == 0)
        {
            StatusText = "Ready";
        }
    });

    /// <summary>Starts a .NET program under netcoredbg on the PC, which this device's debugger drives through the bridge.</summary>
    private async Task DebugOnWindowsBridgeAsync(ProjectInfo project, DebugStart start)
    {
        if (_windowsBridge is not { } bridge)
        {
            return;
        }

        var pc = bridge.PcName ?? "the PC";
        if (await BuildOnWindowsBridgeAsync(project, showOutput: true, skipReferences: false) is not { Success: true, Program: { } program })
        {
            return;
        }

        if (!program.IsDotNet || bridge.Info?.Capabilities.HasFlag(BridgeCapabilities.DebugDotNet) != true)
        {
            Output.WriteLine(
                program.IsDotNet
                    ? $"{pc} has no netcoredbg, so {project.Name} starts without the debugger. Install it in the Windows Bridge window on {pc}."
                    : $"Started {project.Name} without the debugger: netcoredbg debugs .NET programs only.",
                OutputKind.Info);
            await RunOnWindowsBridgeAsync(project, showOutput: false, skipReferences: true, replacing: null);
            return;
        }

        var adapter = BridgeDebugLauncher.Adapter(pc);
        Output.WriteLine($"Debugging {project.Name} on {pc} with netcoredbg...", OutputKind.Info);
        State = IdeState.Busy;
        StatusText = $"Starting netcoredbg on {pc}";
        DebugAdapterSession session;
        try
        {
            var options = new DebugAdapterSessionOptions { TraceMessages = _languageServerServices.TraceMessages };
            session = await DebugAdapterSession.StartAsync(adapter, BridgeDebugLauncher.Command, project.DirectoryPath, new BridgeDebugLauncher(bridge.Client, project.FilePath), options);
        }
        catch (Exception ex) when (ex is DebugAdapterException or ServerStartException or OperationCanceledException or TimeoutException)
        {
            FailAdapterStart(adapter, ex.Message);
            return;
        }

        _adapter = session;
        _adapterRoot = session;
        _adapterExitCode = null;
        _adapterTarget = null;
        _adapterLaunched = false;
        _adapterProgram = project.Name;
        _sentBreakpoints.Clear();
        _bridgeDebugSession = session;
        _bridgeDebugProject = project;
        WireAdapterEvents(session);
        session.Ended += (_, _) => _ui.Post(() => OnBridgeDebugEnded(session, project));
        if (session.HasEnded)
        {
            EndAdapterSession(session);
            return;
        }

        ShowExceptionFilters(session);
        if (start == DebugStart.RunToCursor && ActiveDocument is { } cursorDocument)
        {
            _cursorBreakpoint = (cursorDocument.FilePath, cursorDocument.CaretLine);
        }

        State = IdeState.Debugging;
        StatusText = $"Debugging {project.Name} on {pc}";
        OnPropertyChanged(nameof(IsDebuggingWithAdapter));
        var configuration = new LaunchConfiguration
        {
            Name = project.Name,
            Type = adapter.Id,
            Request = "launch",
            Properties = new JsonObject
            {
                ["program"] = program.Path,
                ["cwd"] = Path.GetDirectoryName(program.Path),
                ["stopAtEntry"] = start == DebugStart.StepInto,
                ["justMyCode"] = true,
            },
        };
        try
        {
            var results = await session.LaunchAsync(configuration, AdapterBreakpoints(session), EnabledExceptionFilters(), CancellationToken.None);
            if (ReferenceEquals(session, _adapter))
            {
                _adapterLaunched = true;
                foreach (var (file, set) in results)
                {
                    Breakpoints.ApplyAdapterResults(file, set);
                }
            }
        }
        catch (Exception ex) when (ex is DebugAdapterException or TimeoutException or OperationCanceledException)
        {
            Output.WriteLine($"netcoredbg on {pc} could not start {project.Name}: {ex.Message}", OutputKind.Failure);
            await session.DisposeAsync();
        }
    }

    /// <summary>Whether a debug adapter's session is netcoredbg on the PC, which debugs C# files too.</summary>
    private bool IsWindowsBridgeSession(DebugAdapterSession session) => ReferenceEquals(session, _bridgeDebugSession);

    /// <summary>The debug session on the PC ended, after the debugger's own ending: Restart starts it again on the PC.</summary>
    private void OnBridgeDebugEnded(DebugAdapterSession session, ProjectInfo project)
    {
        if (!ReferenceEquals(session, _bridgeDebugSession))
        {
            return;
        }

        _bridgeDebugSession = null;
        _bridgeDebugProject = null;
        if (_adapterRestart && !_disposed)
        {
            _adapterRestart = false;
            _ = DebugOnWindowsBridgeAsync(project, DebugStart.Run);
        }
    }

    /// <summary>A program this workspace started on the PC.</summary>
    private sealed class BridgeRun(ProjectInfo project, int appId)
    {
        /// <summary>The project it was built from.</summary>
        public ProjectInfo Project { get; } = project;

        /// <summary>The bridge's number for it.</summary>
        public int AppId { get; } = appId;

        /// <summary>The projects saved since it started, which a restart builds.</summary>
        public HashSet<string> ChangedProjects { get; } = new(StringComparer.Ordinal);

        /// <summary>Whether it is being built again to restart.</summary>
        public bool IsRestarting { get; set; }

        /// <summary>Whether a new run replaced it, so its exit is not reported.</summary>
        public bool IsReplaced { get; set; }
    }
}
