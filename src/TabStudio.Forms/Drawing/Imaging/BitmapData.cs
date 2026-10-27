namespace System.Drawing.Imaging;

/// <summary>The pixels of a locked part of a bitmap, as <see cref="Bitmap.LockBits(Rectangle, ImageLockMode, PixelFormat)"/> hands them out.</summary>
public sealed class BitmapData
{
    /// <summary>The width of the locked part in pixels.</summary>
    public int Width { get; set; }

    /// <summary>The height of the locked part in pixels.</summary>
    public int Height { get; set; }

    /// <summary>The bytes from one row to the next, a multiple of four.</summary>
    public int Stride { get; set; }

    /// <summary>The format of the pixels.</summary>
    public PixelFormat PixelFormat { get; set; }

    /// <summary>The address of the first pixel of the first row.</summary>
    public IntPtr Scan0 { get; set; }

    /// <summary>Reserved.</summary>
    public int Reserved { get; set; }

    /// <summary>The locked rectangle of the bitmap.</summary>
    internal Rectangle Rectangle { get; set; }

    /// <summary>How the pixels were locked.</summary>
    internal ImageLockMode Mode { get; set; }

    /// <summary>Whether the memory was allocated by the lock, rather than handed in by the caller.</summary>
    internal bool OwnsMemory { get; set; }
}
