using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Tests.Infrastructure;

/// <summary>Saves pictures of rendered windows, for the screenshots CI publishes.</summary>
internal static class Screenshots
{
    /// <summary>The folder screenshots go to: TABSTUDIO_SCREENSHOTS, or ci-out/forms-screenshots at the repository root.</summary>
    public static string? Folder
    {
        get
        {
            if (Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") is { Length: > 0 } configured)
            {
                return configured;
            }

            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx")))
                {
                    return Path.Combine(directory.FullName, "ci-out", "forms-screenshots");
                }
            }

            return null;
        }
    }

    /// <summary>Draws a window's client area and its popups on white, and saves it as a PNG. UI thread only.</summary>
    /// <param name="frame">The window's frame.</param>
    /// <param name="name">The file name, without extension.</param>
    /// <returns>The picture's pixels, for tests to check.</returns>
    public static WriteableBitmapPixels Save(WindowFrame frame, string name)
    {
        var size = new PixelSize(Math.Max(1, frame.Client.Width), Math.Max(1, frame.Client.Height));
        foreach (var popup in frame.Popups)
        {
            size = new PixelSize(Math.Max(size.Width, popup.Bounds.Right), Math.Max(size.Height, popup.Bounds.Bottom));
        }

        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        var renderer = new DisplayListRenderer();
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, size.Width, size.Height));
            renderer.Render(context, frame.Content);
            foreach (var popup in frame.Popups)
            {
                renderer.Render(context, popup.Content);
            }
        }

        if (Folder is { } folder)
        {
            Directory.CreateDirectory(folder);
            bitmap.Save(Path.Combine(folder, name + ".png"));
        }

        return WriteableBitmapPixels.From(bitmap);
    }
}

/// <summary>The pixels of a rendered picture.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Pixels">The pixels, as BGRA or RGBA bytes.</param>
/// <param name="IsRgba">Whether the bytes are in RGBA order.</param>
internal sealed record WriteableBitmapPixels(int Width, int Height, byte[] Pixels, bool IsRgba)
{
    /// <summary>Reads a bitmap's pixels, through a PNG, which every bitmap can be saved as.</summary>
    public static WriteableBitmapPixels From(Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png);
        png.Position = 0;
        using var decoded = WriteableBitmap.Decode(png);
        using var buffer = decoded.Lock();
        var width = buffer.Size.Width;
        var height = buffer.Size.Height;
        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address + (row * buffer.RowBytes), pixels, row * width * 4, width * 4);
        }

        return new WriteableBitmapPixels(width, height, pixels, buffer.Format == Avalonia.Platform.PixelFormat.Rgba8888);
    }

    /// <summary>The color of a pixel.</summary>
    public System.Drawing.Color At(int x, int y)
    {
        var i = ((y * Width) + x) * 4;
        return IsRgba
            ? System.Drawing.Color.FromArgb(Pixels[i + 3], Pixels[i], Pixels[i + 1], Pixels[i + 2])
            : System.Drawing.Color.FromArgb(Pixels[i + 3], Pixels[i + 2], Pixels[i + 1], Pixels[i]);
    }
}
