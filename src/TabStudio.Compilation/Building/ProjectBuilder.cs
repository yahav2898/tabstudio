using System.Diagnostics;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.Compilation.Building;

/// <summary>
/// The outcome of building a project and the projects it depends on.
/// </summary>
/// <param name="ProjectName">The project that was built.</param>
/// <param name="Diagnostics">Diagnostics from every compiled project, most severe first.</param>
/// <param name="EntryAssembly">The project's own assembly, or <see langword="null"/> when the build failed.</param>
/// <param name="Dependencies">The assemblies of the projects it references, which must be loaded alongside it.</param>
/// <param name="Duration">How long the build took.</param>
public sealed record BuildResult(
    string ProjectName,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    CompiledAssembly? EntryAssembly,
    IReadOnlyList<CompiledAssembly> Dependencies,
    TimeSpan Duration)
{
    /// <summary>Whether the build produced an assembly.</summary>
    public bool Success => EntryAssembly is not null;

    /// <summary>The number of errors.</summary>
    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverityLevel.Error);

    /// <summary>The number of warnings.</summary>
    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverityLevel.Warning);
}

/// <summary>
/// Builds a project and its project references in dependency order, entirely in memory.
/// </summary>
/// <remarks>
/// Compilation goes through the <see cref="RoslynWorkspaceHost"/>, so a build reuses the compilations the
/// editor already keeps warm for live diagnostics. A build after a small edit only recompiles what changed.
/// </remarks>
public sealed class ProjectBuilder
{
    /// <summary>Reported for a project in a language TabStudio cannot compile.</summary>
    public const string UnsupportedLanguageId = "TS2002";

    /// <summary>Reported for a NuGet package that has not been restored.</summary>
    public const string UnrestoredPackageId = "TS2003";

    private readonly RoslynWorkspaceHost _host;

    /// <summary>Creates a builder over a loaded workspace host.</summary>
    /// <param name="host">The host that holds the projects.</param>
    public ProjectBuilder(RoslynWorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>Builds a project and everything it references.</summary>
    /// <param name="project">The project to build.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public async Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var stopwatch = Stopwatch.StartNew();
        if (project.Language != ProjectLanguage.CSharp || _host.FindProject(project.FilePath) is null)
        {
            var diagnostic = new BuildDiagnostic(
                UnsupportedLanguageId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' cannot be built on this device: only C# projects are compiled by TabStudio.",
                null,
                project.Name);
            return new BuildResult(project.Name, [diagnostic], null, [], stopwatch.Elapsed);
        }

        var diagnostics = new List<BuildDiagnostic>();
        var dependencies = new List<CompiledAssembly>();
        CompiledAssembly? entry = null;

        foreach (var current in _host.GetBuildOrder(project.FilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            diagnostics.AddRange(current.PackageReferences.Select(package => new BuildDiagnostic(
                UnrestoredPackageId,
                DiagnosticSeverityLevel.Warning,
                $"NuGet package '{package.Name}' is not restored on this device, so code that uses it will not compile.",
                null,
                current.Name)));

            var isEntry = string.Equals(current.FilePath, project.FilePath, StringComparison.Ordinal);
            var result = await _host.EmitAsync(current.FilePath, checkInProcessSafety: isEntry && current.IsExecutable, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(result.Diagnostics);

            if (!result.Success)
            {
                return new BuildResult(project.Name, Sort(diagnostics), null, dependencies, stopwatch.Elapsed);
            }

            if (isEntry)
            {
                entry = result.Assembly;
            }
            else
            {
                dependencies.Add(result.Assembly!);
            }
        }

        return new BuildResult(project.Name, Sort(diagnostics), entry, dependencies, stopwatch.Elapsed);
    }

    private static List<BuildDiagnostic> Sort(List<BuildDiagnostic> diagnostics) =>
        diagnostics.OrderByDescending(d => d.Severity).ToList();
}
