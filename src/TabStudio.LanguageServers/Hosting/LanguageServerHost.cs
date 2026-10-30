using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// The language servers of one workspace: one per language server and folder, started the first time a file it
/// serves opens, and shut down with the workspace.
/// </summary>
/// <remarks>
/// <para>
/// A file goes to the first enabled server of its language whose program is installed. Its folder is the nearest
/// one above it, inside the workspace, with one of the server's root markers (such as <c>pyproject.toml</c> or
/// <c>package.json</c>), or else the workspace's root, so a repository with several projects gets a server per
/// project, as VS Code's multi-root workspaces do.
/// </para>
/// <para>Events are raised on background threads.</para>
/// </remarks>
public sealed class LanguageServerHost : IAsyncDisposable, ISnapshotSource
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, LanguageServerInstance> _servers = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Creates the host for a workspace. No server starts until a document opens.</summary>
    /// <param name="configuration">The workspace's configuration.</param>
    /// <param name="launcher">Starts the servers' programs.</param>
    /// <param name="options">How to introduce TabStudio and look after the servers; the defaults when <see langword="null"/>.</param>
    public LanguageServerHost(WorkspaceConfiguration configuration, IServerProcessLauncher launcher, LanguageServerHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(launcher);
        Configuration = configuration;
        Launcher = launcher;
        Options = options ?? new LanguageServerHostOptions();
    }

    /// <summary>Raised with a line for the language servers' log.</summary>
    public event EventHandler<LanguageServerLogEventArgs>? LogReceived;

    /// <summary>Raised when a server starts, runs, restarts, fails or stops.</summary>
    public event EventHandler? ServersChanged;

    /// <summary>Raised when a server publishes diagnostics for a file, open or not.</summary>
    public event EventHandler<LanguageServerDiagnosticsEventArgs>? DiagnosticsPublished;

    /// <summary>Raised when a server shows a message to the user.</summary>
    public event EventHandler<LanguageServerMessageEventArgs>? MessageShown;

    /// <summary>Raised when a server reports progress, such as while indexing.</summary>
    public event EventHandler<LanguageServerProgressEventArgs>? ProgressChanged;

    /// <summary>The workspace's configuration.</summary>
    public WorkspaceConfiguration Configuration { get; private set; }

    /// <summary>Starts the servers' programs.</summary>
    public IServerProcessLauncher Launcher { get; }

    /// <summary>How the host introduces TabStudio and looks after the servers.</summary>
    public LanguageServerHostOptions Options { get; }

    /// <summary>
    /// Answers a server's question (<c>window/showMessageRequest</c>) with the title of the chosen action, or
    /// <see langword="null"/> when the user dismissed it. Without a handler, questions are left unanswered.
    /// </summary>
    public Func<LanguageServerMessageRequest, CancellationToken, Task<string?>>? MessageRequestHandler { get; set; }

    /// <summary>
    /// Applies an edit a server asks for (<c>workspace/applyEdit</c>), with its label, and tells whether it was applied.
    /// Without a handler, such edits are refused.
    /// </summary>
    public Func<LspWorkspaceEdit, string?, CancellationToken, Task<bool>>? ApplyEditHandler { get; set; }

    /// <summary>
    /// Opens a file a server asks to show (<c>window/showDocument</c>), with a range to select, and tells whether it
    /// was shown. Without a handler, such requests fail.
    /// </summary>
    public Func<string, TextSpan?, CancellationToken, Task<bool>>? ShowDocumentHandler { get; set; }

    /// <summary>The servers that were started, for the status bar.</summary>
    public IReadOnlyList<LanguageServerStatus> Servers
    {
        get
        {
            lock (_gate)
            {
                return _servers.Values.Select(s => s.Status).ToList();
            }
        }
    }

    /// <summary>
    /// Which server would serve a file, and where its program is: the first enabled server of the file's language
    /// whose program is installed.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <returns>The language, server and program, or <see langword="null"/> when no installed server serves the file.</returns>
    public LanguageServerMatch? FindServer(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (Configuration.Registry.FindLanguage(filePath) is not { } language)
        {
            return null;
        }

        foreach (var server in Configuration.Registry.FindServers(language.Id))
        {
            foreach (var command in server.Commands)
            {
                if (Launcher.ResolveForDirectory(command, Path.GetDirectoryName(Path.GetFullPath(filePath))!) is { } executable)
                {
                    return new LanguageServerMatch(language, server, executable);
                }
            }
        }

        return null;
    }

    /// <summary>The servers of a file's language that are not installed, with the packages that install them.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public IReadOnlyList<LanguageServerDefinition> FindMissingServers(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return Configuration.Registry.FindLanguage(filePath) is { } language
            ? Configuration.Registry.FindServers(language.Id).ToList()
            : [];
    }

    /// <summary>
    /// Opens a document on the server that serves it, starting the server if it is not running yet.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="text">The editor's text.</param>
    /// <returns>The document, or <see langword="null"/> when no installed server serves the file.</returns>
    public LanguageServerDocument? OpenDocument(string filePath, string text)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);
        if (FindServer(filePath) is not { } match)
        {
            return null;
        }

        var root = FindRoot(Path.GetFullPath(filePath), match.Server.RootMarkers);
        var key = match.Server.Id + "|" + root;
        LanguageServerInstance instance;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_servers.TryGetValue(key, out var existing))
            {
                existing = new LanguageServerInstance(this, match.Server, root, match.Executable, Configuration.Registry.SettingsFor(match.Server));
                _servers[key] = existing;
            }

            instance = existing;
        }

        var document = new LanguageServerDocument(instance, Path.GetFullPath(filePath), match.Language, text);
        instance.AddDocument(document);
        OnServerStateChanged();
        return document;
    }

    /// <summary>Searches the symbols of every running server that can, best matches first as each server orders them.</summary>
    /// <param name="query">What the user typed.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public async Task<IReadOnlyList<LspWorkspaceSymbol>> FindWorkspaceSymbolsAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        List<LanguageServerInstance> servers;
        lock (_gate)
        {
            servers = [.. _servers.Values.Where(s => s.State == LanguageServerState.Running && s.Capabilities.WorkspaceSymbol)];
        }

        var results = await Task.WhenAll(servers.Select(s => WorkspaceSymbols.FindAsync(s, query, cancellationToken))).ConfigureAwait(false);
        return [.. results.SelectMany(r => r)];
    }

    /// <summary>Changes the workspace's configuration and sends the new settings to every running server.</summary>
    /// <param name="configuration">The configuration, read again after <c>.tabstudio/settings.json</c> changed.</param>
    /// <remarks>A server whose definition changed, such as a new command, uses it the next time it starts.</remarks>
    public async Task UpdateConfigurationAsync(WorkspaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Configuration = configuration;
        List<LanguageServerInstance> servers;
        lock (_gate)
        {
            servers = [.. _servers.Values];
        }

        foreach (var server in servers)
        {
            var definition = configuration.Registry.Servers.FirstOrDefault(s => s.Id == server.Definition.Id) ?? server.Definition;
            await server.UpdateSettingsAsync(configuration.Registry.SettingsFor(definition)).ConfigureAwait(false);
        }
    }

    /// <summary>Tells every running server that files were created, changed or deleted.</summary>
    /// <param name="changes">Each file's full path and what happened to it.</param>
    public async Task NotifyFilesChangedAsync(IReadOnlyList<(string FilePath, FileChangeKind Kind)> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        List<LanguageServerInstance> servers;
        lock (_gate)
        {
            servers = [.. _servers.Values];
        }

        foreach (var server in servers)
        {
            await server.NotifyFilesChangedAsync(changes).ConfigureAwait(false);
        }
    }

    /// <summary>Shuts every server down.</summary>
    public async ValueTask DisposeAsync()
    {
        List<LanguageServerInstance> servers;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            servers = [.. _servers.Values];
        }

        await Task.WhenAll(servers.Select(s => s.DisposeAsync().AsTask())).ConfigureAwait(false);
    }

    /// <inheritdoc />
    TextSnapshot? ISnapshotSource.GetSnapshot(string filePath) => GetSnapshot(filePath);

    /// <summary>The current text of a file: its open document's, or the file's.</summary>
    internal TextSnapshot? GetSnapshot(string filePath)
    {
        List<LanguageServerInstance> servers;
        lock (_gate)
        {
            servers = [.. _servers.Values];
        }

        foreach (var server in servers)
        {
            if (server.FindDocument(filePath) is { } document)
            {
                return document.Snapshot;
            }
        }

        return Options.ReadText(filePath) is { } text ? new TextSnapshot(text, 0) : null;
    }

    /// <summary>Writes a line to the log.</summary>
    internal void Log(string? serverName, LanguageServerLogLevel level, string message) =>
        LogReceived?.Invoke(this, new LanguageServerLogEventArgs(serverName, level, message));

    internal void OnServerStateChanged() => ServersChanged?.Invoke(this, EventArgs.Empty);

    internal void OnDiagnosticsPublished(LanguageServerDiagnosticsEventArgs e) => DiagnosticsPublished?.Invoke(this, e);

    internal void OnMessageShown(LanguageServerMessageEventArgs e) => MessageShown?.Invoke(this, e);

    internal void OnProgress(LanguageServerProgressEventArgs e) => ProgressChanged?.Invoke(this, e);

    internal async Task<string?> RequestMessageAsync(LanguageServerMessageRequest request, CancellationToken cancellationToken) =>
        MessageRequestHandler is { } handler ? await handler(request, cancellationToken).ConfigureAwait(false) : null;

    internal async Task<bool> ApplyEditAsync(LspWorkspaceEdit edit, string? label, CancellationToken cancellationToken) =>
        ApplyEditHandler is { } handler && await handler(edit, label, cancellationToken).ConfigureAwait(false);

    internal async Task<bool> ShowDocumentAsync(string filePath, TextSpan? selection, CancellationToken cancellationToken) =>
        ShowDocumentHandler is { } handler && await handler(filePath, selection, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// The folder a file's server serves: the nearest folder above the file, inside the workspace, with a root
    /// marker, or else the workspace's root; a file outside the workspace is served from its own folder.
    /// </summary>
    private string FindRoot(string filePath, IReadOnlyList<string> markers)
    {
        var workspace = Path.GetFullPath(Configuration.RootDirectory);
        var folder = Path.GetDirectoryName(filePath);
        var inside = folder is not null && IsInside(folder, workspace);
        while (folder is not null && markers.Count > 0 && (!inside || IsInside(folder, workspace)))
        {
            if (markers.Any(m => File.Exists(Path.Combine(folder, m)) || Directory.Exists(Path.Combine(folder, m))))
            {
                return folder;
            }

            if (DocumentUri.PathComparer.Equals(folder.TrimEnd(Path.DirectorySeparatorChar), workspace.TrimEnd(Path.DirectorySeparatorChar)))
            {
                break;
            }

            folder = Path.GetDirectoryName(folder);
        }

        return inside ? workspace : Path.GetDirectoryName(filePath) ?? workspace;
    }

    private static bool IsInside(string folder, string root)
    {
        var relative = Path.GetRelativePath(root, folder);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }
}

/// <summary>The server that serves a file, and where its program is installed.</summary>
/// <param name="Language">The file's language.</param>
/// <param name="Server">The server.</param>
/// <param name="Executable">The full path of its program.</param>
public sealed record LanguageServerMatch(LanguageDefinition Language, LanguageServerDefinition Server, string Executable);
