using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Drawing;

/// <summary>
/// Every scene of <see cref="DrawingScenes"/>, drawn on a bitmap with <see cref="Graphics.FromImage"/> on a thread
/// of its own, as an app's Forms thread draws, and compared with its reference PNG. References drawn by GDI+ hold
/// shapes to GDI+'s output; text scenes are held to TabStudio's own earlier output, since the fonts differ.
/// </summary>
public sealed class DrawingSceneTests
{
    /// <summary>The share of a channel's range a pixel may differ by before it counts as different.</summary>
    private const int PixelTolerance = 48;

    /// <summary>The scenes' names.</summary>
    public static TheoryData<string> Scenes => [.. DrawingScenes.All.Select(scene => scene.Name)];

    [AvaloniaTheory]
    [MemberData(nameof(Scenes))]
    public void A_scene_drawn_on_a_bitmap_matches_its_reference(string name)
    {
        var scene = DrawingScenes.All.Single(s => s.Name == name);
        var actual = Render(scene);
        Save(actual, scene.Width, scene.Height, name);

        var path = Path.Combine(AppContext.BaseDirectory, "Drawing", "References", name + ".png");
        Assert.True(File.Exists(path), $"There is no reference for '{name}': the drawing was saved to the screenshots folder (drawing/{name}.png); review it and add it as Drawing/References/{name}.png.");
        using var reference = new Bitmap(path);
        Assert.Equal(new Size(scene.Width, scene.Height), reference.Size);

        var comparison = PixelComparison.Compare(actual, Pixels(reference), scene.Width, scene.Height, PixelTolerance);
        var (maxShare, maxMean) = Limits(scene);
        Assert.True(
            comparison.DifferentShare <= maxShare && comparison.MeanDifference <= maxMean,
            $"'{name}' differs from its reference: {comparison.DifferentShare:P2} of pixels differ (at most {maxShare:P2}), mean difference {comparison.MeanDifference:F2} (at most {maxMean:F2}); first difference at {comparison.FirstDifference}.");
    }

    [AvaloniaFact]
    public void Drawing_and_pixel_access_agree_in_both_directions()
    {
        using var bitmap = new Bitmap(20, 10);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Red, 0, 0, 10, 10);
        }

        Assert.Equal(Color.FromArgb(255, 255, 0, 0).ToArgb(), bitmap.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(15, 5).ToArgb());

        bitmap.SetPixel(15, 5, Color.Blue);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.FillRectangle(Brushes.Lime, 0, 0, 5, 5);
        }

        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(15, 5).ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), bitmap.GetPixel(2, 2).ToArgb());
        Assert.Equal(Color.FromArgb(255, 255, 0, 0).ToArgb(), bitmap.GetPixel(7, 7).ToArgb());

        // A semi-transparent pixel set directly comes back exactly, as GDI+ keeps it.
        bitmap.SetPixel(19, 9, Color.FromArgb(10, 200, 100, 50));
        Assert.Equal(Color.FromArgb(10, 200, 100, 50).ToArgb(), bitmap.GetPixel(19, 9).ToArgb());
    }

    [AvaloniaFact]
    public void Drawing_shows_after_Flush_without_disposing_and_the_graphics_can_go_on()
    {
        using var bitmap = new Bitmap(10, 10);
        var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Black);
        g.Flush();
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());

        g.FillRectangle(Brushes.White, 0, 0, 5, 5);
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(1, 1).ToArgb());
        g.Dispose();
        Assert.Throws<ObjectDisposedException>(() => g.FillRectangle(Brushes.White, 0, 0, 5, 5));
    }

    [AvaloniaFact]
    public void Clear_with_a_transparent_color_makes_pixels_transparent()
    {
        using var bitmap = new Bitmap(10, 10);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Red);
        g.SetClip(new Rectangle(0, 0, 5, 10));
        g.Clear(Color.Transparent);
        g.Flush();

        Assert.Equal(0, bitmap.GetPixel(2, 2).A);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(7, 2).ToArgb());
    }

    [AvaloniaFact]
    public void A_bitmap_drawn_into_another_and_into_itself_keeps_its_pixels()
    {
        using var source = DrawingScenes.Checker();
        using var target = new Bitmap(16, 8);
        using (var g = Graphics.FromImage(target))
        {
            g.DrawImageUnscaled(source, 0, 0);
            g.DrawImageUnscaled(source, 8, 0);
        }

        Assert.Equal(Color.Red.ToArgb(), target.GetPixel(1, 0).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), target.GetPixel(9, 0).ToArgb());
        Assert.Equal(Color.Yellow.ToArgb(), target.GetPixel(14, 6).ToArgb());

        using (var g = Graphics.FromImage(target))
        {
            g.DrawImage(target, new Rectangle(0, 0, 8, 4));
        }

        // The bitmap drew a snapshot of itself: the top left now holds both copies, scaled; the rest is unchanged.
        Assert.Equal(255, target.GetPixel(1, 1).A);
        Assert.Equal(Color.Yellow.ToArgb(), target.GetPixel(14, 6).ToArgb());
    }

    [AvaloniaFact]
    public void LockBits_hands_out_and_takes_back_pixels_in_32_and_24_bits()
    {
        using var bitmap = new Bitmap(4, 2);
        bitmap.SetPixel(1, 0, Color.FromArgb(255, 10, 20, 30));

        var data = bitmap.LockBits(new Rectangle(0, 0, 4, 2), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        Assert.Equal(16, data.Stride);
        var bytes = new byte[data.Stride * data.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        Assert.Equal([30, 20, 10, 255], bytes[4..8]);
        bytes[0] = 255;
        bytes[3] = 255;
        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bitmap.UnlockBits(data);
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());

        var rgb = bitmap.LockBits(new Rectangle(1, 0, 3, 2), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        Assert.Equal(12, rgb.Stride);
        var row = new byte[rgb.Stride * rgb.Height];
        Marshal.Copy(rgb.Scan0, row, 0, row.Length);
        Assert.Equal([30, 20, 10], row[0..3]);
        row[3] = 0;
        row[4] = 255;
        row[5] = 0;
        Marshal.Copy(row, 0, rgb.Scan0, row.Length);
        bitmap.UnlockBits(rgb);
        Assert.Equal(Color.Lime.ToArgb(), bitmap.GetPixel(2, 0).ToArgb());
    }

    [AvaloniaFact]
    public void Save_writes_PNG_JPEG_and_BMP_that_read_back()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Red, 0, 0, 8, 16);
        }

        foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Bmp })
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, format);
            stream.Position = 0;
            using var read = new Bitmap(stream);
            Assert.Equal(format, read.RawFormat);
            var red = read.GetPixel(3, 8);
            var white = read.GetPixel(12, 8);
            Assert.True(red.R > 200 && red.G < 60 && red.B < 60, $"{format}: {red}");
            Assert.True(white.R > 200 && white.G > 200 && white.B > 200, $"{format}: {white}");
        }
    }

    [AvaloniaFact]
    public void Clone_of_a_rectangle_and_Bitmap_of_an_image_copy_the_pixels()
    {
        using var source = DrawingScenes.Checker();
        using var part = source.Clone(new Rectangle(4, 0, 4, 4), PixelFormat.Format32bppArgb);
        Assert.Equal(new Size(4, 4), part.Size);
        Assert.Equal(Color.Lime.ToArgb(), part.GetPixel(1, 1).ToArgb());

        using var copy = new Bitmap(source);
        source.SetPixel(1, 0, Color.White);
        Assert.Equal(Color.Red.ToArgb(), copy.GetPixel(1, 0).ToArgb());
        Assert.Throws<OutOfMemoryException>(() => source.Clone(new Rectangle(6, 6, 4, 4), PixelFormat.Format32bppArgb));
    }

    [AvaloniaFact]
    public void RotateFlip_turns_the_pixels()
    {
        using var bitmap = new Bitmap(3, 2);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
        Assert.Equal(new Size(2, 3), bitmap.Size);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(1, 0).ToArgb());
    }

    /// <summary>Draws a scene on a thread that is not Avalonia's UI thread, as an app's Forms thread draws.</summary>
    internal static int[] Render(DrawingScene scene)
    {
        int[]? pixels = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var bitmap = new Bitmap(scene.Width, scene.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                    scene.Draw(g);
                }

                pixels = Pixels(bitmap);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw new InvalidOperationException($"Drawing '{scene.Name}' failed.", error);
        }

        return pixels!;
    }

    /// <summary>A bitmap's pixels as 0xAARRGGBB values, read with LockBits.</summary>
    internal static int[] Pixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new int[bitmap.Width * bitmap.Height];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), pixels, y * bitmap.Width, bitmap.Width);
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void Save(int[] pixels, int width, int height, string name)
    {
        if (Screenshots.Folder is not { } folder)
        {
            return;
        }

        var directory = Path.Combine(folder, "drawing");
        Directory.CreateDirectory(directory);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, handle.AddrOfPinnedObject());
            bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>How far a scene may be from its reference: text and antialiased edges differ the most from GDI+.</summary>
    private static (double Share, double Mean) Limits(DrawingScene scene) => scene.FromGdiPlus ? (0.04, 6) : (0.01, 1.5);
}
