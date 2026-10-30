using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Toolchains;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Setting projects up when they open. With "Set projects up when they open" on, the default, opening a repository
/// or folder installs what its projects need without asking: their toolchains from the Toolchain Store, and their
/// packages (<c>pip install</c> into the project's <c>.venv</c>, <c>npm install</c>, or <c>npm ci</c> when a lock
/// file records them), with progress in the status bar and the Output panel. It can be cancelled, and it sets each
/// project up once, until its manifest changes. With the setting off, the "Install packages?" notice and F5's
/// question offer each install instead. .NET projects restore their packages on open either way.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private ProjectSetupStore? _setupStore;
    private ProjectSetupSettings? _setupSettings;
    private CancellationTokenSource? _setup;
    private Task _setupTask = Task.CompletedTask;
    private string? _setupToolchain;
    private bool _setupRequested;

    /// <summary>
    /// Whether opening a repository or folder sets its projects up without asking (Tools &gt; Set Projects Up When
    /// They Open). The setting is the app's, kept for every workspace.
    /// </summary>
    public bool SetUpProjectsWhenTheyOpen
    {
        get => SetupSettings.SetUpProjectsWhenTheyOpen;
        set
        {
            if (value == SetUpProjectsWhenTheyOpen)
            {
                return;
            }

            _setupSettings = SetupSettings with { SetUpProjectsWhenTheyOpen = value };
            SetupStore.Save(_setupSettings);
            OnPropertyChanged();
            if (value)
            {
                StartProjectSetup();
            }
            else
            {
                CancelProjectSetup();
            }

            UpdateToolchainNotice();
        }
    }

    /// <summary>
    /// What setting the projects up does now, for the status bar, such as "Installing Python 40%" or "Setting up
    /// Greeter: npm ci"; <see langword="null"/> when it is not running.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingUpProjects))]
    public partial string? ProjectSetupText { get; private set; }

    /// <summary>Whether the projects are being set up, which the status bar shows with a Cancel button.</summary>
    public bool IsSettingUpProjects => ProjectSetupText is not null;

    /// <summary>The task setting the projects up, which is complete when nothing is being set up.</summary>
    public Task ProjectSetupTask => _setupTask;

    private ProjectSetupStore SetupStore => _setupStore ??= new ProjectSetupStore(_paths);

    private ProjectSetupSettings SetupSettings => _setupSettings ??= SetupStore.Load();

    /// <summary>Stops setting the projects up: the toolchain's download is kept for the next attempt.</summary>
    [RelayCommand]
    private void CancelProjectSetup()
    {
        _setupRequested = false;
        _setup?.Cancel();
        if (_setupToolchain is { } id)
        {
            ToolchainStore.Cancel(id);
        }
    }

    /// <summary>Sets the workspace's projects up in the background, when the setting is on.</summary>
    private void StartProjectSetup()
    {
        // Built-in .NET projects restore elsewhere; do not probe external compilers for an unrelated workspace.
        if (!SetUpProjectsWhenTheyOpen || _disposed || IsRemoteWorkspace || Workspace is null || (_toolchainProjects.Count == 0 && StaticSite is null))
        {
            return;
        }

        _setupRequested = true;
        if (_setupTask.IsCompleted)
        {
            _setupTask = RunProjectSetupQueueAsync();
        }
    }

    private async Task RunProjectSetupQueueAsync()
    {
        try
        {
            do
            {
                _setupRequested = false;
                await SetUpProjectsAsync();
            }
            while (_setupRequested && SetUpProjectsWhenTheyOpen && !_disposed && !IsRemoteWorkspace);
        }
        finally
        {
            _setupTask = Task.CompletedTask;
            if (!_disposed)
            {
                UpdateToolchainNotice();
            }
        }
    }

    /// <summary>Waits for setting the projects up to finish, so that F5 runs what it installed.</summary>
    private async Task WaitForProjectSetupAsync()
    {
        if (_setupTask.IsCompleted)
        {
            return;
        }

        StatusText = "Waiting for the project setup to finish";
        await _setupTask;
    }

    /// <summary>A saved manifest or lock file sets its project up again, as opening the workspace would.</summary>
    /// <param name="document">The saved document.</param>
    private void OnDocumentSavedForProjectSetup(DocumentViewModel document)
    {
        var file = document.FilePath;
        if (_toolchainProjects.Any(item => RunPlanner.DependenciesOf(item.Project) is { } dependency
            && RunPlanner.DependencyInputs(item.Project, dependency, _toolchainProjects.Select(p => p.Project))
                .Contains(file, StringComparer.Ordinal)))
        {
            StartProjectSetup();
        }
    }

    private void DisposeProjectSetup() => CancelProjectSetup();

    private async Task SetUpProjectsAsync()
    {
        if (IsRemoteWorkspace)
        {
            _setupRequested = false;
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _setup = cancellation;
        var token = cancellation.Token;
        var installed = new List<string>();
        ProjectSetupText = "Checking project setup";
        try
        {
            await _toolchains.EnsureStatusAsync().WaitAsync(token);
            if (IsRemoteWorkspace)
            {
                _setupRequested = false;
                return;
            }

            var current = CurrentToolchainProject();
            var projects = _toolchainProjects.OrderBy(item => ReferenceEquals(item, current) ? 0 : 1).Select(item => item.Project).ToList();
            foreach (var toolchain in ToolchainsToInstall(projects))
            {
                token.ThrowIfCancellationRequested();
                if (await InstallToolchainForSetupAsync(toolchain, token))
                {
                    installed.Add(toolchain.Name);
                }
            }

            foreach (var project in projects.Select(p => NodeWorkspaces.DependencyOwner(p, projects)).DistinctBy(p => (p.Toolchain.Id, p.Directory)))
            {
                token.ThrowIfCancellationRequested();
                if (RunPlanner.DependenciesOf(project) is not { } dependency || !_toolchains.Manager.IsReady(project.Toolchain.Id))
                {
                    continue;
                }

                var manifest = Path.Combine(project.Directory, dependency.Manifest);
                var fingerprint = RunPlanner.FingerprintOf(project, dependency, projects);
                var recorded = SetupSettings.Projects?.GetValueOrDefault(manifest);
                var pending = RunPlanner.IsPending(project, dependency);
                if ((!pending && recorded == fingerprint) || !RunPlanner.RequiresPackages(project, dependency))
                {
                    // Installed already, or nothing to install: set up until the manifest changes.
                    RememberSetUp(manifest, fingerprint);
                    continue;
                }

                // A fresh clone installs what its lock file records; a changed manifest updates the install and the lock.
                var manifests = RunPlanner.FingerprintOf(project, dependency, projects, includeLocks: false);
                if (await InstallPackagesForSetupAsync(project, RunPlanner.DependenciesOf(project, useLockFile: pending || recorded is null) ?? dependency, token))
                {
                    if (manifests == RunPlanner.FingerprintOf(project, dependency, projects, includeLocks: false))
                    {
                        RememberSetUp(manifest, RunPlanner.FingerprintOf(project, dependency, projects));
                    }
                    else
                    {
                        // A save during the install must not mark packages from the old manifest as current.
                        _setupRequested = true;
                    }

                    installed.Add(project.Name);
                }
            }

            // A Laravel app's .env, application key and SQLite file, once its packages are there.
            foreach (var project in projects.Where(p => p.Toolchain.Id == "php" && _toolchains.Manager.IsReady("php")))
            {
                token.ThrowIfCancellationRequested();
                if (await SetUpLaravelAsync(project, token))
                {
                    installed.Add(project.Name);
                }
            }

            if (installed.Count > 0 && State is IdeState.Ready or IdeState.Failed)
            {
                StatusText = $"Set up: {string.Join(", ", installed)}";
            }
        }
        catch (OperationCanceledException)
        {
            _setupRequested = false;
            Output.WriteLine("The project setup was cancelled. Tools > Install Project Packages and the Toolchain Store install what is left.", OutputKind.Info);
            if (State is IdeState.Ready or IdeState.Failed)
            {
                StatusText = "Project setup cancelled";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Output.WriteLine($"Project setup failed: {ex.Message.TrimEnd('.')}. Use Tools > Install Project Packages to retry.", OutputKind.Failure);
            ShowSetupOutput();
        }
        finally
        {
            _setup = null;
            ProjectSetupText = null;
            if (!_disposed)
            {
                UpdateToolchainRows();
                UpdateToolchainNotice();
            }
        }
    }

    /// <summary>
    /// The toolchains to install: the projects' own, and Node.js for a static web site, whose HTML, CSS and JSON
    /// language servers come with it. Only ones the Toolchain Store can install and that are not installed yet.
    /// </summary>
    private List<ToolchainDefinition> ToolchainsToInstall(IEnumerable<ToolchainProject> projects)
    {
        var manager = _toolchains.Manager;
        var toolchains = projects.Select(p => p.Toolchain).ToList();
        if (StaticSite is not null && _toolchains.Catalog.ForTool("vscode-html-language-server") is { Listed: true } servers)
        {
            toolchains.Add(servers);
        }

        return manager.CanInstall
            ? [.. toolchains.DistinctBy(t => t.Id).Where(t => manager.Status(t.Id) is { } status && status.State != ToolchainState.Unavailable
                && (!status.IsReady || (status.State == ToolchainState.UpdateAvailable && t.AdditionalLanguageServers.Any(s => s.Command.Count > 0 && _toolchains.Runtime.Resolve(s.Command[0]) is null))))]
            : [];
    }

    private async Task<bool> InstallToolchainForSetupAsync(ToolchainDefinition toolchain, CancellationToken cancellationToken)
    {
        if (BlockLocalToolchainActionForRemote()) return false;
        var manager = _toolchains.Manager;
        Output.WriteLine($"------ Installing {toolchain.Name} from the Toolchain Store ------", OutputKind.Info);
        ProjectSetupText = $"Installing {toolchain.Name}";
        _setupToolchain = toolchain.Id;
        string? lastMessage = null;
        void OnProgress(object? sender, ToolchainStatus status) => _ui.Post(() =>
        {
            if (status.Toolchain.Id != toolchain.Id || _setupToolchain != toolchain.Id || status.Progress is not { } progress)
            {
                return;
            }

            ProjectSetupText = progress.Fraction is { } fraction
                ? string.Create(CultureInfo.CurrentCulture, $"Installing {toolchain.Name} {(int)Math.Round(fraction * 100)}%")
                : $"Installing {toolchain.Name}";
            if (progress.Message != lastMessage)
            {
                lastMessage = progress.Message;
                Output.WriteLine(progress.Message, OutputKind.Program);
            }
        });

        manager.StatusChanged += OnProgress;
        try
        {
            await (manager.Status(toolchain.Id).State == ToolchainState.UpdateAvailable
                ? ToolchainStore.UpdateAsync(toolchain.Id)
                : ToolchainStore.InstallAsync(toolchain.Id));
        }
        finally
        {
            manager.StatusChanged -= OnProgress;
            _setupToolchain = null;
        }

        // The Store's install ends quietly when it is cancelled.
        cancellationToken.ThrowIfCancellationRequested();
        var status = manager.Status(toolchain.Id);
        if (status.IsReady)
        {
            Output.WriteLine($"========== {toolchain.Name}{(status.Version is { } version ? " " + version : string.Empty)}: installed ==========", OutputKind.Success);
            return true;
        }

        Output.WriteLine($"========== {toolchain.Name}: not installed ({status.Error ?? status.Note ?? "the Toolchain Store could not install it"}) ==========", OutputKind.Failure);
        ShowSetupOutput();
        return false;
    }

    private async Task<bool> InstallPackagesForSetupAsync(ToolchainProject project, DependencyInstallDefinition dependency, CancellationToken cancellationToken)
    {
        if (BlockLocalToolchainActionForRemote()) return false;
        Output.WriteLine($"------ Setting up {project.Name}: {dependency.Name} ------", OutputKind.Info);
        ProjectSetupText = $"Setting up {project.Name}: {dependency.Name}";
        if (await RunDependencyInstallAsync(project, dependency, cancellationToken) is { } result)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Output.WriteLine($"========== {dependency.Name}: failed ({result.Exit?.Description ?? result.StartError}) ==========", OutputKind.Failure);
            ShowSetupOutput();
            return false;
        }

        Output.WriteLine($"========== {dependency.Name}: succeeded ==========", OutputKind.Success);
        return true;
    }

    /// <summary>
    /// Shows the setup's log when a step failed. Setup runs in the background as a project opens, with its progress in
    /// the status bar, so it no longer switches the bottom panel at every step: a person who had chosen Logcat or the
    /// Error List was moved to Output, and on a phone could not get back.
    /// </summary>
    private void ShowSetupOutput()
    {
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
    }

    private void RememberSetUp(string manifest, string fingerprint)
    {
        if (SetupSettings.Projects?.GetValueOrDefault(manifest) == fingerprint)
        {
            return;
        }

        var projects = new Dictionary<string, string>(SetupSettings.Projects ?? new Dictionary<string, string>(), StringComparer.Ordinal)
        {
            [manifest] = fingerprint,
        };
        _setupSettings = SetupSettings with { Projects = projects };
        SetupStore.Save(_setupSettings);
    }
}
