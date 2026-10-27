namespace System.Drawing.Imaging;

/// <summary>The settings an image is saved with.</summary>
public sealed class EncoderParameters : IDisposable
{
    /// <summary>Creates room for one setting.</summary>
    public EncoderParameters()
        : this(1)
    {
    }

    /// <summary>Creates room for a number of settings.</summary>
    /// <param name="count">The number of settings.</param>
    public EncoderParameters(int count) => Param = new EncoderParameter[count];

    /// <summary>The settings.</summary>
#pragma warning disable CA1819 // GDI+'s own shape: apps assign Param[0].
    public EncoderParameter[] Param { get; set; }
#pragma warning restore CA1819

    /// <summary>The JPEG quality asked for, or <see langword="null"/>.</summary>
    internal int? Quality =>
        Param.FirstOrDefault(p => p?.Encoder.Guid == Encoder.Quality.Guid) is { } quality ? (int)Math.Clamp(quality.Value, 0, 100) : null;

    /// <inheritdoc />
    public void Dispose()
    {
        // Settings hold nothing to release.
    }
}
