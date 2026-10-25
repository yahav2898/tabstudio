using System.Threading.Channels;
using TabStudio.Link;
using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// One tablet on the bridge: its workspace's mirror, the programs running for it, their streams, and the debugger's
/// tunnels. It answers the tablet's requests on whichever connection the tablet is on now, and keeps its programs
/// running while the tablet is away.
/// </summary>
internal sealed class BridgeTablet : ILinkMessageHandler, IDisposable
{
    private readonly BridgeServer _server;
    private readonly LinkWorkspaceMirror _mirror;
    private readonly SemaphoreSlim _workspace = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<int, BridgeApp> _apps = [];
    private readonly Dictionary<int, BridgeStream> _streams = [];
    private readonly Dictionary<int, BridgeDebugTunnel> _tunnels = [];
    private readonly Channel<InputBatch> _input = Channel.CreateUnbounded<InputBatch>(new UnboundedChannelOptions { SingleReader = true });
    private LinkConnection? _connection;
    private OrderedNotifier? _notifier;
    private BridgePathMapper? _paths;
    private string? _activity;
    private int _nextApp;
    private int _nextStream;
    private int _disposed;

    /// <summary>Creates the tablet's state.</summary>
    /// <param name="server">The bridge.</param>
    /// <param name="tablet">The tablet.</param>
    /// <param name="folder">The folder for the tablet's workspaces on the PC.</param>
    public BridgeTablet(BridgeServer server, LinkPeer tablet, string folder)
    {
        _server = server;
        Tablet = tablet;
        _mirror = new LinkWorkspaceMirror(folder);
        _ = Task.Run(PlayInputAsync);
    }

    /// <summary>The tablet, as it last connected.</summary>
    public LinkPeer Tablet { get; private set; }

    /// <summary>What the bridge is doing for the tablet, or <see langword="null"/>.</summary>
    public string? Activity
    {
        get
        {
            lock (_gate)
            {
                return _activity;
            }
        }
    }

    /// <summary>The programs running for the tablet.</summary>
    public IReadOnlyList<BridgeAppInfo> Apps
    {
        get
        {
            lock (_gate)
            {
                return [.. _apps.Values.Select(a => a.Info)];
            }
        }
    }

    private IBridgeDesktop Desktop => _server.Desktop;

    /// <summary>Serves the tablet on a new connection, and tells it what runs.</summary>
    /// <param name="tablet">The tablet, as it connected.</param>
    /// <param name="connection">The connection.</param>
    public void Attach(LinkPeer tablet, LinkConnection connection)
    {
        OrderedNotifier notifier;
        List<BridgeApp> apps;
        lock (_gate)
        {
            Tablet = tablet;
            _connection = connection;
            _notifier?.Complete();
            notifier = _notifier = new OrderedNotifier(connection);
            apps = [.. _apps.Values];
        }

        connection.Closed += (_, _) => Detach(connection);
        foreach (var app in apps)
        {
            notifier.Post(BridgeMethods.AppStarted, BridgeJson.ToJson(new AppStarted(app.Id, app.Name, app.ProcessId, app.ProjectPath, app.IsDebugging)));
            notifier.Post(BridgeMethods.AppWindows, BridgeJson.ToJson(app.WindowList));
        }

        SendStatus();
    }

    /// <summary>Tells the tablet what the PC is doing and what runs.</summary>
    public void SendStatus() => Post(BridgeMethods.Status, BridgeJson.ToJson(new BridgeStatus(Desktop.State, Activity, Apps)));

    /// <summary>Stops a program, if it is the tablet's.</summary>
    /// <param name="appId">The program.</param>
    public void StopApp(int appId)
    {
        BridgeApp? app;
        lock (_gate)
        {
            app = _apps.GetValueOrDefault(appId);
        }

        app?.Stop();
    }

    /// <inheritdoc />
    public async Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case BridgeMethods.Info:
                return LinkReply.Of(_server.Info, BridgeJson.Info<BridgeInfo>());
            case BridgeMethods.PrepareWorkspace:
                return await WithWorkspaceAsync(() =>
                {
                    var missing = _mirror.Prepare(payload.Read(LinkLanguageJson.Info<PrepareWorkspaceRequest>()));
                    return LinkReply.Of(new PrepareWorkspaceResponse(missing), LinkLanguageJson.Info<PrepareWorkspaceResponse>());
                }, cancellationToken).ConfigureAwait(false);
            case BridgeMethods.UploadFiles:
                return await WithWorkspaceAsync(() =>
                {
                    _mirror.Write(payload.Read(LinkLanguageJson.Info<UploadFilesRequest>()).Files, payload.Attachments);
                    return LinkReply.Empty;
                }, cancellationToken).ConfigureAwait(false);
            case BridgeMethods.CommitWorkspace:
                return await WithWorkspaceAsync(() =>
                {
                    var map = _mirror.Commit();
                    lock (_gate)
                    {
                        _paths = BridgePathMapper.ForThisMachine(map);
                    }

                    return LinkReply.Empty;
                }, cancellationToken).ConfigureAwait(false);
            case BridgeMethods.Build:
                return await BuildAsync(BridgeJson.Read<BuildRequest>(payload), cancellationToken).ConfigureAwait(false);
            case BridgeMethods.Run:
                return LinkReply.Of(Run(BridgeJson.Read<RunRequest>(payload)), BridgeJson.Info<AppStarted>());
            case BridgeMethods.Stop:
                StopApp(BridgeJson.Read<AppReference>(payload).AppId);
                return LinkReply.Empty;
            case BridgeMethods.StartStream:
                return LinkReply.Of(StartStream(BridgeJson.Read<StreamRequest>(payload)), BridgeJson.Info<StreamStarted>());
            case BridgeMethods.Resize:
                Resize(BridgeJson.Read<ResizeRequest>(payload));
                return LinkReply.Empty;
            case BridgeMethods.GetClipboard:
                return LinkReply.Of(new ClipboardText(Desktop.GetClipboardText()), BridgeJson.Info<ClipboardText>());
            case BridgeMethods.SetClipboard:
                Desktop.SetClipboardText(BridgeJson.Read<ClipboardText>(payload).Text ?? string.Empty);
                return LinkReply.Empty;
            default:
                throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, $"The Windows Bridge does not know '{method}'.");
        }
    }

    /// <inheritdoc />
    public void HandleNotification(string method, LinkPayload payload)
    {
        switch (method)
        {
            case BridgeMethods.FrameShown:
                var shown = BridgeJson.Read<FrameShown>(payload);
                FindStream(shown.StreamId)?.OnShown(shown.Sequence);
                break;
            case BridgeMethods.Input:
                _input.Writer.TryWrite(BridgeJson.Read<InputBatch>(payload));
                break;
            case BridgeMethods.StopStream:
                StopStream(BridgeJson.Read<StreamReference>(payload).StreamId);
                break;
            case BridgeMethods.OpenDebugger:
                OpenDebugger(BridgeJson.Read<DebuggerOpen>(payload));
                break;
            case BridgeMethods.DebuggerData:
                FindTunnel(BridgeJson.Read<DebuggerData>(payload).TunnelId)?.FromTablet(payload.GetAttachment(0));
                break;
            case BridgeMethods.CloseDebugger:
                if (FindTunnel(BridgeJson.Read<DebuggerClosed>(payload).TunnelId) is { } tunnel)
                {
                    _ = Task.Run(tunnel.Dispose);
                }

                break;
        }
    }

    /// <summary>Stops the streams and the debugger's tunnels, and every program the tablet ran.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        List<BridgeStream> streams;
        List<BridgeDebugTunnel> tunnels;
        List<BridgeApp> apps;
        lock (_gate)
        {
            streams = [.. _streams.Values];
            tunnels = [.. _tunnels.Values];
            apps = [.. _apps.Values];
            _streams.Clear();
            _tunnels.Clear();
            _apps.Clear();
            _notifier?.Complete();
            _notifier = null;
            _connection = null;
        }

        _input.Writer.TryComplete();
        foreach (var stream in streams)
        {
            stream.Dispose();
        }

        foreach (var tunnel in tunnels)
        {
            tunnel.Dispose();
        }

        foreach (var app in apps)
        {
            app.Stop();
            app.Dispose();
        }

        _workspace.Dispose();
    }

    /// <summary>The tablet left: its streams and debug sessions end; its programs keep running for when it is back.</summary>
    private void Detach(LinkConnection connection)
    {
        List<BridgeStream> streams;
        List<BridgeDebugTunnel> tunnels;
        lock (_gate)
        {
            if (!ReferenceEquals(_connection, connection))
            {
                return;
            }

            _connection = null;
            _notifier?.Complete();
            _notifier = null;
            streams = [.. _streams.Values];
            tunnels = [.. _tunnels.Values];
            _streams.Clear();
            _tunnels.Clear();
        }

        _ = Task.Run(() =>
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }

            foreach (var tunnel in tunnels)
            {
                tunnel.Dispose();
            }
        });
        _server.RaiseChanged();
    }

    private void Post(string method, System.Text.Json.JsonElement body)
    {
        OrderedNotifier? notifier;
        lock (_gate)
        {
            notifier = _notifier;
        }

        notifier?.Post(method, body);
    }

    private void SetActivity(string? activity)
    {
        lock (_gate)
        {
            _activity = activity;
        }

        SendStatus();
        _server.RaiseChanged();
    }

    private BridgePathMapper Paths()
    {
        lock (_gate)
        {
            return _paths ?? throw new LinkRemoteException(LinkErrorCodes.NoWorkspace, "Send the workspace to the Windows Bridge first.");
        }
    }

    private async Task<LinkReply> WithWorkspaceAsync(Func<LinkReply> work, CancellationToken cancellationToken)
    {
        await _workspace.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _workspace.Release();
        }
    }

    private async Task<LinkReply> BuildAsync(BuildRequest request, CancellationToken cancellationToken)
    {
        var paths = Paths();
        await _workspace.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetActivity($"Building {Path.GetFileNameWithoutExtension(request.ProjectPath)}");
            var outcome = await BridgeBuilder.BuildAsync(
                request,
                paths,
                _server.Toolchains.Tools,
                line => Post(BridgeMethods.BuildOutput, BridgeJson.ToJson(new BuildOutputLine(request.BuildId, line))),
                cancellationToken).ConfigureAwait(false);

            // The output arrives before the outcome, as it was written.
            if (CurrentNotifier() is { } notifier)
            {
                await notifier.FlushAsync().ConfigureAwait(false);
            }

            return LinkReply.Of(outcome, BridgeJson.Info<BuildOutcome>());
        }
        finally
        {
            _workspace.Release();
            SetActivity(null);
        }
    }

    private OrderedNotifier? CurrentNotifier()
    {
        lock (_gate)
        {
            return _notifier;
        }
    }

    private AppStarted Run(RunRequest request)
    {
        var paths = Paths();
        var program = paths.ToHost(request.Program.Executable ?? request.Program.Path)
            ?? throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The program is not in the workspace the bridge built.");
        if (!File.Exists(program))
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, $"{Path.GetFileName(program)} is not built on the PC. Build it again.");
        }

        var projectDirectory = Path.GetDirectoryName(paths.ToHost(request.ProjectPath)) ?? Path.GetDirectoryName(program)!;
        var isAssembly = program.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var dotnet = _server.Toolchains.Tools.DotNet ?? "dotnet";
        var start = new BridgeProcessStart(
            isAssembly ? dotnet : program,
            isAssembly ? [program, .. request.Arguments] : request.Arguments,
            request.Program.IsDotNet ? Path.GetDirectoryName(program)! : projectDirectory);
        BridgeProcess process;
        try
        {
            process = BridgeProcess.Start(start);
        }
        catch (BridgeProcessException ex)
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, ex.Message);
        }

        var name = Path.GetFileNameWithoutExtension(request.Program.Path);
        var app = AddApp(name, request.ProjectPath, process.Id, process, isDebugging: false);
        process.OutputLine += (_, line) => Post(BridgeMethods.AppOutput, BridgeJson.ToJson(new AppOutput(app.Id, paths.ToClientText(line), false)));
        process.ErrorLine += (_, line) => Post(BridgeMethods.AppOutput, BridgeJson.ToJson(new AppOutput(app.Id, paths.ToClientText(line), true)));
        process.BeginReading();
        return new AppStarted(app.Id, app.Name, app.ProcessId, app.ProjectPath, false);
    }

    private BridgeApp AddApp(string name, string projectPath, int processId, BridgeProcess? process, bool isDebugging)
    {
        BridgeApp app;
        lock (_gate)
        {
            app = new BridgeApp(++_nextApp, name, projectPath, processId, process, isDebugging, Desktop);
            _apps[app.Id] = app;
        }

        app.WindowsChanged += (_, _) =>
        {
            Post(BridgeMethods.AppWindows, BridgeJson.ToJson(app.WindowList));
            _server.RaiseChanged();
        };
        _ = app.Exited.ContinueWith(t => OnAppExited(app, t.Result), TaskScheduler.Default);
        Post(BridgeMethods.AppStarted, BridgeJson.ToJson(new AppStarted(app.Id, app.Name, app.ProcessId, app.ProjectPath, isDebugging)));
        SendStatus();
        _server.RaiseChanged();
        return app;
    }

    private void OnAppExited(BridgeApp app, int? exitCode)
    {
        List<BridgeStream> streams;
        lock (_gate)
        {
            _apps.Remove(app.Id);
            streams = [.. _streams.Values.Where(s => s.AppId == app.Id)];
            foreach (var stream in streams)
            {
                _streams.Remove(stream.Id);
            }
        }

        foreach (var stream in streams)
        {
            stream.Dispose();
        }

        app.Dispose();
        Post(BridgeMethods.AppExited, BridgeJson.ToJson(new AppExited(app.Id, exitCode)));
        SendStatus();
        _server.RaiseChanged();
    }

    private StreamStarted StartStream(StreamRequest request)
    {
        BridgeApp app;
        LinkConnection connection;
        List<BridgeStream> replaced;
        lock (_gate)
        {
            app = _apps.GetValueOrDefault(request.AppId)
                ?? throw new LinkRemoteException(LinkErrorCodes.Failed, "The program is not running any more.");
            connection = _connection ?? throw new LinkConnectionLostException("The tablet is not connected.");
            replaced = [.. _streams.Values.Where(s => s.WindowId == request.WindowId)];
            foreach (var stream in replaced)
            {
                _streams.Remove(stream.Id);
            }
        }

        foreach (var stream in replaced)
        {
            stream.Dispose();
        }

        if (!app.Owns(request.WindowId))
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, "That window is closed.");
        }

        BridgeStream started;
        lock (_gate)
        {
            var id = ++_nextStream;
            started = new BridgeStream(
                id,
                app.Id,
                request.WindowId,
                request.MaxWidth,
                request.MaxHeight,
                Desktop,
                (header, frame) => connection.NotifyAsync(BridgeMethods.Frame, BridgeJson.ToJson(header), [frame]));
            _streams[id] = started;
        }

        started.Ended += (_, _) =>
        {
            lock (_gate)
            {
                _streams.Remove(started.Id);
            }
        };
        return new StreamStarted(started.Id, started.CaptureMethod);
    }

    private void StopStream(int streamId)
    {
        BridgeStream? stream;
        lock (_gate)
        {
            _streams.Remove(streamId, out stream);
        }

        if (stream is not null)
        {
            _ = Task.Run(stream.Dispose);
        }
    }

    private BridgeStream? FindStream(int streamId)
    {
        lock (_gate)
        {
            return _streams.GetValueOrDefault(streamId);
        }
    }

    private BridgeDebugTunnel? FindTunnel(int tunnelId)
    {
        lock (_gate)
        {
            return _tunnels.GetValueOrDefault(tunnelId);
        }
    }

    private void Resize(ResizeRequest request)
    {
        if (!OwnsWindow(request.AppId, request.WindowId))
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, "That window is closed.");
        }

        Desktop.Resize(request.WindowId, Math.Clamp(request.Width, 120, 8192), Math.Clamp(request.Height, 80, 8192));
        WakeStreams(request.WindowId);
    }

    private bool OwnsWindow(int appId, long windowId)
    {
        lock (_gate)
        {
            return _apps.GetValueOrDefault(appId)?.Owns(windowId) == true;
        }
    }

    private void WakeStreams(long windowId)
    {
        List<BridgeStream> streams;
        lock (_gate)
        {
            streams = [.. _streams.Values.Where(s => s.WindowId == windowId)];
        }

        foreach (var stream in streams)
        {
            stream.Wake();
        }
    }

    /// <summary>Plays the tablet's input into the windows, in order, off the connection's reading thread.</summary>
    private async Task PlayInputAsync()
    {
        await foreach (var batch in _input.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (!OwnsWindow(batch.AppId, batch.WindowId))
            {
                continue;
            }

            try
            {
                Desktop.SendInput(batch.WindowId, batch.Events);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // A window that closed while the input was on its way.
            }

            WakeStreams(batch.WindowId);
        }
    }

    private void OpenDebugger(DebuggerOpen request)
    {
        BridgePathMapper? paths;
        LinkConnection? connection;
        lock (_gate)
        {
            paths = _paths;
            connection = _connection;
        }

        if (connection is null)
        {
            return;
        }

        var tools = _server.Toolchains.Tools;
        if (paths is null || tools.Netcoredbg is not { } netcoredbg)
        {
            Post(BridgeMethods.CloseDebugger, BridgeJson.ToJson(new DebuggerClosed(
                request.TunnelId,
                paths is null ? "Send the workspace to the Windows Bridge first." : "netcoredbg is not installed on the PC. Install it from the Windows Bridge window on the PC.")));
            return;
        }

        var mapper = paths;
        BridgeDebugTunnel tunnel;
        try
        {
            var directory = mapper.ToHost(request.WorkingDirectory) is { } host && Directory.Exists(host) ? host : mapper.HostRoot;
            tunnel = new BridgeDebugTunnel(
                request.TunnelId,
                netcoredbg,
                tools.DotNet,
                directory,
                mapper,
                data => connection.NotifyAsync(BridgeMethods.DebuggerData, BridgeJson.ToJson(new DebuggerData(request.TunnelId)), [data]));
        }
        catch (BridgeProcessException ex)
        {
            Post(BridgeMethods.CloseDebugger, BridgeJson.ToJson(new DebuggerClosed(request.TunnelId, ex.Message)));
            return;
        }

        BridgeApp? debugged = null;
        tunnel.ProgramStarted += (_, started) => debugged = AddApp(started.Name, request.ProjectPath, started.ProcessId, null, isDebugging: true);
        tunnel.Closed += (_, reason) =>
        {
            lock (_gate)
            {
                _tunnels.Remove(request.TunnelId);
            }

            // Ending a debug session ends its program, as in Visual Studio.
            debugged?.Stop();
            debugged?.MarkExited(null);
            Post(BridgeMethods.CloseDebugger, BridgeJson.ToJson(new DebuggerClosed(request.TunnelId, reason)));
        };
        lock (_gate)
        {
            _tunnels[request.TunnelId] = tunnel;
        }
    }
}
