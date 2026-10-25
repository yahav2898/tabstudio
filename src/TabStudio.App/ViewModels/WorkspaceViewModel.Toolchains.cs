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
/// File runs the file being edited. A toolchain that is not installed is installed only after the user agrees.
/// Debugging these languages comes from their debug adapters (the catalog's <c>debugAdapter</c>); until one is
/// connected, F5 runs without the debugger.
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
    public bool CanRunCurrentFile => ActiveDocument is { } document && _toolchains.Catalog.ForFile(document.FilePath) is { Listed: true } && CanRun();

    private void InitializeToolchains(ToolchainServices? toolchains)
    {
        _ownsToolchains = toolchains is null;
        _toolchains = toolchains ?? new ToolchainServices(_paths);
        ToolchainStore = new ToolchainStoreViewModel(_toolchains, _ui, (title, message, confirm, destructive) => Prompt.ConfirmAsync(title, message, confirm, destructive));
        Terminal = new TerminalViewModel(_toolchains, _ui, () => Workspace?.RootDirectory ?? _paths.Projects, ToolchainStore.Open);
        _toolchains.Manager.StatusChanged += OnToolchainStatusChanged;
        Output.LinkFinder = FindOutputLinks;
        Output.LinkActivated += OnOutputLinkActivated;
        PropertyChanged += OnToolchainPropertyChanged;
        ToolchainStore.PropertyChanged += OnToolchainStorePropertyChanged;
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
        PropertyChanged -= OnToolchainPropertyChanged;
        ToolchainStore.PropertyChanged -= OnToolchainStorePropertyChanged;
        Output.LinkActivated -= OnOutputLinkActivated;
        _toolchains.Manager.StatusChanged -= OnToolchainStatusChanged;
        Terminal.Dispose();
        ToolchainStore.Dispose();
        if (_ownsToolchains)
        {
            _toolchains.Dispose();
        }
    }

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
        else if (StartupProject is null && _toolchainProjects.Count > 0)
        {
            StartupProject = _toolchainProjects[0].Info;
        }

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
        Kind = project.Toolchain.Id switch
        {
            "python" => ProjectKind.Python,
            "node" => ProjectKind.Node,
            _ => ProjectKind.Unknown,
        },
        OutputType = "Exe",
    };

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
                Status = CompatibilityStatus.Ready,
                Explanation = $"Runs with {toolchain.Name}{version} {source}, with its output in the Output panel and errors in the Error List.",
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
                if (IsTerminalSelected)
                {
                    Terminal.EnsureStarted();
                }

                break;
            case nameof(StartupProject):
                UpdateToolchainNotice();
                break;
            case nameof(ActiveDocument) or nameof(State):
                if (e.PropertyName == nameof(State) && _openToolchainEntry && State == IdeState.Ready)
                {
                    _openToolchainEntry = false;
                    if (!HasDocuments && CurrentToolchainProject() is { } item && EntryFileOf(item.Project) is { } entry)
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
        if (CurrentToolchainProject() is { } item && RunPlanner.DependenciesOf(item.Project) is { } dependency)
        {
            await InstallDependenciesAsync(item.Project, dependency);
        }
    }

    private bool CanInstallProjectPackages() =>
        CurrentToolchainProject() is { } item && RunPlanner.DependenciesOf(item.Project) is not null && State is IdeState.Ready or IdeState.Failed;

    /// <summary>F5 and Ctrl+F5 on a toolchain project.</summary>
    private async Task RunToolchainProjectAsync(ProjectInfo info)
    {
        if (FindToolchainProject(info) is not { } item)
        {
            return;
        }

        var project = item.Project;
        var file = ActiveDocument?.FilePath;
        await RunToolchainPlanAsync(project, () =>
            RunPlanner.PlanProject(project)
            ?? (file is not null && project.Contains(file) && project.Toolchain.RunsFile(file) ? RunPlanner.PlanFile(project, file) : null)
            ?? OnlyProgramOf(project));
    }

    /// <summary>The file to show first: the entry point's source, or the project's first program, or its project file.</summary>
    private static string? EntryFileOf(ToolchainProject project)
    {
        var toolchain = project.Toolchain;
        foreach (var rule in toolchain.Run?.Entries ?? [])
        {
            var file = Path.Combine(project.Directory, rule.File);
            if (!File.Exists(file))
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

        return Directory.EnumerateFiles(project.Directory).Where(toolchain.RunsFile).Order(StringComparer.Ordinal).FirstOrDefault() ?? project.MarkerPath;
    }

    /// <summary>For a folder with one program at its top and no entry point, that program.</summary>
    private static RunPlan? OnlyProgramOf(ToolchainProject project)
    {
        var programs = Directory.EnumerateFiles(project.Directory).Where(project.Toolchain.RunsFile).Take(2).ToList();
        return programs.Count == 1 ? RunPlanner.PlanFile(project, programs[0]) : null;
    }

    private async Task RunToolchainPlanAsync(ToolchainProject project, Func<RunPlan?> planner)
    {
        var toolchain = project.Toolchain;
        if (!await EnsureToolchainReadyAsync(toolchain, $"{project.Name} runs with {toolchain.Name}, which is not installed yet."))
        {
            return;
        }

        await SaveAllCoreAsync();
        Output.Clear();
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        if (planner() is not { } plan)
        {
            var extensions = string.Join(" or ", toolchain.FileExtensions.Take(2));
            Output.WriteLine($"{project.Name} has no entry point to run. Open a {extensions} file and press F5 to run it, or add {(toolchain.Run?.Entries is [var first, ..] ? first.File : "one")}.", OutputKind.Failure);
            StatusText = "Nothing to run";
            return;
        }

        ErrorList.SetRunError(project.Directory, null);
        _runDirectory = plan.Run.WorkingDirectory ?? project.Directory;
        State = IdeState.Running;
        StatusText = $"Running {plan.EntryPoint}";
        var status = _toolchains.Manager.Status(toolchain.Id);
        Output.WriteLine($"Running {plan.EntryPoint} with {toolchain.Name}{(status.Version is { } version ? " " + version : string.Empty)}", OutputKind.Info);
        _run = new CancellationTokenSource();
        OutputProcessResult result;
        var input = Output.BeginProcessInput();
        try
        {
            if (plan.Build is { } build)
            {
                Output.WriteLine("> " + build.CommandLine, OutputKind.Info);
                var built = await OutputProcess.RunAsync(_toolchains.Runtime, build, Output, null, _run.Token);
                if (!built.Succeeded)
                {
                    result = built;
                    ReportToolchainRun(project, plan.EntryPoint, result);
                    return;
                }
            }

            result = await OutputProcess.RunAsync(_toolchains.Runtime, plan.Run, Output, input, _run.Token);
        }
        finally
        {
            Output.EndInput();
            _run?.Dispose();
            _run = null;
        }

        ReportToolchainRun(project, plan.EntryPoint, result);
    }

    /// <summary>Ends a run: the exit line, as Visual Studio writes it, the Error List and the status bar.</summary>
    private void ReportToolchainRun(ToolchainProject project, string entryPoint, OutputProcessResult result)
    {
        Output.WriteLine();
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
            StatusText = "Ready";
            return;
        }

        if (Matcher(project.Toolchain).FindError(result.Lines, IsWorkspaceFile) is { } error)
        {
            ErrorList.SetRunError(project.Directory, ToDiagnostic(project, error));
        }

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
        await _toolchains.EnsureStatusAsync();
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
        if (!await EnsureToolchainReadyAsync(project.Toolchain, $"Installing {project.Name}'s packages needs {project.Toolchain.Name}, which is not installed yet."))
        {
            return;
        }

        await SaveAllCoreAsync();
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
            foreach (var step in RunPlanner.PlanDependencies(project, dependency))
            {
                Output.WriteLine("> " + step.CommandLine, OutputKind.Info);
                var result = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, _run.Token);
                if (!result.Succeeded)
                {
                    failed = result;
                    break;
                }
            }
        }
        finally
        {
            _run?.Dispose();
            _run = null;
        }

        if (failed is null)
        {
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
        if (CurrentToolchainProject() is not { } item)
        {
            return null;
        }

        var project = item.Project;
        var toolchain = item.Toolchain;
        var manager = _toolchains.Manager;
        var status = manager.Status(toolchain.Id);
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

    /// <summary>A printed path as a full path: relative ones are relative to the folder the program ran in.</summary>
    private string? ResolveOutputPath(string file)
    {
        try
        {
            return Path.IsPathRooted(file) ? Path.GetFullPath(file) : _runDirectory is { } folder ? Path.GetFullPath(Path.Combine(folder, file)) : null;
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
