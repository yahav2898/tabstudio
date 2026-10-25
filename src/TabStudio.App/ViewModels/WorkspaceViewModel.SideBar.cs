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
}

/// <summary>The side bar's tool windows, docked together as Visual Studio docks Solution Explorer, Git Changes and Test Explorer, with the agent beside them.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The tool window the side bar shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolutionExplorerSelected), nameof(IsGitChangesSelected), nameof(IsTestExplorerSelected), nameof(IsAgentSelected))]
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
