namespace System.Drawing.Imaging;

/// <summary>The format of the color data of each pixel of an image, with GDI+'s values.</summary>
#pragma warning disable CA1069 // GDI+ gives DontCare and Undefined the same value.
public enum PixelFormat
{
    /// <summary>No format.</summary>
    Undefined = 0,

    /// <summary>The format is not important.</summary>
    DontCare = 0,

    /// <summary>The largest format value.</summary>
    Max = 15,

    /// <summary>The pixel data are indexes into a color palette.</summary>
    Indexed = 0x00010000,

    /// <summary>The pixel data hold GDI colors.</summary>
    Gdi = 0x00020000,

    /// <summary>The pixel data have alpha.</summary>
    Alpha = 0x00040000,

    /// <summary>The pixel data are premultiplied by alpha.</summary>
    PAlpha = 0x00080000,

    /// <summary>Reserved.</summary>
    Extended = 0x00100000,

    /// <summary>The default format: 32 bits per pixel with alpha.</summary>
    Canonical = 0x00200000,

    /// <summary>1 bit per pixel, indexed.</summary>
    Format1bppIndexed = 196865,

    /// <summary>4 bits per pixel, indexed.</summary>
    Format4bppIndexed = 197634,

    /// <summary>8 bits per pixel, indexed.</summary>
    Format8bppIndexed = 198659,

    /// <summary>16 bits of gray per pixel.</summary>
    Format16bppGrayScale = 1052676,

    /// <summary>16 bits per pixel: 5 bits each of red, green and blue.</summary>
    Format16bppRgb555 = 135173,

    /// <summary>16 bits per pixel: 5 bits of red, 6 of green and 5 of blue.</summary>
    Format16bppRgb565 = 135174,

    /// <summary>16 bits per pixel: 1 bit of alpha and 5 bits each of red, green and blue.</summary>
    Format16bppArgb1555 = 397319,

    /// <summary>24 bits per pixel: 8 bits each of red, green and blue.</summary>
    Format24bppRgb = 137224,

    /// <summary>32 bits per pixel: 8 bits each of red, green and blue; the rest is unused.</summary>
    Format32bppRgb = 139273,

    /// <summary>32 bits per pixel: 8 bits each of alpha, red, green and blue.</summary>
    Format32bppArgb = 2498570,

    /// <summary>32 bits per pixel, premultiplied by alpha.</summary>
    Format32bppPArgb = 925707,

    /// <summary>48 bits per pixel: 16 bits each of red, green and blue.</summary>
    Format48bppRgb = 1060876,

    /// <summary>64 bits per pixel: 16 bits each of alpha, red, green and blue.</summary>
    Format64bppArgb = 3424269,

    /// <summary>64 bits per pixel, premultiplied by alpha.</summary>
    Format64bppPArgb = 1851406,
}
#pragma warning restore CA1069
