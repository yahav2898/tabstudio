using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages;

/// <summary>Turns a project's <c>&lt;PackageReference&gt;</c> items into the dependencies a restore starts from.</summary>
internal static class ProjectPackageReferences
{
    /// <summary>Reads the direct dependencies of a project.</summary>
    /// <param name="project">The project.</param>
    /// <param name="diagnostics">Receives problems with the references, such as a version that does not parse.</param>
    /// <returns>One dependency per valid reference, in project order.</returns>
    public static List<PackageDependency> Read(ProjectInfo project, List<BuildDiagnostic> diagnostics)
    {
        var central = project.PackageReferences.Any(p => p.Version is null)
            ? CentralPackageVersions.Load(project)
            : new Dictionary<string, string>();

        var dependencies = new List<PackageDependency>();
        foreach (var reference in project.PackageReferences)
        {
            var text = reference.Version ?? central.GetValueOrDefault(reference.Name);
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
