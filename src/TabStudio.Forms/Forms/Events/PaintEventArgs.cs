using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Data of the Paint event: the surface to draw on and the area to draw.</summary>
public class PaintEventArgs : EventArgs, IDisposable
{
    /// <summary>Creates the data of a Paint event.</summary>
    /// <param name="graphics">The surface.</param>
    /// <param name="clipRect">The area to draw, in the control's client coordinates.</param>
    public PaintEventArgs(Graphics graphics, Rectangle clipRect)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        Graphics = graphics;
        ClipRectangle = clipRect;
    }

    /// <summary>The surface to draw on, with its origin at the control's client area.</summary>
    public Graphics Graphics { get; }

    /// <summary>The area to draw, in the control's client coordinates.</summary>
    public Rectangle ClipRectangle { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the data. The surface belongs to the window's drawing and is not disposed.</summary>
    /// <param name="disposing">Whether this is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}

/// <summary>Handles a Paint event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void PaintEventHandler(object? sender, PaintEventArgs e);
