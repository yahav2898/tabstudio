using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.Services.Extensions;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Progress;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>The panels in the bottom dock.</summary>
public enum BottomPanel
{
    /// <summary>The Error List.</summary>
    ErrorList,

    /// <summary>Build output and the running program's console.</summary>
    Output,

    /// <summary>The Compatibility Doctor.</summary>
    Compatibility,

    /// <summary>Find All References and the other lists of symbol locations.</summary>
    References,

    /// <summary>The paused program's local variables.</summary>
    Locals,

    /// <summary>Expressions evaluated every time the program pauses.</summary>
    Watch,

    /// <summary>The paused program's call stack.</summary>
    CallStack,

    /// <summary>The Terminal: a shell in the workspace folder.</summary>
    Terminal,

    /// <summary>The running Android app's logcat.</summary>
    Logcat,
    /// <summary>The USB serial monitor, independent of the firmware build.</summary>
    SerialMonitor,

    /// <summary>CMSIS-SVD metadata and explicitly requested halted-target register snapshots.</summary>
    Peripherals,
}

/// <summary>What the IDE is doing, for the status bar.</summary>
public enum IdeState
{
    /// <summary>Idle.</summary>
    Ready,

    /// <summary>Opening a workspace or building.</summary>
    Busy,

    /// <summary>A program is running.</summary>
    Running,

    /// <summary>The last build failed.</summary>
    Failed,

    /// <summary>A program runs under the debugger.</summary>
    Debugging,

    /// <summary>The debugged program is paused (break mode).</summary>
    Paused,
}

/// <summary>A project that can be chosen as the startup project.</summary>
/// <param name="Project">The project.</param>
/// <param name="Select">Makes it the startup project.</param>
public sealed record StartupProjectOption(ProjectInfo Project, IRelayCommand Select)
{
    /// <summary>The project name.</summary>
    public string Name => Project.Name;
}

/// <summary>
/// The IDE for one open workspace: documents, the Solution Explorer, the bottom panels, building and running,
/// and the status bar. This file holds its state and setup; the other parts of the class are in
/// <c>WorkspaceViewModel.*.cs</c>.
/// </summary>
public sealed partial class WorkspaceViewModel : ObservableObject, IExplorerActions, IDisposable
{
    private readonly CompilerServices _compiler;
    private readonly IDeviceStatus _device;
    private readonly DeviceProfile _profile;
    private readonly IUiDispatcher _ui;
    private readonly SessionStore _sessions;
    private readonly AppPaths _paths;
    private readonly ProgramRunner _runner = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "TabStudio Link provides a remote implementation of the same interface.")]
    private ILanguageService? _language;
    private CancellationTokenSource? _run;
    private CancellationTokenSource? _background = new();
    private bool _disposed;

    /// <summary>Creates the IDE. Call <see cref="LoadAsync"/> to open a workspace.</summary>
    /// <param name="gitHubAccount">The app's GitHub account; without it, the Git Changes window starts signed out.</param>
    /// <param name="browser">Opens web pages, such as GitHub's; the system browser when <see langword="null"/>.</param>
    /// <param name="webViews">Creates the web preview's embedded browser; none when <see langword="null"/>.</param>
    /// <param name="notifications">The system's notifications, for finished CI runs; none when <see langword="null"/>.</param>
    /// <param name="appActivity">Whether the app is in the background, so CI polling rests; Avalonia's lifetime when <see langword="null"/>.</param>
    /// <param name="link">The app's TabStudio Link; without it, this device always compiles on its own.</param>
    /// <param name="linkDialog">The TabStudio Link dialog, opened from the File menu and the status bar.</param>
    /// <param name="languageServers">Starts language servers and debug adapters; the desktop's launcher when <see langword="null"/>.</param>
    /// <param name="extensions">The installed VS Code extensions and the Open VSX registry; without it the Extensions menu is unavailable.</param>
    /// <param name="toolchains">The device's toolchains; the computer's own, found on its PATH, when <see langword="null"/>.</param>
    /// <param name="swarm">The swarm, which runs the app on phones and tablets; none when <see langword="null"/>.</param>
    /// <param name="recentWorkspaces">The solutions opened recently, which the search finds; none when <see langword="null"/>.</param>
    /// <param name="windowsBridge">The Windows Bridge, which builds and runs Windows-only projects on a paired PC; none when <see langword="null"/>.</param>
    /// <param name="serviceDatabases">Optional adapter to a Database Explorer installed in this head.</param>
    public WorkspaceViewModel(CompilerServices compiler, IDeviceStatus device, DeviceProfile profile, IUiDispatcher ui, SessionStore sessions, AppPaths paths, GitHubAccountViewModel? gitHubAccount = null, IStoragePicker? browser = null, IWebViewFactory? webViews = null, INotifications? notifications = null, IAppActivity? appActivity = null, LinkService? link = null, LinkViewModel? linkDialog = null, Services.LanguageServers.LanguageServerServices? languageServers = null, ExtensionsService? extensions = null, Services.Toolchains.ToolchainServices? toolchains = null, global::TabStudio.App.Swarm.SwarmServices? swarm = null, RecentWorkspaces? recentWorkspaces = null, Services.WindowsBridge.WindowsBridgeService? windowsBridge = null, global::TabStudio.Database.IDatabaseSecretStore? databaseSecrets = null, RemoteSshServices? remoteSsh = null, IAndroidAppInstaller? androidInstaller = null, Services.LocalAI.LocalAIService? localAI = null, BackingServices.IServiceDatabaseExplorer? serviceDatabases = null, global::TabStudio.App.Services.Live.LiveService? live = null, global::TabStudio.App.ViewModels.Live.LiveJoinViewModel? liveJoin = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(sessions);

        _compiler = compiler;
        _device = device;
        _profile = profile;
        _ui = ui;
        _sessions = sessions;
        _paths = paths;
        _link = link;
        _linkDialog = linkDialog;

        Explorer = new SolutionExplorerViewModel(this);
        Output = new OutputViewModel(ui, profile.OutputCharacterLimit);
        Logcat = new OutputViewModel(ui, profile.OutputCharacterLimit);
        _androidInstaller = androidInstaller;
        Explorer.OpenFileRequested += async (_, path) => await OpenDocumentAsync(path);
        Explorer.SelectionChanged += (_, node) => ShowProperties(node);
        ErrorList.NavigateRequested += async (_, diagnostic) => await NavigateToAsync(diagnostic.Location!.FilePath, diagnostic.Location.Line, diagnostic.Location.Column);
        _device.ThermalChanged += OnThermalChanged;
        _device.MemoryPressureRaised += OnMemoryPressure;
        _compiler.Warmed += OnCompilerWarmed;
        UpdateThermal();
        GitChanges = CreateGitChanges(gitHubAccount, browser);
        WebPreview = new WebPreviewViewModel(webViews ?? new NoWebViewFactory(), browser ?? new TopLevelStoragePicker(), new PreviewZoomStore(paths));
        _serverBrowser = browser ?? new TopLevelStoragePicker();
        InitializeDebugging();
        InitializeTests();
        LocalAI = localAI ?? new Services.LocalAI.LocalAIService(paths, device);
        _ownsLocalAI = localAI is null;
        InitializeAgent();
        InitializeLink();
        Documents.CollectionChanged += (_, _) => UpdateActiveProjects();
        InitializeActions(browser, notifications, appActivity);
        InitializeOperations(notifications, appActivity);
        InitializePullRequests();
        InitializeLiveTesting();
        InitializeLanguageServers(languageServers, browser);
        InitializeExtensions(extensions, browser);
        InitializeToolchains(toolchains);
        InitializeToolchainOperations();
        InitializeRemoteSsh(remoteSsh);
        InitializeContainers();
        InitializeServices(serviceDatabases);
        InitializeSwarmScreens(swarm?.Registry);
        InitializeSwarm(swarm);
        InitializeProperties();
        InitializeStatusBar();
        InitializeWindowsBridge(windowsBridge);
        InitializeDatabase(databaseSecrets);
        InitializeLive(live, liveJoin);
        InitializeActivity();
        Search = CreateSearch(recentWorkspaces);
    }

    /// <summary>Raised when the user closes the workspace to return to the start screen.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The open workspace, once loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial Workspace? Workspace { get; private set; }

    /// <summary>The workspace name, shown in the menu bar.</summary>
    public string Title => IsRemoteWorkspace ? $"{Workspace?.Name} [SSH: {_remoteMarker}]" : Workspace?.Name ?? "TabStudio";

    /// <summary>The Solution Explorer.</summary>
    public SolutionExplorerViewModel Explorer { get; }

    /// <summary>The in-app dialog that asks for names.</summary>
    public PromptViewModel Prompt { get; } = new();

    /// <summary>The Properties panel.</summary>
    public PropertiesViewModel Properties { get; } = new();

    /// <summary>The Error List.</summary>
    public ErrorListViewModel ErrorList { get; } = new();

    /// <summary>The Output panel.</summary>
    public OutputViewModel Output { get; }

    /// <summary>The Compatibility Doctor's verdict for every project.</summary>
    public ObservableCollection<CompatibilityRowViewModel> Compatibility { get; } = [];

    /// <summary>The detected technologies.</summary>
    public ObservableCollection<StackChipViewModel> Stacks { get; } = [];

    /// <summary>The selected bottom panel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsErrorListSelected), nameof(IsOutputSelected), nameof(IsCompatibilitySelected))]
    [NotifyPropertyChangedFor(nameof(IsEmbeddedPeripheralsSelected), nameof(ShowsEmbeddedPeripheralsTab))]
    public partial BottomPanel SelectedPanel { get; set; } = BottomPanel.ErrorList;

    /// <summary>Whether the Error List is the visible bottom panel.</summary>
    public bool IsErrorListSelected => SelectedPanel == BottomPanel.ErrorList;

    /// <summary>Whether the Output panel is the visible bottom panel.</summary>
    public bool IsOutputSelected => SelectedPanel == BottomPanel.Output;

    /// <summary>Whether the Compatibility Doctor is the visible bottom panel.</summary>
    public bool IsCompatibilitySelected => SelectedPanel == BottomPanel.Compatibility;

    /// <summary>Whether the Solution Explorer and Properties are shown.</summary>
    [ObservableProperty]
    public partial bool IsSideBarVisible { get; set; } = true;

    /// <summary>Whether the bottom dock is shown.</summary>
    [ObservableProperty]
    public partial bool IsBottomPanelVisible { get; set; } = true;

    /// <summary>What the IDE is doing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(BuildCommand), nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(BuildEmbeddedCommand), nameof(UploadEmbeddedCommand), nameof(DebugEmbeddedChipCommand),
        nameof(UploadEmbeddedOtaCommand), nameof(BackupEmbeddedFirmwareCommand), nameof(ProductionFlashEmbeddedCommand),
        nameof(OpenMicroPythonReplCommand), nameof(SyncMicroPythonFileCommand), nameof(RunEmbeddedTestsCommand), nameof(UploadUf2StorageCommand),
        nameof(StartEmbeddedLogsCommand), nameof(IdentifyEmbeddedTargetCommand), nameof(RunEmbeddedBoardTestsCommand), nameof(RunSerialEmbeddedTestsCommand))]
    public partial IdeState State { get; private set; } = IdeState.Busy;

    /// <summary>Whether a load or build is in progress.</summary>
    public bool IsBusy => State == IdeState.Busy;

    /// <summary>Whether a program is running.</summary>
    public bool IsRunning => State == IdeState.Running;

    /// <summary>The status bar's main message.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Opening";

    /// <summary>The language service's state for the status bar while it is not ready, such as "Loading C#…"; <see langword="null"/> once it is.</summary>
    [ObservableProperty]
    public partial string? LanguageServiceText { get; private set; } = "Loading C#…";

    /// <summary>The thermal state, for the status bar.</summary>
    [ObservableProperty]
    public partial string ThermalText { get; private set; } = "Thermal OK";

    /// <summary>Whether the device is warm enough that TabStudio is holding back.</summary>
    [ObservableProperty]
    public partial bool IsThrottled { get; private set; }

    /// <summary>Shown in the status bar on small devices, where TabStudio saves memory.</summary>
    public string? DeviceText => _profile.Class == DeviceClass.Compact
        ? $"Lite mode · {_device.Device.TotalMemoryGigabytes} GB"
        : null;

    /// <summary>The projects that can be started, for the Run button's menu.</summary>
    public ObservableCollection<StartupProjectOption> StartupProjects { get; } = [];

    /// <summary>The project F5 builds and runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunLabel), nameof(RunHostText))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial ProjectInfo? StartupProject { get; private set; }

    /// <summary>The Run button's label: the startup project's name.</summary>
    public string RunLabel => StartupProject?.Name ?? LaunchTargetLabel ?? "Start";

    /// <summary>Where the startup project runs, such as "In-process".</summary>
    public string RunHostText => IsRemoteWorkspace ? _remoteMarker! : StartupProject is { } project ? IsRunOnSwarm ? SwarmRunHostText : RunsViaFor(project) : LaunchTargetHost ?? "Nothing to run";

    /// <summary>Whether there is somewhere to run: the toolbar shows no "Run on Nothing to run" without a startup project.</summary>
    public bool HasRunHost => IsRemoteWorkspace || StartupProject is not null || LaunchTargetHost is not null;

    /// <summary>The build configuration, "Debug" or "Release".</summary>
    [ObservableProperty]
    public partial string Configuration { get; private set; } = "Debug";

    /// <summary>The configurations, for the toolbar menu.</summary>
    public IReadOnlyList<string> Configurations { get; } = ["Debug", "Release"];

    /// <summary>Opens a workspace: loads it, starts the language service and restores or opens documents.</summary>
    /// <param name="path">A solution, project or folder.</param>
    /// <param name="session">A saved session for this workspace to restore, if any.</param>
    public async Task LoadAsync(string path, SessionState? session = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        State = IdeState.Busy;
        StatusText = "Opening";
        LoadRemoteState(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);

        var notebook = NotebookToOpen(path);
        var preview = await WorkspaceInspector.InspectAsync(notebook is null ? path : Path.GetDirectoryName(notebook)!);
        Workspace = preview.Workspace;
        Explorer.Load(preview.Workspace, preview.Reports);
        ShowProperties(Explorer.SelectedItem?.Node);

        Compatibility.Clear();
        foreach (var report in preview.Reports)
        {
            Compatibility.Add(new CompatibilityRowViewModel(report));
        }

        Stacks.Clear();
        foreach (var component in preview.Stack.Components)
        {
            Stacks.Add(new StackChipViewModel(component.Version is null ? component.DisplayName : $"{component.DisplayName} {component.Version}", component.Evidence));
        }

        DatabaseExplorer.EfCore.LoadProjects(preview.Workspace.Projects);
        StartDatabaseCheck(preview.Workspace);
        ShowNetFrameworkDiagnostics(preview.Workspace);
        LoadStartupProjects(preview.Workspace, session?.StartupProject);
        await LoadToolchainProjectsAsync(preview.Workspace, session?.StartupProject, opening: true);
        await Services.LoadAsync(preview.Workspace.RootDirectory);

        StatusText = "Loading C#";
        var loading = Operations.Begin(LanguageOperation, "Loading C#");
        try
        {
            _language = await CreateLanguageServiceAsync(preview.Workspace);
        }
        finally
        {
            loading.Complete();
        }

        LanguageServiceText = null;

        RestoreDebuggingState(session);
        RestoreShellState(session);
        await RestoreDocumentsAsync(preview.Workspace, session);
        if (notebook is not null)
        {
            await OpenDocumentAsync(notebook);
        }

        State = IdeState.Ready;
        StatusText = NoStartupProjectStatus() ?? "Ready";
        OnLoadedForLive();
        UpdateActiveProjects();
        StartPackageRestore(preview.Workspace);
        StartIntelliSenseWarmUp();
        if (_profile.AnalyzeWholeSolution)
        {
            StartSolutionAnalysis();
        }
    }

    /// <summary>Makes a project the startup project.</summary>
    /// <param name="project">The project.</param>
    public void SetStartupProject(ProjectInfo? project) => StartupProject = project;

    /// <summary>
    /// Why F5 has nothing to start, for the status bar once the workspace is open, such as
    /// <c>This solution has only libraries and tests: run the tests in Test Explorer.</c>; <see langword="null"/> when
    /// there is a startup project or launch configuration.
    /// </summary>
    private string? NoStartupProjectStatus() =>
        IsRemoteWorkspace || StartupProject is not null || UsesLaunchTarget ? null : Workspace?.NoStartupProjectReason;

    [RelayCommand]
    private void SelectPanel(BottomPanel panel)
    {
        SelectedPanel = panel;
        IsBottomPanelVisible = true;
    }

    [RelayCommand]
    private void ToggleSideBar() => IsSideBarVisible = !IsSideBarVisible;

    [RelayCommand]
    private void ToggleBottomPanel() => IsBottomPanelVisible = !IsBottomPanelVisible;

    [RelayCommand]
    private void SetConfiguration(string? configuration)
    {
        if (configuration is null || configuration == Configuration)
        {
            return;
        }

        Configuration = configuration;
        _language?.SetOptimization(configuration == "Release");
        foreach (var document in Documents)
        {
            _ = document.AnalyzeAgainAsync();
        }
    }

    [RelayCommand]
    private async Task CloseWorkspaceAsync()
    {
        if (await SaveAllCoreAsync())
        {
            _sessions.Clear();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    private async Task DisposeServicesAndContainersAsync()
    {
        await Services.DisposeAsync();
        await Containers.DisposeAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _background?.Cancel();
        _background?.Dispose();
        _background = null;
        _run?.Cancel();
        RemoteExplorer.Dispose();
        DisconnectRemote();

        // A session save still waiting would reach for the UI thread after the workspace is gone.
        _sessionSave?.Cancel();
        DatabaseExplorer.Dispose();
        DisposeDebugging();
        EndHotReload();
        DisposeTests();
        DisposeSwarm();
        DisposeLiveTesting();
        DisposeLink();
        DisposeWindowsBridge();
        DisposeLive();
        _agent?.Dispose();
        _device.ThermalChanged -= OnThermalChanged;
        _device.MemoryPressureRaised -= OnMemoryPressure;
        _compiler.Warmed -= OnCompilerWarmed;
        foreach (var document in Documents)
        {
            DetachXamlDesigner(document);
            DetachFormsDesigner(document);
            document.Dispose();
        }

        Documents.Clear();
        Output.EndInput();
        _language?.Dispose();
        _language = null;
        DisposeActions();
        DisposePullRequests();
        _ = DisposeServicesAndContainersAsync();
        DisposeOperations();
        DisposeToolchains();
        if (_ownsLocalAI) LocalAI.Dispose();
        GitChanges.Dispose();
        DisposeLanguageServers();
        DisposeExtensions();
        DisposeSwarmScreens();
        DisposeSearch();
    }

    private void LoadStartupProjects(Workspace workspace, string? preferred)
    {
        StartupProjects.Clear();
        var choices = workspace.Projects
            .Where(p => (p.IsExecutable || IsWindowsBridgeStartupProject(p)) && !p.IsTestProject && p.Kind is not (ProjectKind.AspireAppHost or ProjectKind.BuildScript) && !ProjectClassifier.NeedsPlatformWorkload(p))
            .ToList();

        // In the order F5 chooses from, the default first; the programs it never picks by itself, such as benchmarks,
        // follow in solution order, for a person to choose.
        var candidates = workspace.StartupCandidates.Where(choices.Contains).ToList();
        foreach (var project in candidates.Concat(choices.Except(candidates)))
        {
            StartupProjects.Add(new StartupProjectOption(project, new RelayCommand(() => SetStartupProject(project))));
        }

        // A build script or a project that needs a platform workload, remembered from an earlier version, which picked
        // them, is not kept.
        StartupProject = workspace.Projects.FirstOrDefault(p => p.FilePath == preferred && p.Kind != ProjectKind.BuildScript && !ProjectClassifier.NeedsPlatformWorkload(p))
            ?? workspace.DefaultStartupProject;
    }

    private async Task AnalyzeSolutionAsync(CancellationToken cancellationToken)
    {
        if (_language is not { } language || Workspace is not { } workspace)
        {
            return;
        }

        // The projects of open documents first: what is on screen gets its diagnostics before the rest.
        var projects = InAnalysisOrder(workspace.Projects.Where(p => p.IsRoslynLanguage));
        var operation = Operations.Begin(AnalysisOperation, "Analyzing", new OperationProgress("Analyzing", 0, projects.Count, ProgressUnit.Projects));
        try
        {
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                while (IsThrottled && _device.Thermal == ThermalState.Hot)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }

                operation.Log("Analyzing " + project.Name);
                var diagnostics = await language.GetProjectDiagnosticsAsync(project.FilePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyProjectDiagnostics(project, diagnostics);
                operation.Report(new OperationProgress("Analyzing", i + 1, projects.Count, ProgressUnit.Projects));
            }

            operation.Complete();
        }
        catch (OperationCanceledException)
        {
            // The workspace closed, or a newer analysis replaced this one, which ended this one's operation.
        }
        catch (Exception)
        {
            operation.Complete(OperationOutcome.Failed);
            throw;
        }
    }

    private void ApplyProjectDiagnostics(ProjectInfo project, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ErrorList.SetProjectDiagnostics(project.DirectoryPath, diagnostics);

        // Open documents have fresher results from live analysis; keep those.
        foreach (var document in Documents.Where(d => (d.HasLanguageService || d.HasXamlFeatures) && d.FilePath.StartsWith(project.DirectoryPath, StringComparison.Ordinal)))
        {
            ErrorList.SetFileDiagnostics(document.FilePath, document.Diagnostics);
        }
    }

    private void OnCompilerWarmed(object? sender, EventArgs e) => _ui.Post(() =>
    {
        // The status bar says nothing about a language service that is ready.
        if (_language is not null)
        {
            LanguageServiceText = null;
        }
    });

    private void OnThermalChanged(object? sender, EventArgs e) => _ui.Post(UpdateThermal);

    private void UpdateThermal()
    {
        var thermal = _device.Thermal;
        IsThrottled = thermal != ThermalState.Normal;
        ThermalText = thermal switch
        {
            ThermalState.Normal => "Thermal OK",
            ThermalState.Warm => "Warm · building on one core",
            _ => "Hot · background analysis paused",
        };
        _language?.SetConcurrentBuild(thermal == ThermalState.Normal);
    }

    private void OnMemoryPressure(object? sender, MemoryPressure pressure) => _ui.Post(() =>
    {
        // Roslyn's cached compilations are by far the largest thing TabStudio can rebuild on demand. They are let go
        // by a reload on a background thread, not here: on a 3 GB tablet a reload and its skeleton compile on the UI
        // thread froze the IDE. The collection runs once they are let go, which is what makes it free them.
        var released = _language?.TrimMemoryAsync() ?? Task.CompletedTask;
        _ = released.ContinueWith(
            _ => GC.Collect(2, pressure == MemoryPressure.Critical ? GCCollectionMode.Aggressive : GCCollectionMode.Forced, blocking: true, compacting: true),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    });
}

