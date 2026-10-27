namespace System.Drawing;

/// <summary>
/// A drawing buffer for double buffering: draw on <see cref="Graphics"/>, then <see cref="Render()"/> copies the
/// whole buffer to the target surface at once. The buffer's surface uses the target's coordinates.
/// </summary>
public sealed class BufferedGraphics : IDisposable
{
    private readonly Graphics? _target;
    private readonly Rectangle _targetRectangle;
    private Bitmap? _bitmap;
    private Graphics? _graphics;

    internal BufferedGraphics(Graphics? target, Rectangle targetRectangle)
    {
        _target = target;
        _targetRectangle = targetRectangle;
        _bitmap = new Bitmap(Math.Max(1, targetRectangle.Width), Math.Max(1, targetRectangle.Height), Imaging.PixelFormat.Format32bppPArgb);
        _graphics = Graphics.FromImage(_bitmap);
        _graphics.TranslateTransform(-targetRectangle.X, -targetRectangle.Y);
    }

    /// <summary>The buffer's surface, in the target's coordinates.</summary>
    /// <exception cref="ObjectDisposedException">The buffer was disposed.</exception>
    public Graphics Graphics => _graphics ?? throw new ObjectDisposedException(nameof(BufferedGraphics));

    /// <summary>Copies the buffer to the surface it was allocated for.</summary>
    public void Render()
    {
        if (_target is not null)
        {
            Render(_target);
        }
    }

    /// <summary>Copies the buffer to a surface, at the rectangle it was allocated for.</summary>
    /// <param name="target">The surface.</param>
    public void Render(Graphics? target)
    {
        if (target is null || _bitmap is null)
        {
            return;
        }

        // Like BitBlt, the copy replaces the pixels under it.
        target.CopyImage(_bitmap.Snapshot(), _targetRectangle);
    }

    /// <summary>Device contexts do not exist on this device.</summary>
    /// <param name="target">The device context.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void Render(IntPtr target) =>
        throw new NotSupportedException("BufferedGraphics.Render(IntPtr) is not supported by TabStudio's Forms layer: there is no GDI device context on this device; use Render() or Render(Graphics).");

    /// <inheritdoc />
    public void Dispose()
    {
        _graphics?.Dispose();
        _graphics = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
