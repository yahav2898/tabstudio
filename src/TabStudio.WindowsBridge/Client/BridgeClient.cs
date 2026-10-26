using System.Collections.Concurrent;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Link;
using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Client;

/// <summary>What sending the workspace to the bridge changed there.</summary>
/// <param name="SentFiles">The files that were new or changed, on the tablet's paths.</param>
public sealed record WorkspaceSyncResult(IReadOnlyList<string> SentFiles);

/// <summary>A frame of a streamed window, as it arrived.</summary>
/// <param name="Header">Which stream and frame it is, and the window's size.</param>
/// <param name="Data">The encoded frame, for a <see cref="Frames.FrameDecoder"/>.</param>
public sealed record BridgeFrame(FrameHeader Header, byte[] Data);

/// <summary>
/// The tablet's side of the Windows Bridge: typed requests to a paired PC, and its notifications as events. Create
/// it, pass it as the handler when connecting with <see cref="TabStudio.Link.Networking.LinkClient"/>, then
/// <see cref="Attach"/> the connection.
/// </summary>
/// <remarks>
/// Events are raised on the connection's reading thread, in the order the bridge sent them; handlers must be quick
/// and move to the UI thread themselves. Every method may be called from any thread.
/// </remarks>
public sealed class BridgeClient : ILinkMessageHandler
{
    /// <summary>The most bytes of files sent in one upload request.</summary>
    private const long UploadBatchLength = 8L * 1024 * 1024;

    private readonly ConcurrentDictionary<string, FileHash> _hashes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, Action<string>> _buildOutput = new();
    private readonly ConcurrentDictionary<int, BridgeDebugProcess> _debuggers = new();
    private LinkConnection? _connection;
    private OrderedSender? _sender;
    private int _nextBuild;
    private int _nextTunnel;
    private int _nextStream;

    /// <summary>Raised when a program started on the PC, also when the debugger started it.</summary>
    public event EventHandler<AppStarted>? AppStarted;

    /// <summary>Raised with what a program wrote to its console.</summary>
    public event EventHandler<AppOutput>? AppOutput;

    /// <summary>Raised when a program's windows changed.</summary>
    public event EventHandler<AppWindows>? AppWindowsChanged;

    /// <summary>Raised when a program exited.</summary>
    public event EventHandler<AppExited>? AppExited;

    /// <summary>Raised with each frame of a streamed window.</summary>
    public event EventHandler<BridgeFrame>? FrameReceived;

    /// <summary>Raised when a stream ended on its own, as when its window could not be captured any more.</summary>
    public event EventHandler<StreamEnded>? StreamEnded;

    /// <summary>Raised when the bridge's status changed: the PC's state, its work, its programs.</summary>
    public event EventHandler<BridgeStatus>? StatusChanged;

    /// <summary>The connection to the bridge, once attached.</summary>
    public LinkConnection? Connection => Volatile.Read(ref _connection);

    /// <summary>Whether the bridge is connected now.</summary>
    public bool IsConnected => Connection is { IsOpen: true };

    /// <summary>
    /// Starts using a connection that this client handles the notifications of. A client outlives its connections:
    /// after a reconnection, the new one is attached and the events go on.
    /// </summary>
    /// <param name="connection">The authenticated connection to the bridge.</param>
    public void Attach(LinkConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var previous = Interlocked.Exchange(ref _connection, connection);
        Interlocked.Exchange(ref _sender, new OrderedSender(connection))?.Complete();
        if (previous is not null)
        {
            CloseDebuggers("The Windows Bridge reconnected; the debug session ended.");
        }

        connection.Closed += (_, e) => OnClosed(connection, e.Error);
        if (!connection.IsOpen)
        {
            OnClosed(connection, connection.CloseError);
        }
    }

    /// <summary>What the PC is and what it can build.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public Task<BridgeInfo> GetInfoAsync(CancellationToken cancellationToken = default) =>
        RequestAsync<BridgeInfo>(BridgeMethods.Info, null, cancellationToken);

    /// <summary>Sends the workspace's files to the bridge: only those its mirror lacks or has in another version.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="sources">The editor's text of C# and XAML files.</param>
    /// <param name="progress">Receives what is being sent, such as <c>Sending 12 files</c>.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The files that were sent.</returns>
    public async Task<WorkspaceSyncResult> SyncWorkspaceAsync(Workspace workspace, ISourceTextProvider sources, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(sources);
        var connection = RequireConnection();
        var content = await Task.Run(() => LinkWorkspaceContent.Collect(workspace, sources, _hashes, cancellationToken), cancellationToken).ConfigureAwait(false);
        var prepare = new PrepareWorkspaceRequest(workspace.Name, content.Root, char.ToString(Path.DirectorySeparatorChar), content.Files);
        var answer = await connection.RequestAsync(BridgeMethods.PrepareWorkspace, LinkLanguageJson.ToJson(prepare), cancellationToken: cancellationToken).ConfigureAwait(false);
        var missing = answer.Read(LinkLanguageJson.Info<PrepareWorkspaceResponse>()).Missing;

        var prefix = content.Root.EndsWith(Path.DirectorySeparatorChar) ? content.Root : content.Root + Path.DirectorySeparatorChar;
        var sent = new List<string>(missing.Count);
        var position = 0;
        while (position < missing.Count)
        {
            var batch = new List<int>();
            var contents = new List<ReadOnlyMemory<byte>>();
            long length = 0;
            while (position < missing.Count && (batch.Count == 0 || length < UploadBatchLength))
            {
                var index = missing[position++];
                if (index < 0 || index >= content.Files.Count)
                {
                    throw new LinkProtocolException($"The bridge asked for file {index}, which does not exist.");
                }

                var bytes = await Task.Run(() => content.Read(index), cancellationToken).ConfigureAwait(false);
                batch.Add(index);
                contents.Add(bytes);
                length += bytes.Length;
                sent.Add(prefix + content.Files[index].Path.Replace('/', Path.DirectorySeparatorChar));
            }

            progress?.Report($"Sending {workspace.Name} to the PC: {position} of {missing.Count} files");
            await connection.RequestAsync(BridgeMethods.UploadFiles, LinkLanguageJson.ToJson(new UploadFilesRequest(batch)), contents, cancellationToken).ConfigureAwait(false);
        }

        await connection.RequestAsync(BridgeMethods.CommitWorkspace, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new WorkspaceSyncResult(sent);
    }

    /// <summary>Builds a project on the PC with its own tools.</summary>
    /// <param name="projectPath">The project file.</param>
    /// <param name="configuration"><c>Debug</c> or <c>Release</c>.</param>
    /// <param name="skipReferences">Whether only this project changed, so the projects it references need no build.</param>
    /// <param name="output">Receives each line of the build's output, in order, on the connection's thread.</param>
    /// <param name="cancellationToken">Stops the build.</param>
    public async Task<BuildOutcome> BuildAsync(string projectPath, string configuration, bool skipReferences, Action<string> output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        var id = Interlocked.Increment(ref _nextBuild);
        _buildOutput[id] = output;
        try
        {
            return await RequestAsync<BuildOutcome>(BridgeMethods.Build, BridgeJson.ToJson(new BuildRequest(id, projectPath, configuration, skipReferences)), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _buildOutput.TryRemove(id, out _);
        }
    }

    /// <summary>Programs and verifies a PIC image with the paired PC's licensed MPLAB IPE.</summary>
    /// <param name="projectPath">MPLAB descriptor path.</param>
    /// <param name="firmwarePath">HEX path returned by a Bridge build.</param>
    /// <param name="device">Device selected in the project.</param>
    /// <param name="tool">IPE tool id.</param>
    /// <param name="serial">Optional tool serial number.</param>
    /// <param name="output">Output lines.</param>
    /// <param name="cancellationToken">Stops programming.</param>
    public async Task<BuildOutcome> ProgramPicAsync(string projectPath, string firmwarePath, string device, string tool, string? serial, Action<string> output, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextBuild);
        _buildOutput[id] = output;
        try
        {
            return await RequestAsync<BuildOutcome>(BridgeMethods.ProgramPic, BridgeJson.ToJson(new PicProgramRequest(id, projectPath, firmwarePath, device, tool, serial)), cancellationToken).ConfigureAwait(false);
        }
        finally { _buildOutput.TryRemove(id, out _); }
    }

    /// <summary>Opens MPLAB X for vendor on-chip debugging on the paired PC.</summary>
    /// <param name="projectPath">Synchronized MPLAB project descriptor.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public Task<AppStarted> OpenMplabAsync(string projectPath, CancellationToken cancellationToken = default) =>
        RequestAsync<AppStarted>(BridgeMethods.OpenMplab, BridgeJson.ToJson(new OpenMplabRequest(projectPath)), cancellationToken);

    /// <summary>Runs a built program on the PC.</summary>
    /// <param name="projectPath">The project it was built from.</param>
    /// <param name="program">The program.</param>
    /// <param name="arguments">Its command-line arguments.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task<AppStarted> RunAsync(string projectPath, BuiltProgram program, IReadOnlyList<string>? arguments = null, CancellationToken cancellationToken = default) =>
        RequestAsync<AppStarted>(BridgeMethods.Run, BridgeJson.ToJson(new RunRequest(projectPath, program, arguments ?? [])), cancellationToken);

    /// <summary>Stops a program on the PC.</summary>
    /// <param name="appId">The program.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task StopAsync(int appId, CancellationToken cancellationToken = default) =>
        RequireConnection().RequestAsync(BridgeMethods.Stop, BridgeJson.ToJson(new AppReference(appId)), cancellationToken: cancellationToken);

    /// <summary>
    /// Starts streaming a window under a number from <see cref="NewStreamId"/>; its frames arrive through
    /// <see cref="FrameReceived"/>, the first one possibly before this answer. <see cref="BridgeStreamReceiver.StartAsync"/>
    /// receives them from the first.
    /// </summary>
    /// <param name="streamId">The number for the stream, from <see cref="NewStreamId"/>.</param>
    /// <param name="appId">The program.</param>
    /// <param name="windowId">The window.</param>
    /// <param name="maxWidth">The most pixels wide to show it.</param>
    /// <param name="maxHeight">The most pixels high to show it.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task<StreamStarted> StartStreamAsync(int streamId, int appId, long windowId, int maxWidth, int maxHeight, CancellationToken cancellationToken = default) =>
        RequestAsync<StreamStarted>(BridgeMethods.StartStream, BridgeJson.ToJson(new StreamRequest(streamId, appId, windowId, maxWidth, maxHeight)), cancellationToken);

    /// <summary>A new number for a stream, unique for this tablet.</summary>
    public int NewStreamId() => Interlocked.Increment(ref _nextStream);

    /// <summary>Stops streaming a window.</summary>
    /// <param name="streamId">The stream.</param>
    public void StopStream(int streamId) => Post(BridgeMethods.StopStream, BridgeJson.ToJson(new StreamReference(streamId)));

    /// <summary>Tells the bridge a frame was drawn, which paces the stream.</summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="sequence">The frame.</param>
    public void FrameShown(int streamId, long sequence) => Post(BridgeMethods.FrameShown, BridgeJson.ToJson(new FrameShown(streamId, sequence)));

    /// <summary>Sends input to a program's window, in order.</summary>
    /// <param name="appId">The program.</param>
    /// <param name="windowId">The window.</param>
    /// <param name="events">The input.</param>
    public void SendInput(int appId, long windowId, IReadOnlyList<BridgeInputEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count > 0)
        {
            Post(BridgeMethods.Input, BridgeJson.ToJson(new InputBatch(appId, windowId, events)));
        }
    }

    /// <summary>Resizes a program's window.</summary>
    /// <param name="appId">The program.</param>
    /// <param name="windowId">The window.</param>
    /// <param name="width">The new width in the PC's pixels.</param>
    /// <param name="height">The new height in the PC's pixels.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task ResizeAsync(int appId, long windowId, int width, int height, CancellationToken cancellationToken = default) =>
        RequireConnection().RequestAsync(BridgeMethods.Resize, BridgeJson.ToJson(new ResizeRequest(appId, windowId, width, height)), cancellationToken: cancellationToken);

    /// <summary>The PC's clipboard text.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<string?> GetClipboardAsync(CancellationToken cancellationToken = default) =>
        (await RequestAsync<ClipboardText>(BridgeMethods.GetClipboard, null, cancellationToken).ConfigureAwait(false)).Text;

    /// <summary>Puts text on the PC's clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task SetClipboardAsync(string text, CancellationToken cancellationToken = default) =>
        RequireConnection().RequestAsync(BridgeMethods.SetClipboard, BridgeJson.ToJson(new ClipboardText(text)), cancellationToken: cancellationToken);

    /// <summary>
    /// Starts netcoredbg on the PC through a tunnel: the tablet's debugger talks to it as to a local adapter, and the
    /// paths in its messages are mapped on the way.
    /// </summary>
    /// <param name="workingDirectory">The folder netcoredbg starts in, on the tablet's paths.</param>
    /// <param name="projectPath">The project whose program it debugs.</param>
    /// <returns>netcoredbg's standard streams on the tablet.</returns>
    public BridgeDebugProcess OpenDebugger(string workingDirectory, string projectPath)
    {
        var id = Interlocked.Increment(ref _nextTunnel);
        var process = new BridgeDebugProcess(this, id);
        _debuggers[id] = process;
        Post(BridgeMethods.OpenDebugger, BridgeJson.ToJson(new DebuggerOpen(id, workingDirectory, projectPath)));
        return process;
    }

    /// <inheritdoc />
    Task<LinkReply> ILinkMessageHandler.HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken) =>
        throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, "The tablet answers no requests from the Windows Bridge.");

    /// <inheritdoc />
    void ILinkMessageHandler.HandleNotification(string method, LinkPayload payload)
    {
        switch (method)
        {
            case BridgeMethods.BuildOutput:
                var line = BridgeJson.Read<BuildOutputLine>(payload);
                if (_buildOutput.TryGetValue(line.BuildId, out var output))
                {
                    output(line.Text);
                }

                break;
            case BridgeMethods.Frame:
                FrameReceived?.Invoke(this, new BridgeFrame(BridgeJson.Read<FrameHeader>(payload), payload.GetAttachment(0)));
                break;
            case BridgeMethods.StreamEnded:
                StreamEnded?.Invoke(this, BridgeJson.Read<StreamEnded>(payload));
                break;
            case BridgeMethods.AppStarted:
                AppStarted?.Invoke(this, BridgeJson.Read<AppStarted>(payload));
                break;
            case BridgeMethods.AppOutput:
                AppOutput?.Invoke(this, BridgeJson.Read<AppOutput>(payload));
                break;
            case BridgeMethods.AppWindows:
                AppWindowsChanged?.Invoke(this, BridgeJson.Read<AppWindows>(payload));
                break;
            case BridgeMethods.AppExited:
                AppExited?.Invoke(this, BridgeJson.Read<AppExited>(payload));
                break;
            case BridgeMethods.Status:
                StatusChanged?.Invoke(this, BridgeJson.Read<BridgeStatus>(payload));
                break;
            case BridgeMethods.DebuggerData:
                if (_debuggers.TryGetValue(BridgeJson.Read<DebuggerData>(payload).TunnelId, out var debugger))
                {
                    debugger.Receive(payload.GetAttachment(0));
                }

                break;
            case BridgeMethods.CloseDebugger:
                var closed = BridgeJson.Read<DebuggerClosed>(payload);
                if (_debuggers.TryRemove(closed.TunnelId, out var ended))
                {
                    ended.OnClosed(closed.Reason);
                }

                break;
        }
    }

    /// <summary>Sends the debugger's bytes through its tunnel.</summary>
    internal void SendDebuggerData(int tunnelId, byte[] data) =>
        Post(BridgeMethods.DebuggerData, BridgeJson.ToJson(new DebuggerData(tunnelId)), data);

    /// <summary>Closes a debugger's tunnel, which ends netcoredbg on the PC.</summary>
    internal void CloseDebugger(int tunnelId)
    {
        if (_debuggers.TryRemove(tunnelId, out _))
        {
            Post(BridgeMethods.CloseDebugger, BridgeJson.ToJson(new DebuggerClosed(tunnelId, null)));
        }
    }

    private void Post(string method, System.Text.Json.JsonElement body, byte[]? attachment = null) =>
        (Volatile.Read(ref _sender) ?? throw new LinkConnectionLostException("The Windows Bridge is not connected.")).Post(method, body, attachment);

    private LinkConnection RequireConnection() =>
        Connection is { IsOpen: true } connection ? connection : throw new LinkConnectionLostException("The Windows Bridge is not connected.");

    private async Task<T> RequestAsync<T>(string method, System.Text.Json.JsonElement? body, CancellationToken cancellationToken)
    {
        var payload = await RequireConnection().RequestAsync(method, body, cancellationToken: cancellationToken).ConfigureAwait(false);
        return BridgeJson.Read<T>(payload);
    }

    private void OnClosed(LinkConnection connection, Exception? error)
    {
        if (!ReferenceEquals(Volatile.Read(ref _connection), connection))
        {
            return;
        }

        Volatile.Read(ref _sender)?.Complete();
        CloseDebuggers(error is null ? "The Windows Bridge disconnected." : $"The Windows Bridge was lost: {error.Message}");
    }

    private void CloseDebuggers(string reason)
    {
        foreach (var id in _debuggers.Keys)
        {
            if (_debuggers.TryRemove(id, out var debugger))
            {
                debugger.OnClosed(reason);
            }
        }
    }
}
