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
        return _projectIds.TryGetValue(projectFilePath, out var id)
            ? BuildOrder.For(_projects[id], _projects.Values.ToDictionary(p => p.FilePath, StringComparer.Ordinal))
            : [];
    }

    /// <inheritdoc />
    public bool ArePackagesRestored(string projectFilePath) => _host.ArePackagesRestored(projectFilePath);

    /// <inheritdoc />
    public bool IsPackageRestored(string projectFilePath, string packageId) => _host.IsPackageRestored(projectFilePath, packageId);

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

        // As in RoslynWorkspaceHost: a program about to run gets its Environment.Exit calls redirected.
        if (checkInProcessSafety)
        {
            compilation = InProcessExit.Redirect(compilation, cancellationToken);
        }

        return await Razor.RazorCompilations.EmitAsync(project, _projects[id], compilation, checkInProcessSafety, stopwatch, cancellationToken).ConfigureAwait(false);
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

            diagnostics.AddRange(await Razor.RazorCompilations.MapAsync(project, info, compilation.GetDiagnostics(cancellationToken), cancellationToken).ConfigureAwait(false));
            if (info.IsExecutable)
            {
                diagnostics.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, info.Name, redirected: true, cancellationToken));
            }
        }

        return [.. diagnostics.OrderByDescending(d => d.Severity)];
    }
}
