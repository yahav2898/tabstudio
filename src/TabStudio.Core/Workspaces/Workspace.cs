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
    /// The project F5 should run when the user has not picked one: the first web app with a UI, else the first app with
    /// a UI, else the first web API, else the first console app, else any other program (see
    /// <see cref="ProjectClassifier.StartupRank"/>), with programs in folders such as <c>docs</c>, <c>scripts</c> or
    /// <c>tests</c> after all the others (see <see cref="ProjectClassifier.IsInAuxiliaryFolder"/>), and the solution's
    /// order between equals, except that a console app another console app references comes before it, as Terminal.Gui's
    /// UICatalog before the ScenarioRunner that runs its scenarios, and a desktop head before a browser head. Libraries,
    /// tests, build scripts, benchmarks, Aspire AppHosts, projects that need a platform workload, the WebAssembly client of
    /// a Blazor Web App and a Blazor WebAssembly app a web app of the solution serves are never chosen;
    /// <see langword="null"/> when nothing else is left.
    /// </summary>
    public ProjectInfo? DefaultStartupProject => StartupCandidates is [var first, ..] ? first : null;

    /// <summary>
    /// The projects F5 can start, best first, in the order <see cref="DefaultStartupProject"/> chooses from: the first is
    /// the default. Libraries, tests and the other projects that are never startup projects are left out.
    /// </summary>
    public IReadOnlyList<ProjectInfo> StartupCandidates => OrderStartupCandidates();

    private List<ProjectInfo> OrderStartupCandidates()
    {
        var candidates = Projects
            .Select((project, index) => (Project: project, Index: index, Rank: ProjectClassifier.StartupRank(project)))
            .Where(p => p.Rank is not null)
            .ToList();

        // A console program that references another one runs it, as a scenario or benchmark runner does: the referenced
        // one is the app.
        var runByAnother = candidates
            .Where(p => p.Project.Kind == ProjectKind.ConsoleApp)
            .SelectMany(p => p.Project.ProjectReferences)
            .ToHashSet(StringComparer.Ordinal);

        // A Blazor WebAssembly app that a web app of the solution references is that app's client, which it serves, as
        // eShopOnWeb's Web serves BlazorAdmin: the server is what a person starts.
        var hostedClients = Projects
            .Where(p => p.IsExecutable && p.Kind is ProjectKind.AspNetCore or ProjectKind.BlazorWebApp)
            .SelectMany(p => p.ProjectReferences)
            .ToHashSet(StringComparer.Ordinal);
        candidates.RemoveAll(p => p.Project.Kind == ProjectKind.BlazorWebAssembly && hostedClients.Contains(p.Project.FilePath));

        // Of a cross-platform app's heads, the desktop one runs here as it does on a computer; its browser head, which
        // runs the same app in a browser, comes after it, as Avalonia's template has Desktop as the one to start.
        return candidates
            .OrderBy(p => ProjectClassifier.IsInAuxiliaryFolder(p.Project, RootDirectory))
            .ThenBy(p => p.Rank)
            .ThenBy(p => ProjectClassifier.IsBrowserHead(p.Project))
            .ThenBy(p => !(p.Project.Kind == ProjectKind.ConsoleApp && runByAnother.Contains(p.Project.FilePath)))
            .ThenBy(p => p.Index)
            .Select(p => p.Project)
            .ToList();
    }

    /// <summary>
    /// Why the workspace has no startup project, in one sentence for the IDE, such as
    /// <c>This solution has only libraries and tests: run the tests in Test Explorer.</c>; <see langword="null"/> when
    /// it has one, or has projects other than .NET ones, such as a Visual C++ program the Windows Bridge starts.
    /// </summary>
    public string? NoStartupProjectReason
    {
        get
        {
            if (Projects.Count == 0 || Projects.Any(p => p.Language == ProjectLanguage.Cpp) || DefaultStartupProject is not null)
            {
                return null;
            }

            // An app that runs on a computer only is named; build scripts and benchmarks are the repository's tooling.
            var hasTests = Projects.Any(p => p.IsTestProject);
            var app = Projects
                .Where(p => !p.IsTestProject)
                .Select(p => (Project: p, Reason: ComputerOnlyReason(p)))
                .FirstOrDefault(p => p.Reason is not null);
            if (app.Reason is not null)
            {
                var tests = hasTests ? " Run the tests in Test Explorer." : string.Empty;
                return $"Nothing in this solution starts on this tablet: {app.Project.Name} {app.Reason}.{tests}";
            }

            var parts = new List<string>();
            if (Projects.Any(p => !p.IsTestProject && p.Kind is not (ProjectKind.Benchmark or ProjectKind.BuildScript)))
            {
                parts.Add("libraries");
            }

            if (hasTests)
            {
                parts.Add("tests");
            }

            if (Projects.Any(p => p.Kind == ProjectKind.Benchmark))
            {
                parts.Add("benchmarks");
            }

            if (Projects.Any(p => p.Kind == ProjectKind.BuildScript))
            {
                parts.Add("a build script");
            }

            var what = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
            return hasTests
                ? $"This solution has only {what}: run the tests in Test Explorer."
                : $"This solution has only {what}: nothing in it starts on its own.";
        }
    }

    /// <summary>Why an app runs on a computer only, as a phrase after its name; <see langword="null"/> for any other project.</summary>
    private static string? ComputerOnlyReason(ProjectInfo project) =>
        ProjectClassifier.PlatformWorkloadReason(project)
        ?? (project.Kind == ProjectKind.AspireAppHost ? "is an Aspire AppHost, which runs on a computer" : null);

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
