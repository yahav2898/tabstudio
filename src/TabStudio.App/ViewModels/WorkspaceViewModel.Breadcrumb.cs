using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>The breadcrumb under the document tabs: the active document's project and folders.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The path segments of the active document, for the breadcrumb under the tabs.</summary>
    public IReadOnlyList<string> Breadcrumb => ActiveDocument is { IsExtensionsPage: false } document && Workspace is { } workspace
        ? BuildBreadcrumb(workspace, document.FilePath)
        : [];

    private static List<string> BuildBreadcrumb(Workspace workspace, string filePath)
    {
        var project = workspace.FindProjectForFile(filePath);
        var root = project?.DirectoryPath ?? workspace.RootDirectory;
        var relative = Path.GetRelativePath(root, filePath);
        var segments = new List<string>();
        if (project is not null)
        {
            segments.Add(project.Name);
        }

        segments.AddRange(relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
        return segments;
    }
}
