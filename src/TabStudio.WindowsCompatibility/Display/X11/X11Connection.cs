using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>
/// A client connection to an X server, speaking the X11 wire protocol directly: the connection setup with an
/// optional MIT-MAGIC-COOKIE-1, requests with and without replies, and the server's events and errors.
/// </summary>
/// <remarks>
/// <para>
/// TabStudio talks to the X server Wine draws into (Xvfb) itself rather than through Xlib, so nothing native is
/// needed on Android. Only what the IDE uses is implemented: the core requests in <see cref="X11Client"/>, and the
/// XTEST and DAMAGE extensions.
/// </para>
/// <para>
/// Requests are written in the client's byte order (LSB first) under a lock, each taking the next sequence number.
/// A reader task reads everything the server sends: a reply completes the request with the same sequence number,
/// an error fails it (or is raised through <see cref="ErrorReceived"/> for a request without a reply), and events
/// are raised through <see cref="EventReceived"/>, on the reader's thread.
/// </para>
/// </remarks>
internal sealed class X11Connection : IAsyncDisposable, IDisposable
{
    private const byte ReplyType = 1;
    private const byte ErrorType = 0;
    private const byte GenericEventType = 35;

    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<X11Reply>> _pending = new();
    private readonly CancellationTokenSource _closing = new();
    private Task _reader = Task.CompletedTask;
    private ushort _sequence;
    private uint _nextId;
    private int _closed;

    private X11Connection(Socket socket, X11Setup setup)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
        Setup = setup;
    }

    /// <summary>What the server said about itself.</summary>
    public X11Setup Setup { get; }

    /// <summary>Whether the connection is still open.</summary>
    public bool IsOpen => Volatile.Read(ref _closed) == 0;

    /// <summary>An event arrived. Raised on the reader's thread; handlers must be quick.</summary>
    public event EventHandler<X11Event>? EventReceived;

    /// <summary>The server reported an error for a request nobody waits for.</summary>
    public event EventHandler<X11Error>? ErrorReceived;

    /// <summary>The connection closed: the server went away, or it was disposed. The exception says why, if it broke.</summary>
    public event EventHandler<Exception?>? Closed;

    /// <summary>Connects and completes the connection setup.</summary>
    /// <param name="endpoint">Where the server listens.</param>
    /// <param name="cookie">The server's cookie, or <see langword="null"/> for a server without access control.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="X11Exception">The server refused the connection.</exception>
    public static async Task<X11Connection> ConnectAsync(X11Endpoint endpoint, X11Cookie? cookie, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var socket = await endpoint.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var stream = new NetworkStream(socket, ownsSocket: false);
            await stream.WriteAsync(SetupRequest(cookie), cancellationToken).ConfigureAwait(false);

            var header = new byte[8];
            await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            var data = new byte[BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6)) * 4];
            await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
            if (header[0] != 1)
            {
                var reason = header[0] == 0 ? Encoding.ASCII.GetString(data, 0, Math.Min(header[1], data.Length)).Trim() : "it asks for more authentication";
                throw new X11Exception($"The X server at {endpoint} refused the connection: {reason}") { Refused = true };
            }

            var connection = new X11Connection(socket, X11Setup.Parse(data));
            connection._reader = Task.Run(connection.ReadLoopAsync, CancellationToken.None);
            return connection;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>A new resource id for this client, such as for a DAMAGE object.</summary>
    public uint NewId()
    {
        var next = Interlocked.Increment(ref _nextId);
        return Setup.ResourceIdBase | (next & Setup.ResourceIdMask);
    }

    /// <summary>Sends a request without a reply.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Gives up waiting to send.</param>
    public async Task SendAsync(X11Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfClosed();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sequence++;
            await _stream.WriteAsync(request.ToBytes(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new X11Exception("The connection to the X server broke.", ex);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Sends a request and waits for its reply.</summary>
    /// <param name="request">The request; it must be one the server replies to.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="X11Exception">The server reported an error, or the connection broke.</exception>
    public async Task<X11Reply> RequestAsync(X11Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfClosed();
        var reply = new TaskCompletionSource<X11Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ushort sequence;
        try
        {
            sequence = ++_sequence;
            _pending[sequence] = reply;
            await _stream.WriteAsync(request.ToBytes(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _pending.TryRemove(_sequence, out _);
            throw new X11Exception("The connection to the X server broke.", ex);
        }
        finally
        {
            _writeGate.Release();
        }

        using (cancellationToken.Register(() => reply.TrySetCanceled(cancellationToken)))
        {
            try
            {
                return await reply.Task.ConfigureAwait(false);
            }
            finally
            {
                _pending.TryRemove(sequence, out _);
            }
        }
    }

    /// <summary>Waits until the server has handled every request sent before, with a round trip.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public Task SyncAsync(CancellationToken cancellationToken = default) =>
        RequestAsync(new X11Request(X11Opcodes.GetInputFocus, 0, 4) { HasReply = true }, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        await _closing.CancelAsync().ConfigureAwait(false);
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
            // Already gone.
        }

        _socket.Dispose();
        try
        {
            await _reader.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The reader stops when the socket closes.
        }

        await _stream.DisposeAsync().ConfigureAwait(false);
        _writeGate.Dispose();
        _closing.Dispose();
        FailPending(null);
        Closed?.Invoke(this, null);
    }

    private static byte[] SetupRequest(X11Cookie? cookie)
    {
        var name = cookie is null ? [] : Encoding.ASCII.GetBytes(X11Cookie.ProtocolName);
        var data = cookie is null ? [] : cookie.Data.ToArray();
        var request = new byte[12 + X11Wire.Pad(name.Length) + X11Wire.Pad(data.Length)];
        request[0] = (byte)'l';
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), 11);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6), (ushort)name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(8), (ushort)data.Length);
        name.CopyTo(request, 12);
        data.CopyTo(request, 12 + X11Wire.Pad(name.Length));
        return request;
    }

    private async Task ReadLoopAsync()
    {
        Exception? failure = null;
        try
        {
            var header = new byte[32];
            while (!_closing.IsCancellationRequested)
            {
                await _stream.ReadExactlyAsync(header, _closing.Token).ConfigureAwait(false);
                var extra = header[0] is ReplyType or GenericEventType ? (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) * 4 : 0;
                var packet = new byte[32 + extra];
                header.CopyTo(packet, 0);
                if (extra > 0)
                {
                    await _stream.ReadExactlyAsync(packet.AsMemory(32), _closing.Token).ConfigureAwait(false);
                }

                Dispatch(packet);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or EndOfStreamException)
        {
            failure = _closing.IsCancellationRequested ? null : ex;
        }

        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            FailPending(failure);
            Closed?.Invoke(this, failure);
        }
    }

    private void Dispatch(byte[] packet)
    {
        switch (packet[0])
        {
            case ReplyType:
                if (_pending.TryGetValue(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)), out var reply))
                {
                    reply.TrySetResult(new X11Reply(packet));
                }

                break;
            case ErrorType:
                var error = X11Error.Parse(packet);
                if (_pending.TryGetValue(error.Sequence, out var failed))
                {
                    failed.TrySetException(new X11Exception(error));
                }
                else
                {
                    ErrorReceived?.Invoke(this, error);
                }

                break;
            default:
                EventReceived?.Invoke(this, new X11Event(packet));
                break;
        }
    }

    private void FailPending(Exception? cause)
    {
        foreach (var (_, pending) in _pending)
        {
            pending.TrySetException(cause is null ? new X11Exception("The connection to the X server was closed.") : new X11Exception("The connection to the X server broke.", cause));
        }

        _pending.Clear();
    }

    private void ThrowIfClosed()
    {
        if (!IsOpen)
        {
            throw new X11Exception("The connection to the X server is closed.");
        }
    }
}
