using System.Drawing.Imaging;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>A small image used to represent a window or a file, read from an <c>.ico</c> file.</summary>
public sealed class Icon : MarshalByRefObject, ICloneable, IDisposable
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, WeakReference<Icon>> Handles = new();
    private static long _nextHandle = 0x2_0000;
    private readonly Bitmap _bitmap;
    private IntPtr _handle;

    /// <summary>
    /// The icon's handle: an identifier of it, made when first read, which <see cref="FromHandle"/> turns back into the
    /// icon. Visual Basic apps pass it to Windows (DualBrain's Solitaire to ShellAbout), which is not called here.
    /// </summary>
    public IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                _handle = new IntPtr(Interlocked.Increment(ref _nextHandle));
                Handles[_handle] = new WeakReference<Icon>(this);
            }

            return _handle;
        }
    }

    /// <summary>The icon a handle from <see cref="Handle"/> names.</summary>
    /// <param name="handle">The handle.</param>
    public static Icon FromHandle(IntPtr handle) =>
        Handles.TryGetValue(handle, out var reference) && reference.TryGetTarget(out var icon)
            ? icon
            : throw new ArgumentException("No icon has this handle.", nameof(handle));

    /// <summary>Reads an icon from a file.</summary>
    /// <param name="fileName">The <c>.ico</c> file's path.</param>
    public Icon(string fileName) => _bitmap = new Bitmap(fileName);

    /// <summary>Reads an icon from a stream.</summary>
    /// <param name="stream">The icon's bytes.</param>
    public Icon(Stream stream) => _bitmap = new Bitmap(stream);

    /// <summary>Copies an icon at a size.</summary>
    /// <param name="original">The icon to copy.</param>
    /// <param name="size">The size.</param>
    public Icon(Icon original, Size size)
    {
        ArgumentNullException.ThrowIfNull(original);
        _bitmap = new Bitmap(original._bitmap, size);
    }

    /// <summary>Copies an icon at a size.</summary>
    /// <param name="original">The icon to copy.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public Icon(Icon original, int width, int height)
        : this(original, new Size(width, height))
    {
    }

    private Icon(Bitmap bitmap) => _bitmap = bitmap;

    /// <summary>The width in pixels.</summary>
    public int Width => _bitmap.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => _bitmap.Height;

    /// <summary>The size in pixels.</summary>
    public Size Size => _bitmap.Size;

    /// <summary>Creates an icon from a bitmap.</summary>
    /// <param name="bitmap">The bitmap.</param>
    internal static Icon FromBitmap(Bitmap bitmap) => new(bitmap);

    /// <summary>Converts the icon to a bitmap.</summary>
    public Bitmap ToBitmap() => new(_bitmap);

    /// <summary>Writes the icon to a stream, as a PNG.</summary>
    /// <param name="outputStream">Where to write.</param>
    public void Save(Stream outputStream) => _bitmap.Save(outputStream, ImageFormat.Png);

    /// <inheritdoc />
    public object Clone() => new Icon(new Bitmap(_bitmap));

    /// <inheritdoc />
    public void Dispose() => _bitmap.Dispose();

    /// <summary>The pixels to draw now.</summary>
    internal ImageSnapshot Snapshot() => _bitmap.Snapshot();
}
