using Microsoft.CodeAnalysis;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation;

public sealed partial class RoslynWorkspaceHost
{
    /// <summary>
    /// Returns the current Roslyn compilation of a loaded project without emitting it, for tools that transform a
    /// project before emitting it. The debugger uses it to instrument the project's syntax trees.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <param name="cancellationToken">Cancels compilation.</param>
    /// <returns>The compilation, or <see langword="null"/> when the project is not loaded.</returns>
    public async Task<RoslynCompilation?> GetCompilationAsync(string projectFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);

        Project? project;
        lock (_gate)
        {
            project = _projectIds.TryGetValue(projectFilePath, out var id) ? _workspace.CurrentSolution.GetProject(id) : null;
        }

        return project is null ? null : await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
    }
}
