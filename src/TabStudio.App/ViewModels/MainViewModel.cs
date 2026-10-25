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
    /// <param name="desktopMode">Desktop mode, which shows the IDE on an external display; none when <see langword="null"/>.</param>
    public MainViewModel(StartViewModel start, Func<WorkspaceViewModel> createWorkspace, RecentWorkspaces recent, SessionStore sessions, CompilerServices compiler, GitHubAccountViewModel gitHubAccount, DesktopModeViewModel? desktopMode = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(createWorkspace);

        DesktopMode = desktopMode;
        Start = start;
        GitHubAccount = gitHubAccount;
        _createWorkspace = createWorkspace;
        _recent = recent;
        _sessions = sessions;
        _compiler = compiler;
        CurrentPage = start;
        Start.OpenRequested += async (_, path) => await OpenWorkspaceAsync(path);
    }

    /// <summary>The start screen.</summary>
    public StartViewModel Start { get; }

    /// <summary>The GitHub account dialog, shared by the start screen and the IDE.</summary>
    public GitHubAccountViewModel GitHubAccount { get; }

    /// <summary>The page shown: the start screen or a <see cref="WorkspaceViewModel"/>.</summary>
    [ObservableProperty]
    public partial object CurrentPage { get; private set; }

    /// <summary>The open workspace, if any.</summary>
    public WorkspaceViewModel? Workspace => CurrentPage as WorkspaceViewModel;

    /// <summary>Starts warming up Roslyn and reopens the last session, if there is one.</summary>
    public async Task StartAsync()
    {
        _ = _compiler.WarmUpAsync();
        if (_sessions.Load() is { } session)
        {
            await OpenWorkspaceAsync(session.WorkspacePath, session);
        }
    }

    /// <summary>Opens a workspace, closing the current one.</summary>
    /// <param name="path">A solution, project or folder.</param>
    /// <param name="session">A session to restore, if any.</param>
    public async Task OpenWorkspaceAsync(string path, SessionState? session = null)
    {
        CloseCurrentWorkspace();

        var workspace = _createWorkspace();
        workspace.CloseRequested += OnWorkspaceCloseRequested;
        CurrentPage = workspace;
        OnPropertyChanged(nameof(Workspace));
        try
        {
            await workspace.LoadAsync(path, session);
            if (workspace.Workspace is { } loaded)
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

    private void CloseCurrentWorkspace()
    {
        if (CurrentPage is WorkspaceViewModel current)
        {
            current.CloseRequested -= OnWorkspaceCloseRequested;
            current.Dispose();
        }
    }
}
