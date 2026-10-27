namespace System.Drawing.Imaging;

/// <summary>A setting of an image encoder, such as <see cref="Quality"/>.</summary>
/// <param name="guid">The setting's identifier.</param>
public sealed class Encoder(Guid guid)
{
    /// <summary>The compression of TIFF images.</summary>
    public static readonly Encoder Compression = new(new Guid("e09d739d-ccd4-44ee-8eba-3fbf8be4fc58"));

    /// <summary>The color depth.</summary>
    public static readonly Encoder ColorDepth = new(new Guid("66087055-ad66-4c7c-9a18-38a2310b8337"));

    /// <summary>The scan method.</summary>
    public static readonly Encoder ScanMethod = new(new Guid("3a4e2661-3109-4e56-8536-42c156e7dcfa"));

    /// <summary>The version.</summary>
    public static readonly Encoder Version = new(new Guid("24d18c76-814a-41a4-bf53-1c219cccf797"));

    /// <summary>The rendering method.</summary>
    public static readonly Encoder RenderMethod = new(new Guid("6d42c53a-229a-4825-8bb7-5c99e2b9a8b8"));

    /// <summary>The JPEG quality, from 0 to 100.</summary>
    public static readonly Encoder Quality = new(new Guid("1d5be4b5-fa4a-452d-9cdd-5db35105e7eb"));

    /// <summary>The transformation of a JPEG image.</summary>
    public static readonly Encoder Transformation = new(new Guid("8d0eb2d1-a58e-4ea8-aa14-108074b7b6f9"));

    /// <summary>The JPEG luminance table.</summary>
    public static readonly Encoder LuminanceTable = new(new Guid("edb33bce-0266-4a77-b904-27216099e717"));

    /// <summary>The JPEG chrominance table.</summary>
    public static readonly Encoder ChrominanceTable = new(new Guid("f2e455dc-09b3-4316-8260-676ada32481c"));

    /// <summary>The frames of a multiple-frame file.</summary>
    public static readonly Encoder SaveFlag = new(new Guid("292266fc-ac40-47bf-8cfc-a85b89a655de"));

    /// <summary>The setting's identifier.</summary>
    public Guid Guid { get; } = guid;
}
