using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using TabStudio.Link.Messaging;
using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>
/// The tablet's side of TabStudio Link: connects to a host, checks it is the expected one, pairs with the code when
/// asked to, and returns the authenticated session.
/// </summary>
/// <remarks>
/// A paired or scanned host is recognized by its pinned certificate fingerprint before anything is sent. A host whose
/// code was typed is not known yet, so it has to prove that it knows the code too before the tablet trusts it.
/// </remarks>
public static class LinkClient
{
    /// <summary>Connects to a host, trying its addresses in order.</summary>
    /// <param name="options">Who the tablet is.</param>
    /// <param name="target">Where the host is and how it is recognized.</param>
    /// <param name="handler">Handles the host's requests and notifications once connected.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The authenticated session.</returns>
    /// <exception cref="LinkAuthenticationException">The host refused this tablet, or is not the expected host.</exception>
    /// <exception cref="LinkUnreachableException">No address answered.</exception>
    public static async Task<LinkClientSession> ConnectAsync(LinkClientOptions options, LinkTarget target, ILinkMessageHandler handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(handler);

        Exception? last = null;
        foreach (var address in target.Addresses)
        {
            if (!LinkEndpoint.TryParse(address, target.Port, out var endpoint))
            {
                continue;
            }

            try
            {
                return await ConnectToAsync(options, target, endpoint, handler, cancellationToken).ConfigureAwait(false);
            }
            catch (LinkAuthenticationException ex) when (ex.Refusal == LinkRefusal.WrongHost)
            {
                // Another device answered at this address, which happens when addresses change; try the next one.
                last = ex;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is SocketException or IOException or OperationCanceledException or TimeoutException)
            {
                last = ex;
            }
        }

        if (last is LinkAuthenticationException refused)
        {
            throw refused;
        }

        var name = target.Name ?? "The host";
        var where = string.Join(", ", target.Addresses);
        throw new LinkUnreachableException(
            last is null ? $"{name} has no valid address." : $"{name} did not answer at {where}: {Describe(last)}",
            last);
    }

    private static async Task<LinkClientSession> ConnectToAsync(LinkClientOptions options, LinkTarget target, LinkEndpoint endpoint, ILinkMessageHandler handler, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        LinkConnection? connection = null;
        LinkClientSession? session = null;
        try
        {
            using (var connecting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connecting.CancelAfter(options.ConnectTimeout);
                try
                {
                    await socket.ConnectAsync(endpoint.Host, endpoint.Port, connecting.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"{endpoint} did not answer within {options.ConnectTimeout.TotalSeconds:0} seconds.");
                }
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.HandshakeTimeout);

            (SslStream Stream, LinkFingerprint Host) authenticated;
            try
            {
                authenticated = await LinkTls.AuthenticateAsClientAsync(new NetworkStream(socket, ownsSocket: true), options.Identity, target.Fingerprint, timeout.Token).ConfigureAwait(false);
            }
            catch (AuthenticationException) when (target.Fingerprint is not null)
            {
                throw new LinkAuthenticationException(
                    LinkRefusal.WrongHost,
                    $"The device at {endpoint} is not {target.Name ?? "the paired host"}: its certificate is different. Pair again with the QR code it shows.");
            }

            if (authenticated.Host == options.Identity.Fingerprint)
            {
                await authenticated.Stream.DisposeAsync().ConfigureAwait(false);
                throw new LinkAuthenticationException(LinkRefusal.Self, "This is this device's own address. Connect to the host from another device.");
            }

            var security = LinkTls.Describe(authenticated.Stream);
            var handshake = new ClientHandshake(handler);
            connection = new LinkConnection(authenticated.Stream, new LinkConnectionOptions { PingInterval = options.PingInterval, PingTimeout = options.PingTimeout });
            connection.Start(handshake);

            var welcome = await handshake.Welcome.WaitAsync(timeout.Token).ConfigureAwait(false);
            var hostName = welcome.Name;
            if (welcome.Protocol != LinkProtocol.Version || !string.Equals(welcome.Version, options.AppVersion, StringComparison.Ordinal))
            {
                throw new LinkAuthenticationException(
                    LinkRefusal.VersionMismatch,
                    $"{hostName} runs TabStudio {welcome.Version} and this device runs TabStudio {options.AppVersion}. Install the same version on both to link them.");
            }

            if (Base64Url.TryDecode(welcome.Nonce) is not { Length: LinkProofs.NonceLength } hostNonce)
            {
                throw new LinkProtocolException("The host's welcome has no valid nonce.");
            }

            var clientNonce = LinkProofs.CreateNonce();
            byte[]? key = null;
            byte[]? transcript = null;
            string? proof = null;
            if (target.Code is { } code)
            {
                key = LinkProofs.DeriveKey(code, hostNonce);
                transcript = LinkProofs.Transcript(authenticated.Host, options.Identity.Fingerprint, hostNonce, clientNonce);
                proof = Base64Url.Encode(LinkProofs.ClientProof(key, transcript));
            }

            var hello = new LinkHello(LinkProtocol.Version, options.AppVersion, options.DeviceName, options.DeviceKind, Base64Url.Encode(clientNonce), proof);
            LinkAccepted accepted;
            try
            {
                var answer = await connection.RequestAsync(
                    LinkHandshakeMethods.Hello,
                    JsonSerializer.SerializeToElement(hello, LinkHandshakeJsonContext.Default.LinkHello),
                    cancellationToken: timeout.Token).ConfigureAwait(false);
                accepted = answer.Read(LinkHandshakeJsonContext.Default.LinkAccepted);
            }
            catch (LinkRemoteException ex)
            {
                throw new LinkAuthenticationException(ToRefusal(ex.Code), ex.Message);
            }

            if (key is not null
                && (Base64Url.TryDecode(accepted.HostProof) is not { } hostProof || !LinkProofs.Matches(LinkProofs.HostProof(key, transcript!), hostProof)))
            {
                throw new LinkAuthenticationException(
                    LinkRefusal.HostNotVerified,
                    $"The device at {endpoint} could not prove that it shows this code, so it may not be {hostName}. Nothing was shared with it.");
            }

            handshake.Accept();
            session = new LinkClientSession(connection, new LinkPeer(hostName, welcome.Kind, authenticated.Host, endpoint.Host, security), endpoint.Port, target.Code is not null);
            return session;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{endpoint} did not finish the TabStudio Link handshake in time.");
        }
        finally
        {
            if (session is null)
            {
                connection?.Dispose();
                socket.Dispose();
            }
        }
    }

    private static LinkRefusal ToRefusal(string code) => code switch
    {
        LinkErrorCodes.WrongCode => LinkRefusal.WrongCode,
        LinkErrorCodes.VersionMismatch => LinkRefusal.VersionMismatch,
        LinkErrorCodes.Busy => LinkRefusal.Busy,
        LinkErrorCodes.Self => LinkRefusal.Self,
        _ => LinkRefusal.NotPaired,
    };

    private static string Describe(Exception error) => error switch
    {
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } => "nothing listens there. Check that TabStudio Link is on.",
        SocketException { SocketErrorCode: SocketError.HostUnreachable or SocketError.NetworkUnreachable } => "the address is not on this network.",
        TimeoutException => "it did not answer in time. Check that both devices are on the same Wi-Fi.",
        _ => error.Message,
    };

    /// <summary>Waits for the welcome, then passes everything to the session's handler once the host accepted.</summary>
    private sealed class ClientHandshake(ILinkMessageHandler next) : ILinkMessageHandler
    {
        private readonly TaskCompletionSource<LinkWelcome> _welcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _gate = new();
        private readonly List<(string Method, LinkPayload Payload)> _early = [];
        private bool _accepted;

        public Task<LinkWelcome> Welcome => _welcome.Task;

        /// <summary>Hands over to the session: notifications that arrived early are handled first, in order.</summary>
        public void Accept()
        {
            lock (_gate)
            {
                _accepted = true;
                foreach (var (method, payload) in _early)
                {
                    next.HandleNotification(method, payload);
                }

                _early.Clear();
            }
        }

        public Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
        {
            bool accepted;
            lock (_gate)
            {
                accepted = _accepted;
            }

            return accepted
                ? next.HandleRequestAsync(method, payload, cancellationToken)
                : throw new LinkRemoteException(LinkErrorCodes.NotAuthenticated, "The handshake is not finished.");
        }

        public void HandleNotification(string method, LinkPayload payload)
        {
            if (method == LinkHandshakeMethods.Welcome)
            {
                try
                {
                    _welcome.TrySetResult(payload.Read(LinkHandshakeJsonContext.Default.LinkWelcome));
                }
                catch (LinkProtocolException ex)
                {
                    _welcome.TrySetException(ex);
                }

                return;
            }

            lock (_gate)
            {
                if (_accepted)
                {
                    next.HandleNotification(method, payload);
                }
                else
                {
                    _early.Add((method, payload));
                }
            }
        }
    }
}
