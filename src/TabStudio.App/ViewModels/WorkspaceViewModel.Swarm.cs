using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Previews;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels.Swarm;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.ViewModels;

/// <summary>
/// TabStudio Swarm on the tablet: "Swarm" in the Run button's menu builds the startup project once and runs it on every
/// device of the swarm (an Avalonia app full screen, a console app with its output here, a web app the tablet serves),
/// every save updates them all, and F5 debugs it on all of them with the tablet's debugger
/// (<c>WorkspaceViewModel.SwarmDebugging.cs</c>). The Swarm tool window lists the devices; recorded UI tests replay on
/// every screen (<c>WorkspaceViewModel.UiTests.cs</c>).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private SwarmServices? _swarmServices;
    private SwarmOutputLines? _swarmLines;
    private ProjectInfo? _swarmProject;
    private bool _restartSwarm;

    /// <summary>The Swarm tool window, or <see langword="null"/> where the app has no swarm, as in some tests.</summary>
    public SwarmViewModel? Swarm { get; private set; }

    /// <summary>Whether the app has a swarm, for the menus and the side bar's tab.</summary>
    public bool IsSwarmAvailable => Swarm is not null;

    /// <summary>Whether the side bar shows the Swarm window.</summary>
    public bool IsSwarmSelected => SelectedSideBarPanel == SideBarPanel.Swarm;

    /// <summary>Whether F5 and Ctrl+F5 run the startup project on every device of the swarm, as "Swarm" in the Run button's menu chooses.</summary>
    [ObservableProperty]
    public partial bool IsRunOnSwarm { get; private set; }

    /// <summary>Whether File &gt; Join a Swarm is offered: the Link dialog has its Join a swarm half.</summary>
    public bool CanJoinSwarm => _linkDialog?.HasSwarm == true;

    /// <summary>The Swarm pane of the Output panel: every device's console output, each line under its name.</summary>
    public OutputViewModel SwarmOutput => field ??= new OutputViewModel(_ui, _profile.OutputCharacterLimit);

    /// <summary>The Run button's "Run on" text while it runs on the swarm, such as "Swarm · 3 devices".</summary>
    private string SwarmRunHostText => _swarmServices?.Coordinator.ConnectedCount switch
    {
        null or 0 => "Swarm · no devices yet",
        1 => "Swarm · 1 device",
        var count => string.Create(CultureInfo.CurrentCulture, $"Swarm · {count} devices"),
    };

    /// <summary>File &gt; Join a Swarm: the Link dialog's Join a swarm half, for a device that runs a tablet's app.</summary>
    [RelayCommand]
    private void JoinSwarm() => _linkDialog?.Open(LinkTab.Swarm);

    /// <summary>Makes the startup project run on the swarm, as choosing "Swarm" in the Run button's menu does.</summary>
    public void SelectSwarmTarget()
    {
        if (Swarm is null)
        {
            return;
        }

        if (StartupProject is null && StartupProjects.FirstOrDefault() is { } first)
        {
            first.Select.Execute(null);
        }

        IsRunOnSwarm = true;
        OnPropertyChanged(nameof(RunHostText));
        if (!Swarm.IsOn)
        {
            _ = Swarm.StartCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Sets up the swarm. Called once, by the constructor.</summary>
    /// <param name="swarm">The app's swarm services, or <see langword="null"/> without one.</param>
    private void InitializeSwarm(SwarmServices? swarm)
    {
        if (swarm is null)
        {
            return;
        }

        _swarmServices = swarm;
        _swarmLines = new SwarmOutputLines(SwarmOutput);
        var recorder = new UiTestRecorderViewModel(swarm.Registry, _ui, SaveUiTestAsync);
        Swarm = new SwarmViewModel(swarm.Coordinator, _ui, recorder, RunUiTestsOnEveryScreenAsync);
        var coordinator = swarm.Coordinator;
        coordinator.Changed += OnSwarmChanged;
        coordinator.OutputReceived += OnSwarmOutput;
        coordinator.AppExited += OnSwarmAppExited;
        coordinator.Deployed += OnSwarmDeployed;
        InitializeSwarmDebugging(coordinator);
        InitializeUiTests(swarm);

        // A swarm that was on when TabStudio closed is on again, so its devices rejoin by themselves.
        _ = coordinator.ResumeAsync();
    }

    /// <summary>Stops listening to the swarm when the workspace closes; the swarm itself stays for the next workspace.</summary>
    private void DisposeSwarm()
    {
        if (_swarmServices?.Coordinator is not { } coordinator)
        {
            return;
        }

        coordinator.Changed -= OnSwarmChanged;
        coordinator.OutputReceived -= OnSwarmOutput;
        coordinator.AppExited -= OnSwarmAppExited;
        coordinator.Deployed -= OnSwarmDeployed;
        DisposeSwarmDebugging(coordinator);
        DisposeUiTests();
        Swarm?.Dispose();
        if (coordinator.Coordinator?.CurrentRun is not null)
        {
            _ = coordinator.Coordinator.StopRunAsync();
        }
    }

    /// <summary>"Swarm" in the Run button's menu, after the startup projects, for apps the devices can run.</summary>
    private void AddSwarmRunTarget(List<RunTargetOption> targets)
    {
        if (Swarm is not null && StartupProjects.Count > 0)
        {
            var project = StartupProject?.Name ?? StartupProjects[0].Name;
            targets.Add(new RunTargetOption("Swarm", $"{project} on every device", new RelayCommand(SelectSwarmTarget)));
        }
    }

    /// <summary>Wraps a startup project's menu entry so that choosing it runs it on the tablet again, not on the swarm.</summary>
    private RelayCommand WithoutSwarm(IRelayCommand select) => new(() =>
    {
        IsRunOnSwarm = false;
        OnPropertyChanged(nameof(RunHostText));
        select.Execute(null);
    });

    /// <summary>
    /// F5 and Ctrl+F5 on the swarm: builds the startup project once, for debugging with F5, and runs it on every device
    /// until it is stopped, building and updating every device again whenever one of its files is saved.
    /// </summary>
    /// <param name="project">The startup project.</param>
    /// <param name="debug">Whether F5 asked for the debugger.</param>
    /// <param name="pauseAtStart">Whether F10 or F11 started it, to pause at its first statement.</param>
    private async Task RunOnSwarmAsync(ProjectInfo project, bool debug, bool pauseAtStart)
    {
        if (_swarmServices?.Coordinator is not { } swarm || _language is not { } language || _swarmProject is not null)
        {
            return;
        }

        if (!swarm.IsOn && !await swarm.StartAsync())
        {
            Output.WriteLine(swarm.Error ?? "The swarm could not start.", OutputKind.Failure);
            StatusText = "The swarm could not start";
            return;
        }

        var kind = SwarmKindOf(project);
        debug &= kind != SwarmAppKind.Web;
        SwarmOutput.Clear();
        SelectSideBarPanel(SideBarPanel.Swarm);
        var restarted = false;
        do
        {
            _restartSwarm = false;
            DebugBuild? debugBuild = null;
            var built = debug
                ? await BuildProjectsAsync([project], showOutput: !restarted, async (target, cancellationToken) =>
                {
                    debugBuild = await language.BuildForDebuggingAsync(target, cancellationToken);
                    return debugBuild.Result;
                })
                : await BuildProjectsAsync([project], showOutput: !restarted);
            if (built is not { Success: true } || built.EntryAssembly is not { } entry || (debug && debugBuild is not { Success: true }))
            {
                break;
            }

            ShowOutputSource(SwarmOutput);
            await RunBuildOnSwarmAsync(swarm, project, kind, built, entry, debugBuild, pauseAtStart && !restarted, restarted);
            restarted = true;
        }
        while (_restartSwarm && !_disposed);
    }

    /// <summary>Runs one build on the swarm until it is stopped or a save replaces it.</summary>
    private async Task RunBuildOnSwarmAsync(
        SwarmCoordinatorService swarm,
        ProjectInfo project,
        SwarmAppKind kind,
        BuildResult built,
        CompiledAssembly entry,
        DebugBuild? debugBuild,
        bool pauseAtStart,
        bool restarted)
    {
        var coordinator = swarm.Coordinator!;
        var packages = _language!.GetPackageRuntimeAssemblies(project.FilePath);
        State = debugBuild is null ? IdeState.Running : IdeState.Debugging;
        StatusText = restarted ? $"Updating {project.Name} on the swarm" : $"Starting {project.Name} on the swarm";
        _run = new CancellationTokenSource();
        _swarmProject = project;
        var token = _run.Token;
        WebAppHost? web = null;
        ConsoleInput? input = null;
        try
        {
            SwarmWebTarget? webTarget = null;
            if (kind == SwarmAppKind.Web)
            {
                // The tablet serves the web app once, on its loopback address only; the devices reach it through the swarm.
                var profile = LaunchSettings.Read(project.DirectoryPath);
                web = await WebAppHost.StartAsync(entry, built.Dependencies, packages, project.DirectoryPath, SwarmOutput, new WebAppOptions { Environment = profile.Environment ?? "Development", Arguments = profile.CommandLineArgs }, token);
                if (!web.IsRunning)
                {
                    SwarmOutput.WriteLine($"{project.Name} could not start on this tablet, so the devices have nothing to show.", OutputKind.Failure);
                    return;
                }

                webTarget = new SwarmWebTarget(web.Address, profile.LaunchUrl is { Length: > 0 } launch ? "/" + launch.TrimStart('/') : "/");
                WebPreview.Show(project.Name, web.Address, profile.LaunchUrl);
            }

            if (debugBuild is not null)
            {
                BeginSwarmDebugging(project, debugBuild);
            }

            if (kind == SwarmAppKind.Console)
            {
                // What is typed in the Swarm pane goes to the console app on every device.
                input = SwarmOutput.BeginInput();
                var reading = input;
                _ = Task.Run(() => ForwardConsoleInput(reading, coordinator), CancellationToken.None);
            }

            var build = new SwarmBuild
            {
                AppName = project.Name,
                Kind = kind,
                Entry = entry,
                Dependencies = kind == SwarmAppKind.Web ? [] : built.Dependencies,
                PackageAssemblies = kind switch
                {
                    SwarmAppKind.Web => [],
                    SwarmAppKind.Avalonia => [.. packages.Where(p => !AvaloniaAppHost.IsSharedWithHost(Path.GetFileNameWithoutExtension(p)))],
                    _ => packages,
                },
                Debug = debugBuild is null ? null : new SwarmDebugOptions(debugBuild.DebugInfo, BreakOnUnhandledExceptions, pauseAtStart),
                Web = webTarget,
            };
            SwarmOutput.WriteLine(
                restarted ? $"Updating every device with the saved changes to {project.Name}." : $"Running {project.Name} on the swarm{(debugBuild is null ? string.Empty : " with the debugger")}.",
                OutputKind.Info);
            var run = await coordinator.RunAsync(build, token);
            var reached = run.Deployments.Count(d => d.Succeeded);
            StatusText = run.Deployments.Count == 0
                ? $"{project.Name} waits for devices to join the swarm"
                : string.Create(CultureInfo.CurrentCulture, $"{(debugBuild is null ? "Running" : "Debugging")} {project.Name} on {reached} of {run.Deployments.Count} devices");
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException)
        {
            // Stopped, or a save replaces this build with the next.
        }
        catch (IOException ex)
        {
            SwarmOutput.WriteLine($"{project.Name} could not be sent to the swarm: {ex.Message}", OutputKind.Failure);
        }
        finally
        {
            if (input is not null)
            {
                SwarmOutput.EndInput();
            }

            if (!_restartSwarm || _disposed)
            {
                await coordinator.StopRunAsync();
                SwarmOutput.WriteLine($"{project.Name} was stopped on every device.", OutputKind.Info);
            }

            if (web is not null)
            {
                await web.StopAsync();
                await web.DisposeAsync();
                WebPreview.ShowStopped();
            }

            EndSwarmDebugging();
            _swarmProject = null;
            _run.Dispose();
            _run = null;
            State = IdeState.Ready;
            StatusText = _restartSwarm ? $"Updating {project.Name} on the swarm" : "Ready";
        }
    }

    /// <summary>Restarts the swarm's app with the saved changes when one of its project's files is saved.</summary>
    private void OnDocumentSavedForSwarm(DocumentViewModel document)
    {
        if (_swarmProject is not { } project || _run is not { } run || Workspace is not { } workspace
            || workspace.FindProjectForFile(document.FilePath) is not { } owner
            || !DependsOn(workspace, project, owner))
        {
            return;
        }

        _restartSwarm = true;
        run.Cancel();
    }

    /// <summary>How the devices run a project: an Avalonia app full screen, a web app through the tablet, or a console app.</summary>
    private static SwarmAppKind SwarmKindOf(ProjectInfo project) =>
        IsAvaloniaApp(project) ? SwarmAppKind.Avalonia : IsWebApp(project) ? SwarmAppKind.Web : SwarmAppKind.Console;

    /// <summary>Sends the lines typed in the Swarm pane to every device, until the run ends. Runs off the UI thread.</summary>
    private static void ForwardConsoleInput(ConsoleInput input, SwarmCoordinator coordinator)
    {
        while (input.ReadLine() is { } line)
        {
            coordinator.SendConsoleInput(line);
        }
    }

    private void OnSwarmChanged(object? sender, EventArgs e)
    {
        if (IsRunOnSwarm)
        {
            OnPropertyChanged(nameof(RunHostText));
        }

        UpdateRunTargets();
    }

    private void OnSwarmOutput(object? sender, SwarmOutputEventArgs e) => _swarmLines?.Write(e.Device, e.Text, e.IsError);

    private void OnSwarmDeployed(object? sender, SwarmDeployment deployment)
    {
        if (_swarmLines is not { } lines)
        {
            return;
        }

        if (!deployment.Succeeded)
        {
            // A device that fails never stops the others.
            lines.Note(deployment.Device, $"did not get the build: {deployment.Error}", OutputKind.Failure);
            return;
        }

        var size = deployment.BytesSent < 1024 * 1024
            ? string.Create(CultureInfo.CurrentCulture, $"{deployment.BytesSent / 1024.0:0.#} KB")
            : string.Create(CultureInfo.CurrentCulture, $"{deployment.BytesSent / (1024.0 * 1024):0.#} MB");
        lines.Note(
            deployment.Device,
            deployment.FilesSent == 0
                ? string.Create(CultureInfo.CurrentCulture, $"is up to date: it had all {deployment.FileCount} files already ({deployment.Duration.TotalSeconds:0.0} s).")
                : string.Create(CultureInfo.CurrentCulture, $"received {deployment.FilesSent} of {deployment.FileCount} files, {size}, in {deployment.Duration.TotalSeconds:0.0} s."),
            OutputKind.Info);
    }

    private void OnSwarmAppExited(object? sender, SwarmExitEventArgs e)
    {
        if (_swarmLines is not { } lines)
        {
            return;
        }

        lines.Flush(e.Device);
        var name = e.Device.AppName ?? _swarmProject?.Name ?? "The app";
        switch (e.Outcome)
        {
            case RunOutcome.Crashed when e.Fault is { } fault:
                lines.Note(e.Device, $"{name} crashed: {fault.Type}: {fault.Message}", OutputKind.Error);
                foreach (var frame in (fault.StackTrace ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    lines.Note(e.Device, frame.TrimEnd('\r'), OutputKind.Error);
                }

                break;
            case RunOutcome.Crashed:
                lines.Note(e.Device, $"{name} crashed.", OutputKind.Error);
                break;
            case RunOutcome.Completed:
                lines.Note(e.Device, string.Create(CultureInfo.InvariantCulture, $"The program '{name}' has exited with code {e.ExitCode} (0x{e.ExitCode:x})."), OutputKind.Info);
                break;
            case RunOutcome.NoEntryPoint:
                lines.Note(e.Device, $"'{name}' has no Main method or top-level statements to run.", OutputKind.Failure);
                break;
        }
    }

    partial void OnIsRunOnSwarmChanged(bool value) => UpdateRunTargets();
}
