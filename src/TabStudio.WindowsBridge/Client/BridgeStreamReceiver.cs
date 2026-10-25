using System.Threading.Channels;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Client;

/// <summary>
/// The tablet's end of one window's stream: decodes its frames in order, off the connection's thread, keeps the
/// window's picture, and tells the bridge which frame was drawn, which paces the stream.
/// </summary>
public sealed class BridgeStreamReceiver : IDisposable
{
    private readonly BridgeClient _client;
    private readonly FrameDecoder _decoder = new();
    private readonly Channel<BridgeFrame> _frames = Channel.CreateUnbounded<BridgeFrame>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Lock _gate = new();
    private long _decoded;
    private int _waiting;
    private long _shown;
    private long _sequence;
    private int _disposed;

    private BridgeStreamReceiver(BridgeClient client, int streamId)
    {
        _client = client;
        StreamId = streamId;
        _client.FrameReceived += OnFrameReceived;
        _client.StreamEnded += OnStreamEnded;
        _ = Task.Run(DecodeAsync);
    }

    /// <summary>Raised on the thread pool when a new picture is ready to draw.</summary>
    public event EventHandler? FrameReady;

    /// <summary>Raised when the bridge ended the stream on its own, with why, if it was not that the window closed.</summary>
    public event EventHandler<string?>? Ended;

    /// <summary>The stream.</summary>
    public int StreamId { get; }

    /// <summary>How the PC captures the window, such as <c>Windows.Graphics.Capture</c>.</summary>
    public string CaptureMethod { get; private set; } = string.Empty;

    /// <summary>The window's width in the PC's pixels, from the newest frame.</summary>
    public int WindowWidth { get; private set; }

    /// <summary>The window's height in the PC's pixels, from the newest frame.</summary>
    public int WindowHeight { get; private set; }

    /// <summary>The quality level of the newest frame, 0 for exact.</summary>
    public int Level { get; private set; }

    /// <summary>How many frames were decoded.</summary>
    public long FrameCount => Interlocked.Read(ref _decoded);

    /// <summary>Why the stream broke, if a frame could not be decoded.</summary>
    public string? Error { get; private set; }

    /// <summary>Starts streaming a window and receiving its frames, from the first one.</summary>
    /// <param name="client">The connected bridge.</param>
    /// <param name="appId">The program.</param>
    /// <param name="windowId">The window.</param>
    /// <param name="maxWidth">The most pixels wide to show it; a wider window is scaled down on the PC.</param>
    /// <param name="maxHeight">The most pixels high to show it.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The stream's receiver, which stops the stream when disposed.</returns>
    /// <exception cref="TabStudio.Link.LinkRemoteException">The program or the window is gone.</exception>
    /// <exception cref="TabStudio.Link.LinkConnectionLostException">The bridge is not connected.</exception>
    public static async Task<BridgeStreamReceiver> StartAsync(BridgeClient client, int appId, long windowId, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        // Listening before asking: the PC sends the first frame as soon as the stream starts, maybe before its answer.
        var receiver = new BridgeStreamReceiver(client, client.NewStreamId());
        try
        {
            var started = await client.StartStreamAsync(receiver.StreamId, appId, windowId, maxWidth, maxHeight, cancellationToken).ConfigureAwait(false);
            receiver.CaptureMethod = started.CaptureMethod;
            return receiver;
        }
        catch
        {
            receiver.Dispose();
            throw;
        }
    }

    /// <summary>Reads the window's picture, under the lock that keeps the decoder from changing it meanwhile.</summary>
    /// <param name="read">Reads the picture, such as by copying it into a bitmap; called only once there is one.</param>
    /// <typeparam name="T">What it reads.</typeparam>
    /// <returns>What it read, or the default when there is no picture yet.</returns>
    public T? Read<T>(Func<FrameBitmap, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        lock (_gate)
        {
            return _decoder.Frame is { } frame ? read(frame) : default;
        }
    }

    /// <summary>Tells the bridge that the newest picture was drawn.</summary>
    public void Shown()
    {
        var decoded = Interlocked.Read(ref _decoded);
        var sequence = Volatile.Read(ref _sequence);
        if (decoded > Interlocked.Exchange(ref _shown, decoded) && sequence > 0)
        {
            try
            {
                _client.FrameShown(StreamId, sequence);
            }
            catch (TabStudio.Link.LinkConnectionLostException)
            {
                // The bridge left; the stream ends with it.
            }
        }
    }

    /// <summary>Stops receiving, and stops the stream on the PC.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _client.FrameReceived -= OnFrameReceived;
        _client.StreamEnded -= OnStreamEnded;
        _frames.Writer.TryComplete();
        try
        {
            _client.StopStream(StreamId);
        }
        catch (TabStudio.Link.LinkConnectionLostException)
        {
            // The bridge left; its streams ended with it.
        }
    }

    private void OnFrameReceived(object? sender, BridgeFrame frame)
    {
        if (frame.Header.StreamId != StreamId)
        {
            return;
        }

        // Counted before it is queued, so the decoder never sees fewer frames waiting than there are.
        Interlocked.Increment(ref _waiting);
        if (!_frames.Writer.TryWrite(frame))
        {
            Interlocked.Decrement(ref _waiting);
        }
    }

    private void OnStreamEnded(object? sender, StreamEnded ended)
    {
        if (ended.StreamId == StreamId)
        {
            Ended?.Invoke(this, ended.Reason);
        }
    }

    /// <summary>Describes the receiver's state, for diagnostics.</summary>
    /// <returns>The frames decoded and drawn, and the newest frame's size and quality.</returns>
    public override string ToString() =>
        $"received {FrameCount} frames (shown {Interlocked.Read(ref _shown)}, newest {Volatile.Read(ref _sequence)}), " +
        $"{WindowWidth}x{WindowHeight} at level {Level}{(Error is { } error ? $", error: {error}" : string.Empty)}";

    private async Task DecodeAsync()
    {
        await foreach (var frame in _frames.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            // A single-reader channel cannot count what waits in it, so the receiver counts.
            var waiting = Interlocked.Decrement(ref _waiting);
            try
            {
                lock (_gate)
                {
                    _decoder.Apply(frame.Data);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IndexOutOfRangeException)
            {
                // The picture is out of step with the bridge: the pane starts the stream over.
                Error = ex.Message;
                FrameReady?.Invoke(this, EventArgs.Empty);
                continue;
            }

            WindowWidth = frame.Header.WindowWidth;
            WindowHeight = frame.Header.WindowHeight;
            Level = frame.Header.Level;
            Volatile.Write(ref _sequence, frame.Header.Sequence);
            Interlocked.Increment(ref _decoded);

            // Only the newest picture is drawn: frames that are already waiting are applied first.
            if (waiting == 0)
            {
                FrameReady?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
