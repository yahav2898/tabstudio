using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using System.Collections.Concurrent;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.LiveTesting.Building;
using TabStudio.Packages;
using TabStudio.Wpf;

namespace TabStudio.App.Services;

/// <summary>The <see cref="ILanguageService"/> that runs Roslyn inside TabStudio's own process.</summary>
public sealed class LocalLanguageService : ILanguageService
{
    private readonly RoslynWorkspaceHost _host;
    private readonly ProjectBuilder _builder;
    private readonly PackageRestorer? _restorer;
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _runtimeAssemblies = new(StringComparer.Ordinal);
    private readonly Lazy<CoverageBuilder> _coverage;

    /// <summary>Creates the service over a host that has a workspace loaded.</summary>
    /// <param name="host">The language service host. Owned by this object from now on.</param>
    /// <param name="restorer">Restores NuGet packages, or <see langword="null"/> when packages are not available.</param>
    public LocalLanguageService(RoslynWorkspaceHost host, PackageRestorer? restorer = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _builder = new ProjectBuilder(host);
        _restorer = restorer;
        _coverage = new Lazy<CoverageBuilder>(() => new CoverageBuilder(host));
        Features = new LocalLanguageFeatures(host);
    }

    /// <summary>The Roslyn host, for features that run in this process, such as IntelliSense and the debugger.</summary>
    public RoslynWorkspaceHost Host => _host;

    /// <inheritdoc />
    public ILanguageFeatures Features { get; }

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
    public Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        Task.Run(() => new DebugBuilder(_host).BuildAsync(project, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<CoverageBuild> BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, CancellationToken cancellationToken) =>
        Task.Run(() => _coverage.Value.BuildAsync(testProject, uninstrumented, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public void SetOptimization(bool optimize) => _host.SetOptimization(optimize);

    /// <inheritdoc />
    public void SetConcurrentBuild(bool enabled) => _host.SetConcurrentBuild(enabled);

    /// <inheritdoc />
    public void TrimMemory()
    {
        _host.TrimMemory();
        if (_coverage.IsValueCreated)
        {
            _coverage.Value.Clear();
        }
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestorePackagesAsync(ProjectInfo project, IProgress<RestoreProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (_restorer is null)
        {
            throw new InvalidOperationException("This language service was created without a package restorer.");
        }

        // A WPF project compiles against OpenSilver, which TabStudio restores in place of the Windows Desktop pack.
        var restored = WpfProject.UsesWpf(project) ? OpenSilverPackages.WithOpenSilver(project) : project;
        var result = await Task.Run(() => _restorer.RestoreAsync(restored, progress, cancellationToken), cancellationToken).ConfigureAwait(false);

        // As in NuGet and Visual Studio, a restore with errors keeps what it restored: one package that is missing or
        // conflicts must not take the others away, or every use of them becomes an error. The restore's errors stay
        // in its diagnostics, and builds report TS2003 only for the references that really are missing.
        _host.SetPackageReferences(project.FilePath, result.CompileAssemblies, result.UnrestoredReferences);
        _host.SetAnalyzers(project.FilePath, result.Analyzers);
        _host.SetPackageBuildFiles(project.FilePath, result.BuildFiles);
        _host.SetPackageContentSources(project.FilePath, result.ContentSources);
        _runtimeAssemblies[project.FilePath] = result.RuntimeFiles;
        return result;
    }

    /// <inheritdoc />
    public void UseSkeletonReferences(string cacheDirectory) => _host.UseSkeletonReferences(cacheDirectory);

    /// <inheritdoc />
    public void SetActiveProjects(IEnumerable<string> projectFilePaths) => _host.SetActiveProjects(projectFilePaths);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken)
    {
        if (_restorer is null)
        {
            return [];
        }

        var (analyzers, diagnostics) = await Task.Run(() => _restorer.RestoreFrameworkAnalyzersAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (analyzers.Count > 0)
        {
            _host.SetFrameworkAnalyzers(analyzers);
        }

        return diagnostics;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPackageRuntimeAssemblies(string projectFilePath) =>
        _runtimeAssemblies.TryGetValue(projectFilePath, out var assemblies) ? assemblies : [];

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
