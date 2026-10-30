using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// One language server for one folder: starts its program, initializes it, opens the documents it serves, answers
/// what it asks, and starts it again, a limited number of times, when it stops unexpectedly. The server-to-client
/// requests are in <c>LanguageServerInstance.ServerRequests.cs</c>.
/// </summary>
internal sealed partial class LanguageServerInstance : IAsyncDisposable
{
    private readonly LanguageServerHost _host;
    private readonly string _executable;
    private readonly Lock _gate = new();
    private readonly List<LanguageServerDocument> _documents = [];
    private readonly List<DateTimeOffset> _crashes = [];
    private readonly CancellationTokenSource _life = new();
    private TaskCompletionSource<bool> _ready = NewReadiness();
    private IServerProcess? _process;
    private JsonRpcConnection? _connection;
    private Task _run = Task.CompletedTask;
    private int _started;
    private volatile bool _stopping;
    private volatile LanguageServerCapabilities _capabilities = LanguageServerCapabilities.None;

    /// <summary>Creates the instance; nothing starts until <see cref="Start"/> or a document is added.</summary>
    /// <param name="host">The host that owns it.</param>
    /// <param name="definition">The server.</param>
    /// <param name="rootDirectory">The folder it serves.</param>
    /// <param name="executable">The program's full path.</param>
    /// <param name="settings">The settings it reads with <c>workspace/configuration</c>.</param>
    public LanguageServerInstance(LanguageServerHost host, LanguageServerDefinition definition, string rootDirectory, string executable, JsonObject settings)
    {
        _host = host;
        Definition = definition;
        RootDirectory = rootDirectory;
        RootUri = DocumentUri.FromPath(rootDirectory);
        _executable = executable;
        Settings = settings;
    }

    /// <summary>The server's definition.</summary>
    public LanguageServerDefinition Definition { get; }

    /// <summary>The server's name.</summary>
    public string Name => Definition.Name;

    /// <summary>The folder it serves.</summary>
    public string RootDirectory { get; }

    /// <summary>The URI of the folder it serves.</summary>
    public string RootUri { get; }

    /// <summary>The settings it reads.</summary>
    public JsonObject Settings { get; private set; }

    /// <summary>Where it is in its life.</summary>
    public LanguageServerState State { get; private set; } = LanguageServerState.Starting;

    /// <summary>Why it failed, or what it is doing, if anything.</summary>
    public string? StatusMessage { get; private set; }

    /// <summary>The version the server reported in <c>initialize</c>, if any.</summary>
    public string? ServerVersion { get; private set; }

    /// <summary>What it can do; nothing until it has answered <c>initialize</c>.</summary>
    public LanguageServerCapabilities Capabilities => _capabilities;

    /// <summary>Its status, for the status bar.</summary>
    public LanguageServerStatus Status => new(Definition.Id, Definition.Name, RootDirectory, State, StatusMessage, ServerVersion);

    /// <summary>The texts of the workspace's files, for turning positions in other files into offsets.</summary>
    public ISnapshotSource Files => _host;

    /// <summary>Starts the server, unless it was started already.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _run = Task.Run(RunAsync);
        }
    }

    /// <summary>
    /// Waits until the server runs and has opened its documents, and returns the connection to it.
    /// </summary>
    /// <param name="cancellationToken">Gives up waiting.</param>
    /// <returns>The connection, or <see langword="null"/> when the server failed or stopped.</returns>
    public async Task<JsonRpcConnection?> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var ready = Volatile.Read(ref _ready);
            if (!await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            if (_connection is { IsClosed: false } connection)
            {
                return connection;
            }

            // The server just stopped; wait for the restart to replace the readiness.
            if (ReferenceEquals(ready, Volatile.Read(ref _ready)))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Adds a document the server serves, opening it on the server, and starts the server if needed.</summary>
    /// <param name="document">The document.</param>
    public void AddDocument(LanguageServerDocument document)
    {
        JsonRpcConnection? connection;
        lock (_gate)
        {
            _documents.Add(document);
            connection = State == LanguageServerState.Running ? _connection : null;
        }

        if (connection is not null)
        {
            document.OpenOn(connection, Capabilities);
        }

        Start();
    }

    /// <summary>Removes a document that closed, closing it on the server.</summary>
    /// <param name="document">The document.</param>
    public void RemoveDocument(LanguageServerDocument document)
    {
        lock (_gate)
        {
            _documents.Remove(document);
        }

        document.CloseOnServer();
    }

    /// <summary>The open document of a file, if this server serves it.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public LanguageServerDocument? FindDocument(string filePath)
    {
        lock (_gate)
        {
            return _documents.Find(d => DocumentUri.SamePath(d.FilePath, filePath));
        }
    }

    /// <summary>Sends new settings, after the workspace's settings changed.</summary>
    /// <param name="settings">The settings.</param>
    public async Task UpdateSettingsAsync(JsonObject settings)
    {
        Settings = settings;
        if (_connection is { IsClosed: false } connection && State == LanguageServerState.Running)
        {
            try
            {
                await connection.SendNotificationAsync(
                    "workspace/didChangeConfiguration",
                    new DidChangeConfigurationParamsDto(settings.DeepClone()),
                    LspJsonContext.Default.DidChangeConfigurationParamsDto).ConfigureAwait(false);
            }
            catch (JsonRpcException)
            {
                // The server stopped; it reads the settings again when it restarts.
            }
        }
    }

    /// <summary>
    /// Tells the server that files were created, changed or deleted outside its open documents: without it, Pyright
    /// kept "Import 'helper' could not be resolved" after helper.py was created, until the workspace was reopened.
    /// </summary>
    /// <param name="changes">Each file's full path and what happened to it.</param>
    public async Task NotifyFilesChangedAsync(IReadOnlyList<(string FilePath, FileChangeKind Kind)> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0 || _connection is not { IsClosed: false } connection || State != LanguageServerState.Running)
        {
            return;
        }

        var events = new JsonArray();
        foreach (var (filePath, kind) in changes)
        {
            events.Add(new JsonObject { ["uri"] = DocumentUri.FromPath(filePath), ["type"] = (int)kind });
        }

        try
        {
            await connection.SendNotificationAsync("workspace/didChangeWatchedFiles", new JsonObject { ["changes"] = events }).ConfigureAwait(false);
        }
        catch (JsonRpcException)
        {
            // The server stopped; it reads the files again when it restarts.
        }
    }

    /// <summary>Logs a request the server answered with an error, other than the ones that only mean "too late".</summary>
    /// <param name="method">The request's method.</param>
    /// <param name="error">The error.</param>
    public void LogRequestFailure(string method, Exception error) =>
        _host.Log(Name, LanguageServerLogLevel.Warning, $"'{method}' failed: {error.Message}");

    /// <summary>Shuts the server down politely, then ends its program.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        if (_connection is { IsClosed: false } connection && State == LanguageServerState.Running)
        {
            try
            {
                using var timeout = new CancellationTokenSource(_host.Options.ShutdownTimeout);
                await connection.SendRequestAsync("shutdown", null, timeout.Token).ConfigureAwait(false);
                await connection.SendNotificationAsync("exit", null, timeout.Token).ConfigureAwait(false);
                if (_process is { } running)
                {
                    await running.Exited.WaitAsync(timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is JsonRpcException or OperationCanceledException)
            {
                // A server that does not shut down in time is ended below.
            }
        }

        await _life.CancelAsync().ConfigureAwait(false);
        if (Interlocked.Exchange(ref _process, null) is { } process)
        {
            await CleanUpAsync(process, Interlocked.Exchange(ref _connection, null)).ConfigureAwait(false);
        }

        try
        {
            await _run.WaitAsync(_host.Options.ShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The run loop ends on its own once the process is gone.
        }

        SetState(LanguageServerState.Stopped, null);
        Volatile.Read(ref _ready).TrySetResult(false);
        _life.Dispose();
    }

    private static TaskCompletionSource<bool> NewReadiness() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs the server, and runs it again after it stopped unexpectedly, until it fails too often.</summary>
    private async Task RunAsync()
    {
        while (!_stopping && !_life.IsCancellationRequested)
        {
            if (!await RunOnceAsync().ConfigureAwait(false) || _stopping || _life.IsCancellationRequested)
            {
                return;
            }

            int crashes;
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow;
                _crashes.RemoveAll(c => now - c > _host.Options.RestartWindow);
                _crashes.Add(now);
                crashes = _crashes.Count;
            }

            if (crashes > _host.Options.MaxRestarts)
            {
                var minutes = _host.Options.RestartWindow.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
                Fail($"{Name} stopped {crashes} times in {minutes} minutes, so it was not started again. Reopen the workspace to try again.");
                return;
            }

            SetState(LanguageServerState.Restarting, "Restarting after it stopped unexpectedly");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(8, 1 << (crashes - 1))), _life.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Starts the program, initializes it and waits until it ends.</summary>
    /// <returns>Whether it stopped unexpectedly and may restart; <see langword="false"/> for a terminal startup failure.</returns>
    private async Task<bool> RunOnceAsync()
    {
        IServerProcess process;
        try
        {
            _host.Log(Name, LanguageServerLogLevel.Info, $"Starting {Name} for {RootDirectory}: {_executable} {string.Join(' ', Definition.Arguments)}");
            process = _host.Launcher.Start(new ServerStartInfo(_executable, Definition.Arguments.Select(a => a.Replace("${workspaceFolder}", RootDirectory, StringComparison.Ordinal).Replace("${serverStorage}", a.Contains("${serverStorage}", StringComparison.Ordinal) ? ServerStorage() : string.Empty, StringComparison.Ordinal)).ToList(), RootDirectory, Definition.Environment));
        }
        catch (ServerStartException ex)
        {
            Fail(ex.Message);
            return false;
        }

        _process = process;
        var standardError = PumpStandardErrorAsync(process);
        var connection = new JsonRpcConnection(process.StandardOutput, process.StandardInput);
        RegisterHandlers(connection);
        if (_host.Options.TraceMessages)
        {
            connection.Traced += (_, e) => _host.Log(Name, LanguageServerLogLevel.Trace, e.Message);
        }

        connection.Start();
        try
        {
            await InitializeAsync(connection).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonRpcException or OperationCanceledException or JsonException)
        {
            // An orderly EOF before initialize answers is the same unexpected process stop as one after it.
            // Keep the original readiness pending while RunAsync applies its existing restart budget and backoff.
            // Protocol/read errors and a server's initialize rejection still fail immediately.
            var restart = !_stopping && !_life.IsCancellationRequested
                && ex is JsonRpcException { IsConnectionClosed: true }
                && connection.IsClosed && connection.CloseReason is null;
            if (!_stopping && !_life.IsCancellationRequested)
            {
                // Drain stderr only for an already exited process; a server that closed stdout may still be running.
                if (process.Exited.IsCompletedSuccessfully)
                {
                    try
                    {
                        await standardError.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // Its inherited stderr may still be held open by a child.
                    }
                }

                var exit = process.Exited.IsCompletedSuccessfully ? $" (exit code {await process.Exited.ConfigureAwait(false)})" : string.Empty;
                var message = ex is OperationCanceledException
                    ? $"{Name} did not answer within {_host.Options.InitializeTimeout.TotalSeconds:0} seconds of starting."
                    : $"{Name} could not be initialized: {ex.Message}{exit}";
                if (restart)
                {
                    _host.Log(Name, LanguageServerLogLevel.Error, message);
                }
                else
                {
                    Fail(message);
                }
            }

            if (Interlocked.Exchange(ref _process, null) is { } failed)
            {
                await CleanUpAsync(failed, connection).ConfigureAwait(false);
            }

            return restart;
        }

        List<LanguageServerDocument> documents;
        lock (_gate)
        {
            _connection = connection;
            State = LanguageServerState.Running;
            StatusMessage = null;
            documents = [.. _documents];
        }

        foreach (var document in documents)
        {
            document.OpenOn(connection, Capabilities);
        }

        _host.Log(Name, LanguageServerLogLevel.Info, ServerVersion is null ? $"{Name} is running." : $"{Name} is running ({ServerVersion}).");
        Volatile.Read(ref _ready).TrySetResult(true);
        _host.OnServerStateChanged();
        NotifyDocuments(d => d.OnServerChanged());

        await Task.WhenAny(process.Exited, connection.Completion).ConfigureAwait(false);
        if (!_stopping && !_life.IsCancellationRequested)
        {
            // Callers wait for the restart from now on, rather than getting the closed connection.
            Volatile.Write(ref _ready, NewReadiness());
            NotifyDocuments(d => d.OnServerStopped());
            var exit = process.Exited.IsCompleted ? $"exit code {await process.Exited.ConfigureAwait(false)}" : "its output closed";
            _host.Log(Name, LanguageServerLogLevel.Error, $"{Name} stopped unexpectedly ({exit}).");
        }

        if (Interlocked.Exchange(ref _process, null) is { } ended)
        {
            await CleanUpAsync(ended, Interlocked.Exchange(ref _connection, null)).ConfigureAwait(false);
        }

        return true;
    }

    private async Task InitializeAsync(JsonRpcConnection connection)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        timeout.CancelAfter(_host.Options.InitializeTimeout);
        var folderName = Path.GetFileName(RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var parameters = new InitializeParamsDto(
            Definition.SendsClientProcessId ? Environment.ProcessId : null,
            new ClientInfoDto(_host.Options.ClientName, _host.Options.ClientVersion),
            "en",
            RootDirectory,
            RootUri,
            InitializationOptions.Resolve(Definition.InitializationOptions, RootDirectory, _executable, (message, isWarning) => _host.Log(Name, isWarning ? LanguageServerLogLevel.Warning : LanguageServerLogLevel.Info, message), _host.Launcher.ToLocalPath),
            ClientCapabilities.Create(),
            _host.Options.TraceMessages ? "messages" : "off",
            [new WorkspaceFolderDto(RootUri, folderName.Length == 0 ? RootDirectory : folderName)]);

        var result = await connection.SendRequestAsync(
            "initialize",
            parameters,
            LspJsonContext.Default.InitializeParamsDto,
            LspJsonContext.Default.InitializeResultDto,
            timeout.Token).ConfigureAwait(false);

        _capabilities = LanguageServerCapabilities.FromJson(result?.Capabilities ?? default);
        ServerVersion = result?.ServerInfo is { } info
            ? info.Version is null ? info.Name : $"{info.Name} {info.Version}"
            : null;
        await connection.SendNotificationAsync("initialized", new JsonObject(), timeout.Token).ConfigureAwait(false);
        await connection.SendNotificationAsync(
            "workspace/didChangeConfiguration",
            new DidChangeConfigurationParamsDto(Settings.DeepClone()),
            LspJsonContext.Default.DidChangeConfigurationParamsDto,
            timeout.Token).ConfigureAwait(false);
    }

    private async Task PumpStandardErrorAsync(IServerProcess process)
    {
        try
        {
            using var reader = new StreamReader(process.StandardError);
            while (await reader.ReadLineAsync(_life.Token).ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0)
                {
                    _host.Log(Name, LanguageServerLogLevel.Log, line);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // The program ended.
        }
    }

    private void Fail(string message)
    {
        SetState(LanguageServerState.Failed, message);
        _host.Log(Name, LanguageServerLogLevel.Error, message);
        Volatile.Read(ref _ready).TrySetResult(false);
        NotifyDocuments(d => d.OnServerChanged());
    }

    private void SetState(LanguageServerState state, string? message)
    {
        lock (_gate)
        {
            State = state;
            StatusMessage = message;
        }

        _host.OnServerStateChanged();
    }

    private void NotifyDocuments(Action<LanguageServerDocument> action)
    {
        List<LanguageServerDocument> documents;
        lock (_gate)
        {
            documents = [.. _documents];
        }

        foreach (var document in documents)
        {
            action(document);
        }
    }

    private static async Task CleanUpAsync(IServerProcess process, JsonRpcConnection? connection)
    {
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        process.Kill();
        try
        {
            await process.Exited.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Killed; the operating system reaps it.
        }

        process.Dispose();
    }
    private string ServerStorage()
    {
        var name = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(RootDirectory + "\n" + Definition.Id)))[..24];
        var folder = Path.Combine(Path.GetTempPath(), "tabstudio-language-servers", name);
        Directory.CreateDirectory(folder);
        return folder;
    }

}
