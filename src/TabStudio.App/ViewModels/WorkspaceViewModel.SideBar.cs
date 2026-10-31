using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>The tool windows of the side bar, which shows one at a time, chosen with the tabs at its bottom.</summary>
public enum SideBarPanel
{
    /// <summary>The Solution Explorer, with the Properties panel below it.</summary>
    SolutionExplorer,

    /// <summary>The Git Changes window.</summary>
    GitChanges,

    /// <summary>The Test Explorer.</summary>
    TestExplorer,

    /// <summary>The agent: a conversation with Claude, who works on the solution.</summary>
    Agent,

    /// <summary>The web preview of the running web app.</summary>
    WebPreview,

    /// <summary>The app preview of the running Avalonia app.</summary>
    AppPreview,

    /// <summary>The GitHub Actions window: CI of the workspace's commit, runs, workflows and logs.</summary>
    Actions,

    /// <summary>The pull requests of the linked repository.</summary>
    PullRequests,

    /// <summary>The Swarm window: the phones and tablets that run the app being built.</summary>
    Swarm,

    /// <summary>The Windows Bridge: the programs running on the paired Windows PC, streamed live.</summary>
    WindowsBridge,

    /// <summary>Database connections and EF Core design tools.</summary>
    Database,
    /// <summary>SSH hosts, remote folders, run profiles and forwarded ports.</summary>
    RemoteExplorer,

    /// <summary>Native repository backing services.</summary>
    Services,

    /// <summary>OCI images, PRoot containers, logs and exec terminals.</summary>
    Containers,

    /// <summary>TabStudio Live: who is in the session, Follow, and what each participant may do.</summary>
    Live,
}

/// <summary>The side bar's tool windows, docked together as Visual Studio docks Solution Explorer, Git Changes and Test Explorer, with the agent beside them.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The tool window the side bar shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolutionExplorerSelected), nameof(IsGitChangesSelected), nameof(IsTestExplorerSelected), nameof(IsAgentSelected), nameof(IsWebPreviewSelected), nameof(IsAppPreviewSelected))]
    [NotifyPropertyChangedFor(nameof(IsActionsSelected))]
    [NotifyPropertyChangedFor(nameof(IsPullRequestsSelected))]
    [NotifyPropertyChangedFor(nameof(IsSwarmSelected), nameof(ShowsSwarmTab))]
    [NotifyPropertyChangedFor(nameof(IsWindowsBridgeSelected), nameof(IsDatabaseSelected), nameof(IsRemoteExplorerSelected))]
    [NotifyPropertyChangedFor(nameof(IsServicesSelected), nameof(IsContainersSelected), nameof(IsLiveSelected))]
    public partial SideBarPanel SelectedSideBarPanel { get; set; } = SideBarPanel.SolutionExplorer;

    /// <summary>Whether the side bar shows the Solution Explorer and Properties.</summary>
    public bool IsSolutionExplorerSelected => SelectedSideBarPanel == SideBarPanel.SolutionExplorer;

    /// <summary>Whether the side bar shows the Git Changes window.</summary>
    public bool IsGitChangesSelected => SelectedSideBarPanel == SideBarPanel.GitChanges;

    /// <summary>Whether the side bar shows the Test Explorer.</summary>
    public bool IsTestExplorerSelected => SelectedSideBarPanel == SideBarPanel.TestExplorer;

    /// <summary>Whether the side bar shows the agent.</summary>
    public bool IsAgentSelected => SelectedSideBarPanel == SideBarPanel.Agent;

    /// <summary>Shows a tool window in the side bar, opening the side bar if it is hidden. Ctrl+E, T shows the Test Explorer.</summary>
    /// <param name="panel">The tool window.</param>
    [RelayCommand]
    private void SelectSideBarPanel(SideBarPanel panel)
    {
        SelectedSideBarPanel = panel;
        IsSideBarVisible = true;
    }
}

