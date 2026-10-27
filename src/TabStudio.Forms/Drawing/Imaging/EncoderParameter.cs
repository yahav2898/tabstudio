namespace System.Drawing.Imaging;

/// <summary>A value of an encoder setting, such as a JPEG quality of 90.</summary>
public sealed class EncoderParameter : IDisposable
{
    /// <summary>Creates a parameter of a number.</summary>
    /// <param name="encoder">The setting.</param>
    /// <param name="value">The value.</param>
    public EncoderParameter(Encoder encoder, long value)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        Encoder = encoder;
        Value = value;
        NumberOfValues = 1;
    }

    /// <summary>Creates a parameter of a byte.</summary>
    /// <param name="encoder">The setting.</param>
    /// <param name="value">The value.</param>
    public EncoderParameter(Encoder encoder, byte value)
        : this(encoder, (long)value)
    {
    }

    /// <summary>Creates a parameter of a number.</summary>
    /// <param name="encoder">The setting.</param>
    /// <param name="value">The value.</param>
    public EncoderParameter(Encoder encoder, short value)
        : this(encoder, (long)value)
    {
    }

    /// <summary>Creates a parameter of a number.</summary>
    /// <param name="encoder">The setting.</param>
    /// <param name="value">The value.</param>
    public EncoderParameter(Encoder encoder, int value)
        : this(encoder, (long)value)
    {
    }

    /// <summary>Creates a parameter of numbers; the first is used.</summary>
    /// <param name="encoder">The setting.</param>
    /// <param name="value">The values.</param>
    public EncoderParameter(Encoder encoder, long[] value)
        : this(encoder, value is { Length: > 0 } ? value[0] : 0)
    {
        NumberOfValues = value?.Length ?? 0;
    }

    /// <summary>The setting.</summary>
    public Encoder Encoder { get; set; }

    /// <summary>The number of values.</summary>
    public int NumberOfValues { get; }

    /// <summary>The value.</summary>
    internal long Value { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        // A parameter holds nothing to release.
    }
}
