using TabStudio.Core.Compatibility;
using TabStudio.Core.Detection;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services;

/// <summary>What TabStudio found in a folder before opening it.</summary>
/// <param name="Workspace">The loaded workspace model.</param>
/// <param name="Stack">The technologies detected in it.</param>
/// <param name="Reports">The Compatibility Doctor's verdict for each project.</param>
public sealed record WorkspacePreview(Workspace Workspace, StackProfile Stack, IReadOnlyList<CompatibilityReport> Reports)
{
    /// <summary>A one-line summary, such as "C# · 2 projects".</summary>
    public string Summary
    {
        get
        {
            var projects = Workspace.Projects.Count;
            var kinds = Stack.Components.Select(c => c.DisplayName).Take(3).ToList();
            var technology = kinds.Count > 0 ? string.Join(", ", kinds) : "Folder";
            return projects switch
            {
                0 => technology,
                1 => $"{technology} · 1 project",
                _ => $"{technology} · {projects} projects",
            };
        }
    }
}

/// <summary>Loads a workspace and runs stack detection and the Compatibility Doctor on it, off the UI thread.</summary>
public static class WorkspaceInspector
{
    /// <summary>Inspects a solution file, project file or folder.</summary>
    /// <param name="path">What to inspect.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static Task<WorkspacePreview> InspectAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var workspace = WorkspaceLoader.Load(path);
            cancellationToken.ThrowIfCancellationRequested();
            var stack = StackDetector.Detect(workspace.RootDirectory, workspace.Projects);
            var reports = CompatibilityDoctor.Diagnose(workspace.Projects);
            return new WorkspacePreview(workspace, stack, reports);
        }, cancellationToken);
}
