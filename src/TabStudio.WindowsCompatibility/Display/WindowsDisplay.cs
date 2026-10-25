using System.Threading.Channels;
using TabStudio.WindowsCompatibility.Display.X11;
using TabStudio.WindowsCompatibility.Input;

namespace TabStudio.WindowsCompatibility.Display;

/// <summary>Where a Windows desktop's X server is and how to reach it.</summary>
/// <param name="Endpoint">The server's socket.</param>
/// <param name="FramebufferPath">The framebuffer file Xvfb maps, <c>Xvfb_screen0</c>, as this process sees it.</param>
/// <param name="Cookie">The server's cookie, or <see langword="null"/> when it checks none.</param>
public sealed record DisplayConnection(X11Endpoint Endpoint, string FramebufferPath, X11Cookie? Cookie);

/// <summary>
/// The screen and input of the desktop Windows apps run on: Wine's virtual desktop in an Xvfb X server. Its
/// frames come straight from Xvfb's framebuffer, with the parts that changed reported through the DAMAGE
/// extension; touch, mouse and keyboard input go back through XTEST.
/// </summary>
/// <remarks>
/// Wine draws its virtual desktop as one X window at the screen's top left corner, so the desktop's pixels are the
/// screen's: <see cref="DesktopSize"/> is the part of the screen to show. Input calls return at once and are sent
/// in order by a background task.
/// </remarks>
public sealed class WindowsDisplay : IDesktopInput, IAsyncDisposable, IDisposable
{
    /// <summary>How often frames are reported at most.</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly X11Client _client;
    private readonly X11Keyboard _keyboard;
    private readonly XwdFramebuffer _framebuffer;
    private readonly Channel<Func<CancellationToken, Task>> _input = Channel.CreateUnbounded<Func<CancellationToken, Task>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private readonly Task _inputPump;
    private readonly Task _framePump;
    private readonly SemaphoreSlim _damaged = new(0, 1);
    private PixelRect _dirty;
    private uint? _damage;
    private uint? _desktopWindow;
    private int _disposed;

    private WindowsDisplay(X11Client client, XwdFramebuffer framebuffer, (int Width, int Height) desktop)
    {
        _client = client;
        _keyboard = new X11Keyboard(client);
        _framebuffer = framebuffer;
        DesktopSize = desktop;
        _client.Damaged += OnDamaged;
        _client.Closed += (_, error) => Disconnected?.Invoke(this, error);
        _inputPump = Task.Run(PumpInputAsync);
        _framePump = Task.Run(PumpFramesAsync);
    }

    /// <summary>The X screen's size; the desktop can grow up to it.</summary>
    public (int Width, int Height) ScreenSize => (_framebuffer.Width, _framebuffer.Height);

    /// <summary>The desktop's size, the part of the screen Windows apps draw on.</summary>
    public (int Width, int Height) DesktopSize { get; private set; }

    /// <summary>Whether the server reports changes itself (DAMAGE) rather than the display looking for them.</summary>
    public bool ReportsDamage => _damage is not null;

    /// <summary>
    /// Part of the desktop changed: the rectangle to copy with <see cref="CopyPixels"/>. Raised on a background
    /// thread, at most once per <see cref="FrameInterval"/>.
    /// </summary>
    public event EventHandler<PixelRect>? FrameChanged;

    /// <summary>The connection to the X server ended, such as when the session stopped.</summary>
    public event EventHandler<Exception?>? Disconnected;

    /// <summary>Connects to the desktop's X server, retrying while it starts.</summary>
    /// <param name="connection">Where the server is.</param>
    /// <param name="desktop">The desktop's size.</param>
    /// <param name="timeout">How long the server may take to start.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="X11Exception">The server did not start in time, or refused the connection.</exception>
    public static async Task<WindowsDisplay> ConnectAsync(DisplayConnection connection, (int Width, int Height) desktop, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            X11Client? client = null;
            try
            {
                if (File.Exists(connection.FramebufferPath) && (connection.Endpoint.SocketPath is not { } socket || File.Exists(socket)))
                {
                    client = await X11Client.ConnectAsync(connection.Endpoint, connection.Cookie, cancellationToken).ConfigureAwait(false);
                    var framebuffer = XwdFramebuffer.Open(connection.FramebufferPath);
                    var display = new WindowsDisplay(client, framebuffer, desktop);
                    await display.StartTrackingAsync(cancellationToken).ConfigureAwait(false);
                    return display;
                }
            }
            catch (Exception ex) when (ex is X11Exception or IOException or System.Net.Sockets.SocketException or InvalidDataException)
            {
                last = ex;
                if (client is not null)
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }

                if (ex is X11Exception { Refused: true })
                {
                    throw;
                }
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw last is null
            ? new X11Exception($"The Windows desktop's X server did not start within {timeout.TotalSeconds:0} seconds.")
            : new X11Exception($"The Windows desktop's X server could not be reached: {last.Message}", last);
    }

    /// <summary>Copies part of the desktop into a buffer of the desktop's size, 32-bit BGRX pixels.</summary>
    /// <param name="area">The part, such as the rectangle <see cref="FrameChanged"/> reported.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="stride">The bytes of one row of the buffer.</param>
    public void CopyPixels(PixelRect area, Span<byte> destination, int stride)
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _framebuffer.Copy(area.Intersect(new PixelRect(0, 0, DesktopSize.Width, DesktopSize.Height)), destination, stride);
            }
        }
    }

    /// <summary>Resizes Wine's desktop window, within the screen. Windows apps see the new screen size.</summary>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task ResizeDesktopAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        width = Math.Clamp(width, 320, _framebuffer.Width);
        height = Math.Clamp(height, 240, _framebuffer.Height);
        if ((width, height) == DesktopSize)
        {
            return;
        }

        DesktopSize = (width, height);
        if (await FindDesktopWindowAsync(cancellationToken).ConfigureAwait(false) is { } window)
        {
            await _client.ConfigureWindowAsync(window, new PixelRect(0, 0, width, height), cancellationToken).ConfigureAwait(false);
        }

        MarkDirty(new PixelRect(0, 0, width, height));
    }

    /// <summary>Waits until every input sent so far reached the X server.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task FlushInputAsync(CancellationToken cancellationToken = default)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(async token =>
        {
            await _client.SyncAsync(token).ConfigureAwait(false);
            done.TrySetResult();
        });
        await done.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void MovePointer(int x, int y) => Enqueue(token => _client.MovePointerAsync(x, y, token));

    /// <inheritdoc />
    public void Button(PointerButton button, bool pressed) => Enqueue(token => _client.ButtonAsync((byte)button, pressed, token));

    /// <inheritdoc />
    public void Wheel(int horizontal, int vertical) => Enqueue(async token =>
    {
        // Buttons 4 and 5 turn the wheel up and down, 6 and 7 left and right; each press is a notch.
        for (var i = 0; i < Math.Abs(vertical); i++)
        {
            var button = (byte)(vertical > 0 ? 4 : 5);
            await _client.ButtonAsync(button, pressed: true, token).ConfigureAwait(false);
            await _client.ButtonAsync(button, pressed: false, token).ConfigureAwait(false);
        }

        for (var i = 0; i < Math.Abs(horizontal); i++)
        {
            var button = (byte)(horizontal > 0 ? 7 : 6);
            await _client.ButtonAsync(button, pressed: true, token).ConfigureAwait(false);
            await _client.ButtonAsync(button, pressed: false, token).ConfigureAwait(false);
        }
    });

    /// <inheritdoc />
    public void Key(WindowsKey key, bool pressed)
    {
        var keysym = KeySyms.For(key);
        if (keysym != 0)
        {
            Enqueue(token => _keyboard.KeyAsync(keysym, pressed, token));
        }
    }

    /// <inheritdoc />
    public void Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 0)
        {
            Enqueue(token => _keyboard.TypeAsync(text, token));
        }
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _input.Writer.TryComplete();
        await _stop.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_inputPump, _framePump).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }

        _client.Damaged -= OnDamaged;
        await _client.DisposeAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _framebuffer.Dispose();
        }

        _keyboard.Dispose();
        _damaged.Dispose();
        _stop.Dispose();
    }

    /// <summary>Finds Wine's desktop window: the top-level window titled "… Wine desktop", or else the largest one.</summary>
    internal async Task<uint?> FindDesktopWindowAsync(CancellationToken cancellationToken)
    {
        if (_desktopWindow is { } known)
        {
            return known;
        }

        uint? largest = null;
        var largestArea = 0L;
        foreach (var window in await _client.TopLevelWindowsAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if ((await _client.WindowNameAsync(window, cancellationToken).ConfigureAwait(false))?.Contains("Wine desktop", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return _desktopWindow = window;
                }

                var bounds = await _client.GeometryAsync(window, cancellationToken).ConfigureAwait(false);
                if ((long)bounds.Width * bounds.Height > largestArea)
                {
                    largest = window;
                    largestArea = (long)bounds.Width * bounds.Height;
                }
            }
            catch (X11Exception)
            {
                // The window went away while it was looked at.
            }
        }

        return largest;
    }

    private async Task StartTrackingAsync(CancellationToken cancellationToken)
    {
        _damage = await _client.TrackDamageAsync(_client.Screen.Root, cancellationToken).ConfigureAwait(false);
        MarkDirty(new PixelRect(0, 0, DesktopSize.Width, DesktopSize.Height));
    }

    private void Enqueue(Func<CancellationToken, Task> action)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _input.Writer.TryWrite(action);
        }
    }

    private async Task PumpInputAsync()
    {
        try
        {
            await foreach (var action in _input.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                try
                {
                    await action(_stop.Token).ConfigureAwait(false);
                }
                catch (X11Exception)
                {
                    // The server went away; the session reports it.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    private void OnDamaged(object? sender, PixelRect area) => MarkDirty(area);

    private void MarkDirty(PixelRect area)
    {
        lock (_gate)
        {
            _dirty = _dirty.Union(area);
        }

        try
        {
            if (_damaged.CurrentCount == 0)
            {
                _damaged.Release();
            }
        }
        catch (SemaphoreFullException)
        {
            // Already signaled.
        }
        catch (ObjectDisposedException)
        {
            // Disposed.
        }
    }

    /// <summary>
    /// Reports what changed, at most once a frame. Damage is cleared before the pixels are read, so drawing that
    /// happens meanwhile is reported again rather than lost.
    /// </summary>
    private async Task PumpFramesAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                if (_damage is null)
                {
                    // Without DAMAGE, look for changes a few times a second.
                    await Task.Delay(100, _stop.Token).ConfigureAwait(false);
                    MarkDirty(new PixelRect(0, 0, DesktopSize.Width, DesktopSize.Height));
                }

                await _damaged.WaitAsync(_stop.Token).ConfigureAwait(false);
                PixelRect area;
                lock (_gate)
                {
                    area = _dirty.Intersect(new PixelRect(0, 0, DesktopSize.Width, DesktopSize.Height));
                    _dirty = default;
                }

                if (_damage is { } damage)
                {
                    try
                    {
                        await _client.ClearDamageAsync(damage, _stop.Token).ConfigureAwait(false);
                    }
                    catch (X11Exception)
                    {
                        return;
                    }
                }

                if (!area.IsEmpty)
                {
                    FrameChanged?.Invoke(this, area);
                }

                await Task.Delay(FrameInterval, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }
}
