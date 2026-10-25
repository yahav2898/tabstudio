using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services;

/// <summary>The <see cref="ILanguageService"/> that runs Roslyn inside TabStudio's own process.</summary>
public sealed class LocalLanguageService : ILanguageService
{
    private readonly RoslynWorkspaceHost _host;
    private readonly ProjectBuilder _builder;

    /// <summary>Creates the service over a host that has a workspace loaded.</summary>
    /// <param name="host">The language service host. Owned by this object from now on.</param>
    public LocalLanguageService(RoslynWorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _builder = new ProjectBuilder(host);
    }

    /// <inheritdoc />
    public void Reload(Workspace workspace, ISourceTextProvider sources) => _host.Load(workspace, sources);

    /// <inheritdoc />
    public bool ContainsDocument(string filePath) => _host.ContainsDocument(filePath);

    /// <inheritdoc />
    public void UpdateText(string filePath, string text) => _host.UpdateText(filePath, text);

    /// <inheritdoc />
    public bool AddDocument(string filePath, string text) => _host.AddDocument(filePath, text);

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() => _host.GetDocumentDiagnosticsAsync(filePath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken) =>
        Task.Run(() => _host.GetProjectDiagnosticsAsync(projectFilePath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() => _host.GetClassificationsAsync(filePath, 0, int.MaxValue, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() => _host.GetFormattingEditsAsync(filePath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        Task.Run(() => _builder.BuildAsync(project, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public void SetOptimization(bool optimize) => _host.SetOptimization(optimize);

    /// <inheritdoc />
    public void SetConcurrentBuild(bool enabled) => _host.SetConcurrentBuild(enabled);

    /// <inheritdoc />
    public void TrimMemory() => _host.TrimMemory();

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
