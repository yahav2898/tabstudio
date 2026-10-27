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

    // Indexed by HatchStyle, 0 to 52.
    private static readonly ulong[] Rows =
    [
        0x00000000000000FF, // Horizontal
        0x0101010101010101, // Vertical
        0x8040201008040201, // ForwardDiagonal
        0x0102040810204080, // BackwardDiagonal
        0x01010101010101FF, // Cross
        0x8142241818244281, // DiagonalCross
        0x0000000100000010, // Percent05
        0x0000001100000011, // Percent10
        0x0000005500000055, // Percent20
        0x0011004400110044, // Percent25
        0x0055002200550022, // Percent30
        0x0055AA5500552255, // Percent40
        0x55AA55AA55AA55AA, // Percent50
        0x77AA77AA77AA77AA, // Percent60
        0x55FFAAFF55FFAAFF, // Percent70
        0x77FFDDFF77FFDDFF, // Percent75
        0x77FFFFFF77FFFFFF, // Percent80
        0x7FFFFFFFF7FFFFFF, // Percent90
        0x8844221188442211, // LightDownwardDiagonal
        0x1122448811224488, // LightUpwardDiagonal
        0xCC663399CC663399, // DarkDownwardDiagonal
        0x993366CC993366CC, // DarkUpwardDiagonal
        0xC1E070381C0E0783, // WideDownwardDiagonal
        0x83070E1C3870E0C1, // WideUpwardDiagonal
        0x8888888888888888, // LightVertical
        0x00FF000000FF0000, // LightHorizontal
        0xAAAAAAAAAAAAAAAA, // NarrowVertical
        0x00FF00FF00FF00FF, // NarrowHorizontal
        0xCCCCCCCCCCCCCCCC, // DarkVertical
        0x0000FFFF0000FFFF, // DarkHorizontal
        0x0000000088442211, // DashedDownwardDiagonal
        0x0000000011224488, // DashedUpwardDiagonal
        0xF00000000F000000, // DashedHorizontal
        0x8080808008080808, // DashedVertical
        0x8004400208200110, // SmallConfetti
        0x8C0D80C80CD80C80, // LargeConfetti
        0x8142241800000000, // ZigZag
        0x0000304A85000000, // Wave
        0x8040201008040201, // DiagonalBrick
        0xFF808080FF080808, // HorizontalBrick
        0x8822552288552288, // Weave
        0xAA55AA55FF0000FF, // Plaid
        0x8000000008142200, // Divot
        0xAA000000AA000000, // DottedGrid
        0x8000220008002200, // DottedDiamond
        0x8106601824420181, // Shingle
        0xFF66FF99FF66FF99, // Trellis
        0x7E8181FFFF7E3C18, // Sphere
        0x88888888FF888888, // SmallGrid
        0xCC33CC33CC33CC33, // SmallCheckerBoard
        0xF0F0F0F00F0F0F0F, // LargeCheckerBoard
        0x8142241824428100, // OutlinedDiamond
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
