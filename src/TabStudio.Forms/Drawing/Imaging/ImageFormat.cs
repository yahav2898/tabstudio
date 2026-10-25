namespace System.Drawing.Imaging;

/// <summary>The file format of an image.</summary>
public sealed class ImageFormat
{
    /// <summary>Creates a format from its GUID.</summary>
    /// <param name="guid">The format's GUID.</param>
    public ImageFormat(Guid guid) => Guid = guid;

    private ImageFormat(string guid, string name)
    {
        Guid = new Guid(guid);
        Name = name;
    }

    /// <summary>The Portable Network Graphics format, the one TabStudio saves images in.</summary>
    public static ImageFormat Png { get; } = new("b96b3caf-0728-11d3-9d7b-0000f81ef32e", "Png");

    /// <summary>The JPEG format.</summary>
    public static ImageFormat Jpeg { get; } = new("b96b3cae-0728-11d3-9d7b-0000f81ef32e", "Jpeg");

    /// <summary>The bitmap (BMP) format.</summary>
    public static ImageFormat Bmp { get; } = new("b96b3cab-0728-11d3-9d7b-0000f81ef32e", "Bmp");

    /// <summary>The Graphics Interchange Format (GIF).</summary>
    public static ImageFormat Gif { get; } = new("b96b3cb0-0728-11d3-9d7b-0000f81ef32e", "Gif");

    /// <summary>The Windows icon format.</summary>
    public static ImageFormat Icon { get; } = new("b96b3cb5-0728-11d3-9d7b-0000f81ef32e", "Icon");

    /// <summary>The Tagged Image File Format (TIFF).</summary>
    public static ImageFormat Tiff { get; } = new("b96b3cb1-0728-11d3-9d7b-0000f81ef32e", "Tiff");

    /// <summary>The Exchangeable Image File (Exif) format.</summary>
    public static ImageFormat Exif { get; } = new("b96b3cb2-0728-11d3-9d7b-0000f81ef32e", "Exif");

    /// <summary>A bitmap in memory.</summary>
    public static ImageFormat MemoryBmp { get; } = new("b96b3caa-0728-11d3-9d7b-0000f81ef32e", "MemoryBMP");

    /// <summary>The WebP format.</summary>
    public static ImageFormat Webp { get; } = new("b96b3cb8-0728-11d3-9d7b-0000f81ef32e", "Webp");

    /// <summary>The format's GUID.</summary>
    public Guid Guid { get; }

    private string? Name { get; }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ImageFormat other && other.Guid == Guid;

    /// <inheritdoc />
    public override int GetHashCode() => Guid.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Name ?? $"[ImageFormat: {Guid}]";
}

/// <summary>The format of the color data of each pixel of an image.</summary>
public enum PixelFormat
{
    /// <summary>No format.</summary>
    Undefined = 0,

    /// <summary>The format is not important.</summary>
    DontCare = 0,

    /// <summary>24 bits per pixel: 8 bits each of red, green and blue.</summary>
    Format24bppRgb = 137224,

    /// <summary>32 bits per pixel: 8 bits each of red, green and blue; the rest is unused.</summary>
    Format32bppRgb = 139273,

    /// <summary>32 bits per pixel: 8 bits each of alpha, red, green and blue.</summary>
    Format32bppArgb = 2498570,

    /// <summary>32 bits per pixel, premultiplied by alpha.</summary>
    Format32bppPArgb = 925707,
}
