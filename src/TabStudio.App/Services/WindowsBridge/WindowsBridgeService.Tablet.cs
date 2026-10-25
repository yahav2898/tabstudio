using System.Net.Sockets;
using TabStudio.App.Services.Link;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.WindowsBridge;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.Services.WindowsBridge;

/// <summary>
/// The tablet's role: pairing with a Windows PC's bridge, staying connected to it, and reconnecting by itself when
/// Wi-Fi drops or the PC sleeps, until the user disconnects. One <see cref="Client"/> serves every connection, so the
/// IDE's handlers of its events stay in place across reconnections.
/// </summary>
public sealed partial class WindowsBridgeService
{
    /// <summary>The pauses between reconnection attempts; the last one repeats.</summary>
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8),
    ];

    private LinkClientSession? _session;
    private CancellationTokenSource? _reconnect;
    private LinkPairedDevice? _pc;
    private BridgeInfo? _info;
    private BridgeStatus? _status;
    private string? _error;
    private LinkClientState _state;
    private bool _lostWhileAsleep;

    /// <summary>Raised on the thread pool when the bridge connected or disconnected.</summary>
    public event EventHandler? ConnectionChanged;

    /// <summary>The PCs this tablet is paired with.</summary>
    public LinkTrustStore PairedPcs { get; }

    /// <summary>The client of the bridge, for requests while connected and for its events.</summary>
    public BridgeClient Client { get; } = new();

    /// <summary>Where the connection to the PC stands.</summary>
    public LinkClientState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Whether the PC is connected now.</summary>
    public bool IsConnected => State == LinkClientState.Connected;

    /// <summary>The PC this tablet uses: the one connected or reconnecting, else the paired one seen last.</summary>
    public LinkPairedDevice? Pc
    {
        get
        {
            lock (_gate)
            {
                return _pc;
            }
        }
    }

    /// <summary>The PC's name, such as <c>DESKTOP-7Q2M</c>, while a PC is paired.</summary>
    public string? PcName => Pc?.Name ?? FirstPairedPc()?.Name;

    /// <summary>Whether a PC is paired, so Windows-only projects can run.</summary>
    public bool HasPairedPc => PcName is not null;

    /// <summary>What the PC is and can build, from the last connection.</summary>
    public BridgeInfo? Info
    {
        get
        {
            lock (_gate)
            {
                return _info;
            }
        }
    }

    /// <summary>What the PC last said it is doing, and whether it is awake.</summary>
    public BridgeStatus? Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    /// <summary>The PC as the current connection verified it, while connected.</summary>
    public LinkPeer? ConnectedPc
    {
        get
        {
            lock (_gate)
            {
                return _session?.Host;
            }
        }
    }

    /// <summary>The round trip to the PC, while connected.</summary>
    public TimeSpan? Latency
    {
        get
        {
            lock (_gate)
            {
                return _session?.Connection.Latency;
            }
        }
    }

    /// <summary>Why the last attempt to connect failed, or why the connection was lost.</summary>
    public string? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    /// <summary>Whether the PC said it was going to sleep or lock before the connection dropped.</summary>
    public bool IsPcAsleep
    {
        get
        {
            lock (_gate)
            {
                return _lostWhileAsleep && _state != LinkClientState.Connected;
            }
        }
    }

    /// <summary>Pairs with a PC's bridge, by its scanned QR code or its typed address and code, and connects.</summary>
    /// <param name="target">The PC and the pairing code.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the PC is connected; <see cref="Error"/> says why not.</returns>
    public Task<bool> PairAsync(LinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ConnectCoreAsync(target, cancellationToken);
    }

    /// <summary>Connects to a paired PC.</summary>
    /// <param name="pc">The PC.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the PC is connected; <see cref="Error"/> says why not.</returns>
    public Task<bool> ConnectAsync(LinkPairedDevice pc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pc);
        return ConnectCoreAsync(LinkTarget.Paired(pc), cancellationToken);
    }

    /// <summary>
    /// The bridge, connected: as it is when connected, or after connecting to the paired PC. While the connection is
    /// being restored, waits for it a few seconds.
    /// </summary>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The connected client, or <see langword="null"/> when the PC cannot be reached; <see cref="Error"/> says why.</returns>
    public async Task<BridgeClient?> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return Client;
        }

        if (State is LinkClientState.Reconnecting or LinkClientState.Connecting)
        {
            for (var waited = 0; waited < 40 && !IsConnected && State != LinkClientState.Off; waited++)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            return IsConnected ? Client : null;
        }

        var pc = Pc ?? FirstPairedPc();
        return pc is not null && await ConnectAsync(pc, cancellationToken).ConfigureAwait(false) ? Client : null;
    }

    /// <summary>Ends the connection and stops reconnecting.</summary>
    public async Task DisconnectAsync()
    {
        LinkClientSession? session;
        CancellationTokenSource? reconnect;
        lock (_gate)
        {
            session = _session;
            reconnect = _reconnect;
            _session = null;
            _reconnect = null;
            _status = null;
            _error = null;
            _lostWhileAsleep = false;
            _state = LinkClientState.Off;
        }

        if (reconnect is not null)
        {
            await reconnect.CancelAsync().ConfigureAwait(false);
            reconnect.Dispose();
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            RaiseConnectionChanged();
        }

        RaiseChanged();
    }

    /// <summary>Forgets a PC: it has to pair again. Disconnects first when connected to it.</summary>
    /// <param name="pc">The PC.</param>
    public async Task ForgetPcAsync(LinkPairedDevice pc)
    {
        ArgumentNullException.ThrowIfNull(pc);
        if (Pc?.Fingerprint == pc.Fingerprint)
        {
            await DisconnectAsync().ConfigureAwait(false);
            lock (_gate)
            {
                _pc = null;
            }
        }

        PairedPcs.Forget(pc.Fingerprint);
    }

    private LinkPairedDevice? FirstPairedPc() => PairedPcs.Devices is { Count: > 0 } devices ? devices[0] : null;

    private void InitializeTablet()
    {
        _pc = FirstPairedPc();
        Client.StatusChanged += (_, status) =>
        {
            lock (_gate)
            {
                _status = status;
                _lostWhileAsleep = status.State is BridgePcState.Sleeping or BridgePcState.Locked or BridgePcState.ShuttingDown;
            }

            RaiseChanged();
        };
    }

    private LinkClientOptions ClientOptions() => new() { Identity = _link.Identity, DeviceName = _link.DeviceName, DeviceKind = _link.DeviceKind };

    private async Task<bool> ConnectCoreAsync(LinkTarget target, CancellationToken cancellationToken)
    {
        await DisconnectAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _state = LinkClientState.Connecting;
            _error = null;
        }

        RaiseChanged();
        try
        {
            var session = await LinkClient.ConnectAsync(ClientOptions(), target, Client, cancellationToken).ConfigureAwait(false);
            return await AdoptAsync(session, target.Addresses, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is LinkAuthenticationException or LinkUnreachableException or IOException or SocketException or TimeoutException or OperationCanceledException or LinkConnectionLostException)
        {
            lock (_gate)
            {
                _state = LinkClientState.Off;
                _error = Describe(ex);
            }

            RaiseChanged();
            return false;
        }
    }

    /// <summary>
    /// Makes a new connection the current one, after checking it is a bridge and not a TabStudio Link host, and
    /// remembers the PC and where it answered.
    /// </summary>
    private async Task<bool> AdoptAsync(LinkClientSession session, IReadOnlyList<string> tried, CancellationToken cancellationToken)
    {
        Client.Attach(session.Connection);
        BridgeInfo info;
        try
        {
            info = await Client.GetInfoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (LinkRemoteException ex) when (ex.Code == LinkErrorCodes.UnknownMethod)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            lock (_gate)
            {
                _state = LinkClientState.Off;
                _error = $"{session.Host.Name} is a TabStudio Link host, not a Windows Bridge. On the PC, open File > TabStudio Link > Windows Bridge, and pair with the code it shows there.";
            }

            RaiseChanged();
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var known = PairedPcs.Find(session.Host.Fingerprint);
        var addresses = new[] { session.Host.Address }
            .Concat(tried.Select(a => LinkEndpoint.TryParse(a, out var endpoint) ? endpoint.Host : a))
            .Concat(known?.Addresses ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        var pc = new LinkPairedDevice(session.Host.Name, session.Host.Kind, session.Host.Fingerprint, known?.PairedAt ?? now, now, addresses, session.Port);
        PairedPcs.Remember(pc);
        lock (_gate)
        {
            _session = session;
            _pc = pc;
            _info = info;
            _state = LinkClientState.Connected;
            _error = null;
            _lostWhileAsleep = false;
        }

        session.Connection.LatencyChanged += (_, _) => RaiseChanged();
        session.Connection.Closed += (_, e) => OnSessionClosed(session, e);
        if (!session.Connection.IsOpen)
        {
            OnSessionClosed(session, new LinkClosedEventArgs(session.Connection.CloseError));
        }

        RaiseConnectionChanged();
        RaiseChanged();
        return true;
    }

    private void OnSessionClosed(LinkClientSession session, LinkClosedEventArgs e)
    {
        LinkPairedDevice? pc;
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            pc = _pc;
            _session = null;
            _state = pc is null ? LinkClientState.Off : LinkClientState.Reconnecting;
            var name = pc?.Name ?? "The PC";
            _error = _lostWhileAsleep
                ? $"{name} is asleep or locked. Keep working here: the bridge reconnects when the PC is back."
                : e.Error is null ? $"{name} closed the Windows Bridge." : $"Lost {name}: {e.Error.Message}";
        }

        session.Dispose();
        RaiseConnectionChanged();
        RaiseChanged();
        if (pc is not null && Volatile.Read(ref _disposed) == 0)
        {
            StartReconnecting(pc);
        }
    }

    private void StartReconnecting(LinkPairedDevice pc)
    {
        var reconnect = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _reconnect;
            _reconnect = reconnect;
        }

        previous?.Cancel();
        previous?.Dispose();
        _ = Task.Run(() => ReconnectAsync(pc, reconnect.Token));
    }

    private async Task ReconnectAsync(LinkPairedDevice pc, CancellationToken cancellationToken)
    {
        for (var attempt = 0; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                var discovered = await LinkDiscovery.FindAsync(pc.Fingerprint, TimeSpan.FromSeconds(1), cancellationToken, BridgeProtocol.DiscoveryPort).ConfigureAwait(false);
                var current = PairedPcs.Find(pc.Fingerprint) ?? pc;
                var target = LinkTarget.Paired(current, discovered.Select(d => d.Endpoint.ToString()));
                var session = await LinkClient.ConnectAsync(ClientOptions(), target, Client, cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    if (cancellationToken.IsCancellationRequested || _pc?.Fingerprint != pc.Fingerprint)
                    {
                        session.Dispose();
                        return;
                    }
                }

                await AdoptAsync(session, target.Addresses, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (LinkAuthenticationException ex) when (ex.Refusal is LinkRefusal.NotPaired or LinkRefusal.VersionMismatch or LinkRefusal.WrongHost)
            {
                // Trying again cannot help: the PC forgot this tablet, or runs another version.
                lock (_gate)
                {
                    _state = LinkClientState.Off;
                    _error = ex.Message;
                }

                RaiseChanged();
                return;
            }
            catch (Exception ex) when (ex is LinkAuthenticationException or LinkUnreachableException or IOException or SocketException or TimeoutException or OperationCanceledException or LinkRemoteException or LinkConnectionLostException)
            {
                lock (_gate)
                {
                    if (!_lostWhileAsleep)
                    {
                        _error = Describe(ex);
                    }
                }

                RaiseChanged();
            }
        }
    }

    private void RaiseConnectionChanged()
    {
        var handler = ConnectionChanged;
        if (handler is not null)
        {
            _ = Task.Run(() => handler(this, EventArgs.Empty));
        }
    }
}
