using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace TabStudio.WindowsCompatibility.Tests.Infrastructure;

/// <summary>An input XTEST was asked to fake.</summary>
/// <param name="Type">The core event type: 2 key press, 3 key release, 4 button press, 5 button release, 6 motion.</param>
/// <param name="Detail">The key code or button.</param>
/// <param name="X">The pointer's x, for motion.</param>
/// <param name="Y">The pointer's y, for motion.</param>
public sealed record FakeInput(byte Type, byte Detail, short X, short Y);

/// <summary>
/// Just enough of an X server for TabStudio's X11 client: the connection setup (with a cookie), XTEST and DAMAGE,
/// the keyboard mapping, and a desktop window named like Wine's. It records what clients ask for and can send
/// damage events.
/// </summary>
public sealed class FakeXServer : IAsyncDisposable
{
    public const byte XTestOpcode = 140;
    public const byte DamageOpcode = 141;
    public const byte DamageFirstEvent = 90;
    public const uint Root = 0x100;
    public const uint DesktopWindow = 0x400001;
    public const byte FirstKeycode = 8;
    public const byte LastKeycode = 255;

    private readonly Socket _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accept;
    private readonly byte[]? _cookie;
    private readonly uint[] _keysyms;
    private NetworkStream? _client;
    private readonly SemaphoreSlim _write = new(1, 1);
    private ushort _sequence;

    public FakeXServer(string socketPath, byte[]? cookie = null, int width = 1280, int height = 800)
    {
        SocketPath = socketPath;
        _cookie = cookie;
        Width = width;
        Height = height;
        _keysyms = new uint[(LastKeycode - FirstKeycode + 1) * 2];
        SetKey(38, 'a', 'A');
        SetKey(50, 0xFFE1, 0);
        SetKey(37, 0xFFE3, 0);
        SetKey(36, 0xFF0D, 0);
        SetKey(65, ' ', 0);
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        _listener.Listen(4);
        _accept = Task.Run(AcceptAsync);
    }

    public string SocketPath { get; }

    public int Width { get; }

    public int Height { get; }

    public ConcurrentQueue<FakeInput> Inputs { get; } = new();

    public ConcurrentQueue<(uint Window, int X, int Y, int Width, int Height)> Configured { get; } = new();

    public ConcurrentQueue<(byte Keycode, uint Keysym)> Remapped { get; } = new();

    public int DamageCreated { get; private set; }

    public int DamageCleared;

    public bool Refused { get; private set; }

    public uint? Damage { get; private set; }

    public void SetKey(byte keycode, uint plain, uint shifted)
    {
        _keysyms[(keycode - FirstKeycode) * 2] = plain;
        _keysyms[((keycode - FirstKeycode) * 2) + 1] = shifted;
    }

    /// <summary>Sends a DamageNotify for an area, as Xvfb does after drawing.</summary>
    public async Task SendDamageAsync(short x, short y, ushort width, ushort height)
    {
        var e = new byte[32];
        e[0] = DamageFirstEvent;
        e[1] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(2), _sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(e.AsSpan(4), Root);
        BinaryPrimitives.WriteUInt32LittleEndian(e.AsSpan(8), Damage ?? 0);
        BinaryPrimitives.WriteInt16LittleEndian(e.AsSpan(16), x);
        BinaryPrimitives.WriteInt16LittleEndian(e.AsSpan(18), y);
        BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(20), width);
        BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(22), height);
        await WriteAsync(e);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Dispose();
        _client?.Dispose();
        try
        {
            await _accept;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
        }

        _stop.Dispose();
        _write.Dispose();
        File.Delete(SocketPath);
    }

    private async Task AcceptAsync()
    {
        using var socket = await _listener.AcceptAsync(_stop.Token);
        _client = new NetworkStream(socket, ownsSocket: false);
        var stream = _client;
        var setup = new byte[12];
        await stream.ReadExactlyAsync(setup, _stop.Token);
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(setup.AsSpan(6));
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(setup.AsSpan(8));
        var auth = new byte[Pad(nameLength) + Pad(dataLength)];
        await stream.ReadExactlyAsync(auth, _stop.Token);
        var data = auth.AsSpan(Pad(nameLength), dataLength).ToArray();
        if (_cookie is not null && !data.AsSpan().SequenceEqual(_cookie))
        {
            Refused = true;
            var reason = "No protocol specified\n"u8.ToArray();
            var failed = new byte[8 + Pad(reason.Length)];
            failed[1] = (byte)reason.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(failed.AsSpan(2), 11);
            BinaryPrimitives.WriteUInt16LittleEndian(failed.AsSpan(6), (ushort)(Pad(reason.Length) / 4));
            reason.CopyTo(failed, 8);
            await stream.WriteAsync(failed, _stop.Token);
            return;
        }

        await stream.WriteAsync(SetupReply(), _stop.Token);
        var header = new byte[4];
        while (!_stop.IsCancellationRequested)
        {
            await stream.ReadExactlyAsync(header, _stop.Token);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2)) * 4;
            var request = new byte[length];
            header.CopyTo(request, 0);
            await stream.ReadExactlyAsync(request.AsMemory(4), _stop.Token);
            _sequence++;
            await HandleAsync(request);
        }
    }

    private async Task HandleAsync(byte[] request)
    {
        switch (request[0])
        {
            case 98: // QueryExtension
                var name = Encoding.ASCII.GetString(request, 8, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(4)));
                var reply = Reply();
                (reply[8], reply[9], reply[10]) = name switch
                {
                    "XTEST" => ((byte)1, XTestOpcode, (byte)0),
                    "DAMAGE" => ((byte)1, DamageOpcode, DamageFirstEvent),
                    _ => ((byte)0, (byte)0, (byte)0),
                };
                await WriteAsync(reply);
                break;
            case XTestOpcode when request[1] == 0:
                var version = Reply();
                version[1] = 2;
                BinaryPrimitives.WriteUInt16LittleEndian(version.AsSpan(8), 2);
                await WriteAsync(version);
                break;
            case XTestOpcode when request[1] == 2:
                Inputs.Enqueue(new FakeInput(request[4], request[5], BinaryPrimitives.ReadInt16LittleEndian(request.AsSpan(24)), BinaryPrimitives.ReadInt16LittleEndian(request.AsSpan(26))));
                break;
            case DamageOpcode when request[1] == 0:
                var damageVersion = Reply();
                BinaryPrimitives.WriteUInt32LittleEndian(damageVersion.AsSpan(8), 1);
                BinaryPrimitives.WriteUInt32LittleEndian(damageVersion.AsSpan(12), 1);
                await WriteAsync(damageVersion);
                break;
            case DamageOpcode when request[1] == 1:
                Damage = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(4));
                DamageCreated++;
                break;
            case DamageOpcode when request[1] == 3:
                Interlocked.Increment(ref DamageCleared);
                break;
            case 43: // GetInputFocus
                await WriteAsync(Reply());
                break;
            case 15: // QueryTree
                var tree = Reply(4);
                BinaryPrimitives.WriteUInt32LittleEndian(tree.AsSpan(8), Root);
                BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(16), 1);
                BinaryPrimitives.WriteUInt32LittleEndian(tree.AsSpan(32), DesktopWindow);
                await WriteAsync(tree);
                break;
            case 20: // GetProperty
                var title = "TabStudio - Wine desktop"u8.ToArray();
                var property = Reply(Pad(title.Length));
                property[1] = 8;
                BinaryPrimitives.WriteUInt32LittleEndian(property.AsSpan(8), 31);
                BinaryPrimitives.WriteUInt32LittleEndian(property.AsSpan(16), (uint)title.Length);
                title.CopyTo(property, 32);
                await WriteAsync(property);
                break;
            case 14: // GetGeometry
                var geometry = Reply();
                geometry[1] = 24;
                BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(16), (ushort)Width);
                BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(18), (ushort)Height);
                await WriteAsync(geometry);
                break;
            case 12: // ConfigureWindow
                Configured.Enqueue((BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(4)), (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(12)),
                    (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(16)), (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20)),
                    (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(24))));
                break;
            case 101: // GetKeyboardMapping
                var count = request[5];
                var mapping = Reply(count * 2 * 4);
                mapping[1] = 2;
                for (var i = 0; i < count * 2; i++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(mapping.AsSpan(32 + (4 * i)), _keysyms[((request[4] - FirstKeycode) * 2) + i]);
                }

                await WriteAsync(mapping);
                break;
            case 100: // ChangeKeyboardMapping
                var keycode = request[4];
                var keysym = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(8));
                SetKey(keycode, keysym, BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(12)));
                Remapped.Enqueue((keycode, keysym));
                break;
        }
    }

    private byte[] Reply(int extra = 0)
    {
        var reply = new byte[32 + extra];
        reply[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(2), _sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), (uint)(extra / 4));
        return reply;
    }

    private async Task WriteAsync(byte[] bytes)
    {
        await _write.WaitAsync();
        try
        {
            await _client!.WriteAsync(bytes);
        }
        finally
        {
            _write.Release();
        }
    }

    private byte[] SetupReply()
    {
        var vendor = "TabStudio test server"u8.ToArray();
        var data = new List<byte>();
        void U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); data.AddRange(b); }
        void U16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); data.AddRange(b); }
        U32(12101000);
        U32(0x00400000);
        U32(0x001FFFFF);
        U32(256);
        U16((ushort)vendor.Length);
        U16(65535);
        data.Add(1); // screens
        data.Add(1); // formats
        data.Add(0);
        data.Add(0);
        data.Add(32);
        data.Add(32);
        data.Add(FirstKeycode);
        data.Add(LastKeycode);
        U32(0);
        data.AddRange(vendor);
        data.AddRange(new byte[Pad(vendor.Length) - vendor.Length]);
        data.AddRange([24, 32, 32, 0, 0, 0, 0, 0]);
        U32(Root);
        U32(0x20);
        U32(0xFFFFFF);
        U32(0);
        U32(0);
        U16((ushort)Width);
        U16((ushort)Height);
        U16(338);
        U16(211);
        U16(1);
        U16(1);
        U32(0x21);
        data.Add(0);
        data.Add(0);
        data.Add(24);
        data.Add(1); // depths
        data.AddRange([24, 0, 1, 0, 0, 0, 0, 0]);
        U32(0x21);
        data.Add(4);
        data.Add(8);
        U16(256);
        U32(0xFF0000);
        U32(0xFF00);
        U32(0xFF);
        U32(0);
        var reply = new byte[8 + data.Count];
        reply[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(2), 11);
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(6), (ushort)(data.Count / 4));
        data.CopyTo(reply, 8);
        return reply;
    }

    private static int Pad(int length) => (length + 3) & ~3;
}
