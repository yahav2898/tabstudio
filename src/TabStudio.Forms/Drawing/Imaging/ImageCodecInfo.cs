namespace System.Drawing.Imaging;

/// <summary>An image encoder or decoder: the formats <see cref="Image.Save(string, ImageCodecInfo, EncoderParameters?)"/> writes.</summary>
public sealed class ImageCodecInfo
{
    private static readonly ImageCodecInfo[] Codecs =
    [
        new(ImageFormat.Bmp, "image/bmp", "*.BMP;*.DIB;*.RLE", "Built-in BMP Codec", "BMP"),
        new(ImageFormat.Jpeg, "image/jpeg", "*.JPG;*.JPEG;*.JPE;*.JFIF", "Built-in JPEG Codec", "JPEG"),
        new(ImageFormat.Gif, "image/gif", "*.GIF", "Built-in GIF Codec", "GIF"),
        new(ImageFormat.Tiff, "image/tiff", "*.TIF;*.TIFF", "Built-in TIFF Codec", "TIFF"),
        new(ImageFormat.Png, "image/png", "*.PNG", "Built-in PNG Codec", "PNG"),
    ];

    private ImageCodecInfo(ImageFormat format, string mimeType, string extensions, string name, string description)
    {
        FormatID = format.Guid;
        Clsid = format.Guid;
        MimeType = mimeType;
        FilenameExtension = extensions;
        CodecName = name;
        FormatDescription = description;
        DllName = null;
        Version = 1;
    }

    /// <summary>The codec's identifier.</summary>
    public Guid Clsid { get; set; }

    /// <summary>The identifier of the codec's file format, the <see cref="ImageFormat.Guid"/>.</summary>
    public Guid FormatID { get; set; }

    /// <summary>The codec's name.</summary>
    public string? CodecName { get; set; }

    /// <summary>The library of the codec: none.</summary>
    public string? DllName { get; set; }

    /// <summary>The file format's name, such as <c>JPEG</c>.</summary>
    public string? FormatDescription { get; set; }

    /// <summary>The file name extensions, such as <c>*.JPG;*.JPEG</c>.</summary>
    public string? FilenameExtension { get; set; }

    /// <summary>The MIME type, such as <c>image/jpeg</c>.</summary>
    public string? MimeType { get; set; }

    /// <summary>The codec's version.</summary>
    public int Version { get; set; }

    /// <summary>The image encoders.</summary>
    public static ImageCodecInfo[] GetImageEncoders() => [.. Codecs];

    /// <summary>The image decoders.</summary>
    public static ImageCodecInfo[] GetImageDecoders() => [.. Codecs];
}
