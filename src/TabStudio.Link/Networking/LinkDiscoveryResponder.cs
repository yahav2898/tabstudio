using System.Net;
using System.Net.Sockets;
using System.Text;
using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>Answers <see cref="LinkDiscovery"/> requests for a host while it is running.</summary>
public sealed class LinkDiscoveryResponder : IDisposable
{
    private readonly LinkFingerprint _fingerprint;
    private readonly Func<int> _port;
    private readonly Func<string> _name;
    private readonly CancellationTokenSource _stopping = new();
    private Socket? _socket;

    /// <summary>Creates a responder.</summary>
    /// <param name="fingerprint">The host's fingerprint.</param>
    /// <param name="port">The host's TCP port, read for every answer.</param>
    /// <param name="name">The host's name, read for every answer.</param>
    public LinkDiscoveryResponder(LinkFingerprint fingerprint, Func<int> port, Func<string> name)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(name);
        _fingerprint = fingerprint;
        _port = port;
        _name = name;
    }

    /// <summary>Starts answering.</summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <returns>Whether the port could be opened; without it, tablets find the host by its known addresses only.</returns>
    public bool Start(int port = LinkProtocol.DiscoveryPort)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        catch (SocketException)
        {
            socket.Dispose();
            return false;
        }

        _socket = socket;
        _ = Task.Run(() => AnswerAsync(socket));
        return true;
    }

    /// <summary>Stops answering.</summary>
    public void Dispose()
    {
        _stopping.Cancel();
        _socket?.Dispose();
        _stopping.Dispose();
    }

    private async Task AnswerAsync(Socket socket)
    {
        var buffer = new byte[1024];
        var token = _stopping.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), token).ConfigureAwait(false);
                var answer = LinkDiscovery.Answer(Encoding.UTF8.GetString(buffer, 0, received.ReceivedBytes), _fingerprint, _port(), _name());
                if (answer is not null)
                {
                    await socket.SendToAsync(Encoding.UTF8.GetBytes(answer), SocketFlags.None, received.RemoteEndPoint, token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // A datagram that could not be answered; keep listening.
            }
        }
    }
}
