using ZXing;
using ZXing.Common;
using ZXing.Multi.QrCode;
using ZXing.QrCode;

namespace TabStudio.App.Platform;

/// <summary>
/// Finds a QR code in a camera frame, for the scanners of devices with a camera. Thread-safe: each call decodes on
/// its own.
/// </summary>
public static class QrFrameDecoder
{
    private static readonly Dictionary<DecodeHintType, object> Hints = new()
    {
        [DecodeHintType.CHARACTER_SET] = "UTF-8",
        [DecodeHintType.TRY_HARDER] = true,
    };

    /// <summary>Reads a QR code from the brightness of a frame, such as the Y plane of a camera's YUV image.</summary>
    /// <param name="luminance">One byte of brightness per pixel, row after row.</param>
    /// <param name="rowStride">The bytes from the start of one row to the next, at least <paramref name="width"/>.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    /// <returns>The text of the code, or <see langword="null"/> when the frame shows none that can be read.</returns>
    public static string? Decode(byte[] luminance, int rowStride, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(luminance);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowStride, width);
        ArgumentOutOfRangeException.ThrowIfLessThan(luminance.Length, rowStride * height, nameof(luminance));

        var source = new PlanarYUVLuminanceSource(luminance, rowStride, height, 0, 0, width, height, false);
        var bitmap = new BinaryBitmap(new HybridBinarizer(source));

        // ZXing's QR reader stops looking for finder patterns once three of a similar size are confirmed, and a code's
        // data can hold a finder-like run above its real bottom-left finder: the reader then takes the wrong three and
        // reads nothing. That happens to about one in fifty of TabStudio's invitation codes, and as often to codes that
        // ZXing's own encoder makes. The multiple-code reader looks at the whole frame and weighs every candidate.
        return new QRCodeReader().decode(bitmap, Hints)?.Text
            ?? new QRCodeMultiReader().decodeMultiple(bitmap, Hints)?.FirstOrDefault()?.Text;
    }
}
