using System.Net.Sockets;
using System.Threading.Channels;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Web;

/// <summary>
/// One TCP connection carried through the swarm's TabStudio Link connection: on a device, between its web view and
/// the device's loopback gateway; on the tablet, between the coordinator and the web app it serves. Bytes read from
/// the socket travel as <see cref="SwarmMethods.WebData"/> notifications; bytes that arrive are written to the socket
/// in order. Each side closing its direction is told with <see cref="SwarmMethods.WebClose"/>.
/// </summary>
internal sealed class SwarmTunnel
{
    /// <summary>The most bytes one notification carries.</summary>
    private const int ChunkLength = 64 * 1024;

    private readonly Socket _socket;
    private readonly Channel<byte[]> _outgoing = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Action<SwarmTunnel> _closed;
    private int _directionsDone;
    private int _released;

    /// <summary>Creates a tunnel. Call <see cref="Start"/> to begin carrying bytes.</summary>
    /// <param name="stream">The connection's number, which both sides use.</param>
    /// <param name="socket">The local end. Owned by the tunnel from now on.</param>
    /// <param name="link">The swarm's connection.</param>
    /// <param name="closed">Called once both directions are closed.</param>
    public SwarmTunnel(int stream, Socket socket, LinkConnection link, Action<SwarmTunnel> closed)
    {
        Stream = stream;
        _socket = socket;
        Link = link;
        _closed = closed;
    }

    /// <summary>The connection's number.</summary>
    public int Stream { get; }

    /// <summary>The swarm's connection it travels on.</summary>
    public LinkConnection Link { get; }

    /// <summary>Starts carrying bytes both ways.</summary>
    /// <param name="first">Bytes already read from the socket, such as a request's head, sent before anything else.</param>
    public void Start(ReadOnlyMemory<byte> first)
    {
        _ = Task.Run(() => ReadSocketAsync(first));
        _ = Task.Run(WriteSocketAsync);
    }

    /// <summary>Writes bytes that arrived from the other side to the socket, after those before them.</summary>
    /// <param name="data">The bytes.</param>
    public void Deliver(byte[] data) => _outgoing.Writer.TryWrite(data);

    /// <summary>The other side closed its direction: the socket's sending side closes once everything is written.</summary>
    public void CloseFromRemote() => _outgoing.Writer.TryComplete();

    /// <summary>Closes the connection at once, as when the swarm's connection was lost.</summary>
    public void Abort()
    {
        _outgoing.Writer.TryComplete();
        Release();
    }

    private async Task ReadSocketAsync(ReadOnlyMemory<byte> first)
    {
        var body = SwarmJson.Body(new SwarmStreamNotice(Stream), SwarmJsonContext.Default.SwarmStreamNotice);
        try
        {
            if (!first.IsEmpty)
            {
                await Link.NotifyAsync(SwarmMethods.WebData, body, [first]).ConfigureAwait(false);
            }

            var buffer = new byte[ChunkLength];
            while (true)
            {
                var read = await _socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                // The attachment is copied into its frame before NotifyAsync returns, so the buffer can be reused.
                await Link.NotifyAsync(SwarmMethods.WebData, body, [buffer.AsMemory(0, read)]).ConfigureAwait(false);
            }

            await Link.NotifyAsync(SwarmMethods.WebClose, body).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or LinkConnectionLostException)
        {
            // The socket or the swarm's connection closed; the other direction finds out by itself.
            _outgoing.Writer.TryComplete();
        }

        DirectionDone();
    }

    private async Task WriteSocketAsync()
    {
        try
        {
            await foreach (var data in _outgoing.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await _socket.SendAsync(data, SocketFlags.None).ConfigureAwait(false);
            }

            _socket.Shutdown(SocketShutdown.Send);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            // The socket closed.
        }

        DirectionDone();
    }

    private void DirectionDone()
    {
        if (Interlocked.Increment(ref _directionsDone) == 2)
        {
            Release();
        }
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) == 1)
        {
            return;
        }

        _socket.Dispose();
        _closed(this);
    }
}
