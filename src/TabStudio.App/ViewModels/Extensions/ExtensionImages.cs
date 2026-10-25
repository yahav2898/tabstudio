using System.Globalization;
using Avalonia.Media.Imaging;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>
/// Loads extension icons and README images as bitmaps: PNG, JPEG, GIF (first frame), BMP and WebP. SVG images, which
/// some READMEs use, cannot be drawn and are left out. Icons are kept for the session, so lists scroll smoothly.
/// </summary>
public static class ExtensionImages
{
    /// <summary>The largest image file that is loaded.</summary>
    public const int MaximumBytes = 8 * 1024 * 1024;

    private const int MaximumCachedIcons = 400;

    // Icons are shown at 40 to 64 pixels; twice that keeps them sharp on high-density screens.
    private const int IconPixels = 128;

    private static readonly Dictionary<string, Bitmap?> Icons = new(StringComparer.Ordinal);

    /// <summary>Loads an image from the registry or the web, through the client's cache.</summary>
    /// <param name="client">The registry client.</param>
    /// <param name="address">The image's address.</param>
    /// <param name="decodeWidth">The width to decode to, which keeps large screenshots small in memory; 0 for the full size.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be loaded or drawn.</returns>
    public static async Task<Bitmap?> LoadAsync(OpenVsxClient client, Uri address, int decodeWidth, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(address);
        if (IsVectorImage(address.AbsolutePath))
        {
            return null;
        }

        try
        {
            var bytes = await client.GetFileAsync(address, cancellationToken);
            return bytes.Length is 0 or > MaximumBytes ? null : await Task.Run(() => Decode(bytes, decodeWidth), cancellationToken);
        }
        catch (OpenVsxException)
        {
            return null;
        }
    }

    /// <summary>Loads an image file of an installed extension.</summary>
    /// <param name="path">The file.</param>
    /// <param name="decodeWidth">The width to decode to; 0 for the full size.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be read or drawn.</returns>
    public static async Task<Bitmap?> LoadFileAsync(string path, int decodeWidth, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (IsVectorImage(path))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is 0 or > MaximumBytes)
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return await Task.Run(() => Decode(bytes, decodeWidth), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>An extension's icon from the registry, decoded once per session.</summary>
    /// <param name="client">The registry client.</param>
    /// <param name="address">The icon's address.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public static async Task<Bitmap?> IconAsync(OpenVsxClient client, Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        var key = address.AbsoluteUri;
        if (Icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = await LoadAsync(client, address, IconPixels, cancellationToken);
        Remember(key, icon);
        return icon;
    }

    /// <summary>An installed extension's icon, decoded once per session.</summary>
    /// <param name="path">The icon file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<Bitmap?> IconFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var key = "file:" + path;
        if (Icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = await LoadFileAsync(path, IconPixels, cancellationToken);
        Remember(key, icon);
        return icon;
    }

    /// <summary>A count as the Extensions window shows it: 950, 12.3K, 4.5M.</summary>
    /// <param name="count">The count.</param>
    public static string FormatCount(long count) => count switch
    {
        >= 1_000_000_000 => (count / 1_000_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "B",
        >= 1_000_000 => (count / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (count / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };

    private static bool IsVectorImage(string path) =>
        path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase);

    private static Bitmap? Decode(byte[] bytes, int decodeWidth)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            if (decodeWidth <= 0)
            {
                return new Bitmap(stream);
            }

            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width <= decodeWidth)
            {
                return bitmap;
            }

            using (bitmap)
            {
                stream.Position = 0;
                return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            // Not an image Skia can decode, such as an SVG served without its extension.
            return null;
        }
    }

    private static void Remember(string key, Bitmap? icon)
    {
        if (Icons.Count >= MaximumCachedIcons)
        {
            Icons.Clear();
        }

        Icons[key] = icon;
    }
}
