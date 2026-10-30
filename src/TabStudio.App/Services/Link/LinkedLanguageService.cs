using System.Collections.Concurrent;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Link;
using TabStudio.Link.Language;
using TabStudio.LiveTesting.Building;
using TabStudio.Packages;

namespace TabStudio.App.Services.Link;

/// <summary>
/// The workspace's language service when TabStudio Link is available: the linked host's while there is one, this
/// device's own Roslyn otherwise. It switches either way while the workspace is open, without losing an edit.
/// </summary>
/// <remarks>
/// <para>
/// It remembers what it needs to start over on either side: the workspace, the editors' unsaved text, the
/// configuration and the restored projects. When the host goes away, the request that noticed is answered here,
/// after this device loaded the workspace; when the host comes back, the workspace is sent to it again (only what
/// changed), the host restores the same packages, and this device's compilations are released.
/// </para>
/// <para>
/// Package restores always happen on this device too, because programs run here; the host restores for compiling.
/// </para>
/// </remarks>
public sealed class LinkedLanguageService : ILanguageService
{
    private readonly CompilerServices _compiler;
    private readonly LinkService _link;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _switching = new(1, 1);
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectInfo> _restored = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _runtimeAssemblies = new(StringComparer.Ordinal);
    private Workspace? _workspace;
    private ISourceTextProvider _sources = FileSystemTextProvider.Instance;
    private bool _optimize;
    private bool _concurrentBuild = true;
    private string? _skeletonDirectory;
    private IReadOnlyList<string> _activeProjects = [];
    private bool _frameworkGenerators;
    private LocalLanguageService? _local;
    private Workspace? _localWorkspace;
    private Task<LocalLanguageService>? _localLoading;
    private RemoteLanguageService? _remote;
    private bool _disposed;

    /// <summary>Creates the service. Call <see cref="Reload"/> to load a workspace.</summary>
    /// <param name="compiler">Makes this device's language service when it is needed.</param>
    /// <param name="link">The app's link, whose host answers while it is connected.</param>
    public LinkedLanguageService(CompilerServices compiler, LinkService link)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(link);
        _compiler = compiler;
        _link = link;
        Features = new LinkedLanguageFeatures(this);
        _link.LanguageClientChanged += OnLanguageClientChanged;
    }

    /// <summary>Raised on the thread pool when the service switched between the host and this device.</summary>
    public event EventHandler? ModeChanged;

    /// <summary>Whether the linked host answers.</summary>
    public bool IsLinked
    {
        get
        {
            lock (_gate)
            {
                return _remote is not null;
            }
        }
    }

    /// <summary>The name of the host that answers, while linked.</summary>
    public string? HostName
    {
        get
        {
            lock (_gate)
            {
                return _remote?.HostName;
            }
        }
    }

    /// <summary>This device's language service, while it answers or is kept; <see langword="null"/> while the host answers.</summary>
    public LocalLanguageService? Local
    {
        get
        {
            lock (_gate)
            {
                return _remote is null ? _local : null;
            }
        }
    }

    /// <inheritdoc />
    public ILanguageFeatures Features { get; }

    /// <inheritdoc />
    /// <remarks>Waits for the host or for this device's Roslyn to load the workspace. Call off the UI thread.</remarks>
    public void Reload(Workspace workspace, ISourceTextProvider sources)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(sources);
        RemoteLanguageService? remote;
        LocalLanguageService? local;
        lock (_gate)
        {
            _workspace = workspace;
            _sources = sources;
            _texts.Clear();
            remote = _remote;
            local = _local;
        }

        if (remote is not null)
        {
            if (TryReload(remote, workspace, sources))
            {
                return;
            }

            lock (_gate)
            {
                if (ReferenceEquals(_remote, remote))
                {
                    _remote = null;
                }
            }

            remote.Dispose();
            RaiseModeChanged();
        }
        else if (_link.LanguageClient is { } client && _link.ConnectedHost is { } host
            && TryLink(new RemoteLanguageService(client, host.Name), workspace, sources) is not null)
        {
            return;
        }

        if (local is null)
        {
            local = Task.Run(LocalAsync).GetAwaiter().GetResult();
        }

        lock (_gate)
        {
            if (ReferenceEquals(_localWorkspace, workspace))
            {
                return;
            }
        }

        local.Reload(workspace, sources);
        lock (_gate)
        {
            _localWorkspace = workspace;
        }
    }

    /// <inheritdoc />
    public bool ContainsDocument(string filePath) => Active()?.ContainsDocument(filePath) ?? false;

    /// <inheritdoc />
    public void UpdateText(string filePath, string text)
    {
        lock (_gate)
        {
            _texts[filePath] = text;
            ActiveUnderGate()?.UpdateText(filePath, text);
        }
    }

    /// <inheritdoc />
    public bool AddDocument(string filePath, string text)
    {
        lock (_gate)
        {
            _texts[filePath] = text;
            return ActiveUnderGate()?.AddDocument(filePath, text) ?? false;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken) =>
        CallAsync(s => s.GetDocumentDiagnosticsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken) =>
        CallAsync(s => s.GetProjectDiagnosticsAsync(projectFilePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken) =>
        CallAsync(s => s.GetClassificationsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken) =>
        CallAsync(s => s.GetFormattingEditsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    /// <remarks>When the host goes away during the build, the project is built again on this device.</remarks>
    public Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        CallAsync(s => s.BuildAsync(project, cancellationToken));

    /// <inheritdoc />
    public Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        CallAsync(s => s.BuildForDebuggingAsync(project, cancellationToken));

    /// <inheritdoc />
    /// <remarks>When the host goes away during the build, the test project is built again on this device.</remarks>
    public Task<CoverageBuild> BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, CancellationToken cancellationToken) =>
        CallAsync(s => s.BuildForCoverageAsync(testProject, uninstrumented, cancellationToken));

    /// <inheritdoc />
    public void SetOptimization(bool optimize)
    {
        lock (_gate)
        {
            _optimize = optimize;
            _local?.SetOptimization(optimize);
            _remote?.SetOptimization(optimize);
        }
    }

    /// <inheritdoc />
    public void SetConcurrentBuild(bool enabled)
    {
        lock (_gate)
        {
            _concurrentBuild = enabled;
            _local?.SetConcurrentBuild(enabled);
        }
    }

    /// <inheritdoc />
    public void UseSkeletonReferences(string cacheDirectory)
    {
        lock (_gate)
        {
            _skeletonDirectory = cacheDirectory;
            _local?.UseSkeletonReferences(cacheDirectory);
        }
    }

    /// <inheritdoc />
    public void SetActiveProjects(IEnumerable<string> projectFilePaths)
    {
        ArgumentNullException.ThrowIfNull(projectFilePaths);
        var projects = projectFilePaths.ToList();
        lock (_gate)
        {
            _activeProjects = projects;
            _local?.SetActiveProjects(projects);
            _remote?.SetActiveProjects(projects);
        }
    }

    /// <inheritdoc />
    public Task TrimMemoryAsync()
    {
        lock (_gate)
        {
            return _local?.TrimMemoryAsync() ?? Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    /// <remarks>Restores on this device, which runs the program, and on the host while linked, which compiles it.</remarks>
    public async Task<RestoreResult> RestorePackagesAsync(ProjectInfo project, IProgress<RestoreProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        LocalLanguageService? local;
        RemoteLanguageService? remote;
        lock (_gate)
        {
            _restored[project.FilePath] = project;
            local = _local;
            remote = _remote;
        }

        RestoreResult result;
        if (remote is null && local is not null)
        {
            result = await local.RestorePackagesAsync(project, progress, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result = await Task.Run(() => _compiler.Packages.RestoreAsync(project, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        // A restore with errors still gives the packages it restored, as LocalLanguageService does.
        _runtimeAssemblies[project.FilePath] = result.RuntimeFiles;

        if (remote is null)
        {
            return result;
        }

        try
        {
            var onHost = await remote.RestorePackagesAsync(project, null, cancellationToken).ConfigureAwait(false);
            var extra = onHost.Diagnostics
                .Where(d => !result.Diagnostics.Any(l => l.Id == d.Id && l.Message == d.Message))
                .Select(d => d with { Message = $"On {remote.HostName}: {d.Message}" });
            return result with { Diagnostics = [.. result.Diagnostics, .. extra] };
        }
        catch (Exception ex) when (IsLinkFailure(ex))
        {
            // This device's restore is what runs the program; the host restores again when it is back.
            await FallBackAsync(remote).ConfigureAwait(false);
            return result;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _frameworkGenerators = true;
        }

        return CallAsync(s => s.AddFrameworkGeneratorsAsync(cancellationToken));
    }

    /// <summary>The errors of the workspace with changes not applied yet, such as the agent's, from wherever the workspace is.</summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public Task<IReadOnlyList<BuildDiagnostic>> GetSnapshotDiagnosticsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        CallAsync(s => s switch
        {
            RemoteLanguageService remote => remote.GetSnapshotDiagnosticsAsync(changes, cancellationToken),
            LocalLanguageService local => Task.Run(() => local.Host.CreateSnapshot(changes).GetDiagnosticsAsync(cancellationToken), cancellationToken),
            _ => Task.FromResult<IReadOnlyList<BuildDiagnostic>>([]),
        });

    /// <summary>The test projects of the workspace with changes not applied yet, built wherever the workspace is.</summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the builds.</param>
    public Task<IReadOnlyList<LinkProjectBuild>> BuildSnapshotTestProjectsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        CallAsync(s => s switch
        {
            RemoteLanguageService remote => remote.BuildSnapshotTestProjectsAsync(changes, cancellationToken),
            LocalLanguageService local => Task.Run(() => SnapshotBuilds.BuildTestProjectsAsync(local.Host, changes, cancellationToken), cancellationToken),
            _ => Task.FromResult<IReadOnlyList<LinkProjectBuild>>([]),
        });

    /// <inheritdoc />
    public IReadOnlyList<string> GetPackageRuntimeAssemblies(string projectFilePath) =>
        _runtimeAssemblies.TryGetValue(projectFilePath, out var assemblies) ? assemblies : [];

    /// <inheritdoc />
    public void Dispose()
    {
        LocalLanguageService? local;
        RemoteLanguageService? remote;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            local = _local;
            remote = _remote;
            _local = null;
            _remote = null;
        }

        _link.LanguageClientChanged -= OnLanguageClientChanged;
        local?.Dispose();
        remote?.Dispose();
        _switching.Dispose();
    }

    /// <summary>Runs a request where the workspace is: on the host while linked, falling back to this device when the host is gone.</summary>
    internal async Task<T> CallAsync<T>(Func<ILanguageService, Task<T>> call)
    {
        RemoteLanguageService? remote;
        lock (_gate)
        {
            remote = _remote;
        }

        if (remote is not null)
        {
            try
            {
                return await call(remote).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsLinkFailure(ex))
            {
                await FallBackAsync(remote).ConfigureAwait(false);
            }
        }

        return await call(await LocalAsync().ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>The IntelliSense of the service that answers now, for questions too frequent to wait for.</summary>
    internal ILanguageFeatures? ActiveFeatures() => Active()?.Features;

    /// <summary>Whether a request failed because the link is gone, rather than because of what it asked.</summary>
    private static bool IsLinkFailure(Exception error) =>
        error is LinkConnectionLostException or LinkUnreachableException or LinkProtocolException
            or LinkRemoteException { Code: LinkErrorCodes.NoWorkspace };

    private ILanguageService? Active()
    {
        lock (_gate)
        {
            return ActiveUnderGate();
        }
    }

    /// <summary>The service that answers now. The caller holds the gate.</summary>
    private ILanguageService? ActiveUnderGate() => (ILanguageService?)_remote ?? _local;

    /// <summary>Loads the workspace on the host; on failure, nothing changes and this device answers.</summary>
    private RemoteLanguageService? TryLink(RemoteLanguageService remote, Workspace workspace, ISourceTextProvider sources)
    {
        lock (_gate)
        {
            remote.SetOptimization(_optimize);
            remote.SetActiveProjects(_activeProjects);
        }

        if (!TryReload(remote, workspace, sources) || !TryPrepare(remote, workspace))
        {
            remote.Dispose();
            return null;
        }

        LocalLanguageService? released;
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_workspace, workspace))
            {
                remote.Dispose();
                return null;
            }

            // Edits made while the host loaded: the host has the sources' text, not these.
            foreach (var (path, text) in _texts)
            {
                remote.UpdateText(path, text);
            }

            _remote = remote;
            released = _local;
            _local = null;
            _localWorkspace = null;
        }

        released?.Dispose();
        RaiseModeChanged();
        return remote;
    }

    /// <summary>
    /// Gives the host what this device set up after loading: the packages it restored, which the host compiles
    /// against, and .NET's source generators when they were added.
    /// </summary>
    private bool TryPrepare(RemoteLanguageService remote, Workspace workspace)
    {
        List<ProjectInfo> restored;
        bool generators;
        lock (_gate)
        {
            restored = [.. workspace.Projects.Where(p => _restored.ContainsKey(p.FilePath))];
            generators = _frameworkGenerators;
        }

        try
        {
            Task.Run(async () =>
            {
                foreach (var project in restored)
                {
                    await remote.RestorePackagesAsync(project, null, CancellationToken.None).ConfigureAwait(false);
                }

                if (generators)
                {
                    await remote.AddFrameworkGeneratorsAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (IsLinkFailure(ex) || ex is IOException)
        {
            return false;
        }
    }

    private static bool TryReload(RemoteLanguageService remote, Workspace workspace, ISourceTextProvider sources)
    {
        try
        {
            remote.Reload(workspace, sources);
            return true;
        }
        catch (Exception ex) when (IsLinkFailure(ex) || ex is IOException)
        {
            return false;
        }
    }

    /// <summary>Stops using a host that is gone; this device's Roslyn answers from now on.</summary>
    private async Task FallBackAsync(RemoteLanguageService remote)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_remote, remote))
            {
                return;
            }

            _remote = null;
        }

        remote.Dispose();
        RaiseModeChanged();
        await LocalAsync().ConfigureAwait(false);
    }

    /// <summary>This device's language service, made and loaded with the workspace and its unsaved text when first needed.</summary>
    private Task<LocalLanguageService> LocalAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_local is { } local)
            {
                return Task.FromResult(local);
            }

            return _localLoading ??= Task.Run(CreateLocalAsync);
        }
    }

    private async Task<LocalLanguageService> CreateLocalAsync()
    {
        var local = new LocalLanguageService(await _compiler.CreateHostAsync().ConfigureAwait(false), _compiler.Packages);
        Workspace? workspace;
        OverlayTextProvider sources;
        List<ProjectInfo> restored;
        bool generators;
        lock (_gate)
        {
            if (_skeletonDirectory is { } skeletons)
            {
                local.UseSkeletonReferences(skeletons);
            }

            local.SetOptimization(_optimize);
            local.SetConcurrentBuild(_concurrentBuild);
            workspace = _workspace;
            sources = new OverlayTextProvider(new Dictionary<string, string>(_texts, StringComparer.Ordinal), _sources);
            restored = [.. _restored.Values];
            generators = _frameworkGenerators;
        }

        if (workspace is not null)
        {
            local.Reload(workspace, sources);
        }

        foreach (var project in restored)
        {
            // From the restore cache, so this is quick; it gives IntelliSense the packages here too.
            var result = await local.RestorePackagesAsync(project, null, CancellationToken.None).ConfigureAwait(false);
            _runtimeAssemblies[project.FilePath] = result.RuntimeFiles;
        }

        if (generators)
        {
            await local.AddFrameworkGeneratorsAsync(CancellationToken.None).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _localLoading = null;
            if (_disposed)
            {
                local.Dispose();
                throw new ObjectDisposedException(nameof(LinkedLanguageService));
            }

            // Edits made while this device loaded the workspace.
            foreach (var (path, text) in _texts)
            {
                if (!sources.Has(path, text))
                {
                    local.UpdateText(path, text);
                }
            }

            local.SetActiveProjects(_activeProjects);
            _local = local;
            _localWorkspace = workspace;
        }

        return local;
    }

    private void OnLanguageClientChanged(object? sender, EventArgs e) => _ = SwitchAsync();

    /// <summary>Follows the link: to the host when it connected, back to this device when it is gone.</summary>
    private async Task SwitchAsync()
    {
        try
        {
            await _switching.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            RemoteLanguageService? remote;
            Workspace? workspace;
            OverlayTextProvider sources;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                remote = _remote;
                workspace = _workspace;
                sources = new OverlayTextProvider(new Dictionary<string, string>(_texts, StringComparer.Ordinal), _sources);
            }

            var client = _link.LanguageClient;
            if (remote is not null && !ReferenceEquals(remote.Client, client))
            {
                await FallBackAsync(remote).ConfigureAwait(false);
                remote = null;
            }

            if (remote is null && client is not null && workspace is not null && _link.ConnectedHost is { } host)
            {
                await Task.Run(() => TryLink(new RemoteLanguageService(client, host.Name), workspace, sources)).ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
            // The workspace closed while switching.
        }
        finally
        {
            try
            {
                _switching.Release();
            }
            catch (ObjectDisposedException)
            {
                // Disposed meanwhile.
            }
        }
    }

    private void RaiseModeChanged()
    {
        var handler = ModeChanged;
        if (handler is not null)
        {
            _ = Task.Run(() => handler(this, EventArgs.Empty));
        }
    }

    /// <summary>The editors' text where it differs from the sources the workspace was loaded with.</summary>
    private sealed class OverlayTextProvider(Dictionary<string, string> texts, ISourceTextProvider sources) : ISourceTextProvider
    {
        public string GetText(string filePath) => texts.TryGetValue(filePath, out var text) ? text : sources.GetText(filePath);

        public bool Has(string filePath, string text) => texts.TryGetValue(filePath, out var known) && string.Equals(known, text, StringComparison.Ordinal);
    }
}
