using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>What the Solution Explorer's context menu can do. Implemented by the workspace.</summary>
public interface IExplorerActions
{
    /// <summary>Opens a file.</summary>
    Task OpenAsync(WorkspaceNode node);

    /// <summary>Creates a file in a folder or project.</summary>
    Task NewFileAsync(WorkspaceNode node);

    /// <summary>Creates a folder in a folder or project.</summary>
    Task NewFolderAsync(WorkspaceNode node);

    /// <summary>Renames a file or folder.</summary>
    Task RenameAsync(WorkspaceNode node);

    /// <summary>Moves a file or folder to TabStudio's trash.</summary>
    Task DeleteAsync(WorkspaceNode node);

    /// <summary>Converts a project into a sibling Avalonia project.</summary>
    Task PortToAvaloniaAsync(WorkspaceNode node) => Task.CompletedTask;

    /// <summary>Makes a project the startup project.</summary>
    void SetAsStartupProject(WorkspaceNode node);
}
