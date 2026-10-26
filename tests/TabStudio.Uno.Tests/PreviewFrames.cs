using SkiaSharp;
using TabStudio.Uno.Hosting;

namespace TabStudio.Uno.Tests;

/// <summary>The frames Uno Platform draws for the app preview, as pictures to check and to look at.</summary>
internal static class PreviewFrames
{
    /// <summary>
    /// Where frames are saved: TABSTUDIO_SCREENSHOTS, or the published CI folder, or the test's output folder.
    /// </summary>
    public static string Folder { get; } =
        Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS")
        ?? (Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") is { Length: > 0 } workspace
            ? Path.Combine(workspace, "ci-out", "uno-frames")
            : Path.Combine(AppContext.BaseDirectory, "uno-frames"));

    /// <summary>Copies the newest frame, waiting for one to be drawn.</summary>
    public static async Task<SKBitmap> CaptureAsync(UnoRuntime runtime)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            var (width, height) = runtime.FrameSize;
            if (width > 0 && height > 0)
            {
                var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                if (runtime.CopyFrame(bitmap.GetPixels(), width, height, bitmap.RowBytes))
                {
                    return bitmap;
                }

                bitmap.Dispose();
            }

            Assert.True(DateTime.UtcNow < deadline, "Uno Platform drew no frame in 30 seconds.");
            await Task.Delay(100);
        }
    }

    /// <summary>Saves the newest frame as a PNG named <paramref name="name"/>, and returns it.</summary>
    public static async Task<SKBitmap> SaveAsync(UnoRuntime runtime, string name)
    {
        var bitmap = await CaptureAsync(runtime);
        Directory.CreateDirectory(Folder);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        await File.WriteAllBytesAsync(Path.Combine(Folder, name + ".png"), png.ToArray());
        return bitmap;
    }

    /// <summary>How many pixels differ clearly from the frame's top-left pixel, the page's background.</summary>
    public static int CountDrawnPixels(SKBitmap frame)
    {
        var background = frame.GetPixel(0, 0);
        var drawn = 0;
        for (var y = 0; y < frame.Height; y += 2)
        {
            for (var x = 0; x < frame.Width; x += 2)
            {
                var pixel = frame.GetPixel(x, y);
                if (Math.Abs(pixel.Red - background.Red) + Math.Abs(pixel.Green - background.Green) + Math.Abs(pixel.Blue - background.Blue) > 96)
                {
                    drawn++;
                }
            }
        }

        return drawn;
    }

    /// <summary>Waits until the app shows a text, failing with what it shows instead.</summary>
    public static async Task WaitForTextAsync(UnoApp app, string text)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        IReadOnlyList<string> texts = [];
        while (DateTime.UtcNow < deadline)
        {
            texts = await app.GetTextsAsync();
            if (texts.Contains(text, StringComparer.Ordinal))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"The app did not show \"{text}\". It shows: {string.Join(" | ", texts)}");
    }
}
