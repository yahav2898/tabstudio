using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using TabStudio.Link.Messaging;
using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>Creates what serves a tablet once it is authenticated.</summary>
/// <param name="client">The tablet.</param>
/// <param name="connection">Its connection.</param>
public delegate ILinkMessageHandler LinkSessionFactory(LinkPeer client, LinkConnection connection);

/// <summary>
/// The host's side of TabStudio Link: listens for tablets on the local network, runs TLS and the handshake, pairs new
/// tablets with the one-time code, and hands each authenticated tablet to a session that serves it.
/// </summary>
/// <remarks>
/// <para>
/// One tablet is served at a time: another paired tablet is refused as busy until the first disconnects, and the
/// same tablet connecting again replaces its old connection (after Wi-Fi dropped, for example). A host with a higher
/// <see cref="LinkHostOptions.MaxClients"/>, such as a tablet coordinating a swarm of phones, serves that many devices
/// at once.
/// </para>
/// <para>
/// Events are raised on the thread pool. Every member is thread-safe.
/// </para>
/// </remarks>
public sealed class LinkHost : IAsyncDisposable
{
    /// <summary>How many devices may be in the handshake at once; pairing proofs cost a tenth of a second each.</summary>
    private const int MaxConcurrentHandshakes = 4;

    private readonly LinkHostOptions _options;
    private readonly LinkSessionFactory _createSession;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _handshakes = new(MaxConcurrentHandshakes, MaxConcurrentHandshakes);
    private readonly Timer _codeExpiry;
    private Socket? _listener;
    private Task _acceptLoop = Task.CompletedTask;
    private LinkPairingCode _code = LinkPairingCode.Create();
    private DateTimeOffset _codeExpires = DateTimeOffset.MaxValue;
    private int _wrongCodes;
    private readonly List<ServedClient> _clients = [];
    private int _disposed;

    /// <summary>Creates a host. Call <see cref="Start"/> to listen.</summary>
    /// <param name="options">How to listen, and the host's identity.</param>
    /// <param name="createSession">Creates what serves an authenticated tablet.</param>
    public LinkHost(LinkHostOptions options, LinkSessionFactory createSession)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(createSession);
        _options = options;
        _createSession = createSession;
        _codeExpiry = new Timer(_ => RenewPairingCode(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Raised when the pairing code changed: it was used, guessed wrong, expired or renewed.</summary>
    public event EventHandler? PairingCodeChanged;

    /// <summary>Raised when a tablet (or, with <see cref="LinkHostOptions.MaxClients"/> above one, any device) connected or disconnected.</summary>
    public event EventHandler? ClientChanged;

    /// <summary>Raised when a new tablet paired.</summary>
    public event EventHandler<LinkPeer>? DevicePaired;

    /// <summary>The TCP port the host listens on, once started.</summary>
    public int Port { get; private set; }

    /// <summary>Whether the host is listening.</summary>
    public bool IsRunning => _listener is not null && Volatile.Read(ref _disposed) == 0;

    /// <summary>The host's options.</summary>
    public LinkHostOptions Options => _options;

    /// <summary>The current one-time pairing code.</summary>
    public LinkPairingCode PairingCode
    {
        get
        {
            lock (_gate)
            {
                return _code;
            }
        }
    }

    /// <summary>When the current pairing code expires.</summary>
    public DateTimeOffset PairingCodeExpires
    {
        get
        {
            lock (_gate)
            {
                return _codeExpires;
            }
        }
    }

    /// <summary>The connected tablet, if any; the first one connected when the host serves several.</summary>
    public LinkPeer? Client
    {
        get
        {
            lock (_gate)
            {
                return _clients.Count > 0 ? _clients[0].Peer : null;
            }
        }
    }

    /// <summary>The connected tablet's connection, if any; the first one's when the host serves several.</summary>
    public LinkConnection? ClientConnection
    {
        get
        {
            lock (_gate)
            {
                return _clients.Count > 0 ? _clients[0].Connection : null;
            }
        }
    }

    /// <summary>Every connected device, in the order they connected.</summary>
    public IReadOnlyList<LinkPeer> Clients
    {
        get
        {
            lock (_gate)
            {
                return [.. _clients.Select(c => c.Peer)];
            }
        }
    }

    /// <summary>Starts listening, on <see cref="LinkHostOptions.Port"/> when it is free and on any port otherwise.</summary>
    /// <exception cref="SocketException">No port could be opened.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        if (_listener is not null)
        {
            throw new InvalidOperationException("The host is already listening.");
        }

        var listener = Bind(_options.Port) ?? (_options.Port != 0 ? Bind(0) : null)
            ?? throw new SocketException((int)SocketError.AddressAlreadyInUse);
        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndPoint!).Port;
        RenewPairingCode();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(listener));
    }

    /// <summary>The invitation the QR code shows, for the host's current addresses.</summary>
    /// <param name="addresses">The host's addresses on the local network, the most likely first.</param>
    public LinkInvitation CreateInvitation(IReadOnlyList<string> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return new LinkInvitation(_options.DeviceName, addresses, Port, _options.Identity.Fingerprint, PairingCode) { Role = _options.Role };
    }

    /// <summary>Draws a new pairing code; the old one stops working.</summary>
    public void RenewPairingCode()
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            _code = LinkPairingCode.Create();
            _wrongCodes = 0;
            _codeExpires = DateTimeOffset.UtcNow + _options.PairingCodeLifetime;
            _codeExpiry.Change(_options.PairingCodeLifetime, Timeout.InfiniteTimeSpan);
        }

        PairingCodeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Disconnects the connected tablet, which falls back to compiling on its own; every device when the host serves several.</summary>
    public void DisconnectClient()
    {
        foreach (var connection in Connections())
        {
            connection.Close();
        }
    }

    /// <summary>Disconnects one device.</summary>
    /// <param name="fingerprint">The device.</param>
    /// <returns>Whether it was connected.</returns>
    public bool DisconnectClient(LinkFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        LinkConnection? connection;
        lock (_gate)
        {
            connection = _clients.Find(c => c.Peer.Fingerprint == fingerprint)?.Connection;
        }

        connection?.Close();
        return connection is not null;
    }

    /// <summary>Stops listening and disconnects the tablet.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        await _acceptLoop.ConfigureAwait(false);
        await _codeExpiry.DisposeAsync().ConfigureAwait(false);

        List<LinkConnection> connections;
        lock (_gate)
        {
            connections = [.. _clients.Select(c => c.Connection)];
            _clients.Clear();
        }

        foreach (var connection in connections)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        if (connections.Count > 0)
        {
            ClientChanged?.Invoke(this, EventArgs.Empty);
        }

        _stopping.Dispose();
        _handshakes.Dispose();
    }

    private Socket? Bind(int port)
    {
        if (!_options.LoopbackOnly && Socket.OSSupportsIPv6 && TryBind(AddressFamily.InterNetworkV6, IPAddress.IPv6Any, port, dualMode: true) is { } dualStack)
        {
            return dualStack;
        }

        return TryBind(AddressFamily.InterNetwork, _options.LoopbackOnly ? IPAddress.Loopback : IPAddress.Any, port, dualMode: false);
    }

    private static Socket? TryBind(AddressFamily family, IPAddress address, int port, bool dualMode)
    {
        var socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            if (dualMode)
            {
                socket.DualMode = true;
            }

            // On Linux and Android, so a restarted host gets its port back while old connections linger in TIME_WAIT.
            if (!OperatingSystem.IsWindows())
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            socket.Bind(new IPEndPoint(address, port));
            socket.Listen(16);
            return socket;
        }
        catch (SocketException)
        {
            socket.Dispose();
            return null;
        }
    }

    private async Task AcceptLoopAsync(Socket listener)
    {
        while (!_stopping.IsCancellationRequested)
        {
            Socket socket;
            try
            {
                socket = await listener.AcceptAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // A connection that failed while it was accepted; keep listening.
                continue;
            }

            _ = Task.Run(() => ServeAsync(socket));
        }
    }

    private async Task ServeAsync(Socket socket)
    {
        bool admitted;
        try
        {
            admitted = await _handshakes.WaitAsync(_options.HandshakeQueueTimeout, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException)
        {
            admitted = false;
        }

        if (!admitted)
        {
            socket.Dispose();
            return;
        }

        LinkConnection? connection = null;
        HostHandshake? handshake = null;
        var accepted = false;
        try
        {
            socket.NoDelay = true;
            var address = socket.RemoteEndPoint is IPEndPoint remote
                ? (remote.Address.IsIPv4MappedToIPv6 ? remote.Address.MapToIPv4() : remote.Address).ToString()
                : "unknown";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            timeout.CancelAfter(_options.HandshakeTimeout);

            var (tls, clientFingerprint) = await LinkTls.AuthenticateAsHostAsync(new NetworkStream(socket, ownsSocket: true), _options.Identity, timeout.Token).ConfigureAwait(false);
            connection = new LinkConnection(tls, new LinkConnectionOptions { IdleTimeout = _options.IdleTimeout });
            var nonce = LinkProofs.CreateNonce();
            handshake = new HostHandshake(this, connection, clientFingerprint, nonce, address, LinkTls.Describe(tls));
            connection.Start(handshake);

            var welcome = new LinkWelcome(LinkProtocol.Version, _options.AppVersion, _options.DeviceName, _options.DeviceKind, Base64Url.Encode(nonce), _options.Role);
            await connection.NotifyAsync(LinkHandshakeMethods.Welcome, JsonSerializer.SerializeToElement(welcome, LinkHandshakeJsonContext.Default.LinkWelcome)).ConfigureAwait(false);
            accepted = await handshake.Completion.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or AuthenticationException or OperationCanceledException or ObjectDisposedException or TimeoutException)
        {
            // TLS failed, the handshake timed out, or the device went away. Only the connection needs closing.
        }
        finally
        {
            try
            {
                _handshakes.Release();
            }
            catch (ObjectDisposedException)
            {
                // Stopped meanwhile.
            }

            if (!accepted)
            {
                await CloseRefusedAsync(connection, handshake).ConfigureAwait(false);
                socket.Dispose();
            }
        }
    }

    /// <summary>Lets a refused device read why, then closes its connection.</summary>
    private static async Task CloseRefusedAsync(LinkConnection? connection, HostHandshake? handshake)
    {
        if (connection is null)
        {
            return;
        }

        if (handshake?.Completion.IsCompletedSuccessfully == true)
        {
            // The answer is on its way; the device closes the connection once it has read it.
            await Task.WhenAny(connection.Completion, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }

        await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Decides whether a device that sent its hello is served. Throws the refusal otherwise.</summary>
    private LinkReply Accept(LinkConnection connection, LinkFingerprint client, byte[] hostNonce, string address, string security, LinkHello hello)
    {
        var host = _options.DeviceName;
        if (hello.Protocol != LinkProtocol.Version || !string.Equals(hello.Version, _options.AppVersion, StringComparison.Ordinal))
        {
            throw new LinkRemoteException(
                LinkErrorCodes.VersionMismatch,
                $"{host} runs TabStudio {_options.AppVersion} and {hello.Name} runs TabStudio {hello.Version}. Install the same version on both to link them.");
        }

        if (client == _options.Identity.Fingerprint)
        {
            throw new LinkRemoteException(LinkErrorCodes.Self, "A device cannot be its own TabStudio Link host.");
        }

        if (hello.Role != _options.Role)
        {
            throw new LinkRemoteException(LinkErrorCodes.WrongRole, LinkRoles.Mismatch(host, _options.Role));
        }

        if (Base64Url.TryDecode(hello.Nonce) is not { Length: LinkProofs.NonceLength } clientNonce)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The hello has no valid nonce.");
        }

        var paired = _options.Clients.Find(client);
        string? hostProof = null;
        if (hello.PairingProof is { } proofText)
        {
            var proof = Base64Url.TryDecode(proofText)
                ?? throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The pairing proof is malformed.");
            var code = PairingCode;
            var key = LinkProofs.DeriveKey(code, hostNonce);
            var transcript = LinkProofs.Transcript(_options.Identity.Fingerprint, client, hostNonce, clientNonce);
            if (DateTimeOffset.UtcNow > PairingCodeExpires || !LinkProofs.Matches(LinkProofs.ClientProof(key, transcript), proof))
            {
                if (CountWrongCode())
                {
                    RenewPairingCode();
                    throw new LinkRemoteException(
                        LinkErrorCodes.WrongCode,
                        $"That is not the code {host} shows. For safety {host} now shows a new code: type that one.");
                }

                throw new LinkRemoteException(LinkErrorCodes.WrongCode, $"That is not the code {host} shows. Check it and type it again.");
            }

            hostProof = Base64Url.Encode(LinkProofs.HostProof(key, transcript));
        }
        else if (paired is null)
        {
            throw new LinkRemoteException(
                LinkErrorCodes.NotPaired,
                $"{host} does not know {hello.Name} any more. Pair them again: scan the QR code {host} shows, or type its code.");
        }

        var peer = new LinkPeer(hello.Name, hello.Kind, client, address, security);
        LinkConnection? replaced;
        lock (_gate)
        {
            // Devices whose connection already closed make room; the same device connecting again replaces itself.
            _clients.RemoveAll(c => !c.Connection.IsOpen && c.Peer.Fingerprint != client);
            var others = _clients.Where(c => c.Peer.Fingerprint != client).ToList();
            if (others.Count >= _options.MaxClients)
            {
                throw new LinkRemoteException(
                    LinkErrorCodes.Busy,
                    _options.MaxClients == 1
                        ? $"{host} is linked to {others[0].Peer.Name}. Disconnect that device first."
                        : $"{host} already has {_options.MaxClients.ToString(CultureInfo.InvariantCulture)} devices, the most it takes. Remove one first.");
            }

            var index = _clients.FindIndex(c => c.Peer.Fingerprint == client);
            replaced = index >= 0 ? _clients[index].Connection : null;
            if (index >= 0)
            {
                _clients[index] = new ServedClient(peer, connection);
            }
            else
            {
                _clients.Add(new ServedClient(peer, connection));
            }
        }

        replaced?.Close();
        var now = DateTimeOffset.UtcNow;
        _options.Clients.Remember(new LinkPairedDevice(hello.Name, hello.Kind, client, paired?.PairedAt ?? now, now, [], 0));
        connection.Closed += (_, _) => OnClientClosed(connection);
        if (!connection.IsOpen)
        {
            OnClientClosed(connection);
        }

        connection.ChangeHandler(_createSession(peer, connection));
        if (hostProof is not null)
        {
            // The code is single-use, unless the host keeps it for many devices.
            if (!_options.KeepsPairingCode)
            {
                RenewPairingCode();
            }

            DevicePaired?.Invoke(this, peer);
        }

        ClientChanged?.Invoke(this, EventArgs.Empty);
        return LinkReply.Of(new LinkAccepted(hostProof), LinkHandshakeJsonContext.Default.LinkAccepted);
    }

    /// <summary>Counts a wrong code. Returns whether the code must be drawn again.</summary>
    private bool CountWrongCode()
    {
        lock (_gate)
        {
            _wrongCodes++;
            return _wrongCodes >= Math.Max(1, _options.WrongCodesBeforeRenewal);
        }
    }

    private void OnClientClosed(LinkConnection connection)
    {
        lock (_gate)
        {
            if (_clients.RemoveAll(c => ReferenceEquals(c.Connection, connection)) == 0)
            {
                return;
            }
        }

        ClientChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<LinkConnection> Connections()
    {
        lock (_gate)
        {
            return [.. _clients.Select(c => c.Connection)];
        }
    }

    /// <summary>A device the host serves, and its connection.</summary>
    private sealed record ServedClient(LinkPeer Peer, LinkConnection Connection);

    /// <summary>Answers only the hello, until the device is accepted or refused.</summary>
    private sealed class HostHandshake(LinkHost host, LinkConnection connection, LinkFingerprint client, byte[] nonce, string address, string security) : ILinkMessageHandler
    {
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>True once the device is accepted, false once it is refused.</summary>
        public Task<bool> Completion => _completion.Task;

        public Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
        {
            if (method != LinkHandshakeMethods.Hello || _completion.Task.IsCompleted)
            {
                throw new LinkRemoteException(LinkErrorCodes.NotAuthenticated, "Say hello first.");
            }

            try
            {
                var reply = host.Accept(connection, client, nonce, address, security, payload.Read(LinkHandshakeJsonContext.Default.LinkHello));
                _completion.TrySetResult(true);
                return Task.FromResult(reply);
            }
            catch (Exception ex) when (ex is LinkRemoteException or LinkProtocolException)
            {
                _completion.TrySetResult(false);
                throw;
            }
        }

        public void HandleNotification(string method, LinkPayload payload)
        {
            // Nothing is listened to before the hello.
        }
    }
}
