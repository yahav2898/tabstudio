using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters.Protocol;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.LanguageServers.DebugAdapters;

/// <summary>How a <see cref="DebugAdapterSession"/> introduces TabStudio and how long it waits.</summary>
public sealed record DebugAdapterSessionOptions
{
    /// <summary>The client name sent in <c>initialize</c>.</summary>
    public string ClientName { get; init; } = "TabStudio";

    /// <summary>How long the adapter may take to start and answer <c>initialize</c>.</summary>
    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether every message is written to the log.</summary>
    public bool TraceMessages { get; init; }

    /// <summary>Receives adapter stderr while it starts, before a session is available for event handlers.</summary>
    public Action<string>? StartupLog { get; init; }

    /// <summary>
    /// Whether the adapter may ask for child sessions with <c>startDebugging</c>, as js-debug does for every Node
    /// process; <see cref="DebugAdapterSession.ChildSessionHandler"/> starts them.
    /// </summary>
    public bool SupportsChildSessions { get; init; }

    /// <summary>
    /// Whether the adapter may ask TabStudio to run the program (<c>runInTerminal</c>), so it reads its input from
    /// the user: launches then use the adapter's <see cref="DebugAdapterDefinition.TerminalDefaults"/>, and
    /// <see cref="DebugAdapterSession.RunInTerminalHandler"/> runs the program.
    /// </summary>
    public bool SupportsRunInTerminal { get; init; }
}

/// <summary>A debug adapter's request for a child session, as js-debug sends one for every process it debugs.</summary>
/// <param name="Configuration">The child session's launch configuration, as the adapter gave it.</param>
/// <param name="Request"><c>launch</c> or <c>attach</c>.</param>
public sealed record DapChildSessionRequest(JsonObject Configuration, string Request);

/// <summary>
/// A debug session with a debug adapter: starts the adapter, launches or attaches to the program with a launch
/// configuration, and then sets breakpoints, steps, and reads threads, frames, scopes and variables while the
/// program is paused. The requests are in <c>DebugAdapterSession.Requests.cs</c>.
/// </summary>
/// <remarks>Events are raised on the connection's reading thread.</remarks>
public sealed partial class DebugAdapterSession : IAsyncDisposable
{
    private readonly DebugAdapterConnection _connection;
    private readonly IServerProcess? _process;
    private readonly TcpClient? _socket;
    private readonly TaskCompletionSource _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _terminated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    private bool _runsInTerminal;

    private DebugAdapterSession(DebugAdapterDefinition adapter, DebugAdapterConnection connection, IServerProcess? process, TcpClient? socket, int? port = null)
    {
        Adapter = adapter;
        Port = port;
        _connection = connection;
        _process = process;
        _socket = socket;
        RegisterEvents();
        connection.Completion.ContinueWith(_ => End(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Raised when the program stopped.</summary>
    public event EventHandler<DapStoppedEventArgs>? Stopped;

    /// <summary>Raised when the program runs again on its own, such as after an expression was evaluated.</summary>
    public event EventHandler? Continued;

    /// <summary>Raised with the program's output and the adapter's messages for the user.</summary>
    public event EventHandler<DapOutputEventArgs>? OutputReceived;

    /// <summary>Raised when the adapter changed a breakpoint.</summary>
    public event EventHandler<DapBreakpointEventArgs>? BreakpointChanged;

    /// <summary>Raised when the program exited, with its exit code.</summary>
    public event EventHandler<DapExitedEventArgs>? Exited;

    /// <summary>Raised when the session ended: the program finished or was stopped, or the adapter closed.</summary>
    public event EventHandler? Ended;

    /// <summary>Raised with a line of the adapter's own log: its standard error and, when traced, every message.</summary>
    public event EventHandler<string>? AdapterLog;

    /// <summary>Flutter's web target has supplied the URL to open in the preview.</summary>
    public event EventHandler<Uri>? FlutterWebLaunch;

    /// <summary>The Flutter application has started, including launches without a VM service.</summary>
    public event EventHandler? FlutterAppStarted;

    /// <summary>The adapter.</summary>
    public DebugAdapterDefinition Adapter { get; }

    /// <summary>What the adapter can do, from its answer to <c>initialize</c>.</summary>
    public DapCapabilities Capabilities { get; private set; } = new();

    /// <summary>Whether the session ended.</summary>
    public bool HasEnded => _ended.Task.IsCompleted;

    /// <summary>The exit code the program reported, once it exited.</summary>
    public int? ExitCode { get; private set; }

    /// <summary>The port the adapter listens on, when it is reached over TCP; child sessions connect to it too.</summary>
    public int? Port { get; }

    /// <summary>
    /// Starts a child session the adapter asks for with <c>startDebugging</c>; without one, such requests are refused.
    /// Set it before <see cref="LaunchAsync"/>, with <see cref="DebugAdapterSessionOptions.SupportsChildSessions"/>.
    /// </summary>
    public Func<DapChildSessionRequest, CancellationToken, Task>? ChildSessionHandler { get; set; }

    /// <summary>
    /// Runs the program the adapter asks TabStudio to run (<c>runInTerminal</c>), and completes with its process
    /// id once it started, or <see langword="null"/> when that is not known. Set it before <see cref="LaunchAsync"/>,
    /// with <see cref="DebugAdapterSessionOptions.SupportsRunInTerminal"/>; without one, such requests are refused.
    /// </summary>
    public Func<DapRunInTerminalRequest, CancellationToken, Task<int?>>? RunInTerminalHandler { get; set; }

    /// <summary>Whether the program runs in TabStudio's console, started through <see cref="RunInTerminalHandler"/>.</summary>
    public bool RunsInTerminal => _runsInTerminal && RunInTerminalHandler is not null;

    /// <summary>Whether input can be sent to the debuggee through this socket adapter's own standard input.</summary>
    public bool AcceptsProgramInput => Adapter.UsesProcessInput && Adapter.Transport != DebugAdapterTransport.Stdio && _process is not null;

    /// <summary>Sends a line typed in the Output panel to a socket adapter whose child inherits standard input.</summary>
    /// <param name="line">The line, without its newline.</param>
    /// <param name="cancellationToken">Stops writing.</param>
    /// <exception cref="InvalidOperationException">This adapter's standard input carries protocol messages.</exception>
    public async Task WriteProgramInputAsync(string line, CancellationToken cancellationToken = default)
    {
        if (!AcceptsProgramInput)
        {
            throw new InvalidOperationException("This adapter does not accept program input on its standard input.");
        }

        await _process!.StandardInput.WriteAsync(System.Text.Encoding.UTF8.GetBytes(line + "\n"), cancellationToken).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts an adapter and initializes it.</summary>
    /// <param name="adapter">The adapter.</param>
    /// <param name="executable">The full path of its program, as the launcher resolved it.</param>
    /// <param name="workingDirectory">The folder to start it in, usually the workspace's.</param>
    /// <param name="launcher">Starts the program.</param>
    /// <param name="options">How to introduce TabStudio; the defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Gives up starting.</param>
    /// <exception cref="ServerStartException">The adapter could not be started.</exception>
    /// <exception cref="DebugAdapterException">The adapter did not initialize.</exception>
    public static async Task<DebugAdapterSession> StartAsync(
        DebugAdapterDefinition adapter,
        string executable,
        string workingDirectory,
        IServerProcessLauncher launcher,
        DebugAdapterSessionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(launcher);
        options ??= new DebugAdapterSessionOptions();
        var port = adapter.Transport == DebugAdapterTransport.Tcp ? FreePort() : 0;
        var socketDirectory = adapter.Transport == DebugAdapterTransport.UnixSocket ? CreateUnixSocketDirectory() : null;
        var socketPath = socketDirectory is null ? string.Empty : Path.Combine(socketDirectory, "d");
        var arguments = adapter.Arguments.Select(a => a.Replace("${port}", port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("${socket}", socketPath, StringComparison.Ordinal)).ToList();
        var process = StartAdapterProcess(launcher, new ServerStartInfo(executable, arguments, workingDirectory, adapter.Environment), socketDirectory);
        DebugAdapterSession? session = null;
        var initializing = 1;
        var started = false;
        PumpStandardError(process, line =>
        {
            if (Volatile.Read(ref initializing) != 0)
            {
                options.StartupLog?.Invoke(line);
            }
            else
            {
                session?.AdapterLog?.Invoke(session, line);
            }
        });
        try
        {
            if (adapter.Transport == DebugAdapterTransport.Tcp)
            {
                var socket = await ConnectWithRetriesAsync(IPAddress.Loopback.ToString(), port, options.StartTimeout, process, cancellationToken).ConfigureAwait(false);
                session = new DebugAdapterSession(adapter, new DebugAdapterConnection(socket.GetStream(), socket.GetStream()), process, socket, port);
            }
            else if (adapter.Transport == DebugAdapterTransport.UnixSocket)
            {
                var socket = await ConnectToUnixSocketAsync(socketPath, options.StartTimeout, process, cancellationToken).ConfigureAwait(false);
                var stream = new NetworkStream(socket, ownsSocket: true);
                session = new DebugAdapterSession(adapter, new DebugAdapterConnection(stream, stream), process, null) { _unixSocketDirectory = socketDirectory };
            }
            else
            {
                session = new DebugAdapterSession(adapter, new DebugAdapterConnection(process.StandardOutput, process.StandardInput), process, null);
            }

            await session.InitializeAsync(options, cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref initializing, 0);
            started = true;
            return session;
        }
        catch (DebugAdapterException)
        {
            await AbandonAsync(session, process).ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await AbandonAsync(session, process).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or TimeoutException)
        {
            await AbandonAsync(session, process).ConfigureAwait(false);
            throw new DebugAdapterException($"{adapter.Name} did not start: {ex.Message}", ex);
        }
        finally
        {
            if (!started)
            {
                RemoveUnixSocketDirectory(socketDirectory);
            }
        }
    }

    /// <summary>Connects to an adapter that is already running and listening on a port, as <c>debugServer</c> asks.</summary>
    /// <param name="adapter">The adapter.</param>
    /// <param name="port">The port on this device.</param>
    /// <param name="options">How to introduce TabStudio; the defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Gives up connecting.</param>
    /// <exception cref="DebugAdapterException">The adapter could not be reached or did not initialize.</exception>
    public static async Task<DebugAdapterSession> ConnectAsync(DebugAdapterDefinition adapter, int port, DebugAdapterSessionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        options ??= new DebugAdapterSessionOptions();
        DebugAdapterSession? session = null;
        try
        {
            var socket = await ConnectWithRetriesAsync(IPAddress.Loopback.ToString(), port, options.StartTimeout, null, cancellationToken).ConfigureAwait(false);
            session = new DebugAdapterSession(adapter, new DebugAdapterConnection(socket.GetStream(), socket.GetStream()), null, socket, port);
            await session.InitializeAsync(options, cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch (DebugAdapterException)
        {
            await AbandonAsync(session, null).ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await AbandonAsync(session, null).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or TimeoutException)
        {
            await AbandonAsync(session, null).ConfigureAwait(false);
            throw new DebugAdapterException($"The debug adapter on port {port.ToString(CultureInfo.InvariantCulture)} could not be reached: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Launches or attaches to the program, as the configuration asks, with the breakpoints and exception filters
    /// set before it runs, in the order the protocol requires.
    /// </summary>
    /// <param name="configuration">The launch configuration, with its variables substituted.</param>
    /// <param name="breakpoints">The breakpoints of every file, by full path.</param>
    /// <param name="exceptionFilters">The exception filters to turn on.</param>
    /// <param name="cancellationToken">Gives up launching.</param>
    /// <returns>The breakpoints as the adapter set them, by file, in the order they were given.</returns>
    /// <exception cref="DebugAdapterException">The adapter could not launch or attach, with its reason.</exception>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DapBreakpoint>>> LaunchAsync(
        LaunchConfiguration configuration,
        IReadOnlyDictionary<string, IReadOnlyList<DapSourceBreakpoint>> breakpoints,
        IReadOnlyList<string> exceptionFilters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(breakpoints);
        ArgumentNullException.ThrowIfNull(exceptionFilters);
        var start = _connection.SendRequestAsync(configuration.IsAttach ? "attach" : "launch", configuration.ToRequestArguments(Adapter, RunsInTerminal), cancellationToken);

        // Adapters send "initialized" when they are ready for breakpoints; some answer the launch first.
        if (await Task.WhenAny(_initialized.Task, start).ConfigureAwait(false) == start)
        {
            await start.ConfigureAwait(false);
        }

        await _initialized.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        var results = new Dictionary<string, IReadOnlyList<DapBreakpoint>>(StringComparer.Ordinal);
        foreach (var (file, fileBreakpoints) in breakpoints)
        {
            results[file] = await SetBreakpointsAsync(file, fileBreakpoints, cancellationToken).ConfigureAwait(false);
        }

        if (Capabilities.ExceptionFilters.Count > 0)
        {
            await SetExceptionBreakpointsAsync(exceptionFilters, cancellationToken).ConfigureAwait(false);
        }

        if (Capabilities.ConfigurationDone)
        {
            await _connection.SendRequestAsync("configurationDone", null, cancellationToken).ConfigureAwait(false);
        }

        await start.ConfigureAwait(false);
        return results;
    }

    /// <summary>Ends the session: stops the program, closes the adapter and ends its process.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!_connection.IsClosed)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _connection.SendRequestAsync("disconnect", new DisconnectArgumentsDto(false, true), DapJsonContext.Default.DisconnectArgumentsDto, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DebugAdapterException or OperationCanceledException)
            {
                // An adapter that does not answer is ended below.
            }
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _socket?.Dispose();
        if (_process is not null)
        {
            try
            {
                await _process.Exited.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _process.Kill();
            }

            _process.Dispose();
        }

        RemoveUnixSocketDirectory(_unixSocketDirectory);
        End();
    }

    /// <summary>Ends what a failed start left behind: the session, or else the adapter's process.</summary>
    private static async Task AbandonAsync(DebugAdapterSession? session, IServerProcess? process)
    {
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        else if (process is not null)
        {
            process.Kill();
            process.Dispose();
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Connects to a port, trying again while the adapter is still starting.</summary>
    private static async Task<TcpClient> ConnectWithRetriesAsync(string host, int port, TimeSpan timeout, IServerProcess? process, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
                return client;
            }
            catch (SocketException) when (DateTime.UtcNow < deadline && process?.Exited.IsCompleted != true)
            {
                client.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }

    private async Task InitializeAsync(DebugAdapterSessionOptions options, CancellationToken cancellationToken)
    {
        if (options.TraceMessages)
        {
            _connection.Traced += (_, e) => AdapterLog?.Invoke(this, e.Message);
        }

        _runsInTerminal = options.SupportsRunInTerminal;
        _connection.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.StartTimeout);
        var arguments = new InitializeArgumentsDto(
            "tabstudio",
            options.ClientName,
            Adapter.Id,
            "en",
            LinesStartAt1: true,
            ColumnsStartAt1: true,
            PathFormat: "path",
            SupportsVariableType: true,
            SupportsVariablePaging: false,
            SupportsRunInTerminalRequest: options.SupportsRunInTerminal,
            SupportsMemoryReferences: true,
            SupportsProgressReporting: false,
            SupportsInvalidatedEvent: false,
            SupportsStartDebuggingRequest: options.SupportsChildSessions,
            SupportsArgsCanBeInterpretedByShell: false);
        var body = await _connection.SendRequestAsync("initialize", arguments, DapJsonContext.Default.InitializeArgumentsDto, timeout.Token).ConfigureAwait(false);
        Capabilities = ToCapabilities(body);
    }

    private void RegisterEvents()
    {
        _connection.AddEventHandler("flutter.appStarted", _ => FlutterAppStarted?.Invoke(this, EventArgs.Empty));
        _connection.AddEventHandler("flutter.forwardedEvent", body =>
        {
            if (body.TryGetProperty("event", out var name) && name.GetString() == "app.webLaunchUrl"
                && body.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("url", out var url)
                && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var address)
                && address.Scheme is "http" or "https" && address.IsLoopback)
            {
                FlutterWebLaunch?.Invoke(this, address);
            }
        });
        _connection.AddEventHandler("initialized", _ => _initialized.TrySetResult());
        _connection.AddEventHandler("stopped", body =>
        {
            if (Read(body, DapJsonContext.Default.StoppedEventDto) is { } stopped)
            {
                // Dart pauses isolates briefly on exit to resolve stack traces. These are protocol bookkeeping,
                // including isolates used by evaluation, not user breakpoints. Grant the resume permission so
                // DDS can finish them without exposing a stale frame in the IDE.
                if (Adapter.Id is "dart" or "flutter" && stopped.Reason == "exit" && stopped.ThreadId is { } exitingThread)
                {
                    _ = ResumeDartExitAsync(exitingThread);
                    return;
                }
                Stopped?.Invoke(this, new DapStoppedEventArgs(stopped.Reason ?? "pause", stopped.ThreadId, stopped.AllThreadsStopped == true, stopped.Description, stopped.Text, stopped.HitBreakpointIds ?? []));
            }
        });
        _connection.AddEventHandler("continued", _ => Continued?.Invoke(this, EventArgs.Empty));
        _connection.AddEventHandler("exited", body =>
        {
            if (Read(body, DapJsonContext.Default.ExitedEventDto) is { } exited)
            {
                ExitCode = exited.ExitCode;
                Exited?.Invoke(this, new DapExitedEventArgs(exited.ExitCode));
            }
        });
        _connection.AddEventHandler("terminated", _ =>
        {
            _terminated.TrySetResult();
            End();
        });
        _connection.AddEventHandler("output", body =>
        {
            if (Read(body, DapJsonContext.Default.OutputEventDto) is { Output: { Length: > 0 } text } output && output.Category != "telemetry")
            {
                OutputReceived?.Invoke(this, new DapOutputEventArgs(output.Category ?? "console", text));
            }
        });
        _connection.AddEventHandler("breakpoint", body =>
        {
            if (Read(body, DapJsonContext.Default.BreakpointEventDto) is { Breakpoint: { } breakpoint } changed)
            {
                BreakpointChanged?.Invoke(this, new DapBreakpointEventArgs(changed.Reason ?? "changed", ToBreakpoint(breakpoint)));
            }
        });
        _connection.AddEventHandler("capabilities", body =>
        {
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("capabilities", out var capabilities))
            {
                // The event carries only the capabilities that changed (lldb-dap sends supportsRestartRequest after
                // every launch): the others keep what initialize said.
                Capabilities = ToCapabilities(capabilities, Capabilities);
            }
        });
        _connection.AddRequestHandler("runInTerminal", async (arguments, cancellationToken) =>
        {
            if (RunInTerminalHandler is not { } handler || ReadRunInTerminal(arguments) is not { } request)
            {
                throw new DebugAdapterException("TabStudio runs programs in its own console; set \"console\": \"internalConsole\".");
            }

            var processId = await handler(request, cancellationToken).ConfigureAwait(false);
            return processId is { } id ? new JsonObject { ["processId"] = id } : new JsonObject();
        });
        _connection.AddRequestHandler("startDebugging", async (arguments, cancellationToken) =>
        {
            if (ChildSessionHandler is not { } handler
                || arguments.ValueKind != JsonValueKind.Object
                || !arguments.TryGetProperty("configuration", out var configuration)
                || JsonNode.Parse(configuration.GetRawText()) is not JsonObject properties)
            {
                throw new DebugAdapterException($"TabStudio cannot start the child session {Adapter.Name} asks for.");
            }

            var request = arguments.TryGetProperty("request", out var kind) && kind.GetString() is { } value ? value : "launch";
            await handler(new DapChildSessionRequest(properties, request), cancellationToken).ConfigureAwait(false);
            return null;
        });
    }

    private async Task ResumeDartExitAsync(int thread)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ContinueAsync(thread, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DebugAdapterException or OperationCanceledException)
        {
            // The adapter may have resumed the isolate itself, or closed as its last isolate exited.
        }
    }

    /// <summary>The program a <c>runInTerminal</c> request asks for, or <see langword="null"/> when it names none.</summary>
    private static DapRunInTerminalRequest? ReadRunInTerminal(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("args", out var args)
            || args.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var program = args.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()!).ToList();
        if (program.Count == 0 || program[0].Length == 0)
        {
            return null;
        }

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (arguments.TryGetProperty("env", out var env) && env.ValueKind == JsonValueKind.Object)
        {
            foreach (var variable in env.EnumerateObject())
            {
                environment[variable.Name] = variable.Value.ValueKind == JsonValueKind.String ? variable.Value.GetString() : null;
            }
        }

        return new DapRunInTerminalRequest(
            program,
            arguments.TryGetProperty("cwd", out var cwd) && cwd.ValueKind == JsonValueKind.String ? cwd.GetString()! : string.Empty,
            environment,
            arguments.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String ? title.GetString() : null,
            arguments.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "external");
    }

    private static void PumpStandardError(IServerProcess process, Action<string> log) => _ = Task.Run(async () =>
    {
        try
        {
            using var reader = new StreamReader(process.StandardError);
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0)
                {
                    log(line);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The adapter ended.
        }
    });

    private void End()
    {
        if (_ended.TrySetResult())
        {
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }

    private static DapCapabilities ToCapabilities(JsonElement body, DapCapabilities? before = null)
    {
        before ??= new DapCapabilities();
        if (Read(body, DapJsonContext.Default.CapabilitiesDto) is not { } dto)
        {
            return before;
        }

        return new DapCapabilities
        {
            ConfigurationDone = dto.SupportsConfigurationDoneRequest ?? before.ConfigurationDone,
            ConditionalBreakpoints = dto.SupportsConditionalBreakpoints ?? before.ConditionalBreakpoints,
            HitConditionalBreakpoints = dto.SupportsHitConditionalBreakpoints ?? before.HitConditionalBreakpoints,
            LogPoints = dto.SupportsLogPoints ?? before.LogPoints,
            EvaluateForHovers = dto.SupportsEvaluateForHovers ?? before.EvaluateForHovers,
            SetVariable = dto.SupportsSetVariable ?? before.SetVariable,
            SetExpression = dto.SupportsSetExpression ?? before.SetExpression,
            ReadMemory = dto.SupportsReadMemoryRequest ?? before.ReadMemory,
            WriteMemory = dto.SupportsWriteMemoryRequest ?? before.WriteMemory,
            Disassemble = dto.SupportsDisassembleRequest ?? before.Disassemble,
            ExceptionInfo = dto.SupportsExceptionInfoRequest ?? before.ExceptionInfo,
            Terminate = dto.SupportsTerminateRequest ?? before.Terminate,
            ExceptionFilters = dto.ExceptionBreakpointFilters is { } filters
                ? [.. filters
                    .Where(f => !string.IsNullOrEmpty(f.Filter))
                    .Select(f => new DapExceptionFilter(f.Filter, f.Label ?? f.Filter, f.Description, f.Default == true))]
                : before.ExceptionFilters,
        };
    }

    private static DapBreakpoint ToBreakpoint(BreakpointDto breakpoint) =>
        new(breakpoint.Id, breakpoint.Verified, breakpoint.Line, breakpoint.Message);

    private static T? Read<T>(JsonElement body, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
        where T : class
    {
        try
        {
            return body.ValueKind == JsonValueKind.Object ? body.Deserialize(type) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
