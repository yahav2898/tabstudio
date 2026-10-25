using System.Globalization;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>A format for text: a family, a size and a style.</summary>
public sealed class Font : MarshalByRefObject, ICloneable, IDisposable
{
    private FontSpec? _spec;

    /// <summary>Creates a font of a family, in points.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="emSize">The size in points.</param>
    public Font(string familyName, float emSize)
        : this(new FontFamily(familyName), emSize, FontStyle.Regular, GraphicsUnit.Point)
    {
    }

    /// <summary>Creates a font of a family and style, in points.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="emSize">The size in points.</param>
    /// <param name="style">The style.</param>
    public Font(string familyName, float emSize, FontStyle style)
        : this(new FontFamily(familyName), emSize, style, GraphicsUnit.Point)
    {
    }

    /// <summary>Creates a font of a family and style in a unit.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="style">The style.</param>
    /// <param name="unit">The unit of the size.</param>
    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
        : this(new FontFamily(familyName), emSize, style, unit)
    {
    }

    /// <summary>Creates a font, as the Windows Forms designer writes it.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="style">The style.</param>
    /// <param name="unit">The unit of the size.</param>
    /// <param name="gdiCharSet">The GDI character set, kept for <see cref="GdiCharSet"/>.</param>
    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
        : this(new FontFamily(familyName), emSize, style, unit, gdiCharSet)
    {
    }

    /// <summary>Creates a font, as the Windows Forms designer writes it.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="style">The style.</param>
    /// <param name="unit">The unit of the size.</param>
    /// <param name="gdiCharSet">The GDI character set, kept for <see cref="GdiCharSet"/>.</param>
    /// <param name="gdiVerticalFont">Kept for <see cref="GdiVerticalFont"/>.</param>
    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
        : this(new FontFamily(familyName), emSize, style, unit, gdiCharSet, gdiVerticalFont)
    {
    }

    /// <summary>Creates a font of a family in a unit.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="unit">The unit of the size.</param>
    public Font(string familyName, float emSize, GraphicsUnit unit)
        : this(new FontFamily(familyName), emSize, FontStyle.Regular, unit)
    {
    }

    /// <summary>Creates a font of a family, in points.</summary>
    /// <param name="family">The family.</param>
    /// <param name="emSize">The size in points.</param>
    public Font(FontFamily family, float emSize)
        : this(family, emSize, FontStyle.Regular, GraphicsUnit.Point)
    {
    }

    /// <summary>Creates a font of a family and style, in points.</summary>
    /// <param name="family">The family.</param>
    /// <param name="emSize">The size in points.</param>
    /// <param name="style">The style.</param>
    public Font(FontFamily family, float emSize, FontStyle style)
        : this(family, emSize, style, GraphicsUnit.Point)
    {
    }

    /// <summary>Creates a font of a family in a unit.</summary>
    /// <param name="family">The family.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="unit">The unit of the size.</param>
    public Font(FontFamily family, float emSize, GraphicsUnit unit)
        : this(family, emSize, FontStyle.Regular, unit)
    {
    }

    /// <summary>Creates a font of a family and style in a unit.</summary>
    /// <param name="family">The family.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="style">The style.</param>
    /// <param name="unit">The unit of the size.</param>
    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
        : this(family, emSize, style, unit, 1)
    {
    }

    /// <summary>Creates a font with a GDI character set.</summary>
    /// <param name="family">The family.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="style">The style.</param>
    /// <param name="unit">The unit of the size.</param>
    /// <param name="gdiCharSet">The GDI character set, kept for <see cref="GdiCharSet"/>.</param>
    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
        : this(family, emSize, style, unit, gdiCharSet, gdiVerticalFont: false)
    {
    }

    /// <summary>Creates a font with a GDI character set and orientation.</summary>
    /// <param name="family">The family.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="style">The style.</param>
    /// <param name="unit">The unit of the size.</param>
    /// <param name="gdiCharSet">The GDI character set, kept for <see cref="GdiCharSet"/>.</param>
    /// <param name="gdiVerticalFont">Kept for <see cref="GdiVerticalFont"/>.</param>
    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (float.IsNaN(emSize) || float.IsInfinity(emSize) || emSize <= 0)
        {
            throw new ArgumentException($"Value of '{emSize.ToString(CultureInfo.InvariantCulture)}' is not valid for 'emSize'. 'emSize' should be greater than 0 and less than or equal to System.Single.MaxValue.", nameof(emSize));
        }

        if (unit == GraphicsUnit.Display)
        {
            throw new ArgumentException("GraphicsUnit.Display is not a valid unit for a font.", nameof(unit));
        }

        FontFamily = family;
        Size = emSize;
        Style = style;
        Unit = unit;
        GdiCharSet = gdiCharSet;
        GdiVerticalFont = gdiVerticalFont;
    }

    /// <summary>Creates a copy of a font with another style.</summary>
    /// <param name="prototype">The font to copy.</param>
    /// <param name="newStyle">The new style.</param>
    public Font(Font prototype, FontStyle newStyle)
        : this((prototype ?? throw new ArgumentNullException(nameof(prototype))).FontFamily, prototype.Size, newStyle, prototype.Unit, prototype.GdiCharSet, prototype.GdiVerticalFont)
    {
    }

    /// <summary>The family.</summary>
    public FontFamily FontFamily { get; }

    /// <summary>The family name.</summary>
    public string Name => FontFamily.Name;

    /// <summary>The name of the font when it was created, which is <see cref="Name"/>.</summary>
    public string OriginalFontName => Name;

    /// <summary>The size, in <see cref="Unit"/>.</summary>
    public float Size { get; }

    /// <summary>The size in points.</summary>
    public float SizeInPoints => Unit switch
    {
        GraphicsUnit.Pixel or GraphicsUnit.World => Size * 72f / 96f,
        GraphicsUnit.Inch => Size * 72f,
        GraphicsUnit.Document => Size * 72f / 300f,
        GraphicsUnit.Millimeter => Size * 72f / 25.4f,
        _ => Size,
    };

    /// <summary>The style.</summary>
    public FontStyle Style { get; }

    /// <summary>The unit of <see cref="Size"/>.</summary>
    public GraphicsUnit Unit { get; }

    /// <summary>Whether the font is bold.</summary>
    public bool Bold => (Style & FontStyle.Bold) != 0;

    /// <summary>Whether the font is italic.</summary>
    public bool Italic => (Style & FontStyle.Italic) != 0;

    /// <summary>Whether the font is underlined.</summary>
    public bool Underline => (Style & FontStyle.Underline) != 0;

    /// <summary>Whether the font is struck out.</summary>
    public bool Strikeout => (Style & FontStyle.Strikeout) != 0;

    /// <summary>The GDI character set the font was created with.</summary>
    public byte GdiCharSet { get; }

    /// <summary>Whether the font was created as a vertical GDI font.</summary>
    public bool GdiVerticalFont { get; }

    /// <summary>Whether the font is one of <see cref="SystemFonts"/>.</summary>
    public bool IsSystemFont => SystemFontName is not null;

    /// <summary>The name of the system font this is, such as <c>DefaultFont</c>, or <see langword="null"/>.</summary>
    public string? SystemFontName { get; internal init; }

    /// <summary>The line spacing in pixels, rounded up.</summary>
    public int Height => (int)Math.Ceiling(GetHeight());

    /// <summary>The size in device-independent pixels (1/96 inch).</summary>
    internal float SizeInPixels => SizeInPoints * 96f / 72f;

    /// <summary>The font as drawing and measuring use it.</summary>
    internal FontSpec Spec => _spec ??= new FontSpec(FontFamily.DrawingFamily, SizeInPixels, Bold, Italic, Underline, Strikeout);

    /// <summary>The line spacing in pixels.</summary>
    public float GetHeight() => (float)TextMetrics.LineHeight(Spec);

    /// <summary>The line spacing at a resolution.</summary>
    /// <param name="dpi">The resolution in dots per inch.</param>
    public float GetHeight(float dpi) => GetHeight() * dpi / 96f;

    /// <summary>The line spacing on a drawing surface.</summary>
    /// <param name="graphics">The drawing surface.</param>
    public float GetHeight(Graphics graphics) => GetHeight();

    /// <inheritdoc />
    public object Clone() => new Font(FontFamily, Size, Style, Unit, GdiCharSet, GdiVerticalFont);

    /// <inheritdoc />
    public void Dispose()
    {
        // A font holds nothing to release.
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Font other
        && other.FontFamily.Equals(FontFamily)
        && other.Size == Size
        && other.Style == Style
        && other.Unit == Unit
        && other.GdiCharSet == GdiCharSet
        && other.GdiVerticalFont == GdiVerticalFont;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(FontFamily, Size, Style, Unit);

    /// <inheritdoc />
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"[Font: Name={Name}, Size={Size}, Units={(int)Unit}, GdiCharSet={GdiCharSet}, GdiVerticalFont={GdiVerticalFont}]");
}
