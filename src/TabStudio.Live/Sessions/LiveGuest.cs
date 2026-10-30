using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>
/// A device that joined a session: pairs with the host by its QR code or code, copies the shared files into a private
/// folder so its own IDE and IntelliSense work on them, edits the shared documents with everyone, and reconnects by
/// itself when Wi-Fi drops, sending the edits it made meanwhile (<c>LiveGuest.*.cs</c>).
/// </summary>
/// <remarks>
/// State lives on <see cref="LiveSession.Dispatcher"/>'s thread, where events are raised. Members that wait for the
/// host may be called from it and come back to it.
/// </remarks>
public sealed partial class LiveGuest : LiveSession, IAsyncDisposable
{
    /// <summary>How many files one request fetches.</summary>
    private const int FilesPerRequest = 128;

    private readonly LiveGuestOptions _options;
    private readonly SemaphoreSlim _mirrorGate = new(1, 1);
    private readonly Dictionary<string, Task<LiveDocument?>> _opening = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TextOperation>> _early = new(StringComparer.Ordinal);
    private LinkClientSession? _link;
    private LiveSender? _sender;
    private GuestLink? _handler;
    private LiveMirror? _mirror;
    private LinkPairedDevice? _host;
    private CancellationTokenSource? _reconnect;
    private LiveGuestState _state;
    private string? _error;
    private int _disposed;

    /// <summary>Creates a guest; nothing connects until it joins a session.</summary>
    /// <param name="options">Who the guest is, and where it keeps copies of shared workspaces.</param>
    /// <param name="dispatcher">The session's thread: the UI thread in the app.</param>
    public LiveGuest(LiveGuestOptions options, ILiveDispatcher dispatcher)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Raised on the session's thread when <see cref="State"/> or <see cref="Error"/> changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised on the session's thread while the shared files are copied: how many of how many are done.</summary>
    public event EventHandler<(int Done, int Total)>? CopyProgress;

    /// <summary>Raised on the session's thread when files of the copy changed because they changed on the host.</summary>
    public event EventHandler<IReadOnlyList<string>>? FilesChanged;

    /// <summary>Raised on the session's thread when the host refused this device's edits of a document and it was loaded again.</summary>
    public event EventHandler<LiveDocument>? DocumentReloaded;

    /// <inheritdoc />
    public override bool IsHost => false;

    /// <summary>Where the guest stands with the session.</summary>
    public LiveGuestState State => _state;

    /// <summary>Why joining failed, why the connection was lost, or why the session ended.</summary>
    public string? Error => _error;

    /// <summary>What to open in the IDE: the solution in the copy, or the copy's folder; <see langword="null"/> before joining.</summary>
    public string? EntryPath { get; private set; }

    /// <summary>The files the host shares.</summary>
    public IReadOnlyList<LiveFileEntry> Files { get; private set; } = [];

    /// <summary>The round trip to the host, while connected.</summary>
    public TimeSpan? Latency => _link?.Connection.Latency;

    /// <summary>
    /// Joins a session: pairs with the host by its scanned QR code or typed code, copies the shared files and is in the
    /// session. Call on the session's thread.
    /// </summary>
    /// <param name="target">The host, and the session's code.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the device joined; <see cref="Error"/> says why not.</returns>
    public async Task<bool> JoinAsync(LinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        if (_state is LiveGuestState.Joining or LiveGuestState.Joined or LiveGuestState.Reconnecting)
        {
            throw new InvalidOperationException("This device is in a session already. Leave it first.");
        }

        SetState(LiveGuestState.Joining, null);
        try
        {
            await ConnectAsync(target, rejoin: false, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (IsConnectFailure(ex))
        {
            await Dispatcher.InvokeAsync(() => SetState(LiveGuestState.Off, Describe(ex))).ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>Leaves the session: the host is told, and the device stops reconnecting. Call on the session's thread.</summary>
    public async Task LeaveAsync()
    {
        if (_sender is { } sender)
        {
            sender.Notify(LiveMethods.Leave);
            await Task.WhenAny(sender.FlushAsync(), Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }

        await Dispatcher.InvokeAsync(() => EndSession("You left the session.")).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes this device's copy of the shared workspace, so nothing the host shared stays on it. Call once the IDE
    /// closed the copy, after the session ended.
    /// </summary>
    public void DeleteCopy()
    {
        _mirror?.Delete();
        _mirror = null;
    }

    /// <inheritdoc />
    public override Task<LiveDocument?> OpenDocumentAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (LiveWorkspacePaths.Normalize(path) is not { } normalized)
        {
            return Task.FromResult<LiveDocument?>(null);
        }

        if (FindDocument(normalized) is { } open)
        {
            return Task.FromResult<LiveDocument?>(open);
        }

        if (_opening.TryGetValue(normalized, out var opening))
        {
            return opening;
        }

        if (_link is not { } link || _mirror is not { } mirror || _state != LiveGuestState.Joined)
        {
            return Task.FromResult<LiveDocument?>(null);
        }

        var task = OpenFromHostAsync(link, mirror, normalized, cancellationToken);
        if (!task.IsCompleted)
        {
            _opening[normalized] = task;
        }

        return task;
    }

    /// <inheritdoc />
    public override void CloseDocument(LiveDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // While disconnected the document stays, so the edits made meanwhile still reach the host.
        if (_sender is { } sender && _state == LiveGuestState.Joined)
        {
            RemoveDocument(document.Path);
            sender.Notify(LiveMethods.Close, LiveJson.Body(new LivePathRequest(document.Path), LiveJsonContext.Default.LivePathRequest));
        }
    }

    /// <summary>Saves a document on the host, once the host has this device's edits of it. Call on the session's thread.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="LinkRemoteException">The host refused, such as for a guest that only watches.</exception>
    /// <exception cref="InvalidOperationException">The device is not connected to the host.</exception>
    public Task SaveAsync(LiveDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var request = new LiveSaveRequest(document.Path, document.Text.Version.ToDictionary());
        return RequestAsync(LiveMethods.Save, LiveJson.Body(request, LiveJsonContext.Default.LiveSaveRequest), cancellationToken);
    }

    /// <summary>Asks the host's debugger to continue, step or pause. Call on the session's thread.</summary>
    /// <param name="command">What to do.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="LinkRemoteException">The host refused: it has not allowed this guest to step, or nothing is paused.</exception>
    public Task ControlDebuggerAsync(LiveDebugCommand command, CancellationToken cancellationToken = default) =>
        RequestAsync(LiveMethods.DebugCommand, LiveJson.Body(new LiveDebugCommandRequest(command), LiveJsonContext.Default.LiveDebugCommandRequest), cancellationToken);

    /// <summary>Sets or removes a breakpoint on the host. Call on the session's thread.</summary>
    /// <param name="path">The file, relative to the workspace's folder.</param>
    /// <param name="line">The one-based line.</param>
    /// <param name="condition">When it stops, or <see langword="null"/>.</param>
    /// <param name="remove">Whether to remove the breakpoint on that line instead.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task SetBreakpointAsync(string path, int line, string? condition, bool remove, CancellationToken cancellationToken = default) =>
        RequestAsync(LiveMethods.Breakpoint, LiveJson.Body(new LiveBreakpointRequest(path, line, condition, remove), LiveJsonContext.Default.LiveBreakpointRequest), cancellationToken);

    /// <summary>Reads the members or items of a value of the host's paused program.</summary>
    /// <param name="pauseId">The pause.</param>
    /// <param name="path">The value's path.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<IReadOnlyList<TabStudio.Swarm.SwarmVariable>> GetVariablesAsync(long pauseId, string path, CancellationToken cancellationToken = default)
    {
        var connection = Connection();
        var answer = await connection.RequestAsync(
            LiveMethods.Variables,
            LiveJson.Body(new LiveVariablesRequest(pauseId, path), LiveJsonContext.Default.LiveVariablesRequest),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return answer.Read(LiveJsonContext.Default.LiveVariablesResponse).Variables;
    }

    /// <summary>Evaluates a Watch or DataTip expression in the host's paused program.</summary>
    /// <param name="pauseId">The pause.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<LiveEvaluation> EvaluateAsync(long pauseId, string expression, CancellationToken cancellationToken = default)
    {
        var connection = Connection();
        var answer = await connection.RequestAsync(
            LiveMethods.Evaluate,
            LiveJson.Body(new LiveEvaluateRequest(pauseId, expression), LiveJsonContext.Default.LiveEvaluateRequest),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return answer.Read(LiveJsonContext.Default.LiveEvaluation);
    }

    /// <summary>Types into the host's Terminal; the host ignores it unless it allows this guest to. Call on the session's thread.</summary>
    /// <param name="input">The bytes, as the Terminal's keys encode them.</param>
    public void WriteTerminal(ReadOnlyMemory<byte> input)
    {
        if (!input.IsEmpty)
        {
            _sender?.Notify(LiveMethods.TerminalInput, null, input);
        }
    }

    /// <summary>Raises or lowers this guest's hand. Call on the session's thread.</summary>
    /// <param name="raised">Whether the hand is up.</param>
    public void RaiseHand(bool raised) =>
        _sender?.Notify(LiveMethods.Hand, LiveJson.Body(new LiveHandNotice(raised), LiveJsonContext.Default.LiveHandNotice));

    /// <summary>Leaves the session and stops reconnecting.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_state is LiveGuestState.Joined or LiveGuestState.Reconnecting)
        {
            await LeaveAsync().ConfigureAwait(false);
        }

        await Dispatcher.InvokeAsync(() => EndSession(null)).ConfigureAwait(false);
        _mirrorGate.Dispose();
    }

    /// <inheritdoc />
    internal override void SendEdits(LiveDocument document, IReadOnlyList<TextOperation> operations)
    {
        // While disconnected, edits stay in the document and go to the host when the device reconnects.
        _sender?.Notify(LiveMethods.Edit, LiveJson.Body(new LiveEditNotice(document.Path, operations), LiveJsonContext.Default.LiveEditNotice));
    }

    /// <inheritdoc />
    private protected override void SendPresence(LivePresence presence) =>
        _sender?.Notify(LiveMethods.Presence, LiveJson.Body(presence, LiveJsonContext.Default.LivePresence));

    private static bool IsConnectFailure(Exception error) =>
        error is LinkAuthenticationException or LinkUnreachableException or LinkRemoteException or LinkConnectionLostException
            or LinkProtocolException or IOException or SocketException or TimeoutException or OperationCanceledException or InvalidDataException;

    private static string Describe(Exception error) => error switch
    {
        TimeoutException => "The host did not answer in time. Check that both devices are on the same Wi-Fi.",
        OperationCanceledException => "Cancelled.",
        LinkRemoteException { Code: LinkErrorCodes.UnknownMethod } => "That device does not share a TabStudio Live session.",
        _ => error.Message,
    };

    /// <summary>The folder for one session's copy: under the host's fingerprint, named as the workspace.</summary>
    private string MirrorRoot(LinkFingerprint host, string workspace)
    {
        var hostFolder = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host.Value)))[..12];
        var name = new StringBuilder();
        foreach (var c in workspace)
        {
            name.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ' ' ? c : '-');
            if (name.Length == 48)
            {
                break;
            }
        }

        var folder = name.ToString().Trim('.', ' ', '-');
        return Path.Combine(_options.MirrorDirectory, hostFolder, folder.Length == 0 ? "Workspace" : folder);
    }

    private LinkConnection Connection() =>
        _link?.Connection is { IsOpen: true } connection ? connection : throw new InvalidOperationException("This device is not connected to the host now.");

    private async Task RequestAsync(string method, System.Text.Json.JsonElement body, CancellationToken cancellationToken)
    {
        var connection = Connection();
        await connection.RequestAsync(method, body, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private void SetState(LiveGuestState state, string? error)
    {
        _state = state;
        _error = error;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ends the session on this device: no more reconnecting, the connection closed. Runs on the session's thread.</summary>
    private void EndSession(string? reason)
    {
        _reconnect?.Cancel();
        _reconnect?.Dispose();
        _reconnect = null;
        var link = _link;
        _link = null;
        _handler = null;
        _ = _sender?.CompleteAsync();
        _sender = null;
        if (link is not null)
        {
            _ = link.DisposeAsync().AsTask();
        }

        if (_state != LiveGuestState.Off || reason is not null)
        {
            SetState(LiveGuestState.Ended, reason ?? _error);
            End(reason ?? "The session ended.");
        }
    }
}
