using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The root of the app: shows the start screen or an open workspace, and restores the last session when the
/// app starts.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<WorkspaceViewModel> _createWorkspace;
    private readonly RecentWorkspaces _recent;
    private readonly SessionStore _sessions;
    private readonly CompilerServices _compiler;

    /// <summary>Creates the root view model.</summary>
    /// <param name="start">The start screen.</param>
    /// <param name="createWorkspace">Creates the IDE for a workspace.</param>
    /// <param name="recent">The recent workspace list.</param>
    /// <param name="sessions">The saved session.</param>
    /// <param name="compiler">The shared compiler services, warmed up at startup.</param>
    /// <param name="gitHubAccount">The GitHub account dialog, shown over every page.</param>
    /// <param name="link">The TabStudio Link dialog, shown over every page.</param>
    /// <param name="desktopMode">Desktop mode, which shows the IDE on an external display; none when <see langword="null"/>.</param>
    /// <param name="swarmDevice">The page this device shows in a swarm; none when <see langword="null"/>.</param>
    public MainViewModel(StartViewModel start, Func<WorkspaceViewModel> createWorkspace, RecentWorkspaces recent, SessionStore sessions, CompilerServices compiler, GitHubAccountViewModel gitHubAccount, LinkViewModel link, DesktopModeViewModel? desktopMode = null, Swarm.SwarmDevicePageViewModel? swarmDevice = null, Live.LiveJoinViewModel? liveJoin = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(createWorkspace);

        DesktopMode = desktopMode;
        SwarmDevice = swarmDevice;
        Start = start;
        GitHubAccount = gitHubAccount;
        Link = link;
        _createWorkspace = createWorkspace;
        _recent = recent;
        _sessions = sessions;
        _compiler = compiler;
        CurrentPage = start;
        Start.OpenRequested += async (_, path) => await OpenWorkspaceAsync(path);
        LiveJoin = liveJoin;
        if (liveJoin is not null)
        {
            liveJoin.Live.GuestJoined += OnLiveGuestJoined;
            liveJoin.Live.GuestEnded += OnLiveGuestEnded;
        }
    }

    /// <summary>TabStudio Live's Join dialog, shown over every page; none in some tests.</summary>
    public Live.LiveJoinViewModel? LiveJoin { get; }

    /// <summary>The start screen.</summary>
    public StartViewModel Start { get; }

    /// <summary>The GitHub account dialog, shared by the start screen and the IDE.</summary>
    public GitHubAccountViewModel GitHubAccount { get; }

    /// <summary>The TabStudio Link dialog, shared by the start screen and the IDE.</summary>
    public LinkViewModel Link { get; }

    /// <summary>What this device shows while it is in a tablet's swarm: the tablet's app, full screen, over everything else.</summary>
    public Swarm.SwarmDevicePageViewModel? SwarmDevice { get; }

    /// <summary>The page shown: the start screen or a <see cref="WorkspaceViewModel"/>.</summary>
    [ObservableProperty]
    public partial object CurrentPage { get; private set; }

    /// <summary>The open workspace, if any.</summary>
    public WorkspaceViewModel? Workspace => CurrentPage as WorkspaceViewModel;

    /// <summary>Starts warming up Roslyn and reopens the last session, if there is one.</summary>
    public async Task StartAsync()
    {
        _ = _compiler.WarmUpAsync();

        // Copies of other devices' workspaces from sessions the app did not end, as when it was closed in one.
        LiveJoin?.Live.DeleteCopies();
        if (_sessions.Load() is { } session && LiveJoin?.Live.IsCopy(session.WorkspacePath) != true)
        {
            await OpenWorkspaceAsync(session.WorkspacePath, session);
        }
    }

    /// <summary>Opens a workspace, closing the current one once its unsaved documents are saved.</summary>
    /// <param name="path">A solution, project or folder.</param>
    /// <param name="session">A session to restore, if any.</param>
    /// <returns><see langword="false"/> when the open workspace stays because a document could not be saved.</returns>
    public async Task<bool> OpenWorkspaceAsync(string path, SessionState? session = null)
    {
        // Closing disposes the documents without saving: joining a Live session from an open workspace lost its edits.
        if (CurrentPage is WorkspaceViewModel { IsLiveGuest: false } current && !await current.SaveAllCoreAsync())
        {
            return false;
        }

        CloseCurrentWorkspace();

        var workspace = _createWorkspace();
        workspace.CloseRequested += OnWorkspaceCloseRequested;
        workspace.OpenWorkspaceRequested += OnOpenWorkspaceRequested;
        workspace.NewProjectRequested += OnNewProjectRequested;
        CurrentPage = workspace;
        OnPropertyChanged(nameof(Workspace));
        try
        {
            await workspace.LoadAsync(path, session);
            if (workspace.Workspace is { } loaded && LiveJoin?.Live.IsCopy(path) != true)
            {
                var description = loaded.Projects.Count switch
                {
                    0 => "Folder",
                    1 => "1 project",
                    var count => $"{count} projects",
                };
                _recent.Add(new RecentWorkspace(path, loaded.Name, description, DateTimeOffset.Now));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Core.Workspaces.ProjectLoadException)
        {
            CloseCurrentWorkspace();
            Start.ShowError($"Could not open '{Path.GetFileName(path)}': {ex.Message}");
            Start.RefreshRecent();
            CurrentPage = Start;
            OnPropertyChanged(nameof(Workspace));
        }

        return true;
    }

    /// <summary>This device joined a Live session: the IDE opens the copy of the shared workspace.</summary>
    private async void OnLiveGuestJoined(object? sender, global::TabStudio.Live.Sessions.LiveGuest guest)
    {
        if (guest.WorkspaceRoot is not { } root)
        {
            return;
        }

        var entry = guest.EntryPath is { } relative && guest.FullPathOf(relative) is { } full && File.Exists(full) ? full : root;
        if (!await OpenWorkspaceAsync(entry))
        {
            // The open workspace's edits could not be saved, and it says why: it stays, and the session is left.
            await guest.LeaveAsync();
        }
    }

    /// <summary>The session this device joined ended: its copy of the workspace closes and is deleted.</summary>
    private void OnLiveGuestEnded(object? sender, string reason)
    {
        if (Workspace is { IsLiveGuest: true } || (Workspace?.Workspace is { } open && LiveJoin?.Live.IsCopy(open.RootDirectory) == true))
        {
            CloseCurrentWorkspace();
            Start.RefreshRecent();
            CurrentPage = Start;
            OnPropertyChanged(nameof(Workspace));
        }

        Start.ShowError(reason);
        LiveJoin?.Live.DeleteCopies();
    }

    /// <summary>Saves the session, for example when the app goes to the background.</summary>
    public void SaveSession() => Workspace?.SaveSessionNow();

    private void OnWorkspaceCloseRequested(object? sender, EventArgs e)
    {
        CloseCurrentWorkspace();
        Start.RefreshRecent();
        CurrentPage = Start;
        OnPropertyChanged(nameof(Workspace));
    }

    /// <summary>The search opened a recent solution: it takes the open one's place.</summary>
    private async void OnOpenWorkspaceRequested(object? sender, string path) => await OpenWorkspaceAsync(path);

    /// <summary>The search started a new project from a template: the Start window's New project, with the template chosen.</summary>
    private void OnNewProjectRequested(object? sender, ProjectTemplate template)
    {
        CloseCurrentWorkspace();
        Start.RefreshRecent();
        Start.SelectedAction = StartAction.NewProject;
        Start.SelectedTemplate = template;
        CurrentPage = Start;
        OnPropertyChanged(nameof(Workspace));
    }

    private void CloseCurrentWorkspace()
    {
        if (CurrentPage is WorkspaceViewModel current)
        {
            current.CloseRequested -= OnWorkspaceCloseRequested;
            current.OpenWorkspaceRequested -= OnOpenWorkspaceRequested;
            current.NewProjectRequested -= OnNewProjectRequested;
            current.Dispose();
        }
    }
}
