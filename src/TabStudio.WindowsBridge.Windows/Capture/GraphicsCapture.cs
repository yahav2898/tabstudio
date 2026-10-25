using System.Runtime.InteropServices;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Capture;

/// <summary>
/// Captures a window with Windows.Graphics.Capture, as Windows' own screen recording does: Windows composes the
/// window into Direct3D textures as it changes, including content drawn with DirectX (WPF, WinUI) and parts covered
/// by other windows. A frame arrives only when the window changed, so a still window costs nothing.
/// </summary>
/// <remarks>Windows 10 version 1903 and later. Its WinRT and Direct3D 11 objects are called through their virtual tables.</remarks>
internal sealed unsafe class GraphicsCapture : IWindowCapture
{
    private static readonly Guid ItemInteropId = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid ItemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid SessionStaticsId = new("2224A540-5974-49AA-B232-0882536F4CB5");
    private static readonly Guid FramePoolStatics2Id = new("589B103F-6BBC-5DF5-A991-02E28B3B66D5");
    private static readonly Guid SessionId2 = new("2C39AE40-7D2E-5044-804E-8B6799D4CF9E");
    private static readonly Guid SessionId3 = new("F2CDD966-22AE-5EA1-9596-3A289344C3BE");
    private static readonly Guid ClosableId = new("30D5A829-7FA4-4026-83BB-D75BAE4EA99E");
    private static readonly Guid GraphicsDeviceId = new("A37624AB-8D5F-4650-9D3E-9EAE3D9BC670");
    private static readonly Guid DxgiInterfaceAccessId = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static readonly Guid DxgiDeviceId = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid Texture2DId = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    // Virtual table slots, from the Windows SDK's headers.
    private const int InteropCreateForWindow = 3;
    private const int ItemGetSize = Com.InspectableSlots + 1;
    private const int SessionStaticsIsSupported = Com.InspectableSlots;
    private const int PoolStaticsCreateFreeThreaded = Com.InspectableSlots;
    private const int PoolRecreate = Com.InspectableSlots;
    private const int PoolTryGetNextFrame = Com.InspectableSlots + 1;
    private const int PoolCreateCaptureSession = Com.InspectableSlots + 4;
    private const int SessionStartCapture = Com.InspectableSlots;
    private const int SessionPutBoolean = Com.InspectableSlots + 1;
    private const int FrameGetSurface = Com.InspectableSlots;
    private const int FrameGetContentSize = Com.InspectableSlots + 2;
    private const int ClosableClose = Com.InspectableSlots;
    private const int AccessGetInterface = 3;
    private const int DeviceCreateTexture2D = 5;
    private const int ContextMap = 14;
    private const int ContextUnmap = 15;
    private const int ContextCopyResource = 47;
    private const int TextureGetDesc = 10;

    private readonly nint _window;
    private nint _device;
    private nint _context;
    private nint _graphicsDevice;
    private nint _item;
    private nint _pool;
    private nint _session;
    private nint _staging;
    private NativeMethods.SizeInt32 _poolSize;
    private NativeMethods.Texture2DDescription _stagingDescription;
    private int _disposed;

    private GraphicsCapture(nint window)
    {
        _window = window;
    }

    /// <inheritdoc />
    public string Method => "Windows.Graphics.Capture";

    /// <summary>Whether this PC's Windows has Windows.Graphics.Capture for windows.</summary>
    public static bool IsSupported()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
        {
            return false;
        }

        try
        {
            _ = NativeMethods.RoInitialize(NativeMethods.RoInitMultithreaded);
            var statics = Com.ActivationFactory("Windows.Graphics.Capture.GraphicsCaptureSession", SessionStaticsId);
            try
            {
                byte supported;
                var hr = ((delegate* unmanaged<nint, byte*, int>)Com.Slot(statics, SessionStaticsIsSupported))(statics, &supported);
                return hr >= 0 && supported != 0;
            }
            finally
            {
                Com.Release(ref statics);
            }
        }
        catch (Exception ex) when (ex is NativeCallException or COMException or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Starts capturing a window.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The capture, or <see langword="null"/> when Windows.Graphics.Capture cannot capture it.</returns>
    public static GraphicsCapture? TryStart(nint window)
    {
        var capture = new GraphicsCapture(window);
        try
        {
            capture.Start();
            return capture;
        }
        catch (Exception ex) when (ex is NativeCallException or COMException or DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            capture.Dispose();
            return null;
        }
    }

    /// <inheritdoc />
    public CaptureResult Capture(FrameBitmap? reuse)
    {
        if (!NativeMethods.IsWindow(_window))
        {
            return CaptureResult.Closed;
        }

        if (NativeMethods.IsIconic(_window))
        {
            return CaptureResult.Unavailable;
        }

        // Only the newest frame matters: older ones still in the pool are released unread.
        nint frame = 0;
        while (Com.Call(_pool, PoolTryGetNextFrame, out var next) >= 0 && next != 0)
        {
            Close(ref frame);
            frame = next;
        }

        if (frame == 0)
        {
            return CaptureResult.Unchanged;
        }

        try
        {
            NativeMethods.SizeInt32 size;
            Com.Check(((delegate* unmanaged<nint, NativeMethods.SizeInt32*, int>)Com.Slot(frame, FrameGetContentSize))(frame, &size), "Reading the frame's size");
            if (size.Width <= 0 || size.Height <= 0)
            {
                return CaptureResult.Unavailable;
            }

            var picture = Read(frame, size, reuse);
            if (size.Width != _poolSize.Width || size.Height != _poolSize.Height)
            {
                // The window was resized: the pool's buffers follow, from the next frame on.
                _poolSize = size;
                _ = ((delegate* unmanaged<nint, nint, int, int, NativeMethods.SizeInt32, int>)Com.Slot(_pool, PoolRecreate))(
                    _pool, _graphicsDevice, NativeMethods.DirectXPixelFormatB8G8R8A8UIntNormalized, 2, size);
            }

            return CaptureResult.Of(picture);
        }
        finally
        {
            Close(ref frame);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Close(ref _session);
        Close(ref _pool);
        Com.Release(ref _item);
        Com.Release(ref _staging);
        Com.Release(ref _graphicsDevice);
        Com.Release(ref _context);
        Com.Release(ref _device);
    }

    /// <summary>Closes a WinRT object that is closable, then releases it.</summary>
    private static void Close(ref nint instance)
    {
        if (instance == 0)
        {
            return;
        }

        var closable = Com.QueryInterface(instance, ClosableId);
        if (closable != 0)
        {
            _ = Com.Call(closable, ClosableClose);
            Com.Release(ref closable);
        }

        Com.Release(ref instance);
    }

    private void Start()
    {
        _ = NativeMethods.RoInitialize(NativeMethods.RoInitMultithreaded);
        var hr = NativeMethods.D3D11CreateDevice(0, NativeMethods.D3dDriverTypeHardware, 0, NativeMethods.D3d11CreateDeviceBgraSupport, 0, 0, NativeMethods.D3d11SdkVersion, out _device, out _, out _context);
        if (hr < 0)
        {
            // No GPU, as on a virtual machine: Windows' software renderer composes as well.
            hr = NativeMethods.D3D11CreateDevice(0, NativeMethods.D3dDriverTypeWarp, 0, NativeMethods.D3d11CreateDeviceBgraSupport, 0, 0, NativeMethods.D3d11SdkVersion, out _device, out _, out _context);
        }

        Com.Check(hr, "Creating a Direct3D 11 device");
        var dxgi = Com.QueryInterface(_device, DxgiDeviceId);
        try
        {
            Com.Check(NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable), "Wrapping the Direct3D device for WinRT");
            _graphicsDevice = Com.QueryInterface(inspectable, GraphicsDeviceId);
            Com.Release(ref inspectable);
        }
        finally
        {
            Com.Release(ref dxgi);
        }

        if (_graphicsDevice == 0)
        {
            throw new InvalidOperationException("The Direct3D device has no WinRT interface.");
        }

        var interop = Com.ActivationFactory("Windows.Graphics.Capture.GraphicsCaptureItem", ItemInteropId);
        try
        {
            nint item;
            var itemId = ItemId;
            Com.Check(((delegate* unmanaged<nint, nint, Guid*, nint*, int>)Com.Slot(interop, InteropCreateForWindow))(interop, _window, &itemId, &item), "Capturing the window");
            _item = item;
        }
        finally
        {
            Com.Release(ref interop);
        }

        NativeMethods.SizeInt32 size;
        Com.Check(((delegate* unmanaged<nint, NativeMethods.SizeInt32*, int>)Com.Slot(_item, ItemGetSize))(_item, &size), "Reading the window's size");
        _poolSize = size;

        var statics = Com.ActivationFactory("Windows.Graphics.Capture.Direct3D11CaptureFramePool", FramePoolStatics2Id);
        try
        {
            nint pool;
            Com.Check(((delegate* unmanaged<nint, nint, int, int, NativeMethods.SizeInt32, nint*, int>)Com.Slot(statics, PoolStaticsCreateFreeThreaded))(
                statics, _graphicsDevice, NativeMethods.DirectXPixelFormatB8G8R8A8UIntNormalized, 2, size, &pool), "Creating the frame pool");
            _pool = pool;
        }
        finally
        {
            Com.Release(ref statics);
        }

        Com.Check(Com.Call(_pool, PoolCreateCaptureSession, _item, out _session), "Creating the capture session");

        // The tablet draws its own pointer, and the yellow border Windows 11 draws around captured windows can go.
        SetSessionOption(SessionId2, false);
        SetSessionOption(SessionId3, false);
        Com.Check(Com.Call(_session, SessionStartCapture), "Starting the capture");
    }

    private void SetSessionOption(Guid iid, bool value)
    {
        var session = Com.QueryInterface(_session, iid);
        if (session != 0)
        {
            _ = ((delegate* unmanaged<nint, byte, int>)Com.Slot(session, SessionPutBoolean))(session, value ? (byte)1 : (byte)0);
            Com.Release(ref session);
        }
    }

    /// <summary>Copies a frame's texture into a picture, through a staging texture the processor can read.</summary>
    private FrameBitmap Read(nint frame, NativeMethods.SizeInt32 size, FrameBitmap? reuse)
    {
        Com.Check(Com.Call(frame, FrameGetSurface, out var surface), "Reading the frame's surface");
        nint access = 0;
        nint texture = 0;
        try
        {
            access = Com.QueryInterface(surface, DxgiInterfaceAccessId);
            if (access == 0)
            {
                throw new InvalidOperationException("The frame's surface has no Direct3D texture.");
            }

            var textureId = Texture2DId;
            Com.Check(((delegate* unmanaged<nint, Guid*, nint*, int>)Com.Slot(access, AccessGetInterface))(access, &textureId, &texture), "Reading the frame's texture");

            NativeMethods.Texture2DDescription description;
            ((delegate* unmanaged<nint, NativeMethods.Texture2DDescription*, void>)Com.Slot(texture, TextureGetDesc))(texture, &description);
            EnsureStaging(description);
            ((delegate* unmanaged<nint, nint, nint, void>)Com.Slot(_context, ContextCopyResource))(_context, _staging, texture);

            NativeMethods.MappedSubresource mapped;
            Com.Check(((delegate* unmanaged<nint, nint, uint, uint, uint, NativeMethods.MappedSubresource*, int>)Com.Slot(_context, ContextMap))(
                _context, _staging, 0, NativeMethods.D3d11MapRead, 0, &mapped), "Reading the frame");
            try
            {
                var width = Math.Min(size.Width, (int)description.Width);
                var height = Math.Min(size.Height, (int)description.Height);
                var picture = reuse is { } existing && existing.Width == width && existing.Height == height ? existing : new FrameBitmap(width, height);
                var row = width * FrameBitmap.BytesPerPixel;
                fixed (byte* target = picture.Pixels)
                {
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy((byte*)mapped.Data + ((long)y * mapped.RowPitch), target + ((long)y * row), row, row);
                    }
                }

                return picture;
            }
            finally
            {
                ((delegate* unmanaged<nint, nint, uint, void>)Com.Slot(_context, ContextUnmap))(_context, _staging, 0);
            }
        }
        finally
        {
            Com.Release(ref texture);
            Com.Release(ref access);
            Com.Release(ref surface);
        }
    }

    private void EnsureStaging(NativeMethods.Texture2DDescription source)
    {
        if (_staging != 0 && _stagingDescription.Width == source.Width && _stagingDescription.Height == source.Height)
        {
            return;
        }

        Com.Release(ref _staging);
        var description = source;
        description.MipLevels = 1;
        description.ArraySize = 1;
        description.SampleCount = 1;
        description.SampleQuality = 0;
        description.Usage = NativeMethods.D3d11UsageStaging;
        description.BindFlags = 0;
        description.CpuAccessFlags = NativeMethods.D3d11CpuAccessRead;
        description.MiscFlags = 0;
        nint staging;
        Com.Check(((delegate* unmanaged<nint, NativeMethods.Texture2DDescription*, void*, nint*, int>)Com.Slot(_device, DeviceCreateTexture2D))(_device, &description, null, &staging), "Creating a readable texture");
        _staging = staging;
        _stagingDescription = description;
    }
}
