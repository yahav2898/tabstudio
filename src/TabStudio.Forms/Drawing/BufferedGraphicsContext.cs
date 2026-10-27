namespace System.Drawing;

/// <summary>Makes <see cref="BufferedGraphics"/> buffers for double buffering.</summary>
public sealed class BufferedGraphicsContext : IDisposable
{
    private Size _maximumBuffer = new(225, 96);

    /// <summary>The size of the largest buffer kept for reuse. Kept for compatibility: buffers are made as needed.</summary>
    public Size MaximumBuffer
    {
        get => _maximumBuffer;
        set => _maximumBuffer = value.Width > 0 && value.Height > 0 ? value : throw new ArgumentException("Parameter is not valid: the size must be positive.", nameof(value));
    }

    /// <summary>Makes a buffer for a rectangle of a surface.</summary>
    /// <param name="targetGraphics">The surface the buffer is copied to.</param>
    /// <param name="targetRectangle">The rectangle, in the surface's coordinates.</param>
    public BufferedGraphics Allocate(Graphics targetGraphics, Rectangle targetRectangle)
    {
        ArgumentNullException.ThrowIfNull(targetGraphics);
        return new BufferedGraphics(targetGraphics, targetRectangle);
    }

    /// <summary>Device contexts do not exist on this device.</summary>
    /// <param name="targetDC">The device context.</param>
    /// <param name="targetRectangle">The rectangle.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public BufferedGraphics Allocate(IntPtr targetDC, Rectangle targetRectangle) =>
        throw new NotSupportedException("BufferedGraphicsContext.Allocate(IntPtr, Rectangle) is not supported by TabStudio's Forms layer: there is no GDI device context on this device; pass a Graphics, such as control.CreateGraphics().");

    /// <summary>Releases the buffer kept for reuse.</summary>
    public void Invalidate()
    {
        // Buffers are not kept.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Buffers are not kept.
    }
}
