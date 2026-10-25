using System.Text.Json;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Coordinator;

/// <summary>
/// A phone or tablet in the swarm, as the coordinator sees it: who it is, its screen, what it does with the app, and
/// requests to its screen and its debugger. It stays the same object while the device reconnects.
/// </summary>
/// <remarks>Thread-safe. Its changes are announced by <see cref="SwarmCoordinator.DeviceChanged"/>.</remarks>
public sealed class SwarmDevice
{
    /// <summary>How long a request to a device's screen may take before it counts as unanswered.</summary>
    private static readonly TimeSpan ScreenTimeout = TimeSpan.FromSeconds(20);

    private readonly SwarmCoordinator _owner;
    private readonly Lock _gate = new();
    private LinkConnection? _connection;
    private string _name;
    private string _kind;
    private string _platform = string.Empty;
    private string? _security;
    private SwarmScreenInfo _screen = SwarmScreenInfo.Unknown;
    private SwarmDeviceState _state = SwarmDeviceState.Ready;
    private string? _detail;
    private string? _appName;
    private long _runId;
    private DateTimeOffset? _lastUpdated;
    private TimeSpan? _latency;
    private SwarmFault? _lastFault;
    private SwarmPause? _pause;

    internal SwarmDevice(SwarmCoordinator owner, LinkPeer peer)
    {
        _owner = owner;
        Fingerprint = peer.Fingerprint;
        _name = peer.Name;
        _kind = peer.Kind;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The device's identity, which stays the same across reconnections.</summary>
    public LinkFingerprint Fingerprint { get; }

    /// <summary>When it first joined in this session.</summary>
    public DateTimeOffset JoinedAt { get; }

    /// <summary>The device's name, such as <c>Pixel 9</c>.</summary>
    public string Name
    {
        get
        {
            lock (_gate)
            {
                return _name;
            }
        }
    }

    /// <summary>What it is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string Kind
    {
        get
        {
            lock (_gate)
            {
                return _kind;
            }
        }
    }

    /// <summary>Its system, such as <c>Android 14</c>.</summary>
    public string Platform
    {
        get
        {
            lock (_gate)
            {
                return _platform;
            }
        }
    }

    /// <summary>How the connection is encrypted, such as <c>TLS 1.3</c>, while connected.</summary>
    public string? Security
    {
        get
        {
            lock (_gate)
            {
                return _security;
            }
        }
    }

    /// <summary>How its screen looks to the app.</summary>
    public SwarmScreenInfo Screen
    {
        get
        {
            lock (_gate)
            {
                return _screen;
            }
        }
    }

    /// <summary>What it does with the app.</summary>
    public SwarmDeviceState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>More about <see cref="State"/>, such as why a build could not start.</summary>
    public string? Detail
    {
        get
        {
            lock (_gate)
            {
                return _detail;
            }
        }
    }

    /// <summary>The app it runs, or ran last.</summary>
    public string? AppName
    {
        get
        {
            lock (_gate)
            {
                return _appName;
            }
        }
    }

    /// <summary>The run it runs, or 0.</summary>
    public long RunId
    {
        get
        {
            lock (_gate)
            {
                return _runId;
            }
        }
    }

    /// <summary>When it last received a build.</summary>
    public DateTimeOffset? LastUpdated
    {
        get
        {
            lock (_gate)
            {
                return _lastUpdated;
            }
        }
    }

    /// <summary>The round trip to it, measured every few seconds while it is connected.</summary>
    public TimeSpan? Latency
    {
        get
        {
            lock (_gate)
            {
                return _latency;
            }
        }
    }

    /// <summary>The exception the app last crashed with on this device, with its stack trace.</summary>
    public SwarmFault? LastFault
    {
        get
        {
            lock (_gate)
            {
                return _lastFault;
            }
        }
    }

    /// <summary>Where the debugged app is paused on this device, or <see langword="null"/> while it runs.</summary>
    public SwarmPause? Pause
    {
        get
        {
            lock (_gate)
            {
                return _pause;
            }
        }
    }

    /// <summary>Whether the device is connected.</summary>
    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _connection is { IsOpen: true };
            }
        }
    }

    /// <summary>Whether the app runs on the device and can be paused, stepped and inspected.</summary>
    public bool IsDebugging => State is SwarmDeviceState.Running or SwarmDeviceState.Pausing or SwarmDeviceState.Paused
        && _owner.CurrentRun is { IsDebugging: true } run && run.Id == RunId;

    /// <summary>The connection, while connected.</summary>
    internal LinkConnection? Connection
    {
        get
        {
            lock (_gate)
            {
                return _connection is { IsOpen: true } open ? open : null;
            }
        }
    }

    /// <summary>Whether the device said it leaves, so losing it is not a failure.</summary>
    internal bool IsLeaving { get; set; }

    /// <summary>Replays an input on the device's screen, as if a person made it.</summary>
    /// <param name="input">The input.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the element was found and the input delivered; <see langword="false"/> while the device is offline.</returns>
    public async Task<bool> SendInputAsync(SwarmInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var answer = await AskScreenAsync(SwarmMethods.Input, SwarmJson.Body(input, SwarmJsonContext.Default.SwarmInput), cancellationToken).ConfigureAwait(false);
        return answer?.Read(SwarmJsonContext.Default.SwarmInputResponse).Delivered == true;
    }

    /// <summary>Takes a picture of what the app shows on the device.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A PNG image, or <see langword="null"/> while no app runs or the device is offline.</returns>
    public async Task<byte[]?> CaptureAsync(CancellationToken cancellationToken)
    {
        var answer = await AskScreenAsync(SwarmMethods.Capture, null, cancellationToken).ConfigureAwait(false);
        return answer is not null && answer.Read(SwarmJsonContext.Default.SwarmCaptureResponse).HasImage && answer.Attachments.Count > 0
            ? answer.GetAttachment(0)
            : null;
    }

    /// <summary>Runs the layout checks on what the app shows on the device.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The problems found; none while no app runs or the device is offline.</returns>
    public async Task<IReadOnlyList<SwarmLayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken)
    {
        var answer = await AskScreenAsync(SwarmMethods.Layout, null, cancellationToken).ConfigureAwait(false);
        return answer?.Read(SwarmJsonContext.Default.SwarmLayoutResponse).Issues ?? [];
    }

    /// <summary>Reads the text an element shows on the device's screen, for the checks of recorded tests.</summary>
    /// <param name="elementPath">The element.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The text, or <see langword="null"/> when there is no such element or the device is offline.</returns>
    public async Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(elementPath);
        var answer = await AskScreenAsync(SwarmMethods.ReadText, SwarmJson.Body(new SwarmTextRequest(elementPath), SwarmJsonContext.Default.SwarmTextRequest), cancellationToken).ConfigureAwait(false);
        return answer?.Read(SwarmJsonContext.Default.SwarmTextResponse).Text;
    }

    /// <summary>Shows a short message on the device's screen for a moment, such as an input that could not be delivered there.</summary>
    /// <param name="text">The message.</param>
    public async Task ShowNoticeAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Connection is not { } connection)
        {
            return;
        }

        try
        {
            await connection.NotifyAsync(SwarmMethods.Notice, SwarmJson.Body(new SwarmNotice(text), SwarmJsonContext.Default.SwarmNotice)).ConfigureAwait(false);
        }
        catch (LinkConnectionLostException)
        {
            // The device left; it has nothing to show the message on.
        }
    }

    /// <summary>Continues or steps the device's paused app, or cancels a pause it has not reached yet.</summary>
    /// <param name="mode">How it continues.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the app was paused, or pausing, and continued.</returns>
    public Task<bool> ResumeAsync(SwarmResumeMode mode, CancellationToken cancellationToken) => ResumeAsync(0, mode, cancellationToken);

    /// <summary>Pauses the device's app at its next statement (Break All).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="LinkConnectionLostException">The device is offline.</exception>
    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        var connection = Connection ?? throw Offline();
        await connection.RequestAsync(SwarmMethods.Pause, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    internal async Task<bool> ResumeAsync(long pause, SwarmResumeMode mode, CancellationToken cancellationToken)
    {
        var connection = Connection ?? throw Offline();
        var answer = await connection.RequestAsync(
            SwarmMethods.Resume,
            SwarmJson.Body(new SwarmResumeRequest(pause, mode), SwarmJsonContext.Default.SwarmResumeRequest),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return answer.Read(SwarmJsonContext.Default.SwarmResumeResponse).Resumed;
    }

    internal LinkConnectionLostException Offline() => new($"{Name} is not connected to the swarm.");

    /// <summary>Takes the device's connection and what it said when it joined.</summary>
    internal void Attach(LinkConnection connection, LinkPeer peer, SwarmHello hello)
    {
        lock (_gate)
        {
            _connection = connection;
            _name = hello.Name;
            _kind = hello.Kind;
            _platform = hello.Platform;
            _screen = hello.Screen;
            _security = peer.Security;
            _state = SwarmDeviceState.Ready;
            _detail = null;
            _pause = null;
        }

        IsLeaving = false;
    }

    /// <summary>Forgets a connection that closed, unless a newer one replaced it.</summary>
    /// <returns>Whether it was the device's connection.</returns>
    internal bool Detach(LinkConnection connection, out SwarmPause? pause)
    {
        lock (_gate)
        {
            pause = null;
            if (!ReferenceEquals(_connection, connection))
            {
                return false;
            }

            pause = _pause;
            _connection = null;
            _latency = null;
            _pause = null;
            _state = SwarmDeviceState.Offline;
            _detail = null;
            return true;
        }
    }

    /// <summary>Takes the state the device reported.</summary>
    /// <returns>The pause that ended with it, if the app was paused and is not any more.</returns>
    internal SwarmPause? SetState(long runId, SwarmDeviceState state, string? appName, string? detail)
    {
        lock (_gate)
        {
            if (runId != 0)
            {
                _runId = runId;
            }

            _state = state;
            _detail = detail;
            _appName = appName ?? _appName;
            var ended = _pause;
            if (state is SwarmDeviceState.Paused or SwarmDeviceState.Pausing || ended is null)
            {
                return null;
            }

            _pause = null;
            return ended;
        }
    }

    internal void SetScreen(SwarmScreenInfo screen)
    {
        lock (_gate)
        {
            _screen = screen;
        }
    }

    internal void SetLatency(TimeSpan latency)
    {
        lock (_gate)
        {
            _latency = latency;
        }
    }

    internal void SetUpdated(long runId, string appName, DateTimeOffset when)
    {
        lock (_gate)
        {
            _runId = runId;
            _appName = appName;
            _lastUpdated = when;
            _lastFault = null;
        }
    }

    internal void SetFault(SwarmFault fault)
    {
        lock (_gate)
        {
            _lastFault = fault;
        }
    }

    internal void SetPause(SwarmPause pause)
    {
        lock (_gate)
        {
            _pause = pause;
            _state = SwarmDeviceState.Paused;
            _detail = null;
        }
    }

    /// <summary>Forgets a pause that ended.</summary>
    /// <returns>The pause, when it was the current one.</returns>
    internal SwarmPause? EndPause(long pause)
    {
        lock (_gate)
        {
            if (_pause is not { } current || (pause != 0 && current.Id != pause))
            {
                return null;
            }

            _pause = null;
            if (_state == SwarmDeviceState.Paused)
            {
                _state = SwarmDeviceState.Running;
            }

            return current;
        }
    }

    private async Task<LinkPayload?> AskScreenAsync(string method, JsonElement? body, CancellationToken cancellationToken)
    {
        if (Connection is not { } connection)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ScreenTimeout);
        try
        {
            return await connection.RequestAsync(method, body, cancellationToken: timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A device that does not answer in time, such as one paused at a breakpoint on its UI thread.
            return null;
        }
        catch (Exception ex) when (ex is LinkConnectionLostException or LinkRemoteException or LinkProtocolException)
        {
            return null;
        }
    }
}
