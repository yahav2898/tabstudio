using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Link;
using TabStudio.App.Services.WindowsBridge;
using TabStudio.App.ViewModels.WindowsBridge;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Windows Bridge in the IDE: with a Windows PC paired, the Compatibility Doctor marks Windows-only programs ready
/// on the PC, the Run button's menu offers the PC for any program, the status bar says how the PC is, and the Bridge
/// pane in the side bar shows the programs that run there. Building, running, restarting on save and debugging on the
/// PC are in <c>WorkspaceViewModel.WindowsBridgeRuns.cs</c>.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The Doctor's verdicts as they were before the bridge changed them, by the row that shows the change.</summary>
    private readonly Dictionary<CompatibilityRowViewModel, CompatibilityReport> _reportsWithoutBridge = new(ReferenceEqualityComparer.Instance);

    /// <summary>The projects the user chose to run on the PC although they run on this device too.</summary>
    private readonly HashSet<string> _bridgeRunTargets = new(StringComparer.Ordinal);

    private WindowsBridgeService? _windowsBridge;
    private bool _applyingBridgeToDoctor;

    /// <summary>The Bridge pane, where the app has a Windows Bridge.</summary>
    public BridgePaneViewModel? WindowsBridgePane { get; private set; }

    /// <summary>Whether the app has a Windows Bridge, for its menu entries.</summary>
    public bool HasWindowsBridge => _windowsBridge is not null && _linkDialog?.Bridge is not null;

    /// <summary>The File menu's entry for the Windows Bridge: being one on a PC, pairing with one on a tablet.</summary>
    public string WindowsBridgeMenuHeader => _windowsBridge?.CanBeBridge == true ? "Be a _Windows Bridge..." : "_Windows PC...";

    /// <summary>Whether the side bar shows the Bridge pane.</summary>
    public bool IsWindowsBridgeSelected => SelectedSideBarPanel == SideBarPanel.WindowsBridge;

    /// <summary>The Windows Bridge's status bar item: <c>DESKTOP-7Q2M · 12 ms</c>, <c>DESKTOP-7Q2M is away</c>, or nothing.</summary>
    [ObservableProperty]
    public partial string? WindowsBridgeStatusText { get; private set; }

    /// <summary>What the Windows Bridge's status bar item means, for its tooltip.</summary>
    [ObservableProperty]
    public partial string WindowsBridgeStatusTip { get; private set; } = string.Empty;

    /// <summary>Whether the PC is connected, for the status bar item's dot.</summary>
    [ObservableProperty]
    public partial bool IsWindowsBridgeConnected { get; private set; }

    /// <summary>Whether the PC is paired but not answering, for the status bar item's dot.</summary>
    [ObservableProperty]
    public partial bool IsWindowsBridgeAway { get; private set; }

    /// <summary>Opens the TabStudio Link dialog on its Windows Bridge tab.</summary>
    [RelayCommand(CanExecute = nameof(HasWindowsBridge))]
    private void OpenWindowsBridge() => _linkDialog?.Open(LinkTab.Bridge);

    /// <summary>Shows the Bridge pane in the side bar.</summary>
    [RelayCommand(CanExecute = nameof(HasWindowsBridge))]
    private void ShowWindowsBridgePane()
    {
        SelectedSideBarPanel = SideBarPanel.WindowsBridge;
        IsSideBarVisible = true;
    }

    private void InitializeWindowsBridge(WindowsBridgeService? bridge)
    {
        _windowsBridge = bridge;
        if (bridge is null)
        {
            return;
        }

        WindowsBridgePane = new BridgePaneViewModel(bridge, _ui);
        WindowsBridgePane.RestartRequested += (_, app) => _ = RestartBridgeAppAsync(app.AppId);
        bridge.Changed += OnWindowsBridgeChanged;
        bridge.Client.AppStarted += OnBridgeAppStarted;
        bridge.Client.AppOutput += OnBridgeAppOutput;
        bridge.Client.AppExited += OnBridgeAppExited;
        Compatibility.CollectionChanged += OnCompatibilityChangedForBridge;
        UpdateWindowsBridgeStatus();
    }

    private void DisposeWindowsBridge()
    {
        if (_windowsBridge is not { } bridge)
        {
            return;
        }

        bridge.Changed -= OnWindowsBridgeChanged;
        bridge.Client.AppStarted -= OnBridgeAppStarted;
        bridge.Client.AppOutput -= OnBridgeAppOutput;
        bridge.Client.AppExited -= OnBridgeAppExited;
        Compatibility.CollectionChanged -= OnCompatibilityChangedForBridge;
        WindowsBridgePane?.Dispose();
    }

    /// <summary>The bridge changed: a PC was paired or forgotten, connected or lost. UI thread.</summary>
    private void OnWindowsBridgeChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        ApplyWindowsBridgeToDoctor();
        UpdateRunTargets();
        UpdateWindowsBridgeStatus();
        OnPropertyChanged(nameof(RunHostText));
    }

    /// <summary>Where a project runs, for the Run button: on the PC when the bridge runs it, else the Doctor's answer.</summary>
    private string RunsViaFor(ProjectInfo project) =>
        RunsOnWindowsBridge(project) && _windowsBridge?.PcName is { } pc
            ? WindowsBridgeCompatibility.RunsVia(pc)
            : CompatibilityDoctor.Diagnose(project).RunsVia;

    /// <summary>Whether a project is a startup project because the bridge runs it: a Visual C++ program.</summary>
    private bool IsWindowsBridgeStartupProject(ProjectInfo project) =>
        _windowsBridge is not null && WindowsBridgeCompatibility.IsNativeApplication(project);

    /// <summary>Whether Run and Start Debugging take a project to the PC.</summary>
    private bool RunsOnWindowsBridge(ProjectInfo project) =>
        _windowsBridge is { HasPairedPc: true }
        && WindowsBridgeCompatibility.CanRunOnBridge(project)
        && (_bridgeRunTargets.Contains(project.FilePath) || NeedsWindowsBridge(project));

    /// <summary>Whether a program runs only on Windows, so it runs on the PC whenever one is paired.</summary>
    private static bool NeedsWindowsBridge(ProjectInfo project) =>
        WindowsBridgeCompatibility.IsWindowsOnly(project) && CompatibilityDoctor.Diagnose(project).Status != CompatibilityStatus.Ready;

    /// <summary>The Doctor's rows changed, as when the workspace loads: apply the bridge to the new ones.</summary>
    private void OnCompatibilityChangedForBridge(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_applyingBridgeToDoctor)
        {
            _ui.Post(ApplyWindowsBridgeToDoctor);
        }
    }

    /// <summary>
    /// Makes the Doctor's rows say what the bridge changes: with a PC paired, Windows-only programs are ready, on the
    /// PC; without one, they are as the Doctor found them.
    /// </summary>
    private void ApplyWindowsBridgeToDoctor()
    {
        if (_disposed || Workspace is not { } workspace)
        {
            return;
        }

        var pc = _windowsBridge is { HasPairedPc: true } bridge ? bridge.PcName : null;
        _applyingBridgeToDoctor = true;
        try
        {
            foreach (var gone in _reportsWithoutBridge.Keys.Where(row => !Compatibility.Any(c => ReferenceEquals(c, row))).ToList())
            {
                _reportsWithoutBridge.Remove(gone);
            }

            for (var i = 0; i < Compatibility.Count; i++)
            {
                var row = Compatibility[i];
                var original = _reportsWithoutBridge.GetValueOrDefault(row) ?? row.Report;
                var project = workspace.Projects.FirstOrDefault(p => p.Name == original.ProjectName);
                var report = pc is not null && project is not null ? WindowsBridgeCompatibility.Apply(original, project, pc) : original;
                if (report == row.Report)
                {
                    continue;
                }

                var replacement = new CompatibilityRowViewModel(report);
                _reportsWithoutBridge.Remove(row);
                if (!ReferenceEquals(report, original))
                {
                    _reportsWithoutBridge[replacement] = original;
                }

                Compatibility[i] = replacement;
            }
        }
        finally
        {
            _applyingBridgeToDoctor = false;
        }
    }

    /// <summary>
    /// Adds the PC to the Run button's menu: a program that needs Windows names the PC as where it runs, and any
    /// other program gets a second entry that runs it on the PC.
    /// </summary>
    /// <param name="targets">The menu's entries so far, the startup projects first.</param>
    private void AddWindowsBridgeRunTargets(List<RunTargetOption> targets)
    {
        if (_windowsBridge is not { HasPairedPc: true } bridge || bridge.PcName is not { } pc)
        {
            return;
        }

        var via = WindowsBridgeCompatibility.RunsVia(pc);
        var count = Math.Min(StartupProjects.Count, targets.Count);
        var onPc = new List<RunTargetOption>();
        for (var i = 0; i < count; i++)
        {
            var project = StartupProjects[i].Project;
            if (!WindowsBridgeCompatibility.CanRunOnBridge(project))
            {
                continue;
            }

            if (NeedsWindowsBridge(project))
            {
                targets[i] = targets[i] with { Detail = via };
                continue;
            }

            targets[i] = targets[i] with { Select = new RelayCommand(() => ChooseBridgeRunTarget(project, onPc: false)) };
            onPc.Add(new RunTargetOption(project.Name, via, new RelayCommand(() => ChooseBridgeRunTarget(project, onPc: true))));
        }

        targets.InsertRange(count, onPc);
    }

    /// <summary>Makes a project what F5 runs, on the PC or on this device, as its entry in the Run button's menu does.</summary>
    private void ChooseBridgeRunTarget(ProjectInfo project, bool onPc)
    {
        if (onPc)
        {
            _bridgeRunTargets.Add(project.FilePath);
        }
        else
        {
            _bridgeRunTargets.Remove(project.FilePath);
        }

        SetStartupProject(project);
        OnPropertyChanged(nameof(RunHostText));
    }

    private void UpdateWindowsBridgeStatus()
    {
        var bridge = _windowsBridge;
        IsWindowsBridgeConnected = bridge?.IsConnected == true || bridge?.BridgeServer?.ConnectedTablet is not null;
        IsWindowsBridgeAway = bridge is { HasPairedPc: true } && bridge.State is LinkClientState.Reconnecting;
        if (bridge is null)
        {
            WindowsBridgeStatusText = null;
            return;
        }

        if (bridge.IsBridging)
        {
            var tablet = bridge.BridgeServer?.ConnectedTablet?.Name;
            WindowsBridgeStatusText = tablet is null ? "Windows Bridge" : $"Bridge for {tablet}";
            WindowsBridgeStatusTip = tablet is null
                ? "This PC is a Windows Bridge, waiting for a tablet. Tap for the pairing code."
                : $"This PC builds and runs {tablet}'s Windows-only projects. Tap for what runs.";
            return;
        }

        if (bridge.PcName is not { } pc)
        {
            WindowsBridgeStatusText = null;
            return;
        }

        (WindowsBridgeStatusText, WindowsBridgeStatusTip) = bridge.State switch
        {
            LinkClientState.Connected => (
                LinkDisplay.Latency(bridge.Latency) is { } latency ? $"{pc} · {latency}" : pc,
                $"Windows Bridge: {pc} builds and runs this workspace's Windows-only projects. Tap for details."),
            LinkClientState.Reconnecting when bridge.IsPcAsleep => (
                $"{pc} is away",
                $"Windows Bridge: {pc} is asleep or off the network. Keep working: its programs show again when it is back."),
            LinkClientState.Reconnecting or LinkClientState.Connecting => (
                $"Reconnecting to {pc}",
                $"Windows Bridge: {pc} is not answering yet. Keep working: nothing is lost."),
            _ => (
                pc,
                $"Windows Bridge: {pc} connects when you run a Windows-only project. Tap for details."),
        };
    }
}
