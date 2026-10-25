using System.Net.Sockets;
using TabStudio.Link;
using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.App.Services.Link;

/// <summary>
/// The tablet's role: pairing with a host, staying linked to it, and reconnecting by itself when Wi-Fi drops or the
/// host sleeps, until the user disconnects.
/// </summary>
public sealed partial class LinkService
{
    /// <summary>The pauses between reconnection attempts; the last one repeats.</summary>
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8),
    ];

    private LinkClientSession? _session;
    private LinkLanguageClient? _languageClient;
    private CancellationTokenSource? _reconnect;
    private LinkPairedDevice? _linkedHost;
    private LinkHostStatus? _hostStatus;
    private string? _clientError;
    private LinkClientState _clientState;

    /// <summary>
    /// Raised on the thread pool when <see cref="LanguageClient"/> changed: a link was made, or lost. Language services
    /// switch between this device and the host on it.
    /// </summary>
    public event EventHandler? LanguageClientChanged;

    /// <summary>Where the link to a host stands.</summary>
    public LinkClientState ClientState
    {
        get
        {
            lock (_gate)
            {
                return _clientState;
            }
        }
    }

    /// <summary>The host this device is linked to, or reconnecting to.</summary>
    public LinkPairedDevice? LinkedHost
    {
        get
        {
            lock (_gate)
            {
                return _linkedHost;
            }
        }
    }

    /// <summary>The host as the current connection verified it, while connected.</summary>
    public LinkPeer? ConnectedHost
    {
        get
        {
            lock (_gate)
            {
                return _session?.Host;
            }
        }
    }

    /// <summary>The round trip to the host, while connected.</summary>
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

    /// <summary>What the host last said it is doing, while connected.</summary>
    public LinkHostStatus? HostStatus
    {
        get
        {
            lock (_gate)
            {
                return _hostStatus;
            }
        }
    }

    /// <summary>Why the last attempt to link failed, or why the link was lost.</summary>
    public string? ClientError
    {
        get
        {
            lock (_gate)
            {
                return _clientError;
            }
        }
    }

    /// <summary>The language service of the link, while connected.</summary>
    public LinkLanguageClient? LanguageClient
    {
        get
        {
            lock (_gate)
            {
                return _languageClient;
            }
        }
    }

    /// <summary>Pairs with a host, by its scanned QR code or its typed address and code, and links to it.</summary>
    /// <param name="target">The host and the pairing code.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the devices are linked; <see cref="ClientError"/> says why not.</returns>
    public Task<bool> PairAsync(LinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ConnectCoreAsync(target, cancellationToken);
    }

    /// <summary>Links to a paired host.</summary>
    /// <param name="host">The host.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the devices are linked; <see cref="ClientError"/> says why not.</returns>
    public Task<bool> ConnectAsync(LinkPairedDevice host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ConnectCoreAsync(LinkTarget.Paired(host), cancellationToken);
    }

    /// <summary>Ends the link and stops reconnecting. This device compiles on its own again.</summary>
    public async Task DisconnectAsync()
    {
        LinkClientSession? session;
        CancellationTokenSource? reconnect;
        bool wasLinked;
        lock (_gate)
        {
            session = _session;
            reconnect = _reconnect;
            wasLinked = _languageClient is not null;
            _session = null;
            _languageClient = null;
            _reconnect = null;
            _linkedHost = null;
            _hostStatus = null;
            _clientError = null;
            _clientState = LinkClientState.Off;
        }

        if (reconnect is not null)
        {
            await reconnect.CancelAsync().ConfigureAwait(false);
            reconnect.Dispose();
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        if (wasLinked)
        {
            RaiseLanguageClientChanged();
        }

        RaiseChanged();
    }

    /// <summary>Forgets a host: it has to be paired again. Disconnects first when linked to it.</summary>
    /// <param name="host">The host.</param>
    public async Task ForgetHostAsync(LinkPairedDevice host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (LinkedHost?.Fingerprint == host.Fingerprint)
        {
            await DisconnectAsync().ConfigureAwait(false);
        }

        PairedHosts.Forget(host.Fingerprint);
    }

    private LinkClientOptions ClientOptions() => new() { Identity = Identity, DeviceName = DeviceName, DeviceKind = DeviceKind };

    private async Task<bool> ConnectCoreAsync(LinkTarget target, CancellationToken cancellationToken)
    {
        await DisconnectAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _clientState = LinkClientState.Connecting;
            _clientError = null;
        }

        RaiseChanged();
        try
        {
            var session = await LinkClient.ConnectAsync(ClientOptions(), target, new ClientNotifications(this), cancellationToken).ConfigureAwait(false);
            Adopt(session, target.Addresses);
            return true;
        }
        catch (Exception ex) when (ex is LinkAuthenticationException or LinkUnreachableException or IOException or SocketException or TimeoutException or OperationCanceledException)
        {
            lock (_gate)
            {
                _clientState = LinkClientState.Off;
                _clientError = Describe(ex);
            }

            RaiseChanged();
            return false;
        }
    }

    /// <summary>Makes a new connection the current link, and remembers the host and where it answered.</summary>
    private void Adopt(LinkClientSession session, IReadOnlyList<string> tried)
    {
        var now = DateTimeOffset.UtcNow;
        var known = PairedHosts.Find(session.Host.Fingerprint);
        var addresses = new[] { session.Host.Address }
            .Concat(tried.Select(a => LinkEndpoint.TryParse(a, out var endpoint) ? endpoint.Host : a))
            .Concat(known?.Addresses ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        var host = new LinkPairedDevice(session.Host.Name, session.Host.Kind, session.Host.Fingerprint, known?.PairedAt ?? now, now, addresses, session.Port);
        PairedHosts.Remember(host);

        lock (_gate)
        {
            _session = session;
            _languageClient = new LinkLanguageClient(session.Connection);
            _linkedHost = host;
            _clientState = LinkClientState.Connected;
            _clientError = null;
        }

        session.Connection.LatencyChanged += (_, _) => RaiseChanged();
        session.Connection.Closed += (_, e) => OnSessionClosed(session, e);
        if (!session.Connection.IsOpen)
        {
            OnSessionClosed(session, new LinkClosedEventArgs(session.Connection.CloseError));
        }

        RaiseLanguageClientChanged();
        RaiseChanged();
    }

    private void OnSessionClosed(LinkClientSession session, LinkClosedEventArgs e)
    {
        LinkPairedDevice? host;
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            host = _linkedHost;
            _session = null;
            _languageClient = null;
            _hostStatus = null;
            _clientState = host is null ? LinkClientState.Off : LinkClientState.Reconnecting;
            _clientError = e.Error is null ? $"{host?.Name ?? "The host"} closed the link." : $"Lost {host?.Name ?? "the host"}: {e.Error.Message}";
        }

        session.Dispose();
        RaiseLanguageClientChanged();
        RaiseChanged();
        if (host is not null && Volatile.Read(ref _disposed) == 0)
        {
            StartReconnecting(host);
        }
    }

    private void StartReconnecting(LinkPairedDevice host)
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
        _ = Task.Run(() => ReconnectAsync(host, reconnect.Token));
    }

    private async Task ReconnectAsync(LinkPairedDevice host, CancellationToken cancellationToken)
    {
        for (var attempt = 0; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                var discovered = await LinkDiscovery.FindAsync(host.Fingerprint, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                var current = PairedHosts.Find(host.Fingerprint) ?? host;
                var target = LinkTarget.Paired(current, discovered.Select(d => d.Endpoint.ToString()));
                var session = await LinkClient.ConnectAsync(ClientOptions(), target, new ClientNotifications(this), cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    if (cancellationToken.IsCancellationRequested || _linkedHost?.Fingerprint != host.Fingerprint)
                    {
                        session.Dispose();
                        return;
                    }
                }

                Adopt(session, target.Addresses);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (LinkAuthenticationException ex) when (ex.Refusal is LinkRefusal.NotPaired or LinkRefusal.VersionMismatch or LinkRefusal.WrongHost)
            {
                // Trying again cannot help: the host forgot this device, or runs another version.
                lock (_gate)
                {
                    _clientState = LinkClientState.Off;
                    _linkedHost = null;
                    _clientError = ex.Message;
                }

                RaiseChanged();
                return;
            }
            catch (Exception ex) when (ex is LinkAuthenticationException or LinkUnreachableException or IOException or SocketException or TimeoutException or OperationCanceledException)
            {
                lock (_gate)
                {
                    _clientError = Describe(ex);
                }

                RaiseChanged();
            }
        }
    }

    private void OnHostStatus(LinkHostStatus status)
    {
        lock (_gate)
        {
            _hostStatus = status;
        }

        RaiseChanged();
    }

    private void RaiseLanguageClientChanged()
    {
        var handler = LanguageClientChanged;
        if (handler is not null)
        {
            _ = Task.Run(() => handler(this, EventArgs.Empty));
        }
    }

    /// <summary>What the host tells the tablet without being asked: its status.</summary>
    private sealed class ClientNotifications(LinkService service) : ILinkMessageHandler
    {
        public Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken) =>
            throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, "The tablet answers no requests.");

        public void HandleNotification(string method, LinkPayload payload)
        {
            if (LinkHostStatus.TryRead(method, payload, out var status))
            {
                service.OnHostStatus(status);
            }
        }
    }
}
