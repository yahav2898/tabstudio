using System.Drawing;

namespace TabStudio.Forms.Tests.Drawing;

/// <summary>How two pictures of the same size differ, pixel by pixel.</summary>
/// <param name="DifferentShare">The share of pixels where a channel differs by more than the tolerance.</param>
/// <param name="MeanDifference">The mean absolute difference of all channels, from 0 to 255.</param>
/// <param name="FirstDifference">The first pixel that differs, or <see langword="null"/>.</param>
internal sealed record PixelComparison(double DifferentShare, double MeanDifference, Point? FirstDifference)
{
    /// <summary>Compares two pictures of 0xAARRGGBB pixels, with transparent pixels compared as transparent black.</summary>
    public static PixelComparison Compare(int[] actual, int[] expected, int width, int height, int tolerance)
    {
        long total = 0;
        var different = 0;
        Point? first = null;
        for (var i = 0; i < width * height; i++)
        {
            var a = Normalize(actual[i]);
            var e = Normalize(expected[i]);
            var max = 0;
            for (var shift = 0; shift < 32; shift += 8)
            {
                var d = Math.Abs(((a >> shift) & 0xFF) - ((e >> shift) & 0xFF));
                total += d;
                max = Math.Max(max, d);
            }

            if (max > tolerance)
            {
                different++;
                first ??= new Point(i % width, i / width);
            }
        }

        var pixels = (double)(width * height);
        return new PixelComparison(different / pixels, total / (pixels * 4), first);
    }

    private static int Normalize(int argb) => ((argb >> 24) & 0xFF) == 0 ? 0 : argb;
}
