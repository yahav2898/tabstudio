using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace TabStudio.Forms.Rendering;

/// <summary>The file formats <see cref="System.Drawing.Image.Save(string)"/> writes.</summary>
internal enum ImageFileFormat
{
    /// <summary>PNG, with alpha.</summary>
    Png,

    /// <summary>JPEG, without alpha.</summary>
    Jpeg,

    /// <summary>Windows bitmap: 24 bits per pixel, or 32 with alpha, as GDI+ writes them.</summary>
    Bmp,

    /// <summary>WebP.</summary>
    Webp,

    /// <summary>A Windows icon holding one PNG image.</summary>
    Icon,

    /// <summary>GIF: 256 colors at most, one of them transparent.</summary>
    Gif,
}

/// <summary>Writes pixels as image files: PNG, JPEG and WebP with Skia's encoders, BMP, ICO and GIF directly.</summary>
internal static class ImageEncoder
{
    /// <summary>Writes unpremultiplied 0xAARRGGBB pixels as a file.</summary>
    /// <param name="stream">Where to write.</param>
    /// <param name="pixels">The pixels, row by row.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="opaque">Whether the image has no alpha, so BMP writes 24 bits per pixel.</param>
    /// <param name="format">The file format.</param>
    /// <param name="quality">The JPEG or WebP quality from 0 to 100.</param>
    public static void Encode(Stream stream, int[] pixels, int width, int height, bool opaque, ImageFileFormat format, int quality)
    {
        ArgumentNullException.ThrowIfNull(stream);
        switch (format)
        {
            case ImageFileFormat.Bmp:
                WriteBmp(stream, pixels, width, height, opaque);
                break;
            case ImageFileFormat.Gif:
                GifWriter.Write(stream, pixels, width, height);
                break;
            case ImageFileFormat.Icon:
                var png = EncodeWithSkia(pixels, width, height, SKEncodedImageFormat.Png, 100);
                WriteIcon(stream, png, width, height);
                break;
            default:
                var skiaFormat = format switch
                {
                    ImageFileFormat.Jpeg => SKEncodedImageFormat.Jpeg,
                    ImageFileFormat.Webp => SKEncodedImageFormat.Webp,
                    _ => SKEncodedImageFormat.Png,
                };
                var bytes = EncodeWithSkia(pixels, width, height, skiaFormat, Math.Clamp(quality, 0, 100));
                stream.Write(bytes);
                break;
        }
    }

    private static byte[] EncodeWithSkia(int[] pixels, int width, int height, SKEncodedImageFormat format, int quality)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            // JPEG has no alpha: GDI+ writes the color of transparent pixels, which for a new bitmap is black.
            var alpha = format == SKEncodedImageFormat.Jpeg ? SKAlphaType.Opaque : SKAlphaType.Unpremul;
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, alpha);
            using var image = SKImage.FromPixelCopy(info, handle.AddrOfPinnedObject(), width * 4);
            using var data = image.Encode(format, quality)
                ?? throw new InvalidOperationException($"A generic error occurred in GDI+: the image could not be encoded as {format}.");
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    private static void WriteBmp(Stream stream, int[] pixels, int width, int height, bool opaque)
    {
        var bytesPerPixel = opaque ? 3 : 4;
        var stride = ((width * bytesPerPixel) + 3) & ~3;
        var imageSize = stride * height;
        var header = new byte[54];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(2), 54 + imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(28), (short)(bytesPerPixel * 8));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(34), imageSize);

        // 96 dots per inch, in pixels per meter.
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(38), 3780);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(42), 3780);
        stream.Write(header);

        // Rows go bottom up.
        var row = new byte[stride];
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var c = pixels[(y * width) + x];
                var o = x * bytesPerPixel;
                row[o] = (byte)c;
                row[o + 1] = (byte)(c >> 8);
                row[o + 2] = (byte)(c >> 16);
                if (!opaque)
                {
                    row[o + 3] = (byte)(c >> 24);
                }
            }

            stream.Write(row);
        }
    }

    private static void WriteIcon(Stream stream, byte[] png, int width, int height)
    {
        var header = new byte[22];
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(4), 1);

        // 0 stands for 256 or more.
        header[6] = (byte)(width >= 256 ? 0 : width);
        header[7] = (byte)(height >= 256 ? 0 : height);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(10), 1);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(12), 32);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14), png.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), 22);
        stream.Write(header);
        stream.Write(png);
    }
}
