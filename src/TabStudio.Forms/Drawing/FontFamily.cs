using System.Drawing.Text;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>A group of typefaces with a similar design, such as Segoe UI.</summary>
/// <remarks>
/// Any family name is accepted. A family the device does not have is drawn with the device's own family of the same
/// kind (sans-serif, serif or monospace); <see cref="Name"/> keeps the name the app asked for.
/// </remarks>
public sealed class FontFamily : MarshalByRefObject, IDisposable
{
    /// <summary>Creates a family by name.</summary>
    /// <param name="name">The family name, such as <c>Segoe UI</c>.</param>
    public FontFamily(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Creates a generic family.</summary>
    /// <param name="genericFamily">The kind of family.</param>
    public FontFamily(GenericFontFamilies genericFamily) => Name = genericFamily switch
    {
        GenericFontFamilies.Serif => "Times New Roman",
        GenericFontFamilies.Monospace => "Courier New",
        _ => "Microsoft Sans Serif",
    };

    /// <summary>A generic sans-serif family.</summary>
    public static FontFamily GenericSansSerif => new(GenericFontFamilies.SansSerif);

    /// <summary>A generic serif family.</summary>
    public static FontFamily GenericSerif => new(GenericFontFamilies.Serif);

    /// <summary>A generic monospace family.</summary>
    public static FontFamily GenericMonospace => new(GenericFontFamilies.Monospace);

    /// <summary>The families installed on the device.</summary>
    public static FontFamily[] Families
    {
        get
        {
            try
            {
                return [.. Avalonia.Media.FontManager.Current.SystemFonts.Select(f => new FontFamily(f.Name)).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
            }
            catch (InvalidOperationException)
            {
                return [GenericSansSerif, GenericSerif, GenericMonospace];
            }
        }
    }

    /// <summary>The family name.</summary>
    public string Name { get; }

    /// <summary>The Avalonia family the text is drawn with.</summary>
    internal string DrawingFamily => FontSpec.ResolveFamily(Name);

    /// <summary>Whether a style is available: every style is, simulated where the family lacks it.</summary>
    /// <param name="style">The style.</param>
    public bool IsStyleAvailable(FontStyle style) => true;

    /// <summary>The height of the em square in design units.</summary>
    /// <param name="style">The style.</param>
    public int GetEmHeight(FontStyle style) => 2048;

    /// <summary>The ascent in design units.</summary>
    /// <param name="style">The style.</param>
    public int GetCellAscent(FontStyle style) => 1984;

    /// <summary>The descent in design units.</summary>
    /// <param name="style">The style.</param>
    public int GetCellDescent(FontStyle style) => 496;

    /// <summary>The distance between two lines in design units.</summary>
    /// <param name="style">The style.</param>
    public int GetLineSpacing(FontStyle style) => 2480;

    /// <summary>The family name in a language.</summary>
    /// <param name="language">The language identifier; the name is the same in every language.</param>
    public string GetName(int language) => Name;

    /// <inheritdoc />
    public void Dispose()
    {
        // A family holds nothing to release.
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FontFamily other && string.Equals(other.Name, Name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <inheritdoc />
    public override string ToString() => $"[FontFamily: Name={Name}]";
}
