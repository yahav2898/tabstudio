using TabStudio.Core.Workspaces;

namespace TabStudio.Compilation.Building;

/// <summary>
/// Where <see cref="ProjectBuilder"/> gets compilations from: the live workspace (<see cref="RoslynWorkspaceHost"/>),
/// or a <see cref="WorkspaceSnapshot"/> with changes that are not saved yet.
/// </summary>
public interface IBuildSource
{
    /// <summary>The loaded project with this file, if any.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    ProjectInfo? FindProject(string projectFilePath);

    /// <summary>The projects that must be compiled to run a project, dependencies first and the project itself last.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    IReadOnlyList<ProjectInfo> GetBuildOrder(string projectFilePath);

    /// <summary>Whether a project's NuGet packages are restored and referenced.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    bool ArePackagesRestored(string projectFilePath);

    /// <summary>
    /// Whether one of a project's package references is restored and referenced: the project's packages were
    /// restored, and the restore did not report that package as one it could not restore.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <param name="packageId">The id of the package, as the project references it.</param>
    bool IsPackageRestored(string projectFilePath, string packageId);

    /// <summary>Compiles one project into memory.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <param name="checkInProcessSafety">Whether to reject calls that would close TabStudio.</param>
    /// <param name="cancellationToken">Cancels compilation.</param>
    Task<CompilationResult> EmitAsync(string projectFilePath, bool checkInProcessSafety, CancellationToken cancellationToken = default);
}
