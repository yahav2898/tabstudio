using System.Text;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>
/// The X11 requests TabStudio makes of the X server Wine draws into: finding and resizing Wine's desktop window,
/// fake input through XTEST, damage reports through DAMAGE, and the keyboard mapping.
/// </summary>
internal sealed class X11Client : IAsyncDisposable, IDisposable
{
    /// <summary>The predefined atom <c>WM_NAME</c>.</summary>
    private const uint WmName = 39;

    private readonly X11Connection _connection;
    private readonly byte _xtest;
    private readonly byte _damage;
    private readonly byte _damageEvent;

    private X11Client(X11Connection connection, byte xtest, byte damage, byte damageEvent)
    {
        _connection = connection;
        _xtest = xtest;
        _damage = damage;
        _damageEvent = damageEvent;
        _connection.EventReceived += OnEvent;
        _connection.Closed += (_, error) => Closed?.Invoke(this, error);
    }

    /// <summary>The server's screen.</summary>
    public X11Screen Screen => _connection.Setup.Screen;

    /// <summary>The server's setup.</summary>
    public X11Setup Setup => _connection.Setup;

    /// <summary>Whether the server has the DAMAGE extension, so changes are reported rather than looked for.</summary>
    public bool HasDamage => _damage != 0;

    /// <summary>Whether the connection is open.</summary>
    public bool IsOpen => _connection.IsOpen;

    /// <summary>Part of the screen changed, as a DAMAGE object reports it. Raised on the connection's reader thread.</summary>
    public event EventHandler<PixelRect>? Damaged;

    /// <summary>The keyboard mapping changed.</summary>
    public event EventHandler? KeyboardMappingChanged;

    /// <summary>The connection closed.</summary>
    public event EventHandler<Exception?>? Closed;

    /// <summary>Connects and checks the extensions TabStudio needs: XTEST for input, and DAMAGE when the server has it.</summary>
    /// <param name="endpoint">Where the server listens.</param>
    /// <param name="cookie">Its cookie, if it checks one.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="X11Exception">The server cannot be used: it refused, or it has no XTEST.</exception>
    public static async Task<X11Client> ConnectAsync(X11Endpoint endpoint, X11Cookie? cookie, CancellationToken cancellationToken)
    {
        var connection = await X11Connection.ConnectAsync(endpoint, cookie, cancellationToken).ConfigureAwait(false);
        try
        {
            var xtest = await QueryExtensionAsync(connection, "XTEST", cancellationToken).ConfigureAwait(false)
                ?? throw new X11Exception("The X server has no XTEST extension, which TabStudio sends touch and keys through.");
            await connection.RequestAsync(
                new X11Request(xtest.Opcode, X11Opcodes.XTestGetVersion, 8) { HasReply = true }.Byte(2).Skip(1).UInt16(2),
                cancellationToken).ConfigureAwait(false);

            var damage = await QueryExtensionAsync(connection, "DAMAGE", cancellationToken).ConfigureAwait(false);
            if (damage is { } found)
            {
                await connection.RequestAsync(
                    new X11Request(found.Opcode, X11Opcodes.DamageQueryVersion, 12) { HasReply = true }.UInt32(1).UInt32(1),
                    cancellationToken).ConfigureAwait(false);
            }

            return new X11Client(connection, xtest.Opcode, damage?.Opcode ?? 0, damage?.FirstEvent ?? 0);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The top-level windows: the root window's children, bottom first.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<IReadOnlyList<uint>> TopLevelWindowsAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.RequestAsync(new X11Request(X11Opcodes.QueryTree, 0, 8) { HasReply = true }.UInt32(Screen.Root), cancellationToken).ConfigureAwait(false);
        var count = reply.UInt16(16);
        var windows = new uint[count];
        for (var i = 0; i < count; i++)
        {
            windows[i] = reply.UInt32(32 + (4 * i));
        }

        return windows;
    }

    /// <summary>A window's position and size, relative to its parent.</summary>
    /// <param name="window">The window.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<PixelRect> GeometryAsync(uint window, CancellationToken cancellationToken = default)
    {
        var reply = await _connection.RequestAsync(new X11Request(X11Opcodes.GetGeometry, 0, 8) { HasReply = true }.UInt32(window), cancellationToken).ConfigureAwait(false);
        return new PixelRect(reply.Int16(12), reply.Int16(14), reply.UInt16(16), reply.UInt16(18));
    }

    /// <summary>A window's title (<c>WM_NAME</c>), or <see langword="null"/> when it has none.</summary>
    /// <param name="window">The window.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<string?> WindowNameAsync(uint window, CancellationToken cancellationToken = default)
    {
        var reply = await _connection.RequestAsync(
            new X11Request(X11Opcodes.GetProperty, 0, 24) { HasReply = true }.UInt32(window).UInt32(WmName).UInt32(0).UInt32(0).UInt32(256),
            cancellationToken).ConfigureAwait(false);
        var format = reply.Data;
        var length = (int)reply.UInt32(16) * (format / 8);
        return format == 8 && length > 0 ? Encoding.Latin1.GetString(reply.Bytes, 32, Math.Min(length, reply.Bytes.Length - 32)) : null;
    }

    /// <summary>Moves and resizes a window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="bounds">Its new position and size.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task ConfigureWindowAsync(uint window, PixelRect bounds, CancellationToken cancellationToken = default) =>
        _connection.SendAsync(
            new X11Request(X11Opcodes.ConfigureWindow, 0, 28)
                .UInt32(window).UInt16(0b1111).Skip(2)
                .UInt32((uint)bounds.X).UInt32((uint)bounds.Y).UInt32((uint)bounds.Width).UInt32((uint)bounds.Height),
            cancellationToken);

    /// <summary>Moves the pointer to a point on the screen.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task MovePointerAsync(int x, int y, CancellationToken cancellationToken = default) =>
        FakeInputAsync(X11Opcodes.MotionNotify, 0, x, y, cancellationToken);

    /// <summary>Presses or releases a pointer button: 1 left, 2 middle, 3 right, 4 and 5 the wheel up and down, 6 and 7 left and right.</summary>
    /// <param name="button">The button.</param>
    /// <param name="pressed">Whether it is pressed.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task ButtonAsync(byte button, bool pressed, CancellationToken cancellationToken = default) =>
        FakeInputAsync(pressed ? X11Opcodes.ButtonPress : X11Opcodes.ButtonRelease, button, 0, 0, cancellationToken);

    /// <summary>Presses or releases a key.</summary>
    /// <param name="keycode">The key code.</param>
    /// <param name="pressed">Whether it is pressed.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task KeyAsync(byte keycode, bool pressed, CancellationToken cancellationToken = default) =>
        FakeInputAsync(pressed ? X11Opcodes.KeyPress : X11Opcodes.KeyRelease, keycode, 0, 0, cancellationToken);

    /// <summary>The key symbols of every key code.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<KeyboardMapping> KeyboardMappingAsync(CancellationToken cancellationToken = default)
    {
        var first = Setup.MinKeycode;
        var count = Setup.MaxKeycode - first + 1;
        var reply = await _connection.RequestAsync(
            new X11Request(X11Opcodes.GetKeyboardMapping, 0, 8) { HasReply = true }.Byte(first).Byte((byte)count).Skip(2),
            cancellationToken).ConfigureAwait(false);
        var perKeycode = reply.Data;
        var keysyms = new uint[count * perKeycode];
        for (var i = 0; i < keysyms.Length; i++)
        {
            keysyms[i] = reply.UInt32(32 + (4 * i));
        }

        return new KeyboardMapping(first, perKeycode, keysyms);
    }

    /// <summary>Gives a key code new key symbols, such as a spare one a character to type.</summary>
    /// <param name="keycode">The key code.</param>
    /// <param name="keysyms">Its symbols: without and with Shift.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task ChangeKeyboardMappingAsync(byte keycode, IReadOnlyList<uint> keysyms, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keysyms);
        var request = new X11Request(X11Opcodes.ChangeKeyboardMapping, 1, 8 + (4 * keysyms.Count)).Byte(keycode).Byte((byte)keysyms.Count).Skip(2);
        foreach (var keysym in keysyms)
        {
            request.UInt32(keysym);
        }

        return _connection.SendAsync(request, cancellationToken);
    }

    /// <summary>Starts reporting what changes on a drawable, such as the root window, through <see cref="Damaged"/>.</summary>
    /// <param name="drawable">The drawable.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The damage object, or <see langword="null"/> when the server has no DAMAGE extension.</returns>
    public async Task<uint?> TrackDamageAsync(uint drawable, CancellationToken cancellationToken = default)
    {
        if (!HasDamage)
        {
            return null;
        }

        var id = _connection.NewId();
        await _connection.SendAsync(
            new X11Request(_damage, X11Opcodes.DamageCreate, 16).UInt32(id).UInt32(drawable).Byte(X11Opcodes.DamageReportBoundingBox).Skip(3),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <summary>Clears a damage object, so the next change is reported again.</summary>
    /// <param name="damage">The damage object.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public Task ClearDamageAsync(uint damage, CancellationToken cancellationToken = default) =>
        _connection.SendAsync(new X11Request(_damage, X11Opcodes.DamageSubtract, 16).UInt32(damage).UInt32(0).UInt32(0), cancellationToken);

    /// <summary>Waits until the server has handled every request sent before.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public Task SyncAsync(CancellationToken cancellationToken = default) => _connection.SyncAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _connection.EventReceived -= OnEvent;
        return _connection.DisposeAsync();
    }

    private Task FakeInputAsync(byte type, byte detail, int x, int y, CancellationToken cancellationToken) =>
        _connection.SendAsync(
            new X11Request(_xtest, X11Opcodes.XTestFakeInput, 36)
                .Byte(type).Byte(detail).Skip(2)
                .UInt32(0)
                .UInt32(type == X11Opcodes.MotionNotify ? Screen.Root : 0)
                .Skip(8)
                .Int16((short)x).Int16((short)y)
                .Skip(8),
            cancellationToken);

    private void OnEvent(object? sender, X11Event e)
    {
        if (_damage != 0 && e.Code == _damageEvent)
        {
            Damaged?.Invoke(this, new PixelRect(e.Int16(16), e.Int16(18), e.UInt16(20), e.UInt16(22)));
        }
        else if (e.Code == X11Opcodes.MappingNotify)
        {
            KeyboardMappingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static async Task<(byte Opcode, byte FirstEvent)?> QueryExtensionAsync(X11Connection connection, string name, CancellationToken cancellationToken)
    {
        var reply = await connection.RequestAsync(
            new X11Request(X11Opcodes.QueryExtension, 0, 8 + X11Wire.Pad(name.Length)) { HasReply = true }.UInt16((ushort)name.Length).Skip(2).Text(name),
            cancellationToken).ConfigureAwait(false);
        return reply.Bytes[8] != 0 ? (reply.Bytes[9], reply.Bytes[10]) : null;
    }
}
