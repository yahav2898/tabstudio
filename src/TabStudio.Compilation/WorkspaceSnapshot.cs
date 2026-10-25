using System.Diagnostics;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// The workspace as it would be with unsaved changes, from <see cref="RoslynWorkspaceHost.CreateSnapshot"/>. The
/// agent uses it to compile and test its proposals before the user approves them; the live workspace is not
/// touched. Build it with <see cref="ProjectBuilder"/>.
/// </summary>
public sealed class WorkspaceSnapshot : IBuildSource
{
    private readonly RoslynWorkspaceHost _host;
    private readonly Solution _solution;
    private readonly IReadOnlyDictionary<string, ProjectId> _projectIds;
    private readonly IReadOnlyDictionary<ProjectId, TsProject> _projects;

    internal WorkspaceSnapshot(RoslynWorkspaceHost host, Solution solution, IReadOnlyDictionary<string, ProjectId> projectIds, IReadOnlyDictionary<ProjectId, TsProject> projects)
    {
        _host = host;
        _solution = solution;
        _projectIds = projectIds;
        _projects = projects;
    }

    /// <summary>The C# projects of the snapshot.</summary>
    public IReadOnlyCollection<TsProject> Projects => [.. _projects.Values];

    /// <inheritdoc />
    public TsProject? FindProject(string projectFilePath) =>
        _projectIds.TryGetValue(projectFilePath, out var id) ? _projects[id] : null;

    /// <inheritdoc />
    public IReadOnlyList<TsProject> GetBuildOrder(string projectFilePath)
    {
        if (!_projectIds.TryGetValue(projectFilePath, out var id))
        {
            return [];
        }

        var graph = _solution.GetProjectDependencyGraph();
        var needed = graph.GetProjectsThatThisProjectTransitivelyDependsOn(id).Append(id).ToHashSet();
        return [.. graph.GetTopologicallySortedProjects().Where(needed.Contains).Select(p => _projects[p])];
    }

    /// <inheritdoc />
    public bool ArePackagesRestored(string projectFilePath) => _host.ArePackagesRestored(projectFilePath);

    /// <inheritdoc />
    public async Task<CompilationResult> EmitAsync(string projectFilePath, bool checkInProcessSafety, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!_projectIds.TryGetValue(projectFilePath, out var id) || _solution.GetProject(id) is not { } project)
        {
            throw new ArgumentException($"The project '{projectFilePath}' is not loaded.", nameof(projectFilePath));
        }

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Roslyn could not create a compilation for '{_projects[id].Name}'.");
        return CSharpCompiler.Emit(compilation, checkInProcessSafety, stopwatch, _projects[id].Name, cancellationToken);
    }

    /// <summary>The errors and warnings of every C# project, as the Error List would show them after a build.</summary>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public async Task<IReadOnlyList<BuildDiagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<BuildDiagnostic>();
        foreach (var (id, info) in _projects)
        {
            if (_solution.GetProject(id) is not { } project || await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
            {
                continue;
            }

            diagnostics.AddRange(DiagnosticMapper.Map(compilation.GetDiagnostics(cancellationToken), info.Name));
            if (info.IsExecutable)
            {
                diagnostics.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, info.Name, cancellationToken));
            }
        }

        return [.. diagnostics.OrderByDescending(d => d.Severity)];
    }
}
