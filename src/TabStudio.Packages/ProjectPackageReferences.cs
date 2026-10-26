using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages.Manifests;
using TabStudio.Packages.Resolution;

namespace TabStudio.Packages;

/// <summary>What a restore starts from: a project's references, and the projects it references with theirs.</summary>
/// <param name="Dependencies">The project's own package references and project references.</param>
/// <param name="Projects">Every project it references, directly or not, by name, with that project's own references.</param>
internal sealed record ProjectDependencyGraph(
    IReadOnlyList<PackageDependency> Dependencies,
    IReadOnlyDictionary<string, PackageMatch> Projects)
{
    /// <summary>Whether any project in the graph references a package, so there is something to restore.</summary>
    public bool HasPackages =>
        Dependencies.Any(d => !d.IsProject) ||
        Projects.Values.Any(p => p.Dependencies.Any(d => !d.IsProject));
}

/// <summary>Turns the <c>&lt;PackageReference&gt;</c> and <c>&lt;ProjectReference&gt;</c> items of projects into dependencies.</summary>
internal static class ProjectPackageReferences
{
    /// <summary>
    /// Reads a project and, from disk, every project it references, as NuGet does: the package references of a
    /// referenced project flow to the projects that reference it.
    /// </summary>
    /// <param name="project">The project to restore.</param>
    /// <param name="diagnostics">Receives problems with the references, such as a version that does not parse.</param>
    public static ProjectDependencyGraph ReadGraph(ProjectInfo project, List<BuildDiagnostic> diagnostics)
    {
        var projects = new Dictionary<string, PackageMatch>(StringComparer.OrdinalIgnoreCase);

        // Project file path -> project name, or null while the project is still being read.
        var names = new Dictionary<string, string?>(StringComparer.Ordinal) { [project.FilePath] = null };

        List<PackageDependency> Collect(ProjectInfo current)
        {
            // PrivateAssets="all" keeps a referenced project's package to that project, as NuGet does.
            var dependencies = Read(current, diagnostics, includePrivate: ReferenceEquals(current, project));
            foreach (var path in current.ProjectReferences)
            {
                if (names.TryGetValue(path, out var known))
                {
                    // Read before (a diamond), or still being read (a cycle, which the build reports).
                    if (known is not null)
                    {
                        dependencies.Add(new PackageDependency(known, VersionRange.All, IsProject: true));
                    }

                    continue;
                }

                ProjectInfo referenced;
                try
                {
                    referenced = ProjectParser.Load(path);
                }
                catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
                {
                    // The workspace already reports projects it cannot read.
                    continue;
                }

                names[path] = null;
                var referencedDependencies = Collect(referenced);
                names[path] = referenced.Name;
                projects[referenced.Name] = PackageMatch.Project(referenced.Name, referencedDependencies);
                dependencies.Add(new PackageDependency(referenced.Name, VersionRange.All, IsProject: true));
            }

            return dependencies;
        }

        return new ProjectDependencyGraph(Collect(project), projects);
    }

    /// <summary>
    /// The shared frameworks a project compiles against: its own <c>FrameworkReference</c> items and, as in the .NET SDK,
    /// those of every project it references, directly or not, read from disk. A console tool that references a web
    /// project gets ASP.NET Core this way.
    /// </summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> FrameworkReferencesWithReferencedProjects(ProjectInfo project)
    {
        var frameworks = new List<string>(project.FrameworkReferences);
        var seen = new HashSet<string>(StringComparer.Ordinal) { project.FilePath };
        var pending = new Stack<string>(project.ProjectReferences);
        while (pending.TryPop(out var path))
        {
            if (!seen.Add(path))
            {
                continue;
            }

            ProjectInfo referenced;
            try
            {
                referenced = ProjectParser.Load(path);
            }
            catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
            {
                // The workspace already reports projects it cannot read.
                continue;
            }

            foreach (var framework in referenced.FrameworkReferences)
            {
                if (!frameworks.Contains(framework, StringComparer.OrdinalIgnoreCase))
                {
                    frameworks.Add(framework);
                }
            }

            foreach (var next in referenced.ProjectReferences)
            {
                pending.Push(next);
            }
        }

        return frameworks;
    }

    /// <summary>Reads the package references of one project.</summary>
    /// <param name="project">The project.</param>
    /// <param name="diagnostics">Receives problems with the references, such as a version that does not parse.</param>
    /// <param name="includePrivate">Whether to include references with <c>PrivateAssets="all"</c>: only for the project being restored.</param>
    /// <returns>One dependency per valid reference, in project order.</returns>
    public static List<PackageDependency> Read(ProjectInfo project, List<BuildDiagnostic> diagnostics, bool includePrivate = true)
    {
        var central = project.PackageReferences.Any(p => p.Version is null)
            ? CentralPackageVersions.Load(project)
            : new Dictionary<string, string>();

        var dependencies = new List<PackageDependency>();
        foreach (var reference in project.PackageReferences.Where(r => includePrivate || !r.IsPrivate))
        {
            var text = reference.Version is { } own
                ? CentralPackageVersions.ExpandProperties(own, project.Properties)
                : central.GetValueOrDefault(reference.Name);
            if (string.IsNullOrWhiteSpace(text))
            {
                diagnostics.Add(PackageDiagnostics.Warning(
                    PackageDiagnosticIds.MissingVersion,
                    $"The reference to {reference.Name} has no version, so the lowest available version is used. Add Version=\"...\" to the PackageReference.",
                    project.Name));
                dependencies.Add(new PackageDependency(reference.Name, VersionRange.All));
                continue;
            }

            if (text.Contains("$(", StringComparison.Ordinal) || !VersionRange.TryParse(text.Trim(), out var range))
            {
                diagnostics.Add(PackageDiagnostics.Error(
                    PackageDiagnosticIds.InvalidVersion,
                    $"'{text}' is not a valid NuGet version or version range, so {reference.Name} was not restored.",
                    project.Name));
                continue;
            }

            dependencies.Add(new PackageDependency(reference.Name, range));
        }

        return dependencies;
    }
}
