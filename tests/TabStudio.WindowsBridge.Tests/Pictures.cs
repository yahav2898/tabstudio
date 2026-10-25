using TabStudio.WindowsBridge.Frames;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>Synthetic pictures for the codec's tests: windows of flat colors, as user interfaces are, and gradients.</summary>
internal static class Pictures
{
    /// <summary>A window: a title bar, a light background, a button and some lines of "text".</summary>
    public static FrameBitmap Window(int width, int height)
    {
        var picture = new FrameBitmap(width, height);
        picture.Fill(picture.Bounds, 0xFFF3F3F3);
        picture.Fill(new PixelRect(0, 0, width, Math.Min(30, height)), 0xFF2B579A);
        picture.Fill(new PixelRect(width / 4, height / 2, width / 3, Math.Max(4, height / 8)), 0xFFE1E1E1);
        for (var line = 40; line + 12 < height; line += 20)
        {
            for (var x = 10; x < width - 20; x += 7)
            {
                picture.Fill(new PixelRect(x, line, 4, 9), 0xFF1E1E1E);
            }
        }

        return picture;
    }

    /// <summary>A smooth gradient, which quantization shows the most.</summary>
    public static FrameBitmap Gradient(int width, int height)
    {
        var picture = new FrameBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = (uint)(x * 255 / Math.Max(1, width - 1));
                var g = (uint)(y * 255 / Math.Max(1, height - 1));
                var b = (uint)((x + y) * 255 / Math.Max(1, width + height - 2));
                picture.SetPixel(x, y, 0xFF000000 | (r << 16) | (g << 8) | b);
            }
        }

        return picture;
    }

    /// <summary>A photo-like picture of noise over a gradient, which does not compress well exactly.</summary>
    public static FrameBitmap Noise(int width, int height, int seed = 7)
    {
        var random = new Random(seed);
        var picture = Gradient(width, height);
        for (var i = 0; i < picture.Pixels.Length; i++)
        {
            if (i % 4 != 3)
            {
                picture.Pixels[i] = (byte)Math.Clamp(picture.Pixels[i] + random.Next(-40, 41), 0, 255);
            }
        }

        return picture;
    }

    /// <summary>The largest difference of any color channel between two pictures of the same size.</summary>
    public static int MaxDifference(FrameBitmap a, FrameBitmap b)
    {
        var max = 0;
        for (var i = 0; i < a.Pixels.Length; i++)
        {
            if (i % 4 != 3)
            {
                max = Math.Max(max, Math.Abs(a.Pixels[i] - b.Pixels[i]));
            }
        }

        return max;
    }
}
