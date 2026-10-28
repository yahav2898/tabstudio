using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>What the Solution Explorer's Live Unit Testing menu does. Implemented by the workspace.</summary>
public interface ILiveTestingExplorerActions
{
    /// <summary>Includes or excludes a project in Live Unit Testing: its tests, or, for code under test, its markers.</summary>
    /// <param name="node">The project's row.</param>
    /// <param name="include">Whether to include it.</param>
    void SetLiveTesting(WorkspaceNode node, bool include);
}

/// <summary>A Solution Explorer row's Live Unit Testing menu: Include and Exclude, on C# projects.</summary>
public sealed partial class ExplorerItemViewModel
{
    /// <summary>Whether the row is a C# project that can be included in or excluded from Live Unit Testing.</summary>
    public bool CanSetLiveTesting =>
        _actions is ILiveTestingExplorerActions && Node.Kind == WorkspaceNodeKind.Project && Node.Project is { IsRoslynLanguage: true };

    /// <summary>Includes the project in Live Unit Testing.</summary>
    [RelayCommand(CanExecute = nameof(CanSetLiveTesting))]
    private void IncludeInLiveTesting() => (_actions as ILiveTestingExplorerActions)?.SetLiveTesting(Node, include: true);

    /// <summary>Excludes the project from Live Unit Testing.</summary>
    [RelayCommand(CanExecute = nameof(CanSetLiveTesting))]
    private void ExcludeFromLiveTesting() => (_actions as ILiveTestingExplorerActions)?.SetLiveTesting(Node, include: false);
}
