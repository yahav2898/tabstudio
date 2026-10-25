using System.Threading.Channels;
using TabStudio.WindowsBridge.Frames;

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
    private long _shown;
    private long _sequence;
    private int _disposed;

    /// <summary>Starts receiving a stream's frames.</summary>
    /// <param name="client">The connected bridge.</param>
    /// <param name="streamId">The stream.</param>
    /// <param name="captureMethod">How the PC captures the window.</param>
    public BridgeStreamReceiver(BridgeClient client, int streamId, string captureMethod)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        StreamId = streamId;
        CaptureMethod = captureMethod;
        _client.FrameReceived += OnFrameReceived;
        _ = Task.Run(DecodeAsync);
    }

    /// <summary>Raised on the thread pool when a new picture is ready to draw.</summary>
    public event EventHandler? FrameReady;

    /// <summary>The stream.</summary>
    public int StreamId { get; }

    /// <summary>How the PC captures the window, such as <c>Windows.Graphics.Capture</c>.</summary>
    public string CaptureMethod { get; }

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
        if (frame.Header.StreamId == StreamId)
        {
            _frames.Writer.TryWrite(frame);
        }
    }

    private async Task DecodeAsync()
    {
        await foreach (var frame in _frames.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                lock (_gate)
                {
                    _decoder.Apply(frame.Data);
                }
            }
            catch (InvalidDataException ex)
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
            if (_frames.Reader.Count == 0)
            {
                FrameReady?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
