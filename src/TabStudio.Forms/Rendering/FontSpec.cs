using System.Collections.Concurrent;
using Avalonia.Media;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// A font resolved for drawing: the family Avalonia draws it with, its size in device-independent pixels (1/96 inch),
/// and its style. Measuring and drawing use the same spec, so text is laid out where it is drawn.
/// </summary>
/// <param name="Family">The Avalonia family name.</param>
/// <param name="Size">The em size in device-independent pixels.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="Underline">Whether the text is underlined.</param>
/// <param name="Strikeout">Whether the text is struck out.</param>
/// <param name="LineHeight">The height of a line, or NaN for the family's own: a substituted Windows font keeps its line height.</param>
internal sealed record FontSpec(string Family, double Size, bool Bold, bool Italic, bool Underline, bool Strikeout, double LineHeight = double.NaN)
{
    private static readonly ConcurrentDictionary<string, string> ResolvedFamilies = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> MonospaceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Consolas", "Courier New", "Courier", "Lucida Console", "Lucida Sans Typewriter", "Cascadia Code", "Cascadia Mono",
        "Fixedsys", "Terminal", "Monospace", "Monospaced",
    };

    private static readonly HashSet<string> SerifNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Times New Roman", "Times", "Georgia", "Cambria", "Garamond", "Book Antiqua", "Palatino Linotype", "Constantia", "Serif",
    };

    /// <summary>The Avalonia typeface of this spec.</summary>
    public Typeface Typeface => new(
        new FontFamily(Family),
        Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
        Bold ? FontWeight.Bold : FontWeight.Normal);

    /// <summary>The text decorations of this spec, or <see langword="null"/>.</summary>
    public TextDecorationCollection? Decorations => (Underline, Strikeout) switch
    {
        (true, true) => [.. TextDecorations.Underline, .. TextDecorations.Strikethrough],
        (true, false) => TextDecorations.Underline,
        (false, true) => TextDecorations.Strikethrough,
        _ => null,
    };

    /// <summary>
    /// The family to draw a Windows font family with: the family itself when the device has it, else the device's
    /// own sans-serif, serif or monospace family, chosen by the kind of font asked for.
    /// </summary>
    /// <param name="name">The family name, such as <c>Segoe UI</c>.</param>
    public static string ResolveFamily(string name) => ResolvedFamilies.GetOrAdd(name, static requested =>
    {
        if (IsInstalled(requested))
        {
            return requested;
        }

        if (MonospaceNames.Contains(requested))
        {
            return TabStudio.Forms.FormsFonts.MonospaceFamily;
        }

        return SerifNames.Contains(requested) ? TabStudio.Forms.FormsFonts.SerifFamily : FontFamily.DefaultFontFamilyName;
    });

    /// <summary>Forgets the resolved families, after <see cref="TabStudio.Forms.FormsFonts"/> changed.</summary>
    public static void ClearResolvedFamilies() => ResolvedFamilies.Clear();

    private static bool IsInstalled(string family)
    {
        try
        {
            return FontManager.Current.SystemFonts.TryGetGlyphTypeface(family, Avalonia.Media.FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var typeface)
                && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            // No font manager: Avalonia is not initialized, as in tests of pure layout.
            return false;
        }
    }
}
