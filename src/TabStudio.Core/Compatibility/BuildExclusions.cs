using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Compatibility;

/// <summary>
/// The projects of a solution that TabStudio leaves out of builds, and why: those it cannot build on this device, those
/// that compile against one of them (built without the project they reference, they would only report its types as
/// missing, so the Compatibility Doctor names that project instead), and those the solution's Debug configuration does
/// not build, as Visual Studio leaves them out, unless a project that is built references them.
/// </summary>
public static class BuildExclusions
{
    /// <summary>
    /// Whether a project cannot be built on this device: an Aspire AppHost or another project built with its SDK (eShop's
    /// functional tests), a .NET Framework program that needs Windows, or a project that needs a platform workload,
    /// protoc or a newer .NET SDK.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsNotBuiltHere(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind == ProjectKind.AspireAppHost
            || ProjectClassifier.IsAspireAppHost(project)
            || NetFrameworkCompatibility.NeedsWindows(project)
            || ProjectClassifier.NeedsPlatformWorkload(project);
    }

    /// <summary>
    /// The projects of a solution that are left out of builds, by file path, each with the project that is not built
    /// here that it compiles against, or <see langword="null"/> when it is left out for itself.
    /// </summary>
    /// <param name="projects">The solution's projects.</param>
    public static IReadOnlyDictionary<string, ProjectInfo?> Of(IReadOnlyList<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        var excluded = new Dictionary<string, ProjectInfo?>(StringComparer.Ordinal);
        foreach (var project in projects.Where(IsNotBuiltHere))
        {
            excluded.TryAdd(project.FilePath, null);
        }

        var byPath = projects.GroupBy(p => p.FilePath, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var added = true;
        while (added)
        {
            added = false;
            foreach (var project in projects.Where(p => !excluded.ContainsKey(p.FilePath)))
            {
                var needed = project.ProjectReferences
                    .Where(r => project.GetProjectReference(r).IsCompiledAgainst && excluded.ContainsKey(r))
                    .Select(r => excluded[r] ?? byPath.GetValueOrDefault(r))
                    .FirstOrDefault(p => p is not null);
                if (needed is not null)
                {
                    excluded[project.FilePath] = needed;
                    added = true;
                }
            }
        }

        // A project the solution does not build is still built for a project that is built and references it.
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<ProjectInfo>(projects.Where(p => p.IsBuiltBySolution && !excluded.ContainsKey(p.FilePath)));
        while (pending.TryPop(out var project))
        {
            foreach (var reference in project.ProjectReferences)
            {
                if (referenced.Add(reference) && byPath.TryGetValue(reference, out var next))
                {
                    pending.Push(next);
                }
            }
        }

        foreach (var project in projects.Where(p => !p.IsBuiltBySolution && !referenced.Contains(p.FilePath)))
        {
            excluded.TryAdd(project.FilePath, null);
        }

        return excluded;
    }

    /// <summary>
    /// Why a project is not built, as a phrase after "it", such as <c>is an Aspire AppHost, which runs on a
    /// computer</c>; <see langword="null"/> for one that is built.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string? Reason(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind == ProjectKind.AspireAppHost
            ? "is an Aspire AppHost, which runs on a computer, where Aspire's developer control plane starts the app's projects and containers"
            : ProjectClassifier.IsAspireAppHost(project)
                ? "is built with the Aspire AppHost SDK, which generates the Projects classes it compiles against for the projects it references, on a computer"
                : ProjectClassifier.PlatformWorkloadReason(project) is { } workload
                ? workload
                : NetFrameworkCompatibility.NeedsWindows(project)
                    ? $"is a .NET Framework project that needs Windows: it {NetFrameworkCompatibility.WhyWindows(project)}"
                    : !project.IsBuiltBySolution
                        ? "is not built by the solution's Debug configuration"
                        : null;
    }
}
