using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.Services.WindowsCompatibility;
using TabStudio.App.Views.WindowsCompatibility;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.WindowsCompatibility;
using TabStudio.WindowsCompatibility.Building;
using TabStudio.WindowsCompatibility.Sessions;

namespace TabStudio.App.ViewModels;

/// <summary>One way to run a Windows-only project, as the Compatibility Doctor lists it.</summary>
/// <param name="Option">The run host and whether it can run the project.</param>
/// <param name="IsChosen">Whether F5 uses it.</param>
/// <param name="Choose">Makes F5 use it.</param>
/// <param name="Install">Installs what it needs, when it can be installed.</param>
public sealed record RunHostChoiceViewModel(RunHostOption Option, bool IsChosen, IRelayCommand Choose, IAsyncRelayCommand? Install)
{
    /// <summary>The host's name.</summary>
    public string Name => Option.Name;

    /// <summary>How it runs the project, or why it cannot.</summary>
    public string Explanation => Option.Explanation;

    /// <summary>Whether it runs the project today.</summary>
    public bool IsAvailable => Option.IsAvailable;

    /// <summary>Whether it can be chosen and is not chosen yet.</summary>
    public bool CanChoose => Option.IsAvailable && !IsChosen;

    /// <summary>Whether installing something makes it available.</summary>
    public bool CanInstall => Install is not null;

    /// <summary>A short state, such as "Ready" or "Needs to be installed".</summary>
    public string State => IsChosen ? "Runs with F5" : CompatibilityStatusNames.For(Option.Status);
}

/// <summary>
/// Windows Compatibility: Windows Forms and WPF apps built on the tablet and run there, the real app on the real
/// .NET Windows Desktop runtime under Wine, shown in the app preview with touch, mouse and keyboard, its console in
/// the Output panel, restarted when one of its files is saved.
/// </summary>
/// <remarks>
/// <para>
/// A Windows-only project can run on several hosts: TabStudio's own layers for Windows code, Windows Compatibility,
/// or a PC through the Windows Bridge. Each registers an <see cref="IRunHostProvider"/> in
/// <see cref="RunHostProviders"/>; the Compatibility Doctor lists every option with why it is or is not available,
/// the Run button's menu offers those that run the project, and F5 uses the one chosen, or the best available.
/// </para>
/// <para>
/// The Windows desktop session starts with the first run and stays between runs, so apps start in a moment after
/// the first; it stops when the workspace closes or the device runs low on memory with no app running.
/// </para>
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, RunHost> _chosenHosts = new(StringComparer.Ordinal);
    private WindowsCompatibilityService? _windows;
    private WindowsDesktopView? _windowsView;
    private IReadOnlyList<string>? _netReferenceFiles;

    /// <summary>
    /// The run hosts that say which Windows-only projects they run: Windows Compatibility, and any other a
    /// feature registers, such as TabStudio's Forms layer, OpenSilver or the Windows Bridge.
    /// </summary>
    public IList<IRunHostProvider> RunHostProviders { get; } = [];

    /// <summary>Windows Compatibility on this device.</summary>
    public WindowsCompatibilityService WindowsCompatibility => _windows!;

    /// <summary>The Windows desktop shown in the app preview while a Windows app runs, for tests.</summary>
    internal WindowsDesktopView? WindowsDesktop => _windowsView;

    /// <summary>The ways a project can run, best first, from every registered host.</summary>
    /// <param name="project">The project.</param>
    public IReadOnlyList<RunHostOption> RunHostOptions(ProjectInfo project) => RunHostChoices.For(project, RunHostProviders);

    /// <summary>
    /// The host F5 runs a Windows-only project with: the one chosen, while it can run it, or else the best one that
    /// can or could once installed; <see langword="null"/> for other projects.
    /// </summary>
    /// <param name="project">The project.</param>
    public RunHost? ChosenRunHost(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var options = RunHostOptions(project);
        if (options.Count == 0)
        {
            return null;
        }

        if (_chosenHosts.TryGetValue(project.FilePath, out var chosen) && options.Any(o => o.Host == chosen && (o.IsAvailable || o.IsInstallable)))
        {
            return chosen;
        }

        return options[0] is { } best && (best.IsAvailable || best.IsInstallable) ? best.Host : null;
    }

    /// <summary>Makes F5 run a project with a host, as choosing it in the Run button's menu or the Doctor does.</summary>
    /// <param name="project">The project.</param>
    /// <param name="host">The host.</param>
    public void ChooseRunHost(ProjectInfo project, RunHost host)
    {
        ArgumentNullException.ThrowIfNull(project);
        _chosenHosts[project.FilePath] = host;
        SetStartupProject(project);
        RefreshRunHosts();
    }

    private void InitializeWindowsCompatibility()
    {
        _windows = new WindowsCompatibilityService(_toolchains, _paths);
        RunHostProviders.Add(new WindowsCompatibilityHostProvider(_windows));
        _toolchains.Manager.StatusChanged += OnWindowsToolchainChanged;
        _device.MemoryPressureRaised += OnMemoryPressureForWindows;
    }

    private void DisposeWindowsCompatibility()
    {
        _toolchains.Manager.StatusChanged -= OnWindowsToolchainChanged;
        _device.MemoryPressureRaised -= OnMemoryPressureForWindows;
        _windowsView?.Dispose();
        if (_windows is { } windows)
        {
            _ = Task.Run(async () => await windows.DisposeAsync());
        }
    }

    /// <summary>Whether F5 runs a project as a Windows app under Wine.</summary>
    private bool UsesWindowsCompatibility(ProjectInfo project) => ChosenRunHost(project) == RunHost.WindowsCompatibility;

    /// <summary>The Run button's "Run on" text for a Windows-only project: the host F5 uses.</summary>
    private string? WindowsRunHostText(ProjectInfo project) =>
        ChosenRunHost(project) is { } host ? RunHostOptions(project).FirstOrDefault(o => o.Host == host)?.Name : null;

    /// <summary>The Run button's menu entries for a Windows-only project: one per host that can run it.</summary>
    private List<RunTargetOption> WindowsRunTargets(ProjectInfo project) =>
        [.. RunHostOptions(project)
            .Where(option => option.IsAvailable || option.IsInstallable)
            .Select(option => new RunTargetOption(project.Name, option.Name, new RelayCommand(() => ChooseRunHost(project, option.Host))))];

    /// <summary>
    /// Updates what depends on the run hosts: the Doctor's rows of Windows-only projects, the Run button's label and
    /// its menu. Called when a workspace loads and when a host's state changes.
    /// </summary>
    private void RefreshRunHosts()
    {
        if (Workspace is not { } workspace)
        {
            return;
        }

        foreach (var project in workspace.Projects)
        {
            var options = RunHostOptions(project);
            if (options.Count == 0)
            {
                continue;
            }

            var index = IndexOfRow(project.Name);
            var row = HostChoiceRow(project, options);
            if (index >= 0)
            {
                Compatibility[index] = row;
            }
            else
            {
                Compatibility.Add(row);
            }
        }

        OnPropertyChanged(nameof(RunHostText));
        UpdateRunTargets();
        RunCommand.NotifyCanExecuteChanged();
    }

    private int IndexOfRow(string projectName)
    {
        for (var i = 0; i < Compatibility.Count; i++)
        {
            if (Compatibility[i].Project == projectName)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A Doctor row for a Windows-only project: the verdict of the host F5 uses, and every option below it.</summary>
    private CompatibilityRowViewModel HostChoiceRow(ProjectInfo project, IReadOnlyList<RunHostOption> options)
    {
        var chosen = ChosenRunHost(project);
        var best = options.FirstOrDefault(o => o.Host == chosen) ?? options[0];
        var report = CompatibilityDoctor.Diagnose(project) with
        {
            Host = best.Host,
            Status = best.Status,
            RunsVia = best.Name,
            Explanation = best.Explanation,
        };
        var choices = options.Select(option => new RunHostChoiceViewModel(
            option,
            option.Host == chosen,
            new RelayCommand(() => ChooseRunHost(project, option.Host)),
            option.IsInstallable && option.Host == RunHost.WindowsCompatibility ? new AsyncRelayCommand(InstallWindowsCompatibilityAsync) : null)).ToList();
        return new CompatibilityRowViewModel(report)
        {
            HostChoices = choices,
            ToolchainId = best is { IsInstallable: true, Host: RunHost.WindowsCompatibility } ? WindowsCompatibilityService.ToolchainId : null,
            InstallToolchainText = best is { IsInstallable: true, Host: RunHost.WindowsCompatibility } ? "Install Windows Compatibility" : null,
        };
    }

    private void OnWindowsToolchainChanged(object? sender, TabStudio.Toolchains.Installation.ToolchainStatus status)
    {
        if (status.Toolchain.Id == WindowsCompatibilityService.ToolchainId)
        {
            _ui.Post(() =>
            {
                if (!_disposed)
                {
                    RefreshRunHosts();
                }
            });
        }
    }

    private void OnMemoryPressureForWindows(object? sender, MemoryPressure e)
    {
        // Wine and the X server hold a few hundred megabytes; with no app running, they go.
        if (_windows is { Session: not null } windows && _liveProject is null)
        {
            _ = Task.Run(windows.StopSessionAsync);
        }
    }

    /// <summary>Installs Windows Compatibility from the Toolchain Store.</summary>
    private Task InstallWindowsCompatibilityAsync() => ToolchainStore.InstallAsync(WindowsCompatibilityService.ToolchainId);

    /// <summary>
    /// Makes sure Windows Compatibility is installed, offering to install it from the Toolchain Store first.
    /// </summary>
    /// <returns>Whether apps can run.</returns>
    private async Task<bool> EnsureWindowsCompatibilityAsync(ProjectInfo project)
    {
        var windows = _windows!;
        if (windows.IsInstalled)
        {
            return true;
        }

        if (windows.Problem is { } problem)
        {
            Output.WriteLine($"{project.Name} cannot run here: {problem}", OutputKind.Failure);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            return false;
        }

        if (!windows.CanInstall)
        {
            Output.WriteLine($"{project.Name} needs Windows Compatibility, which cannot be installed on this device.", OutputKind.Failure);
            return false;
        }

        var size = windows.Status.DownloadSize > 0 ? $" It downloads {TabStudio.Toolchains.Linux.DiskSpace.Format(windows.Status.DownloadSize)}." : string.Empty;
        if (!await Prompt.ConfirmAsync(
            "Install Windows Compatibility?",
            $"{project.Name} is a Windows app. Windows Compatibility runs the real app on this tablet: Wine, an X server and .NET's Windows Desktop runtime, from the Toolchain Store.{size}",
            "Install",
            false))
        {
            return false;
        }

        await InstallWindowsCompatibilityAsync();
        return windows.IsInstalled;
    }

    /// <summary>
    /// F5 and Ctrl+F5 for a Windows app: builds it, starts it on the Windows desktop in the app preview, and starts it
    /// again whenever one of its files is saved, until it is stopped or it exits.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="fromDebugger">Whether F5 asked for the debugger, which Windows apps start without.</param>
    private async Task RunWindowsAppAsync(ProjectInfo project, bool fromDebugger)
    {
        if (!await EnsureWindowsCompatibilityAsync(project))
        {
            return;
        }

        var restarted = false;
        do
        {
            _restartLiveApp = false;
            var build = await BuildWindowsAppAsync(project, showOutput: !restarted);
            if (build is not { Success: true, EntryAssembly: { } assembly })
            {
                AppPreview.ShowStopped();
                return;
            }

            if (fromDebugger && !restarted)
            {
                Output.WriteLine("Started without the debugger: the app runs on Windows' .NET under Wine, outside TabStudio's own debugger.", OutputKind.Info);
            }

            await ShowWindowsAppAsync(project, assembly, restarted);
            restarted = true;
        }
        while (_restartLiveApp && !_disposed);
    }

    /// <summary>Builds a Windows app with the Windows Desktop SDK's rules, reporting in the Output panel and the Error List.</summary>
    private async Task<WindowsBuildResult?> BuildWindowsAppAsync(ProjectInfo project, bool showOutput)
    {
        if (LocalRoslynHost() is not { } host)
        {
            Output.WriteLine($"{project.Name} builds on this tablet for Windows Compatibility; disconnect the TabStudio Link host that compiles now, then press F5 again.", OutputKind.Failure);
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

        StatusText = $"Building {project.Name}";
        Output.WriteLine($"1>------ Build started: Project: {project.Name}, Configuration: {Configuration} ------", OutputKind.Info);
        WindowsBuildResult result;
        try
        {
            var references = _netReferenceFiles ??= [.. (await _compiler.GetReferencesAsync()).OfType<PortableExecutableReference>().Select(r => r.FilePath).OfType<string>()];
            var builder = new WindowsDesktopBuilder(host, new NuGetWindowsReferencePack(_compiler.Packages), references);
            var language = _language;
            result = await Task.Run(() => builder.BuildAsync(
                project,
                new WindowsBuildOptions { Configuration = Configuration, PackageRuntimeAssemblies = path => language?.GetPackageRuntimeAssemblies(path) ?? [] }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Output.WriteLine($"1>{project.Name} could not be built: {ex.Message}", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Build failed";
            return null;
        }

        ErrorList.SetBuildResult(result.Build);
        foreach (var diagnostic in result.Build.Diagnostics.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning))
        {
            Output.WriteLine("1>" + diagnostic, diagnostic.Severity == DiagnosticSeverityLevel.Error ? OutputKind.Failure : OutputKind.Program);
        }

        if (result.Success)
        {
            var time = result.Build.Duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
            Output.WriteLine($"1>{project.Name} -> {result.EntryAssembly} ({time} ms)", OutputKind.Info);
            Output.WriteLine("========== Build: 1 succeeded, 0 failed ==========", OutputKind.Success);
            State = IdeState.Ready;
            StatusText = "Build succeeded";
        }
        else
        {
            Output.WriteLine("========== Build: 0 succeeded, 1 failed ==========", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Build failed";
            SelectedPanel = BottomPanel.ErrorList;
        }

        return result;
    }

    /// <summary>Runs one build of a Windows app on the session's desktop, until it exits, is stopped, or restarts.</summary>
    private async Task ShowWindowsAppAsync(ProjectInfo project, string assembly, bool restarted)
    {
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
        var clock = Stopwatch.StartNew();
        RunResult result;
        WindowsProgram? program = null;
        try
        {
            var view = _windowsView ??= CreateWindowsView();
            AppPreview.Show(project.Name, view, "The real Windows app, on .NET's Windows Desktop runtime under Wine on this tablet. A long press right-clicks; two fingers scroll.");
            SelectSideBarPanel(SideBarPanel.AppPreview);
            if (_windows!.Session is null)
            {
                view.ShowStatus("Starting Windows");
            }

            await WaitForLayoutAsync(view, _run.Token);
            var progress = new Progress<string>(message =>
            {
                StatusText = message;
                view.ShowStatus(message);
            });
            var session = await _windows.EnsureSessionAsync(SessionOptions(view), progress, _run.Token);
            view.Attach(session.Display);
            program = session.Start(assembly);
            program.LineWritten += (_, line) => _ui.Post(() => Output.WriteLine(line.Text, line.IsError ? OutputKind.Error : OutputKind.Program));
            StatusText = $"Running {project.Name}";
            Output.WriteLine($"{project.Name} is running in the app preview, under Windows Compatibility.", OutputKind.Info);

            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ended = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnEnded(object? sender, string reason) => ended.TrySetResult(reason);
            session.Ended += OnEnded;
            try
            {
                using (_run.Token.Register(() => stopped.TrySetResult()))
                {
                    var finished = await Task.WhenAny(program.Exited, stopped.Task, ended.Task);
                    if (finished == program.Exited)
                    {
                        var exit = await program.Exited;
                        result = new RunResult(exit.Succeeded || exit.ExitCode == 0 ? RunOutcome.Completed : RunOutcome.Crashed, exit.ExitCode, clock.Elapsed);
                    }
                    else if (finished == ended.Task)
                    {
                        Output.WriteLine(await ended.Task, OutputKind.Failure);
                        result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed);
                    }
                    else
                    {
                        program.Stop();
                        await program.Exited.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
                        result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
                    }
                }
            }
            finally
            {
                session.Ended -= OnEnded;
            }
        }
        catch (OperationCanceledException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
        }
        catch (TimeoutException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
        }
        catch (WindowsCompatibilityException ex)
        {
            Output.WriteLine($"{project.Name} could not start: {ex.Message}", OutputKind.Failure);
            result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, ex);
        }
        catch (Exception ex) when (ex is TabStudio.WindowsCompatibility.Display.X11.X11Exception or TabStudio.Toolchains.Processes.ToolchainRuntimeException or IOException)
        {
            Output.WriteLine($"{project.Name} could not start: {ex.Message}", OutputKind.Failure);
            result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, ex);
        }
        finally
        {
            program?.Dispose();
            _liveProject = null;
            if (!_restartLiveApp)
            {
                AppPreview.ShowStopped();
                _windowsView?.Attach(null);
            }

            _run.Dispose();
            _run = null;
        }

        FinishLiveRun(project, result);
    }

    private WindowsDesktopView CreateWindowsView()
    {
        var view = new WindowsDesktopView();
        view.DesktopSizeChanged += async (_, size) =>
        {
            if (_windows?.Session is { } session)
            {
                try
                {
                    await session.ApplyAsync(session.Options with { Width = size.Width, Height = size.Height });
                }
                catch (Exception ex) when (ex is WindowsCompatibilityException or TabStudio.WindowsCompatibility.Display.X11.X11Exception)
                {
                    // The session ended meanwhile; the run reports it.
                }
            }
        };
        return view;
    }

    /// <summary>The session's settings for the view: as many desktop pixels as the view at the screen's scale.</summary>
    private static WindowsSessionOptions SessionOptions(WindowsDesktopView view)
    {
        var scaling = TopLevelScaling(view);
        view.Scale = Math.Clamp(Math.Round(scaling * 4) / 4, 1, 2);
        var (width, height) = view.DesktopSizeForBounds;
        return new WindowsSessionOptions { Width = width, Height = height, Scale = view.Scale };
    }

    private static double TopLevelScaling(WindowsDesktopView view) => Avalonia.Controls.TopLevel.GetTopLevel(view)?.RenderScaling ?? 1;

    /// <summary>Waits until the preview has laid out its view, so the desktop gets the view's size.</summary>
    private static async Task WaitForLayoutAsync(WindowsDesktopView view, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 50 && !view.HasSize; i++)
        {
            await Task.Delay(20, cancellationToken);
        }
    }

    /// <summary>The editor's own Roslyn workspace, which builds on the tablet, unless a TabStudio Link host compiles.</summary>
    private RoslynWorkspaceHost? LocalRoslynHost() => _language switch
    {
        LocalLanguageService local => local.Host,
        LinkedLanguageService { Local: { } local } => local.Host,
        _ => null,
    };
}
