using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Web;

/// <summary>
/// A device's end of the web tunnel: a gateway on the device's own loopback address, where its web view opens the web
/// app the tablet serves. Every connection it accepts travels to the tablet inside the swarm's encrypted TabStudio
/// Link connection; nothing listens on the Wi-Fi.
/// </summary>
/// <remarks>
/// <para>
/// Other apps on the device could reach a loopback port too, so the gateway requires the run's secret of every
/// connection: the first page is opened with the secret in its query, which the gateway exchanges for an
/// <c>HttpOnly</c>, <c>SameSite=Strict</c> cookie with a redirect to the same page without it. Requests without the
/// cookie are refused. Keep-alive connections are checked on their first request.
/// </para>
/// <para>Thread-safe.</para>
/// </remarks>
internal sealed class SwarmWebGateway : IAsyncDisposable
{
    /// <summary>The longest request head the gateway reads to check the secret.</summary>
    private const int MaxHeadLength = 32 * 1024;

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private readonly Func<LinkConnection?> _link;
    private readonly string _secretText;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, SwarmTunnel> _tunnels = [];
    private readonly CancellationTokenSource _stopping = new();
    private Socket? _listener;
    private int _nextStream;
    private int _disposed;

    /// <summary>Creates a gateway. Call <see cref="Start"/> to listen.</summary>
    /// <param name="link">The swarm's connection, while there is one.</param>
    /// <param name="secret">The run's secret.</param>
    public SwarmWebGateway(Func<LinkConnection?> link, string secret)
    {
        _link = link;
        _secretText = secret;
    }

    /// <summary>Where the gateway listens, such as <c>http://127.0.0.1:50211/</c>, once started.</summary>
    public Uri? Address { get; private set; }

    /// <summary>Starts listening on a free port of the loopback address.</summary>
    /// <exception cref="SocketException">No port could be opened.</exception>
    public void Start()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(32);
        }
        catch
        {
            listener.Dispose();
            throw;
        }

        _listener = listener;
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;
        Address = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
        _ = Task.Run(() => AcceptLoopAsync(listener));
    }

    /// <summary>The address the web view opens first: a page with the secret, which the gateway trades for its cookie.</summary>
    /// <param name="launchPath">The page, such as <c>/</c>.</param>
    /// <exception cref="InvalidOperationException">The gateway is not started.</exception>
    public Uri LaunchAddress(string launchPath)
    {
        var address = Address ?? throw new InvalidOperationException("The gateway is not started.");
        var path = string.IsNullOrWhiteSpace(launchPath) ? "/" : launchPath.StartsWith('/') ? launchPath : "/" + launchPath;
        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return new Uri($"{address.AbsoluteUri.TrimEnd('/')}{path}{separator}{SwarmProtocol.WebSecretParameter}={_secretText}");
    }

    /// <summary>Bytes the tablet sent for one of the gateway's connections.</summary>
    public void Deliver(int stream, byte[] data) => Find(stream)?.Deliver(data);

    /// <summary>The tablet closed its direction of one of the gateway's connections.</summary>
    public void CloseFromCoordinator(int stream) => Find(stream)?.CloseFromRemote();

    /// <summary>Stops listening and closes every connection.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        List<SwarmTunnel> tunnels;
        lock (_gate)
        {
            tunnels = [.. _tunnels.Values];
            _tunnels.Clear();
        }

        foreach (var tunnel in tunnels)
        {
            tunnel.Abort();
        }

        _stopping.Dispose();
    }

    /// <summary>Whether a request head carries the secret in its target's query; returns the target without it.</summary>
    internal static bool TryTakeSecretFromQuery(string target, string secret, out string withoutSecret)
    {
        withoutSecret = target;
        var question = target.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
        {
            return false;
        }

        var kept = new List<string>();
        var found = false;
        foreach (var pair in target[(question + 1)..].Split('&'))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? pair : pair[..equals];
            var value = equals < 0 ? string.Empty : pair[(equals + 1)..];
            if (name == SwarmProtocol.WebSecretParameter)
            {
                found |= SecretMatches(value, secret);
                continue;
            }

            kept.Add(pair);
        }

        withoutSecret = kept.Count == 0 ? target[..question] : $"{target[..question]}?{string.Join('&', kept)}";
        return found;
    }

    /// <summary>Whether a request head's cookies carry the secret.</summary>
    internal static bool CookieHasSecret(IEnumerable<string> cookieHeaders, string secret) =>
        cookieHeaders
            .SelectMany(header => header.Split(';'))
            .Select(cookie => cookie.Trim())
            .Any(cookie => cookie.StartsWith(SwarmProtocol.WebSecretCookie + "=", StringComparison.Ordinal)
                && SecretMatches(cookie[(SwarmProtocol.WebSecretCookie.Length + 1)..], secret));

    private static bool SecretMatches(string candidate, string secret) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(secret));

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
                continue;
            }

            socket.NoDelay = true;
            _ = Task.Run(() => ServeAsync(socket));
        }
    }

    private async Task ServeAsync(Socket socket)
    {
        var handedOver = false;
        try
        {
            var (head, received) = await ReadHeadAsync(socket).ConfigureAwait(false);
            if (head is null)
            {
                return;
            }

            var lines = head.Split("\r\n");
            var requestLine = lines[0].Split(' ');
            var target = requestLine.Length >= 2 ? requestLine[1] : "/";
            var cookies = lines.Skip(1)
                .Where(line => line.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                .Select(line => line["Cookie:".Length..]);

            if (TryTakeSecretFromQuery(target, _secretText, out var withoutSecret))
            {
                await RespondAsync(
                    socket,
                    "302 Found",
                    $"Location: {withoutSecret}\r\nSet-Cookie: {SwarmProtocol.WebSecretCookie}={_secretText}; Path=/; HttpOnly; SameSite=Strict\r\nCache-Control: no-store\r\n",
                    string.Empty).ConfigureAwait(false);
                return;
            }

            if (!CookieHasSecret(cookies, _secretText))
            {
                await RespondAsync(socket, "403 Forbidden", string.Empty, "This address belongs to a TabStudio Swarm session. Open the app from TabStudio on this device.").ConfigureAwait(false);
                return;
            }

            handedOver = await TunnelAsync(socket, received).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or IOException)
        {
            // The web view closed the connection.
        }
        finally
        {
            if (!handedOver)
            {
                socket.Dispose();
            }
        }
    }

    /// <summary>Hands an accepted connection to the tablet; returns whether the tunnel owns the socket now.</summary>
    private async Task<bool> TunnelAsync(Socket socket, byte[] received)
    {
        if (_link() is not { } link)
        {
            await RespondAsync(socket, "503 Service Unavailable", string.Empty, "The tablet that serves this web app is not connected.").ConfigureAwait(false);
            return false;
        }

        var stream = Interlocked.Increment(ref _nextStream);
        var tunnel = new SwarmTunnel(stream, socket, link, Remove);
        lock (_gate)
        {
            _tunnels[stream] = tunnel;
        }

        SwarmConnectResponse answer;
        try
        {
            var reply = await link.RequestAsync(
                SwarmMethods.WebConnect,
                SwarmJson.Body(new SwarmStreamNotice(stream), SwarmJsonContext.Default.SwarmStreamNotice),
                cancellationToken: _stopping.Token).ConfigureAwait(false);
            answer = reply.Read(SwarmJsonContext.Default.SwarmConnectResponse);
        }
        catch (Exception ex) when (ex is LinkConnectionLostException or LinkRemoteException or LinkProtocolException or OperationCanceledException)
        {
            answer = new SwarmConnectResponse(false, ex.Message);
        }

        if (!answer.Connected)
        {
            Remove(tunnel);
            await RespondAsync(socket, "502 Bad Gateway", string.Empty, answer.Error ?? "The tablet could not reach the web app.").ConfigureAwait(false);
            return false;
        }

        tunnel.Start(received);
        return true;
    }

    /// <summary>Reads until the end of the request's head; returns the head as text and every byte read so far.</summary>
    private static async Task<(string? Head, byte[] Received)> ReadHeadAsync(Socket socket)
    {
        var buffer = new byte[MaxHeadLength];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await socket.ReceiveAsync(buffer.AsMemory(length), SocketFlags.None).ConfigureAwait(false);
            if (read == 0)
            {
                return (null, []);
            }

            length += read;
            var end = buffer.AsSpan(0, length).IndexOf(HeadEnd);
            if (end >= 0)
            {
                return (Encoding.Latin1.GetString(buffer, 0, end), buffer[..length]);
            }
        }

        await RespondAsync(socket, "431 Request Header Fields Too Large", string.Empty, "The request's head is too large.").ConfigureAwait(false);
        return (null, []);
    }

    private static async Task RespondAsync(Socket socket, string status, string headers, string body)
    {
        var content = Encoding.UTF8.GetBytes(body);
        var length = content.Length.ToString(CultureInfo.InvariantCulture);
        var head = Encoding.Latin1.GetBytes($"HTTP/1.1 {status}\r\n{headers}Content-Type: text/plain; charset=utf-8\r\nContent-Length: {length}\r\nConnection: close\r\n\r\n");
        await socket.SendAsync(head, SocketFlags.None).ConfigureAwait(false);
        if (content.Length > 0)
        {
            await socket.SendAsync(content, SocketFlags.None).ConfigureAwait(false);
        }

        socket.Shutdown(SocketShutdown.Both);
    }

    private SwarmTunnel? Find(int stream)
    {
        lock (_gate)
        {
            return _tunnels.GetValueOrDefault(stream);
        }
    }

    private void Remove(SwarmTunnel tunnel)
    {
        lock (_gate)
        {
            if (_tunnels.TryGetValue(tunnel.Stream, out var current) && ReferenceEquals(current, tunnel))
            {
                _tunnels.Remove(tunnel.Stream);
            }
        }
    }
}
