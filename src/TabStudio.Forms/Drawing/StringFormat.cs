using System.Drawing.Text;

namespace System.Drawing;

/// <summary>Text layout information for <see cref="Graphics.DrawString(string, Font, Brush, RectangleF, StringFormat)"/>: alignment, wrapping and trimming.</summary>
public sealed class StringFormat : MarshalByRefObject, ICloneable, IDisposable
{
    /// <summary>Creates a format: near-aligned, wrapping, untrimmed.</summary>
    public StringFormat()
    {
    }

    /// <summary>Creates a format with flags.</summary>
    /// <param name="options">The flags.</param>
    public StringFormat(StringFormatFlags options) => FormatFlags = options;

    /// <summary>Creates a format with flags.</summary>
    /// <param name="options">The flags.</param>
    /// <param name="language">The language; ignored.</param>
    public StringFormat(StringFormatFlags options, int language) => FormatFlags = options;

    /// <summary>Copies a format.</summary>
    /// <param name="format">The format to copy.</param>
    public StringFormat(StringFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        Alignment = format.Alignment;
        LineAlignment = format.LineAlignment;
        FormatFlags = format.FormatFlags;
        Trimming = format.Trimming;
        HotkeyPrefix = format.HotkeyPrefix;
    }

    /// <summary>The default format.</summary>
    public static StringFormat GenericDefault => new();

    /// <summary>A typographic format: no padding, no wrapping beyond the layout rectangle's lines.</summary>
    public static StringFormat GenericTypographic => new(StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip);

    /// <summary>The horizontal alignment.</summary>
    public StringAlignment Alignment { get; set; }

    /// <summary>The vertical alignment.</summary>
    public StringAlignment LineAlignment { get; set; }

    /// <summary>Layout flags, such as <see cref="StringFormatFlags.NoWrap"/>.</summary>
    public StringFormatFlags FormatFlags { get; set; }

    /// <summary>How text that does not fit is trimmed.</summary>
    public StringTrimming Trimming { get; set; } = StringTrimming.Character;

    /// <summary>How ampersands are shown.</summary>
    public HotkeyPrefix HotkeyPrefix { get; set; }

    /// <summary>Sets tab stops. Tabs are drawn at the font's own tab width on this device.</summary>
    /// <param name="firstTabOffset">The first tab's offset.</param>
    /// <param name="tabStops">The distances between tab stops.</param>
    public void SetTabStops(float firstTabOffset, float[] tabStops)
    {
        // Tabs keep the text layout's own width.
    }

    /// <inheritdoc />
    public object Clone() => new StringFormat(this);

    /// <inheritdoc />
    public void Dispose()
    {
        // A format holds nothing to release.
    }
}
