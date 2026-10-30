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
/// The <see cref="ILanguageService"/> that runs on a TabStudio Link host: Roslyn, builds and IntelliSense happen on the
/// host, over its mirror of this workspace; the assemblies come back and run here.
/// </summary>
/// <remarks>
/// <para>
/// Edits are queued and sent in order before the next request, keeping only a file's latest text, so typing fast does
/// not flood the network and a request always sees the text it was asked about.
/// </para>
/// <para>
/// When the connection is lost, requests throw <see cref="LinkConnectionLostException"/>; <see cref="LinkedLanguageService"/>
/// then answers from this device instead. Package restores for running programs happen on this device, so
/// <see cref="GetPackageRuntimeAssemblies"/> is answered by <see cref="LinkedLanguageService"/> too.
/// </para>
/// </remarks>
public sealed class RemoteLanguageService : ILanguageService
{
    private readonly LinkLanguageClient _client;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly List<(string Path, string Text, bool IsNew)> _pending = [];
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
    private HashSet<string> _documents = new(StringComparer.Ordinal);
    private Workspace? _workspace;
    private bool _optimize;
    private IReadOnlyList<string> _activeProjects = [];

    /// <summary>Creates the service over a connection to a host.</summary>
    /// <param name="client">The language client of the connection.</param>
    /// <param name="hostName">The host's name, for messages.</param>
    public RemoteLanguageService(LinkLanguageClient client, string hostName)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        HostName = hostName;
        Features = new RemoteLanguageFeatures(this, client);
    }

    /// <summary>The host's name.</summary>
    public string HostName { get; }

    /// <summary>The language client this service talks through.</summary>
    public LinkLanguageClient Client => _client;

    /// <inheritdoc />
    public ILanguageFeatures Features { get; }

    /// <inheritdoc />
    public bool ContainsDocument(string filePath)
    {
        lock (_gate)
        {
            return _documents.Contains(filePath);
        }
    }

    /// <inheritdoc />
    /// <remarks>Sends the files the host lacks and waits until the host has loaded them. Call off the UI thread.</remarks>
    public void Reload(Workspace workspace, ISourceTextProvider sources)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(sources);
        bool optimize;
        IReadOnlyList<string> active;
        lock (_gate)
        {
            // The sources hold every text the queued edits would send.
            _pending.Clear();
            _texts.Clear();
            optimize = _optimize;
            active = _activeProjects;
        }

        var documents = Task.Run(() => _client.LoadAsync(workspace, sources, optimize, active, null, CancellationToken.None)).GetAwaiter().GetResult();
        lock (_gate)
        {
            _documents = new HashSet<string>(documents, StringComparer.Ordinal);
            _workspace = workspace;
        }
    }

    /// <inheritdoc />
    public void UpdateText(string filePath, string text)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            _texts[filePath] = text;
            Queue(filePath, text, isNew: false);
        }

        _ = SendQueuedSafelyAsync();
    }

    /// <inheritdoc />
    public bool AddDocument(string filePath, string text)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            if (!_documents.Contains(filePath) && !BelongsToProject(filePath))
            {
                return false;
            }

            _documents.Add(filePath);
            _texts[filePath] = text;
            Queue(filePath, text, isNew: true);
        }

        _ = SendQueuedSafelyAsync();
        return true;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken) =>
        AskAsync(c => c.GetDocumentDiagnosticsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken) =>
        AskAsync(c => c.GetProjectDiagnosticsAsync(projectFilePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken) =>
        AskAsync(c => c.GetClassificationsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken) =>
        AskAsync(c => c.GetFormattingEditsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return AskAsync(c => c.BuildAsync(project.FilePath, cancellationToken));
    }

    /// <inheritdoc />
    public Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return AskAsync(c => c.BuildForDebuggingAsync(project.FilePath, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The host compiles and instruments; the tests run on this device, so the instrumented code reports to the slots
    /// of this process's coverage recorder, which go with the request.
    /// </remarks>
    public Task<CoverageBuild> BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(testProject);
        ArgumentNullException.ThrowIfNull(uninstrumented);
        IReadOnlyList<ProjectInfo> projects;
        lock (_gate)
        {
            projects = _workspace?.Projects ?? [];
        }

        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in projects.Where(p => p.IsRoslynLanguage))
        {
            slots[project.FilePath] = CoverageBuilder.SlotOf(project.FilePath);
        }

        ProjectInfo? Find(string path) => projects.FirstOrDefault(p => string.Equals(p.FilePath, path, StringComparison.Ordinal));
        return AskAsync(c => c.BuildForCoverageAsync(testProject, uninstrumented, slots, Find, cancellationToken));
    }

    /// <inheritdoc />
    public void SetOptimization(bool optimize)
    {
        lock (_gate)
        {
            _optimize = optimize;
        }

        _ = SendSafelyAsync(() => _client.SetOptimizationAsync(optimize));
    }

    /// <inheritdoc />
    /// <remarks>The host compiles on as many cores as its own temperature allows.</remarks>
    public void SetConcurrentBuild(bool enabled)
    {
    }

    /// <inheritdoc />
    /// <remarks>Memory is the host's to manage; the host has no skeletons to make for this device.</remarks>
    public void UseSkeletonReferences(string cacheDirectory)
    {
    }

    /// <inheritdoc />
    public void SetActiveProjects(IEnumerable<string> projectFilePaths)
    {
        ArgumentNullException.ThrowIfNull(projectFilePaths);
        var projects = projectFilePaths.ToList();
        lock (_gate)
        {
            _activeProjects = projects;
        }

        _ = SendSafelyAsync(() => _client.SetActiveProjectsAsync(projects));
    }

    /// <inheritdoc />
    /// <remarks>The compilations are on the host; this device holds none.</remarks>
    public Task TrimMemoryAsync() => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Restores on the host, which compiles against the packages. Progress is not reported.</remarks>
    public async Task<RestoreResult> RestorePackagesAsync(ProjectInfo project, IProgress<RestoreProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var outcome = await AskAsync(c => c.RestorePackagesAsync(project.FilePath, cancellationToken)).ConfigureAwait(false);
        return new RestoreResult
        {
            ProjectName = project.Name,
            Diagnostics = outcome.Diagnostics,
            FromCache = outcome.FromCache,
            Duration = outcome.Duration,
        };
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken) =>
        AskAsync(c => c.AddFrameworkGeneratorsAsync(cancellationToken));

    /// <inheritdoc />
    /// <remarks>Programs run on this device, which restores their packages itself.</remarks>
    public IReadOnlyList<string> GetPackageRuntimeAssemblies(string projectFilePath) => [];

    /// <summary>Stops sending. The connection itself belongs to the app's link.</summary>
    public void Dispose() => _sending.Dispose();

    /// <summary>The errors of the workspace with changes not applied yet, such as the agent's, from the host.</summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public Task<IReadOnlyList<BuildDiagnostic>> GetSnapshotDiagnosticsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        AskAsync(c => c.GetSnapshotDiagnosticsAsync(changes, cancellationToken));

    /// <summary>The test projects of the workspace with changes not applied yet, built on the host.</summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the builds.</param>
    public Task<IReadOnlyList<LinkProjectBuild>> BuildSnapshotTestProjectsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        AskAsync(c => c.BuildSnapshotTestProjectsAsync(changes, cancellationToken));

    /// <summary>The latest text the editor sent for a file, or <see langword="null"/> when it sent none.</summary>
    /// <param name="filePath">The file.</param>
    internal string? LatestText(string filePath)
    {
        lock (_gate)
        {
            return _texts.GetValueOrDefault(filePath);
        }
    }

    /// <summary>Sends the queued edits, then asks.</summary>
    internal async Task<T> AskAsync<T>(Func<LinkLanguageClient, Task<T>> request)
    {
        await SendQueuedAsync().ConfigureAwait(false);
        return await request(_client).ConfigureAwait(false);
    }

    /// <summary>Queues an edit, replacing a queued edit of the same file. The caller holds the gate.</summary>
    private void Queue(string filePath, string text, bool isNew)
    {
        var index = _pending.FindIndex(p => string.Equals(p.Path, filePath, StringComparison.Ordinal));
        if (index >= 0)
        {
            _pending[index] = (filePath, text, isNew || _pending[index].IsNew);
        }
        else
        {
            _pending.Add((filePath, text, isNew));
        }
    }

    /// <summary>Sends every queued edit, in order, one sender at a time.</summary>
    private async Task SendQueuedAsync()
    {
        await _sending.WaitAsync().ConfigureAwait(false);
        try
        {
            while (true)
            {
                (string Path, string Text, bool IsNew)[] batch;
                lock (_gate)
                {
                    batch = [.. _pending];
                    _pending.Clear();
                }

                if (batch.Length == 0)
                {
                    return;
                }

                foreach (var (path, text, isNew) in batch)
                {
                    await (isNew ? _client.AddDocumentAsync(path, text) : _client.UpdateTextAsync(path, text)).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            try
            {
                _sending.Release();
            }
            catch (ObjectDisposedException)
            {
                // Disposed while sending: the workspace closed.
            }
        }
    }

    private async Task SendQueuedSafelyAsync()
    {
        try
        {
            await SendQueuedAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is LinkConnectionLostException or ObjectDisposedException)
        {
            // The next request fails too, and the IDE answers from this device; the edits are in its texts.
        }
    }

    private static async Task SendSafelyAsync(Func<Task> send)
    {
        try
        {
            await send().ConfigureAwait(false);
        }
        catch (LinkConnectionLostException)
        {
            // Settings are sent again with the next load.
        }
    }

    /// <summary>Whether a new file is in a C# project's folder, as the host's language service decides. The caller holds the gate.</summary>
    private bool BelongsToProject(string filePath) =>
        _workspace is { } workspace
        && workspace.Projects.Any(p => p.IsRoslynLanguage
            && p.Kind != ProjectKind.Unknown
            && filePath.StartsWith(p.DirectoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
