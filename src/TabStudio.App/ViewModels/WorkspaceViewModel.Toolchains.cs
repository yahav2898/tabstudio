using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Toolchains;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.App.ViewModels;

/// <summary>A Python or Node.js project of the workspace, which the toolbar's Start button can run.</summary>
/// <param name="Project">The project, with the toolchain that runs it.</param>
/// <param name="Info">The project as the toolbar, the Compatibility Doctor and the session know it.</param>
public sealed record ToolchainStartupItem(ToolchainProject Project, ProjectInfo Info)
{
    /// <summary>The toolchain that runs it.</summary>
    public ToolchainDefinition Toolchain => Project.Toolchain;
}

/// <summary>An offer shown over the editor, such as to install a project's packages.</summary>
/// <param name="Key">What it is about, so a dismissed offer is not shown again.</param>
/// <param name="Title">The title, such as "Install packages?".</param>
/// <param name="Message">One or two sentences.</param>
/// <param name="ActionText">The button, such as "npm install".</param>
/// <param name="Action">What the button does.</param>
/// <param name="Dismiss">Hides the offer for this session.</param>
public sealed record ToolchainNoticeViewModel(string Key, string Title, string Message, string ActionText, IAsyncRelayCommand Action, IRelayCommand Dismiss);

/// <summary>
/// Toolchain projects: Python and Node.js projects found in the workspace, which F5 and Ctrl+F5 run with their
/// toolchain, with their console in the Output panel, errors in the Error List, and links to the lines in
/// tracebacks; their packages; the Toolchain Store; and the Terminal.
/// </summary>
/// <remarks>
/// F5 runs the project's entry point (<c>main.py</c>, <c>__main__.py</c>, <c>package.json</c>'s <c>start</c>
/// script or <c>main</c>), or, for a folder of scripts without one, the file being edited; Debug &gt; Run Current
/// File runs the file being edited. Opening sets projects up by default; when setup is disabled or cancelled,
/// a missing toolchain is offered before running.
/// F5 debugs the same program with the toolchain's debug adapter (<c>WorkspaceViewModel.ToolchainDebugging.cs</c>);
/// Ctrl+F5 runs it without the debugger, here.
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    private readonly List<ToolchainStartupItem> _toolchainProjects = [];
    private readonly List<CompatibilityRowViewModel> _toolchainRows = [];
    private readonly Dictionary<string, ProblemMatcher> _matchers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dismissedNotices = new(StringComparer.Ordinal);
    private ToolchainServices _toolchains = null!;
    private bool _ownsToolchains;
    private string? _runDirectory;
    private bool _openToolchainEntry;

    /// <summary>The device's toolchains.</summary>
    public ToolchainServices Toolchains => _toolchains;

    /// <summary>The Toolchain Store window.</summary>
    public ToolchainStoreViewModel ToolchainStore { get; private set; } = null!;

    /// <summary>The Terminal panel.</summary>
    public TerminalViewModel Terminal { get; private set; } = null!;

    /// <summary>The permissioned USB serial monitor.</summary>
    public SerialMonitorViewModel SerialMonitor { get; private set; } = null!;

    /// <summary>Whether serial output is visible.</summary>
    public bool IsSerialMonitorSelected => SelectedPanel == BottomPanel.SerialMonitor;

    /// <summary>The workspace's Python and Node.js projects.</summary>
    public IReadOnlyList<ToolchainStartupItem> ToolchainProjects => _toolchainProjects;

    /// <summary>An offer about the startup project's toolchain or packages, while there is one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolchainNotice))]
    public partial ToolchainNoticeViewModel? ToolchainNotice { get; private set; }

    /// <summary>Whether an offer is shown: there is one, and the Store, which offers the same, is closed.</summary>
    public bool HasToolchainNotice => ToolchainNotice is not null && !ToolchainStore.IsOpen;

    /// <summary>Whether the Terminal is the visible bottom panel.</summary>
    public bool IsTerminalSelected => SelectedPanel == BottomPanel.Terminal;

    /// <summary>Whether the file being edited is a program a toolchain runs, such as a <c>.py</c> file.</summary>
    public bool CanRunCurrentFile => !IsRemoteWorkspace && ActiveDocument is { } document && _toolchains.Catalog.ForFile(document.FilePath) is { Listed: true } && CanRun();

    private void InitializeToolchains(ToolchainServices? toolchains)
    {
        _ownsToolchains = toolchains is null;
        _toolchains = toolchains ?? new ToolchainServices(_paths);
        ToolchainStore = new ToolchainStoreViewModel(_toolchains, _ui, (title, message, confirm, destructive) => Prompt.ConfirmAsync(title, message, confirm, destructive));
        Terminal = new TerminalViewModel(_toolchains, _ui, () => Workspace?.RootDirectory ?? _paths.Projects, ToolchainStore.Open);
        SerialMonitor = new SerialMonitorViewModel(_toolchains, _ui);
        InitializeEmbeddedHotplug();
        _toolchains.Manager.StatusChanged += OnToolchainStatusChanged;
        Output.LinkFinder = FindOutputLinks;
        Output.LinkActivated += OnOutputLinkActivated;
        PropertyChanged += OnToolchainPropertyChanged;
        ToolchainStore.PropertyChanged += OnToolchainStorePropertyChanged;
        InitializeToolchainServers();
    }

    private void OnToolchainStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolchainStoreViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(HasToolchainNotice));
        }
    }

    private void DisposeToolchains()
    {
        DisposeEmbeddedHotplug();
        DisposeEmbeddedAnalysis();
        DisposeEmbeddedDebug();
        DisposeProjectSetup();
        DisposeStaticSite();
        DisposeToolchainServers();
        PropertyChanged -= OnToolchainPropertyChanged;
        ToolchainStore.PropertyChanged -= OnToolchainStorePropertyChanged;
        Output.LinkActivated -= OnOutputLinkActivated;
        _toolchains.Manager.StatusChanged -= OnToolchainStatusChanged;
        Terminal.Dispose();
        SerialMonitor.Dispose();
        ToolchainStore.Dispose();
        if (_ownsToolchains)
        {
            _toolchains.Dispose();
        }
    }

    /// <summary>
    /// Whether a toolchain project is the build of the folder opened as the workspace, not of a solution: at its root,
    /// with no .NET project that F5 would start.
    /// </summary>
    private static bool IsRootToolchainProjectOfFolder(Workspace workspace, ToolchainProject project) =>
        workspace.Solution is null && workspace.DefaultStartupProject is null
        && string.Equals(Path.GetFullPath(project.Directory).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(workspace.RootDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal);

    /// <summary>
    /// Finds the workspace's toolchain projects after it loads or changes: they join the toolbar's startup items and
    /// the Compatibility Doctor, and become visible to the toolchains' programs.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="preferred">The startup project the session remembers.</param>
    /// <param name="opening">Whether the workspace is opening, rather than reloading after a change.</param>
    private async Task LoadToolchainProjectsAsync(Workspace workspace, string? preferred, bool opening = false)
    {
        var catalog = _toolchains.Catalog;
        var projects = await Task.Run(() => ToolchainProjectDetector.Detect(workspace.RootDirectory, catalog));
        _toolchainProjects.Clear();
        _toolchainRows.Clear();
        foreach (var project in projects)
        {
            var item = new ToolchainStartupItem(project, ToProjectInfo(project));
            _toolchainProjects.Add(item);
            StartupProjects.Add(new StartupProjectOption(item.Info, new RelayCommand(() => SetStartupProject(item.Info))));
            var row = ToolchainRow(item);
            _toolchainRows.Add(row);
            Compatibility.Add(row);
        }

        if (_toolchainProjects.FirstOrDefault(t => t.Info.FilePath == preferred) is { } remembered)
        {
            StartupProject = remembered.Info;
        }
        else if (StartupProject is null && _toolchainProjects.Count > 0
            && (workspace.Projects.Count == 0 || IsRootToolchainProjectOfFolder(workspace, _toolchainProjects[0].Project)))
        {
            // In a .NET solution, F5 starts one of its projects, as in Visual Studio: a docs site or a script folder
            // beside it, such as RestSharp's docs, is there to pick, not the default. A folder whose own build is a
            // toolchain's, with .NET projects that start nothing beside it (zlib's contrib/dotzlib, raylib's Visual
            // Studio projects), starts that build.
            StartupProject = _toolchainProjects[0].Info;
        }

        ShowProperties(Explorer.SelectedItem?.Node);
        OnToolchainTestProjectsChanged();
        if (_toolchainProjects.Count > 0)
        {
            // Once the workspace is open, a toolchain project shows its entry point, as a C# one shows Program.cs.
            _openToolchainEntry = opening;
            _toolchains.ShareFolder(workspace.RootDirectory);
            _ = RefreshToolchainProjectsAsync();
        }
        else
        {
            ToolchainNotice = null;
        }

        LoadStaticSite(workspace, preferred);
        if (opening)
        {
            // A static web site shows its index.html once the workspace is open.
            _openToolchainEntry |= StaticSite is not null;
            StartProjectSetup();
        }
    }

    private async Task RefreshToolchainProjectsAsync()
    {
        await _toolchains.EnsureStatusAsync();
        UpdateToolchainRows();
        UpdateToolchainNotice();
    }

    private static ProjectInfo ToProjectInfo(ToolchainProject project) => new()
    {
        Name = project.Name,

        // A folder of scripts has no project file; the project is known by a name in its folder.
        FilePath = project.MarkerPath ?? Path.Combine(project.Directory, "." + project.Toolchain.Id),
        Kind = TabStudio.Toolchains.Embedded.Projects.EmbeddedProjectDetector.Read(project.Directory) is not null ? ProjectKind.EmbeddedFirmware : project.Toolchain.Id switch
        {
            "python" => ProjectKind.Python,
            "node" => ProjectKind.Node,
            "go" => ProjectKind.Go,

            "rust" => ProjectKind.Rust,

            "cpp" => ProjectKind.Cpp,

            "java" => ProjectKind.Java,
            "php" => ProjectKind.Php,
            "kotlin" => ProjectKind.Kotlin,
            "swift" => ProjectKind.Swift,
            "android" => ProjectKind.Android,
            _ => ProjectKind.Unknown,
        },
        OutputType = "Exe",
    };

    /// <summary>Shows a Solution Explorer node in the Properties panel, counting the toolchain projects with the solution's.</summary>
    /// <param name="node">The node, or <see langword="null"/> to clear the panel.</param>
    private void ShowProperties(WorkspaceNode? node) =>
        Properties.Show(node, Workspace, [.. _toolchainProjects.Select(t => t.Info)], StartupProject);

    private ToolchainStartupItem? FindToolchainProject(ProjectInfo project) =>
        _toolchainProjects.FirstOrDefault(t => ReferenceEquals(t.Info, project) || t.Info.FilePath == project.FilePath);

    private bool IsToolchainProject(ProjectInfo project) => FindToolchainProject(project) is not null;

    /// <summary>The Compatibility Doctor's row: ready once the toolchain is installed, with an Install fix before.</summary>
    private CompatibilityRowViewModel ToolchainRow(ToolchainStartupItem item)
    {
        var report = CompatibilityDoctor.Diagnose(item.Info);
        var toolchain = item.Toolchain;
        var status = _toolchains.Manager.Status(toolchain.Id);
        var installText = (string?)null;
        if (status.IsReady)
        {
            var source = _toolchains.Manager.CanInstall ? "from the Toolchain Store" : "from this computer";
            var version = status.Version is { } v ? " " + v : string.Empty;
            report = report with
            {
                Status = item.Info.Kind == ProjectKind.EmbeddedFirmware ? CompatibilityStatus.NeedsToolchain : CompatibilityStatus.Ready,
                Explanation = item.Info.Kind == ProjectKind.EmbeddedFirmware
                    ? $"Build tools: {toolchain.Name}{version} {source}. Select the exact SDK/board and permissioned upload or debug transport; an installed compiler does not verify hardware support."
                    : $"Runs with {toolchain.Name}{version} {source}, with its output in the Output panel and errors in the Error List.",
            };
        }
        else if (status.State == ToolchainState.Unavailable)
        {
            report = report with { Explanation = status.Note ?? report.Explanation };
        }
        else if (_toolchains.Manager.CanInstall)
        {
            installText = status.IsBusy ? $"Installing {toolchain.Name}" : $"Install {toolchain.Name}";
        }

        return new CompatibilityRowViewModel(report) { ToolchainId = toolchain.Id, InstallToolchainText = installText };
    }

    private void UpdateToolchainRows()
    {
        for (var i = 0; i < _toolchainProjects.Count && i < _toolchainRows.Count; i++)
        {
            var row = ToolchainRow(_toolchainProjects[i]);
            var index = Compatibility.IndexOf(_toolchainRows[i]);
            if (index >= 0 && row != _toolchainRows[i])
            {
                Compatibility[index] = row;
            }

            _toolchainRows[i] = row;
        }
    }

    private void OnToolchainStatusChanged(object? sender, ToolchainStatus status) => _ui.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        if (_toolchainProjects.Any(t => t.Toolchain.Id == status.Toolchain.Id))
        {
            UpdateToolchainRows();
            UpdateToolchainNotice();
        }
    });

    private void OnToolchainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectedPanel):
                OnPropertyChanged(nameof(IsTerminalSelected));
                OnPropertyChanged(nameof(IsSerialMonitorSelected));
                OnPropertyChanged(nameof(ShowsSerialMonitorTab));
                OnPropertyChanged(nameof(ShowsLogcatTab));
                if (IsTerminalSelected)
                {
                    Terminal.EnsureStarted();
                }

                break;
            case nameof(StartupProject):
                RefreshPicCommands();
                UpdateToolchainNotice();
                ShowProperties(Explorer.SelectedItem?.Node);
                break;
            case nameof(ActiveDocument) or nameof(State):
                RefreshPicCommands();
                if (e.PropertyName == nameof(State) && _openToolchainEntry && State == IdeState.Ready)
                {
                    _openToolchainEntry = false;
                    var entry = StartupProject is { } startup && IsStaticSite(startup) ? startup.FilePath
                        : CurrentToolchainProject() is { } item ? EntryFileOf(item.Project) : null;
                    if (!HasDocuments && entry is not null)
                    {
                        _ = OpenDocumentAsync(entry);
                    }
                }

                OnPropertyChanged(nameof(CanRunCurrentFile));
                RunCurrentFileCommand.NotifyCanExecuteChanged();
                InstallProjectPackagesCommand.NotifyCanExecuteChanged();
                break;
        }
    }

    /// <summary>View &gt; Terminal (Ctrl+`): shows the Terminal, with a shell in the workspace folder.</summary>
    [RelayCommand]
    private void ShowTerminal()
    {
        SelectedPanel = BottomPanel.Terminal;
        IsBottomPanelVisible = true;
        Terminal.EnsureStarted();
        Terminal.RequestFocus();
    }

    /// <summary>View &gt; Serial Monitor.</summary>
    [RelayCommand]
    private void ShowSerialMonitor()
    {
        SelectedPanel = BottomPanel.SerialMonitor;
        IsBottomPanelVisible = true;
        SerialMonitor.Refresh();
    }

    /// <summary>Tools &gt; Toolchain Store.</summary>
    [RelayCommand]
    private void ShowToolchainStore() => ToolchainStore.Open(StartupProject is { } project ? FindToolchainProject(project)?.Toolchain.Id : null);

    /// <summary>The Compatibility Doctor's Install fix: installs the toolchain a project needs, in the Store.</summary>
    [RelayCommand]
    private async Task InstallToolchainAsync(CompatibilityRowViewModel? row)
    {
        if (row?.ToolchainId is not { } id)
        {
            return;
        }

        ToolchainStore.Open(id);
        await ToolchainStore.InstallAsync(id);
    }

    /// <summary>Debug &gt; Run Current File: runs the file being edited with its toolchain.</summary>
    [RelayCommand(CanExecute = nameof(CanRunCurrentFile))]
    private async Task RunCurrentFileAsync()
    {
        if (BlockLocalToolchainActionForRemote()) return;
        if (ActiveDocument?.FilePath is not { } file || _toolchains.Catalog.ForFile(file) is not { Listed: true } toolchain)
        {
            return;
        }

        var project = ToolchainProjectDetector.ForFile(_toolchainProjects.Select(t => t.Project), file)
            ?? new ToolchainProject(toolchain, Path.GetDirectoryName(file)!, null);
        await RunToolchainPlanAsync(project, () => RunPlanner.PlanFile(project, file));
    }

    /// <summary>Tools &gt; Install Project Packages: <c>pip install -r requirements.txt</c> or <c>npm install</c> for the startup project.</summary>
    [RelayCommand(CanExecute = nameof(CanInstallProjectPackages))]
    private async Task InstallProjectPackagesAsync()
    {
        if (BlockLocalToolchainActionForRemote()) return;
        if (CurrentToolchainProject() is { } item && RunPlanner.DependenciesOf(item.Project) is { } dependency)
        {
            await InstallDependenciesAsync(item.Project, dependency);
        }
    }

    private bool CanInstallProjectPackages() =>
        !IsRemoteWorkspace && CurrentToolchainProject() is { } item && RunPlanner.DependenciesOf(item.Project) is not null && State is IdeState.Ready or IdeState.Failed;

    /// <summary>F5 and Ctrl+F5 on a toolchain project.</summary>
    private async Task RunToolchainProjectAsync(ProjectInfo info)
    {
        if (BlockLocalToolchainActionForRemote()) return;
        if (FindToolchainProject(info) is not { } item)
        {
            return;
        }

        if (item.Toolchain.Id == "flutter")
        {
            await StartDartOrFlutterAsync(item, noDebug: true);
            return;
        }

        var project = item.Project;
        if (TabStudio.Toolchains.Embedded.Projects.EmbeddedProjectDetector.Read(project.Directory) is { } embedded)
        {
            try
            {
                if (embedded.Kind == TabStudio.Toolchains.Embedded.Projects.EmbeddedProjectKind.MicroPython)
                {
                    await OpenMicroPythonReplAsync();
                }
                else if (File.Exists(Path.Combine(project.Directory, ".tabstudio", "embedded-flash.json")) ||
                         embedded.Kind == TabStudio.Toolchains.Embedded.Projects.EmbeddedProjectKind.EspIdf)
                {
                    await UploadEmbeddedAsync();
                    if (State == IdeState.Ready && StatusText is "Firmware upload verified" or "Firmware transferred; readback unavailable")
                    {
                        ShowSerialMonitor();
                        if (_lastEmbeddedSerialDevice is { } id && SerialMonitor.Devices.FirstOrDefault(d => d.Id == id) is { } serial)
                        {
                            SerialMonitor.SelectedDevice = serial;
                            _ = SerialMonitor.ConnectCommand.ExecuteAsync(null);
                        }
                    }
                }
                else
                {
                    await BuildEmbeddedCoreAsync(embedded);
                }
            }
            catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
            return;
        }
        if (MicrochipInstallation.IsCompiler(project.Toolchain.Id) && project.MarkerPath is { } marker)
        {
            _lastPicBuildSucceeded = false;
            try
            {
                var selected = MplabProject.Load(marker).SelectedConfiguration();
                if (_toolchains.Catalog.Find(selected.Compiler.ToLowerInvariant()) is { } compiler) project = project with { Toolchain = compiler };
            }
            catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException or System.Xml.XmlException)
            {
                Output.WriteLine(error.Message, OutputKind.Failure);
                State = IdeState.Failed;
                return;
            }
        }
        var file = ActiveDocument?.FilePath;
        if (item.Toolchain.Id == "android")
        {
            await BuildAndroidCoreAsync(install: true);
            return;
        }

        await RunToolchainPlanAsync(project, () => PlanToolchainProject(project, file));
    }

    /// <summary>
    /// What F5 runs for a project: its entry point, else the file being edited when the project runs it, else the
    /// project's only program; <see langword="null"/> when there is nothing to run.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="activeFile">The file being edited, if any.</param>
    private static RunPlan? PlanToolchainProject(ToolchainProject project, string? activeFile) =>
        RunPlanner.PlanProject(project)
        ?? (activeFile is not null && project.Contains(activeFile) && project.Toolchain.RunsFile(activeFile) ? RunPlanner.PlanFile(project, activeFile) : null)
        ?? OnlyProgramOf(project);

    /// <summary>The file to show first: the entry point's source, or the project's first program, or its project file.</summary>
    private static string? EntryFileOf(ToolchainProject project)
    {
        var toolchain = project.Toolchain;
        if (toolchain.Id == "flutter" && File.Exists(Path.Combine(project.Directory, "lib/main.dart")))
            return Path.Combine(project.Directory, "lib/main.dart");
        foreach (var rule in toolchain.Run?.Entries ?? [])
        {
            if (RunPlanner.FindEntry(project.Directory, rule) is not { FullPath: var file })
            {
                continue;
            }

            if (toolchain.RunsFile(file))
            {
                return file;
            }

            if (rule.Json is { } json && RunPlanner.ReadJson(file, json) is { } main && toolchain.RunsFile(main) && File.Exists(Path.Combine(project.Directory, main)))
            {
                return Path.GetFullPath(Path.Combine(project.Directory, main));
            }
        }

        if (toolchain.Id == "rust" && File.Exists(Path.Combine(project.Directory, "src", "main.rs")))
        {
            return Path.Combine(project.Directory, "src", "main.rs");
        }

        if (toolchain.Id == "java" && JavaProject.MainSource(project.Directory) is { } java)
        {
            return java;
        }

        if (toolchain.Id == "kotlin" && KotlinProject.MainSource(project.Directory) is { } kotlin)
        {
            return kotlin;
        }

        if (toolchain.Id == "php" && PhpEntryFile(project.Directory) is { } php)
        {
            return php;
        }

        if (toolchain.Id == "swift" && SwiftEntryFile(project.Directory) is { } swift)
        {
            return swift;
        }

        return Directory.EnumerateFiles(project.Directory).Where(toolchain.RunsFile).Order(StringComparer.Ordinal).FirstOrDefault() ?? project.MarkerPath;
    }

    /// <summary>The PHP file to show first: a Laravel app's web routes, else the front controller or <c>index.php</c>.</summary>
    private static string? PhpEntryFile(string directory) =>
        new[] { Path.Combine(directory, "routes", "web.php"), Path.Combine(directory, "public", "index.php"), Path.Combine(directory, "index.php"), Path.Combine(directory, "main.php"), Path.Combine(directory, "app.php") }
            .FirstOrDefault(File.Exists);

    /// <summary>The Swift file to show first: the first executable's <c>main.swift</c> or only source, else <c>Package.swift</c>.</summary>
    private static string? SwiftEntryFile(string directory)
    {
        foreach (var executable in SwiftProject.Executables(directory))
        {
            var folder = Path.Combine(directory, "Sources", executable);
            var main = Path.Combine(folder, "main.swift");
            if (File.Exists(main))
            {
                return main;
            }

            if (Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.swift").Order(StringComparer.Ordinal).FirstOrDefault() is { } first)
            {
                return first;
            }
        }

        return File.Exists(Path.Combine(directory, "Package.swift")) ? Path.Combine(directory, "Package.swift") : null;
    }

    /// <summary>
    /// For a folder with one program at its top and no entry point, that program. A tool's configuration file, such as
    /// <c>eslint.config.mjs</c> or <c>vite.config.ts</c>, is not a program.
    /// </summary>
    private static RunPlan? OnlyProgramOf(ToolchainProject project)
    {
        var programs = Directory.EnumerateFiles(project.Directory).Where(project.Toolchain.RunsFile).Where(f => !IsToolConfiguration(f)).Take(2).ToList();
        return programs.Count == 1 ? RunPlanner.PlanFile(project, programs[0]) : null;
    }

    private static bool IsToolConfiguration(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return name.StartsWith('.') || name.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// F5 and Ctrl+F5 for a toolchain project: builds it when its plan has a build, then runs it. With
    /// <paramref name="buildOnly"/>, Ctrl+Shift+B: builds it the same way and stops there.
    /// </summary>
    private async Task RunToolchainPlanAsync(ToolchainProject project, Func<RunPlan?> planner, bool buildOnly = false)
    {
        if (BlockLocalToolchainActionForRemote()) return;
        if (!await SaveAllCoreAsync())
        {
            return;
        }

        var toolchain = project.Toolchain;
        if (BlockLocalToolchainActionForRemote()) return;
        if (!await EnsureToolchainReadyAsync(toolchain, $"{project.Name} runs with {toolchain.Name}, which is not installed yet."))
        {
            return;
        }

        if (BlockLocalToolchainActionForRemote()) return;
        Output.Clear();
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        var planned = planner();
        if (buildOnly && planned?.Build is null)
        {
            // Python, Node.js and scripts run from their source: there is nothing to compile.
            Output.WriteLine($"{project.Name} needs no build: {toolchain.Name} runs it from its source. Press Ctrl+F5 to run it.", OutputKind.Info);
            State = IdeState.Ready;
            StatusText = "Nothing to build";
            return;
        }

        if (planned is not { } plan)
        {
            if (RunPlanner.UnmetEntryOf(project) is { } unmet)
            {
                // A web app whose server the project does not list, such as a FastAPI app without uvicorn.
                var missing = string.Join(" and ", unmet.Missing);
                Output.WriteLine($"{project.Name} is a web app in {unmet.Entry.RelativePath}, which runs with {unmet.EntryPoint}, but the project's dependencies do not list {missing}. Add {missing} to them, install the project's packages, and press F5 again.", OutputKind.Failure);
                StatusText = $"{missing} is missing";
                return;
            }

            var extensions = string.Join(" or ", toolchain.FileExtensions.Take(2));
            Output.WriteLine($"{project.Name} has no entry point to run. Open a {extensions} file and press F5 to run it, or add {(toolchain.Run?.Entries is [var first, ..] ? first.File : "one")}.", OutputKind.Failure);
            StatusText = "Nothing to run";
            return;
        }

        ErrorList.SetRunError(project.Directory, null);
        if (!buildOnly && _installFailures.ContainsKey(project.Directory)
            && _installFailureTitles.GetValueOrDefault(project.Directory) is { } notInstalled)
        {
            // It runs anyway, as a person may know the missing package does not matter, but a failure that follows is no surprise.
            Output.WriteLine($"{notInstalled}, so it may fail to start. The install's log is in Output > Project Setup.", OutputKind.Failure);
        }

        _runDirectory = plan.Run.WorkingDirectory ?? project.Directory;
        State = IdeState.Running;
        StatusText = $"Running {plan.ProgramName}";
        var status = _toolchains.Manager.Status(toolchain.Id);
        Output.WriteLine($"{(buildOnly ? "Building" : "Running")} {plan.ProgramName} with {toolchain.Name}{(status.Version is { } version ? " " + version : string.Empty)}", OutputKind.Info);
        _run = new CancellationTokenSource();
        var listening = BeginServerRun();
        OutputProcessResult result;
        var input = Output.BeginProcessInput();
        try
        {
            if (plan.Build is { } build)
            {
                Output.WriteLine("> " + build.CommandLine, OutputKind.Info);
                var run = _run;
                var stage = global::TabStudio.Core.Progress.OutputProgressParser.StageOf(build.Program, build.Arguments, plan.ProgramName);
                (RunPlan Plan, OutputProcessResult Result) compiled;
                using (var building = new CompletingOperation(Operations.Begin("build:" + project.Directory, "Building " + plan.ProgramName, new global::TabStudio.Core.Progress.OperationProgress(stage), run.Cancel)))
                using (FollowOutput(building.Operation))
                {
                    compiled = await BuildCargoOrToolchainAsync(project, plan, run.Token);
                    building.Outcome = compiled.Result.Succeeded || compiled.Result.NothingToRunReason is not null ? global::TabStudio.Core.Progress.OperationOutcome.Succeeded
                        : run.IsCancellationRequested ? global::TabStudio.Core.Progress.OperationOutcome.Cancelled : global::TabStudio.Core.Progress.OperationOutcome.Failed;
                }

                plan = compiled.Plan;
                var built = compiled.Result;
                if (buildOnly)
                {
                    ReportToolchainBuild(project, plan.ProgramName, built);
                    return;
                }

                if (!built.Succeeded)
                {
                    result = built;
                    ReportToolchainRun(project, plan.ProgramName, result);
                    return;
                }
            }

            if (await PreparePrismaDatabaseAsync(project, _run.Token) is { } migration)
            {
                result = migration;
                ReportToolchainRun(project, plan.ProgramName, result);
                return;
            }

            if (await PrepareLaravelDatabaseAsync(project, _run.Token) is { } laravelMigration)
            {
                result = laravelMigration;
                ReportToolchainRun(project, plan.ProgramName, result);
                return;
            }

            StatusText = $"Running {plan.ProgramName}";
            result = await OutputProcess.RunAsync(_toolchains.Runtime, WithServiceEnvironment(plan.Run), Output, input, _run.Token, listening: listening, toolchain: toolchain.Id);
        }
        finally
        {
            Output.EndInput();
            _run?.Dispose();
            _run = null;
            EndServerRun();
        }

        ReportToolchainRun(project, plan.ProgramName, result);
    }

    /// <summary>Ends Ctrl+Shift+B on a toolchain project: the build's errors in the Error List, and whether it succeeded.</summary>
    /// <param name="project">The project that was built.</param>
    /// <param name="name">The program's name (<see cref="RunPlan.ProgramName"/>).</param>
    /// <param name="result">How the build ended.</param>
    private void ReportToolchainBuild(ToolchainProject project, string name, OutputProcessResult result)
    {
        ErrorList.SetRunErrors(project.Directory, [.. Matcher(project.Toolchain).FindErrors(result.Lines, IsWorkspaceFile).Select(error => ToDiagnostic(project, error))]);
        Output.WriteLine();
        if (result.Exit is { WasStopped: true })
        {
            Output.WriteLine($"The build of '{name}' was stopped.", OutputKind.Info);
            State = IdeState.Ready;
            StatusText = "Stopped";
        }
        else if (result.Succeeded || result.NothingToRunReason is not null)
        {
            // A build with several programs and none chosen yet still built them all.
            Output.WriteLine($"Build succeeded: '{name}' is ready to run.", OutputKind.Info);
            State = IdeState.Ready;
            StatusText = "Build succeeded";
        }
        else
        {
            Output.WriteLine($"The build of '{name}' failed; its errors are in the Error List.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Build failed";
        }
    }

    /// <summary>Ends a run: the exit line, as Visual Studio writes it, the Error List and the status bar.</summary>
    /// <param name="project">The project that ran.</param>
    /// <param name="entryPoint">The program's name (<see cref="RunPlan.ProgramName"/>): its target or binary, never a build file.</param>
    /// <param name="result">How it ended.</param>
    private void ReportToolchainRun(ToolchainProject project, string entryPoint, OutputProcessResult result)
    {
        if (MicrochipInstallation.IsCompiler(project.Toolchain.Id))
        {
            ReportPicBuild(project, result.Lines, result.Exit?.Succeeded == true);
        }
        Output.WriteLine();
        if (result.NothingToRunReason is { } reason)
        {
            // The build succeeded but no program was chosen: nothing ran, so no exit code is reported.
            Output.WriteLine(reason, OutputKind.Failure);
            State = IdeState.Ready;
            StatusText = "Nothing to run";
            return;
        }

        if (result.Exit is not { } exit)
        {
            State = IdeState.Failed;
            StatusText = "The program could not start";
            return;
        }

        if (exit.WasStopped)
        {
            Output.WriteLine($"The program '{entryPoint}' was stopped.", OutputKind.Info);
            State = IdeState.Ready;
            StatusText = "Stopped";
            return;
        }

        if (exit.WasKilledBySystem)
        {
            Output.WriteLine(
                $"The program '{entryPoint}' was stopped by Android (signal 9). Android ends processes when memory runs low, and limits how many an app runs in the background (Android 12 and later). Run it again with fewer apps open.",
                OutputKind.Failure);
        }
        else
        {
            var code = exit.ExitCode;
            Output.WriteLine($"The program '{entryPoint}' has exited with code {code.ToString(CultureInfo.InvariantCulture)} (0x{code:x}).", OutputKind.Info);
        }

        if (exit.Succeeded)
        {
            State = IdeState.Ready;
            if (!MicrochipInstallation.IsCompiler(project.Toolchain.Id))
            {
                StatusText = "Ready";
            }
            return;
        }

        ErrorList.SetRunErrors(project.Directory, [.. Matcher(project.Toolchain).FindErrors(result.Lines, IsWorkspaceFile).Select(error => ToDiagnostic(project, error))]);

        if (RunPlanner.DependenciesOf(project) is { } dependency && RunPlanner.IsPending(project, dependency))
        {
            Output.WriteLine($"{project.Name}'s packages are not installed yet: run {dependency.Name} with Tools > Install Project Packages.", OutputKind.Info);
        }

        State = IdeState.Failed;
        StatusText = "The program stopped with an error";
    }

    /// <summary>Makes sure a toolchain is installed, asking the user first and installing it in the Store.</summary>
    /// <returns>Whether it is ready.</returns>
    private async Task<bool> EnsureToolchainReadyAsync(ToolchainDefinition toolchain, string reason)
    {
        if (BlockLocalToolchainActionForRemote()) return false;
        await WaitForProjectSetupAsync();
        await _toolchains.EnsureStatusAsync();
        if (BlockLocalToolchainActionForRemote()) return false;
        var manager = _toolchains.Manager;
        var status = manager.Status(toolchain.Id);
        if (status.IsReady)
        {
            return true;
        }

        if (!manager.CanInstall)
        {
            Output.Clear();
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            Output.WriteLine(status.Note ?? manager.Problem ?? $"{toolchain.Name} is not available on this device.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = $"{toolchain.Name} is not available";
            return false;
        }

        if (!status.IsBusy)
        {
            var about = status.SizesAreExact ? string.Empty : "about ";
            var confirmed = await Prompt.ConfirmAsync(
                $"Install {toolchain.Name}?",
                $"{reason} Install it from the Toolchain Store? It downloads {about}{DiskSpace.Format(status.DownloadSize)}.",
                "Install");
            if (!confirmed)
            {
                return false;
            }
        }

        if (BlockLocalToolchainActionForRemote()) return false;
        StatusText = $"Installing {toolchain.Name}";
        ToolchainStore.Open(toolchain.Id);
        await ToolchainStore.InstallAsync(toolchain.Id);
        if (!manager.IsReady(toolchain.Id))
        {
            StatusText = manager.Status(toolchain.Id).Error is null ? "Ready" : $"{toolchain.Name} was not installed";
            return false;
        }

        ToolchainStore.CloseCommand.Execute(null);
        StatusText = $"{toolchain.Name} is installed";
        return true;
    }

    /// <summary>Installs a project's packages, such as <c>npm install</c>, with the output in the Output panel.</summary>
    private async Task InstallDependenciesAsync(ToolchainProject project, DependencyInstallDefinition dependency)
    {
        if (BlockLocalToolchainActionForRemote()) return;
        project = NodeWorkspaces.DependencyOwner(project, _toolchainProjects.Select(p => p.Project));
        dependency = RunPlanner.DependenciesOf(project) ?? dependency;
        if (!await EnsureToolchainReadyAsync(project.Toolchain, $"Installing {project.Name}'s packages needs {project.Toolchain.Name}, which is not installed yet."))
        {
            return;
        }

        await SaveAllCoreAsync();
        if (BlockLocalToolchainActionForRemote()) return;
        Output.Clear();
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine($"------ {dependency.Name}: {project.Name} ------", OutputKind.Info);
        State = IdeState.Running;
        StatusText = $"{dependency.Name} in {project.Name}";
        _runDirectory = project.Directory;
        _run = new CancellationTokenSource();
        OutputProcessResult? failed = null;
        try
        {
            failed = await RunDependencyInstallAsync(project, dependency, _run.Token);
        }
        catch (OperationCanceledException)
        {
            failed = new OutputProcessResult(new TabStudio.Toolchains.Processes.ProcessExit(-1, null, true), [], "The install was stopped.");
        }
        finally
        {
            _run?.Dispose();
            _run = null;
        }

        if (failed is null)
        {
            RememberSetUp(Path.Combine(project.Directory, dependency.Manifest), RunPlanner.FingerprintOf(project, dependency, _toolchainProjects.Select(p => p.Project)));
            Output.WriteLine($"========== {dependency.Name}: succeeded ==========", OutputKind.Success);
            State = IdeState.Ready;
            StatusText = "Packages installed";
        }
        else
        {
            Output.WriteLine($"========== {dependency.Name}: failed ({failed.Exit?.Description ?? failed.StartError}) ==========", OutputKind.Failure);
            State = failed.Exit?.WasStopped == true ? IdeState.Ready : IdeState.Failed;
            StatusText = $"{dependency.Name} failed";
        }

        UpdateToolchainNotice();
    }

    /// <summary>The startup project when it is a toolchain project, or else the workspace's first one.</summary>
    private ToolchainStartupItem? CurrentToolchainProject() =>
        (StartupProject is { } startup ? FindToolchainProject(startup) : null) ?? (_toolchainProjects.Count > 0 ? _toolchainProjects[0] : null);

    /// <summary>
    /// Offers what the startup project needs next: its toolchain, when the Store can install it, then its packages,
    /// when it lists them and they are not installed.
    /// </summary>
    private void UpdateToolchainNotice()
    {
        ToolchainNotice = NextNotice() is { } notice && !_dismissedNotices.Contains(notice.Key) ? notice : null;
        InstallProjectPackagesCommand.NotifyCanExecuteChanged();
    }

    private ToolchainNoticeViewModel? NextNotice()
    {
        if (IsRemoteWorkspace) { return null; }
        // While the projects are set up by themselves, there is nothing to offer.
        if (CurrentToolchainProject() is not { } item || !_setupTask.IsCompleted)
        {
            return null;
        }

        var project = NodeWorkspaces.DependencyOwner(item.Project, _toolchainProjects.Select(p => p.Project));
        var toolchain = item.Toolchain;
        var manager = _toolchains.Manager;
        var status = manager.Status(toolchain.Id);

        // A failed install says why, with its full log one tap away, before offering to run it again.
        if (InstallFailureNotice(project) is { } failure)
        {
            return failure;
        }

        if (!status.IsReady)
        {
            if (!manager.CanInstall || status.IsBusy)
            {
                return null;
            }

            var about = status.SizesAreExact ? string.Empty : "about ";
            return Notice(
                $"install:{toolchain.Id}",
                $"{project.Name} needs {toolchain.Name}",
                $"Install {toolchain.Name} from the Toolchain Store to run it here. It downloads {about}{DiskSpace.Format(status.DownloadSize)}.",
                $"Install {toolchain.Name}",
                async () =>
                {
                    ToolchainStore.Open(toolchain.Id);
                    await ToolchainStore.InstallAsync(toolchain.Id);
                });
        }

        if (RunPlanner.DependenciesOf(project) is { } dependency && RunPlanner.IsPending(project, dependency))
        {
            return Notice(
                $"packages:{project.Directory}:{dependency.Manifest}",
                "Install packages?",
                $"{project.Name} lists its packages in {dependency.Manifest}. {dependency.Description}",
                dependency.Name,
                () => InstallDependenciesAsync(project, dependency));
        }

        return null;
    }

    private ToolchainNoticeViewModel Notice(string key, string title, string message, string actionText, Func<Task> action) => new(
        key,
        title,
        message,
        actionText,
        new AsyncRelayCommand(async () =>
        {
            ToolchainNotice = null;
            _dismissedNotices.Add(key);
            await action();
        }),
        new RelayCommand(() =>
        {
            _dismissedNotices.Add(key);
            ToolchainNotice = null;
        }));

    private ProblemMatcher Matcher(ToolchainDefinition toolchain)
    {
        if (!_matchers.TryGetValue(toolchain.Id, out var matcher))
        {
            matcher = new ProblemMatcher(toolchain);
            _matchers[toolchain.Id] = matcher;
        }

        return matcher;
    }

    /// <summary>The source folders of Gradle and Maven projects, where a JVM stack frame's package path is.</summary>
    private static readonly string[] JvmSourceFolders =
        ["src/main/java", "src/main/kotlin", "src/test/java", "src/test/kotlin", "app/src/main/java", "app/src/main/kotlin", "src"];

    /// <summary>
    /// A printed path as a full path: relative ones are relative to the folder the program ran in, or, for a JVM
    /// stack frame, to one of its source folders.
    /// </summary>
    private string? ResolveOutputPath(string file)
    {
        try
        {
            if (IsRemoteWorkspace && _remoteWorkspace is { } remote) { return remote.ToLocalPath(file); }
            if (Path.IsPathRooted(file))
            {
                return Path.GetFullPath(file);
            }

            if (_runDirectory is not { } folder)
            {
                return null;
            }

            // A JVM stack frame's file is relative to a source folder, not to where the program ran.
            var path = Path.GetFullPath(Path.Combine(folder, file));
            return File.Exists(path) ? path
                : JvmSourceFolders.Select(source => Path.GetFullPath(Path.Combine(folder, source, file))).FirstOrDefault(File.Exists) ?? path;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private bool IsWorkspaceFile(string file) =>
        ResolveOutputPath(file) is { } path && Workspace is { } workspace
        && path.StartsWith(workspace.RootDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        && File.Exists(path);

    /// <summary>The Output panel's links: file locations the workspace's toolchains print, in files that exist.</summary>
    private List<OutputLink> FindOutputLinks(string line)
    {
        if (_toolchainProjects.Count == 0 && _runDirectory is null)
        {
            return [];
        }

        var links = new List<OutputLink>();
        if (IsRemoteWorkspace)
        {
            links.AddRange(RemoteErrorLocations(line).Select(location => new OutputLink(location.FilePath, location.Line, location.Column, location.Start, location.Length)));
        }
        foreach (var toolchain in _toolchains.Catalog.Listed)
        {
            foreach (var location in Matcher(toolchain).Match(line))
            {
                if (ResolveOutputPath(location.File) is { } path && File.Exists(path) && !links.Any(l => l.Start < location.Start + location.Length && location.Start < l.Start + l.Length))
                {
                    links.Add(new OutputLink(path, location.Line, location.Column ?? 1, location.Start, location.Length));
                }
            }
        }

        return links;
    }

    private async void OnOutputLinkActivated(object? sender, OutputLink link)
    {
        try
        {
            await NavigateToAsync(link.FilePath, link.Line, link.Column);
        }
        catch (IOException)
        {
            // The file went away since it was printed.
        }
    }

    /// <summary>An Error List entry for the error a run ended with.</summary>
    private BuildDiagnostic ToDiagnostic(ToolchainProject project, RunError error)
    {
        DiagnosticLocation? location = null;
        if (error.Location is { } where && ResolveOutputPath(where.File) is { } path)
        {
            location = new DiagnosticLocation(path, OffsetOf(path, where.Line), 0, where.Line, where.Column ?? 1);
        }

        return new BuildDiagnostic(project.Toolchain.Name, DiagnosticSeverityLevel.Error, error.Message, location, project.Name);
    }

    private static int OffsetOf(string path, int line)
    {
        try
        {
            var offset = 0;
            var number = 1;
            foreach (var text in File.ReadLines(path))
            {
                if (number++ >= line)
                {
                    break;
                }

                offset += text.Length + 1;
            }

            return offset;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
