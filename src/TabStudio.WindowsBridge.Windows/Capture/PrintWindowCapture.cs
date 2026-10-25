using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Capture;

/// <summary>
/// Captures a window with PrintWindow, where Windows.Graphics.Capture is not available: the window draws itself into
/// a bitmap, DirectX content included, on every capture. The picture is cut to the window's visible frame, as
/// Windows.Graphics.Capture's is.
/// </summary>
internal sealed unsafe class PrintWindowCapture : IWindowCapture
{
    private readonly nint _window;
    private nint _memory;
    private nint _bitmap;
    private nint _previousObject;
    private void* _bits;
    private int _width;
    private int _height;
    private int _disposed;

    /// <summary>Captures a window.</summary>
    /// <param name="window">The window.</param>
    public PrintWindowCapture(nint window)
    {
        _window = window;
    }

    /// <inheritdoc />
    public string Method => "PrintWindow";

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

        var whole = WindowBounds.Whole(_window);
        var visible = WindowBounds.Visible(_window);
        if (whole.Width <= 0 || whole.Height <= 0)
        {
            return CaptureResult.Unavailable;
        }

        EnsureBitmap(whole.Width, whole.Height);
        if (!NativeMethods.PrintWindow(_window, _memory, NativeMethods.PwRenderFullContent))
        {
            return CaptureResult.Unavailable;
        }

        var left = Math.Clamp(visible.Left - whole.Left, 0, whole.Width - 1);
        var top = Math.Clamp(visible.Top - whole.Top, 0, whole.Height - 1);
        var width = Math.Clamp(visible.Width, 1, whole.Width - left);
        var height = Math.Clamp(visible.Height, 1, whole.Height - top);
        var picture = reuse is { } existing && existing.Width == width && existing.Height == height ? existing : new FrameBitmap(width, height);
        var row = width * FrameBitmap.BytesPerPixel;
        var sourceStride = _width * FrameBitmap.BytesPerPixel;
        fixed (byte* target = picture.Pixels)
        {
            for (var y = 0; y < height; y++)
            {
                var source = (byte*)_bits + ((long)(top + y) * sourceStride) + (left * FrameBitmap.BytesPerPixel);
                Buffer.MemoryCopy(source, target + ((long)y * row), row, row);
            }
        }

        // PrintWindow leaves alpha undefined; the window is opaque.
        for (var i = 3; i < picture.Pixels.Length; i += FrameBitmap.BytesPerPixel)
        {
            picture.Pixels[i] = 255;
        }

        return CaptureResult.Of(picture);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        ReleaseBitmap();
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap != 0 && _width == width && _height == height)
        {
            return;
        }

        ReleaseBitmap();
        var screen = NativeMethods.GetDC(0);
        try
        {
            _memory = NativeMethods.CreateCompatibleDC(screen);
            var header = new NativeMethods.BitmapInfoHeader
            {
                Size = (uint)sizeof(NativeMethods.BitmapInfoHeader),
                Width = width,

                // A negative height draws the rows top to bottom, as the picture holds them.
                Height = -height,
                Planes = 1,
                BitCount = 32,
            };
            _bitmap = NativeMethods.CreateDIBSection(screen, header, 0, out _bits, 0, 0);
            if (_bitmap == 0 || _memory == 0)
            {
                throw new InvalidOperationException("Windows could not make a bitmap to capture the window into.");
            }

            _previousObject = NativeMethods.SelectObject(_memory, _bitmap);
            _width = width;
            _height = height;
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(0, screen);
        }
    }

    private void ReleaseBitmap()
    {
        if (_memory != 0)
        {
            if (_previousObject != 0)
            {
                _ = NativeMethods.SelectObject(_memory, _previousObject);
            }

            _ = NativeMethods.DeleteDC(_memory);
        }

        if (_bitmap != 0)
        {
            _ = NativeMethods.DeleteObject(_bitmap);
        }

        _memory = 0;
        _bitmap = 0;
        _previousObject = 0;
        _bits = null;
        _width = 0;
        _height = 0;
    }
}
