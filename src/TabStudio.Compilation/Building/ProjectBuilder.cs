using System.Diagnostics;
using TabStudio.Core.Compatibility;
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
/// editor already keeps warm for live diagnostics. A build after a small edit only recompiles what changed. A
/// <see cref="WorkspaceSnapshot"/> builds the workspace as it would be with changes that are not saved yet.
/// </remarks>
public sealed class ProjectBuilder
{
    /// <summary>Reported for a project in a language TabStudio cannot compile.</summary>
    public const string UnsupportedLanguageId = "TS2002";

    /// <summary>
    /// Reported for each package reference of a project that is not restored: the project's packages have not been
    /// restored, that is, <see cref="RoslynWorkspaceHost.SetPackageReferences"/> was never called for it, or its
    /// restore could not restore that package.
    /// </summary>
    public const string UnrestoredPackageId = "TS2003";

    /// <summary>
    /// Reported for a .NET Framework program that needs Windows (see <see cref="NetFrameworkCompatibility"/>), which is
    /// built on a PC through the Windows Bridge rather than compiled here against .NET.
    /// </summary>
    public const string NeedsWindowsId = "TS2004";

    /// <summary>
    /// Reported for a project that needs a platform workload TabStudio does not have, such as a .NET MAUI app or the
    /// Android head of a cross-platform app (see <see cref="ProjectClassifier.RequiredWorkload"/>), which is built on a
    /// computer rather than compiled here.
    /// </summary>
    public const string NeedsPlatformWorkloadId = "TS2005";

    /// <summary>
    /// Reported, as a warning, for a project that references a project of the workspace as a source generator
    /// (<c>OutputItemType="Analyzer"</c>) when that project does not compile, cannot be loaded or references the project
    /// back: the code its generators would add is missing, and the errors that follow say where.
    /// </summary>
    public const string GeneratorProjectFailedId = "TS2006";

    private readonly IBuildSource _host;

    /// <summary>Creates a builder over a loaded workspace host or a snapshot of it.</summary>
    /// <param name="host">Where the projects and their compilations come from.</param>
    public ProjectBuilder(IBuildSource host)
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
        if (project.Kind == ProjectKind.AspireAppHost)
        {
            var appHost = new BuildDiagnostic(
                UnsupportedLanguageId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' is an Aspire AppHost, which runs on a computer: Aspire's developer control plane starts the app's projects and containers there. Run the projects it starts on their own.",
                null,
                project.Name);
            return new BuildResult(project.Name, [appHost], null, [], stopwatch.Elapsed);
        }

        // Before the .NET Framework check: a Xamarin project is in the same legacy format, but Upgrade project would not help.
        if (ProjectClassifier.PlatformWorkloadReason(project) is { } workload)
        {
            var needsWorkload = new BuildDiagnostic(
                NeedsPlatformWorkloadId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' cannot be built on this device: it {workload}. TabStudio leaves it out of builds.",
                null,
                project.Name);
            return new BuildResult(project.Name, [needsWorkload], null, [], stopwatch.Elapsed);
        }

        if (NetFrameworkCompatibility.NeedsWindows(project))
        {
            var needsWindows = new BuildDiagnostic(
                NeedsWindowsId,
                DiagnosticSeverityLevel.Error,
                NetFrameworkCompatibility.NeedsWindowsMessage(project) + " It builds and runs on your PC through the Windows Bridge, or Upgrade project makes it an SDK-style .NET project.",
                null,
                project.Name);
            return new BuildResult(project.Name, [needsWindows], null, [], stopwatch.Elapsed);
        }

        if (!project.IsRoslynLanguage || _host.FindProject(project.FilePath) is null)
        {
            var diagnostic = new BuildDiagnostic(
                UnsupportedLanguageId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' cannot be built on this device: only C# and Visual Basic projects are compiled by TabStudio.",
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

            // Only the packages that really are missing: a restore with errors still referenced the others.
            diagnostics.AddRange(current.PackageReferences
                .Where(package => !_host.IsPackageRestored(current.FilePath, package.Name))
                .Select(package => new BuildDiagnostic(
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
