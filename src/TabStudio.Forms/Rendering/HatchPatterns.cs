using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// The 8 by 8 patterns of <see cref="HatchStyle"/>, as GDI+ paints them: each row is a byte whose highest bit is the
/// leftmost pixel, and a set bit is painted with the foreground color.
/// </summary>
internal static class HatchPatterns
{
    private const int CacheLimit = 128;

    private static readonly ConcurrentDictionary<(HatchStyle Style, uint Fore, uint Back), SKImage> Images = new();

    // Indexed by HatchStyle, 0 to 52: GDI+'s own patterns, read from GDI+ by build/gdiplus-references.
    private static readonly ulong[] Rows =
    [
        0xFF00000000000000, // Horizontal
        0x8080808080808080, // Vertical
        0x8040201008040201, // ForwardDiagonal
        0x0102040810204080, // BackwardDiagonal
        0xFF80808080808080, // Cross
        0x8142241818244281, // DiagonalCross
        0x8000000008000000, // Percent05
        0x8000080080000800, // Percent10
        0x8800220088002200, // Percent20
        0x8822882288228822, // Percent25
        0xAA44AA11AA44AA11, // Percent30
        0xAA55AA51AA55AA15, // Percent40
        0xAA55AA55AA55AA55, // Percent50
        0xEE55BB55EE55BB55, // Percent60
        0x77DD77DD77DD77DD, // Percent70
        0x77FFDDFF77FFDDFF, // Percent75
        0xEFFFFEFFEFFFFEFF, // Percent80
        0xFFFFFFF7FFFFFF7F, // Percent90
        0x8844221188442211, // LightDownwardDiagonal
        0x1122448811224488, // LightUpwardDiagonal
        0xCC663399CC663399, // DarkDownwardDiagonal
        0x3366CC993366CC99, // DarkUpwardDiagonal
        0xC1E070381C0E0783, // WideDownwardDiagonal
        0x83070E1C3870E0C1, // WideUpwardDiagonal
        0x8888888888888888, // LightVertical
        0xFF000000FF000000, // LightHorizontal
        0x5555555555555555, // NarrowVertical
        0xFF00FF00FF00FF00, // NarrowHorizontal
        0xCCCCCCCCCCCCCCCC, // DarkVertical
        0xFFFF0000FFFF0000, // DarkHorizontal
        0x0000884422110000, // DashedDownwardDiagonal
        0x0000112244880000, // DashedUpwardDiagonal
        0xF00000000F000000, // DashedHorizontal
        0x8080808008080808, // DashedVertical
        0x8008400210012004, // SmallConfetti
        0xB130031BD8C00C8D, // LargeConfetti
        0x8142241881422418, // ZigZag
        0x001825C0001825C0, // Wave
        0x0102040818244281, // DiagonalBrick
        0xFF808080FF080808, // HorizontalBrick
        0x8854224588142251, // Weave
        0xAA55AA55F0F0F0F0, // Plaid
        0x0010081000800180, // Divot
        0xAA00800080008000, // DottedGrid
        0x8000220008002200, // DottedDiamond
        0x038448300C020101, // Shingle
        0xFF66FF99FF66FF99, // Trellis
        0x77898F8F7798F8F8, // Sphere
        0xFF888888FF888888, // SmallGrid
        0x9966669999666699, // SmallCheckerBoard
        0xF0F0F0F00F0F0F0F, // LargeCheckerBoard
        0x8244281028448201, // OutlinedDiamond
        0x10387CFE7C381000, // SolidDiamond
    ];

    /// <summary>The 8 by 8 rows of a hatch style, top row in the highest byte.</summary>
    /// <param name="style">The style.</param>
    public static ulong Pattern(HatchStyle style)
    {
        var index = (int)style;
        if ((uint)index >= (uint)Rows.Length)
        {
            index = 0;
        }

        return Rows[index];
    }

    /// <summary>Whether a pixel of the pattern is painted with the foreground color.</summary>
    /// <param name="style">The style.</param>
    /// <param name="x">The column, 0 to 7.</param>
    /// <param name="y">The row, 0 to 7.</param>
    public static bool IsSet(HatchStyle style, int x, int y)
    {
        var row = (byte)(Pattern(style) >> ((7 - (y & 7)) * 8));
        return (row & (0x80 >> (x & 7))) != 0;
    }

    /// <summary>The pattern as an 8 by 8 image of two colors.</summary>
    /// <param name="style">The style.</param>
    /// <param name="fore">The foreground color as 0xAARRGGBB.</param>
    /// <param name="back">The background color as 0xAARRGGBB.</param>
    public static SKImage Image(HatchStyle style, uint fore, uint back)
    {
        if (Images.Count >= CacheLimit)
        {
            Images.Clear();
        }

        return Images.GetOrAdd((style, fore, back), static key =>
        {
            var pixels = new uint[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[(y * 8) + x] = IsSet(key.Style, x, y) ? key.Fore : key.Back;
                }
            }

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                return SKImage.FromPixelCopy(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Unpremul), handle.AddrOfPinnedObject(), 32);
            }
            finally
            {
                handle.Free();
            }
        });
    }

    /// <summary>The pattern as unpremultiplied 0xAARRGGBB pixels, for the window renderer's image brush.</summary>
    /// <param name="style">The style.</param>
    /// <param name="fore">The foreground color.</param>
    /// <param name="back">The background color.</param>
    public static int[] Pixels(HatchStyle style, uint fore, uint back)
    {
        var pixels = new int[64];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[(y * 8) + x] = unchecked((int)(IsSet(style, x, y) ? fore : back));
            }
        }

        return pixels;
    }
}
