using NuGet.Frameworks;
using TabStudio.Core.Workspaces;

namespace TabStudio.Packages;

/// <summary>
/// .NET Framework projects, which TabStudio compiles and runs with .NET: the framework assemblies whose types .NET only
/// forwards to a package, which such a project gets that package for.
/// </summary>
public sealed partial class PackageRestorer
{
    /// <summary>
    /// The types a .NET Framework assembly has that .NET's assembly of the same name forwards to a package, by namespace,
    /// with the package and the lowest version of it that runs on .NET 10. .NET Framework's System.Data has
    /// <c>System.Data.SqlClient</c>; .NET's System.Data forwards it to the System.Data.SqlClient package, so a
    /// <c>SqlParameter</c> in a .NET Framework library, such as Dapper.EntityFramework's, is an error without it (CS1069).
    /// </summary>
    private static readonly (string Namespace, string Package, string Version)[] ForwardedFrameworkTypes =
    [
        ("System.Data.SqlClient", "System.Data.SqlClient", "4.9.0"),
    ];

    /// <summary>
    /// For a project that targets the .NET Framework, the project with a reference to each package that has the types
    /// of a framework assembly its code uses and .NET only forwards (see <see cref="ForwardedFrameworkTypes"/>), unless
    /// it references that package already. Code uses a namespace when one of its source files names it; a project that
    /// uses none of them restores nothing more, so it still builds offline.
    /// </summary>
    /// <param name="project">The project to restore.</param>
    internal static ProjectInfo WithForwardedFrameworkPackages(ProjectInfo project)
    {
        if (ProjectParser.EvaluationTargetFramework(project.TargetFrameworks) is not { } framework
            || !NuGetFramework.Parse(framework).IsDesktop())
        {
            return project;
        }

        var missing = ForwardedFrameworkTypes.Where(t => !project.HasPackage(t.Package)).ToList();
        if (missing.Count == 0)
        {
            return project;
        }

        var used = missing.Where(t => SourcesName(project, t.Namespace)).ToList();
        return used.Count == 0
            ? project
            : project with { PackageReferences = [.. project.PackageReferences, .. used.Select(t => new PackageReferenceInfo(t.Package, t.Version))] };
    }

    /// <summary>Whether one of a project's source files has a text in it, such as a namespace.</summary>
    private static bool SourcesName(ProjectInfo project, string text)
    {
        foreach (var path in ProjectSourceFiles.Enumerate(project, extension: project.IsVisualBasic ? ".vb" : ".cs"))
        {
            try
            {
                if (File.ReadAllText(path).Contains(text, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file that cannot be read is left to the build, which reports it.
            }
        }

        return false;
    }
}
