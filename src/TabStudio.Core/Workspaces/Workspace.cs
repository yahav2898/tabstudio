namespace TabStudio.Core.Workspaces;

/// <summary>
/// Everything that is open in TabStudio: a solution, a project or a folder, with its projects and its file tree.
/// </summary>
public sealed class Workspace
{
    /// <summary>The display name: the solution, project or folder name.</summary>
    public required string Name { get; init; }

    /// <summary>The directory that contains everything in the workspace.</summary>
    public required string RootDirectory { get; init; }

    /// <summary>The solution, when one was opened.</summary>
    public SolutionInfo? Solution { get; init; }

    /// <summary>All projects in the workspace, in solution order.</summary>
    public IReadOnlyList<ProjectInfo> Projects { get; init; } = [];

    /// <summary>The tree shown by the Solution Explorer.</summary>
    public required WorkspaceNode Tree { get; init; }

    /// <summary>Problems found while loading, such as a project file that could not be parsed.</summary>
    public IReadOnlyList<string> LoadErrors { get; init; } = [];

    /// <summary>
    /// The project F5 should run when the user has not picked one: the first web app, else the first app with a UI,
    /// else the first console app, else any other program (see <see cref="ProjectClassifier.StartupRank"/>). Libraries,
    /// tests, build scripts, Aspire AppHosts and the WebAssembly client of a Blazor Web App are never chosen.
    /// </summary>
    public ProjectInfo? DefaultStartupProject =>
        Projects.Select((project, index) => (Project: project, Index: index, Rank: ProjectClassifier.StartupRank(project)))
            .Where(p => p.Rank is not null)
            .OrderBy(p => p.Rank)
            .ThenBy(p => p.Index)
            .Select(p => p.Project)
            .FirstOrDefault();

    /// <summary>Finds the project that owns a file: the project whose directory most closely contains it.</summary>
    /// <param name="filePath">Full path of a file in the workspace.</param>
    /// <returns>The owning project, or <see langword="null"/> when the file is outside every project.</returns>
    public ProjectInfo? FindProjectForFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var fullPath = Path.GetFullPath(filePath);

        return Projects
            .Where(p => IsUnder(fullPath, p.DirectoryPath))
            .MaxBy(p => p.DirectoryPath.Length);
    }

    /// <summary>Finds a project by name. Case-insensitive.</summary>
    /// <param name="name">The project name.</param>
    public ProjectInfo? FindProject(string name) =>
        Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsUnder(string filePath, string directory)
    {
        var prefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        return filePath.StartsWith(prefix, StringComparison.Ordinal);
    }
}
