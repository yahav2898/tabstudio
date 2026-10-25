using System.Diagnostics;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// Streams one window to the tablet: captures its picture, sends only the tiles that changed at the quality the
/// link allows, and sends rough tiles again exactly once the window is still. Input to the window wakes the stream at
/// once, so a click or a key shows on the tablet in the next frame.
/// </summary>
internal sealed class BridgeStream : IDisposable
{
    /// <summary>The shortest time between two frames: about 60 frames a second.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>The longest pause between two looks at a window that does not change.</summary>
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>How long frames may go unshown before the stream starts over with a keyframe.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(6);

    private readonly IWindowCapture _capture;
    private readonly Func<FrameHeader, byte[], Task> _send;
    private readonly FrameEncoder _encoder = new();
    private readonly AdaptiveQuality _quality = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _gate = new();
    private readonly Task _loop;
    private long _sequence;
    private TimeSpan _lastShown;
    private int _disposed;

    /// <summary>Starts streaming a window.</summary>
    /// <param name="id">The bridge's number for the stream.</param>
    /// <param name="appId">The program.</param>
    /// <param name="windowId">The window.</param>
    /// <param name="maxWidth">The most pixels wide the tablet shows; a wider window is scaled down.</param>
    /// <param name="maxHeight">The most pixels high the tablet shows.</param>
    /// <param name="desktop">The PC's desktop.</param>
    /// <param name="send">Sends a frame to the tablet.</param>
    public BridgeStream(int id, int appId, long windowId, int maxWidth, int maxHeight, IBridgeDesktop desktop, Func<FrameHeader, byte[], Task> send)
    {
        Id = id;
        AppId = appId;
        WindowId = windowId;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        _send = send;
        _capture = desktop.StartCapture(windowId);
        _loop = Task.Run(RunAsync);
    }

    /// <summary>Raised on the thread pool once, when the window closed and the stream ended.</summary>
    public event EventHandler? Ended;

    /// <summary>The bridge's number for the stream.</summary>
    public int Id { get; }

    /// <summary>The program.</summary>
    public int AppId { get; }

    /// <summary>The window.</summary>
    public long WindowId { get; }

    /// <summary>The most pixels wide the tablet shows.</summary>
    public int MaxWidth { get; }

    /// <summary>The most pixels high the tablet shows.</summary>
    public int MaxHeight { get; }

    /// <summary>How the window is captured.</summary>
    public string CaptureMethod => _capture.Method;

    /// <summary>The quality level now, for the bridge's window.</summary>
    public int Level
    {
        get
        {
            lock (_gate)
            {
                return _quality.Level;
            }
        }
    }

    /// <summary>The tablet drew a frame: the stream may send the next one.</summary>
    /// <param name="sequence">The frame's number.</param>
    public void OnShown(long sequence)
    {
        lock (_gate)
        {
            _quality.OnShown(sequence, _clock.Elapsed);
            _lastShown = _clock.Elapsed;
        }

        Wake();
    }

    /// <summary>Looks at the window at once, such as after input that will change it.</summary>
    public void Wake()
    {
        try
        {
            _wake.Release();
        }
        catch (Exception ex) when (ex is SemaphoreFullException or ObjectDisposedException)
        {
            // Already woken, or stopped.
        }
    }

    /// <summary>Stops streaming.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stopping.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop's failure ended the stream either way.
        }

        _capture.Dispose();
        _stopping.Dispose();
        _wake.Dispose();
    }

    private async Task RunAsync()
    {
        var token = _stopping.Token;
        FrameBitmap? window = null;
        FrameBitmap? scaled = null;
        var idle = TimeSpan.Zero;
        try
        {
            while (!token.IsCancellationRequested)
            {
                bool canSend;
                lock (_gate)
                {
                    canSend = _quality.CanSend;
                    if (!canSend && _clock.Elapsed - _lastShown > StallTimeout && _quality.InFlight > 0)
                    {
                        // The tablet lost frames, as after a reconnect: start over with the whole picture.
                        _quality.Reset();
                        _encoder.Reset();
                        canSend = true;
                    }
                }

                if (!canSend)
                {
                    await _wake.WaitAsync(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    continue;
                }

                var result = _capture.Capture(window);
                if (result.Status == CaptureStatus.Closed)
                {
                    break;
                }

                if (result.Status == CaptureStatus.Unavailable)
                {
                    await _wake.WaitAsync(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
                    continue;
                }

                if (result.Status == CaptureStatus.Captured)
                {
                    window = result.Picture;
                }

                if (window is null || (result.Status == CaptureStatus.Unchanged && !_encoder.HasRoughTiles))
                {
                    idle = Next(idle);
                    await _wake.WaitAsync(idle, token).ConfigureAwait(false);
                    continue;
                }

                var picture = window.ScaleToFit(MaxWidth, MaxHeight, scaled);
                if (!ReferenceEquals(picture, window))
                {
                    scaled = picture;
                }

                int level;
                int refine;
                lock (_gate)
                {
                    level = _quality.Level;
                    refine = _encoder.HasRoughTiles ? _quality.RefinementBudget : 0;
                }

                var encoded = _encoder.Encode(picture, level, refine);
                if (encoded is null)
                {
                    idle = Next(idle);
                    await _wake.WaitAsync(idle, token).ConfigureAwait(false);
                    continue;
                }

                idle = TimeSpan.Zero;
                var sequence = ++_sequence;
                lock (_gate)
                {
                    _quality.OnSent(sequence, encoded.Data.Length, _clock.Elapsed);
                    if (_quality.InFlight == 1)
                    {
                        _lastShown = _clock.Elapsed;
                    }
                }

                await _send(new FrameHeader(Id, sequence, window.Width, window.Height, level), encoded.Data).ConfigureAwait(false);
                await Task.Delay(FrameInterval, token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or TabStudio.Link.LinkConnectionLostException)
        {
            // Stopped, or the tablet left.
        }

        if (!token.IsCancellationRequested && Ended is { } ended)
        {
            _ = Task.Run(() => ended(this, EventArgs.Empty), CancellationToken.None);
        }
    }

    /// <summary>The next pause while the window does not change: a little longer each time, up to a fifth of a second.</summary>
    private static TimeSpan Next(TimeSpan idle) =>
        idle == TimeSpan.Zero ? FrameInterval : TimeSpan.FromTicks(Math.Min(idle.Ticks * 2, IdleInterval.Ticks));
}
