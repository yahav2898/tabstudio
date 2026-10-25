using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>The tool windows of the side bar, which shows one at a time, chosen with the tabs at its bottom.</summary>
public enum SidePanel
{
    /// <summary>The Solution Explorer, with the Properties panel below it.</summary>
    SolutionExplorer,

    /// <summary>The Test Explorer.</summary>
    TestExplorer,
}

/// <summary>The side bar's tool windows, docked together as Visual Studio docks Solution Explorer and Test Explorer.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The tool window the side bar shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolutionExplorerSelected), nameof(IsTestExplorerSelected))]
    public partial SidePanel SelectedSidePanel { get; set; }

    /// <summary>Whether the side bar shows the Solution Explorer and Properties.</summary>
    public bool IsSolutionExplorerSelected => SelectedSidePanel == SidePanel.SolutionExplorer;

    /// <summary>Whether the side bar shows the Test Explorer.</summary>
    public bool IsTestExplorerSelected => SelectedSidePanel == SidePanel.TestExplorer;

    /// <summary>Shows a tool window in the side bar, opening the side bar if it is hidden. Ctrl+E, T shows the Test Explorer.</summary>
    /// <param name="panel">The tool window.</param>
    [RelayCommand]
    private void SelectSidePanel(SidePanel panel)
    {
        SelectedSidePanel = panel;
        IsSideBarVisible = true;
    }
}
