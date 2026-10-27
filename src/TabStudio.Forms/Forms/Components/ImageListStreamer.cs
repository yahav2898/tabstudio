using System.Buffers.Binary;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// The images of an image list as the Windows Forms designer serializes them into a .resx file: Windows' image list
/// format (<c>ImageList_Write</c>), compressed as Windows Forms compresses it. TabStudio reads it from the .resx
/// files of a project, so <c>imageList1.ImageStream = (ImageListStreamer)resources.GetObject(...)</c> loads the
/// images, and makes one from a list's images for another list.
/// </summary>
public sealed class ImageListStreamer : IDisposable
{
    private static readonly byte[] CompressedMagic = "MSFt"u8.ToArray();

    internal ImageListStreamer(Size imageSize, IReadOnlyList<Image> images)
    {
        ImageSize = imageSize;
        Images = images;
    }

    /// <summary>The size of the images.</summary>
    internal Size ImageSize { get; }

    /// <summary>The images.</summary>
    internal IReadOnlyList<Image> Images { get; }

    /// <summary>Reads the bytes Windows Forms serializes: optionally compressed, then an image list stream.</summary>
    /// <param name="data">The bytes of the streamer's <c>Data</c> member.</param>
    /// <exception cref="ArgumentException">The bytes are not an image list.</exception>
    internal static ImageListStreamer Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var stream = data.AsSpan().StartsWith(CompressedMagic) ? Decompress(data) : data;
        return ReadImageList(stream);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var image in Images)
        {
            image.Dispose();
        }
    }

    /// <summary>Expands Windows Forms' run-length encoding: pairs of a count and a byte, after the magic.</summary>
    private static byte[] Decompress(byte[] input)
    {
        var length = 0;
        for (var i = CompressedMagic.Length; i + 1 < input.Length; i += 2)
        {
            length += input[i];
        }

        var output = new byte[length];
        var index = 0;
        for (var i = CompressedMagic.Length; i + 1 < input.Length; i += 2)
        {
            output.AsSpan(index, input[i]).Fill(input[i + 1]);
            index += input[i];
        }

        return output;
    }

    /// <summary>
    /// Reads an image list stream: a 28-byte header (magic <c>IL</c>, version, image count, grow, image width and
    /// height, background color, flags, overlays), the images as one bitmap file, and with <c>ILC_MASK</c> their
    /// masks as another, both four images to a row.
    /// </summary>
    private static ImageListStreamer ReadImageList(byte[] data)
    {
        if (data.Length < 28 || data[0] != (byte)'I' || data[1] != (byte)'L')
        {
            throw new ArgumentException("Parameter is not valid: the data is not an image list.", nameof(data));
        }

        var count = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(4));
        var width = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(10));
        var height = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(12));
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(18));
        var offset = 28;
        var colors = BmpReader.Read(data, ref offset);
        var mask = (flags & 0x0001) != 0 && offset < data.Length ? BmpReader.Read(data, ref offset) : null;
        var columns = Math.Max(1, colors.Width / Math.Max(1, (int)width));

        // A 32-bit image list keeps alpha in its pixels; others use the mask, where set bits are transparent.
        var hasAlpha = colors.HasAlpha;
        var images = new List<Image>(count);
        for (var index = 0; index < count; index++)
        {
            var left = index % columns * width;
            var top = index / columns * height;
            if (left + width > colors.Width || top + height > colors.Height)
            {
                break;
            }

            var pixels = new int[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = colors.Pixels[((top + y) * colors.Width) + left + x];
                    if (!hasAlpha)
                    {
                        color |= unchecked((int)0xFF000000);
                        if (mask is not null && (mask.Pixels[((top + y) * mask.Width) + left + x] & 0xFFFFFF) != 0)
                        {
                            color = 0;
                        }
                    }

                    pixels[(y * width) + x] = color;
                }
            }

            images.Add(Bitmap.FromPixels(width, height, pixels));
        }

        return new ImageListStreamer(new Size(width, height), images);
    }
}
