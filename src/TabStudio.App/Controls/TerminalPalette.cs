using Avalonia.Media;
using Avalonia.Styling;
using TabStudio.App.Theming;
using TabStudio.Toolchains.Terminals;

namespace TabStudio.App.Controls;

/// <summary>
/// The Terminal's colors in one theme, from the palette's <c>Brush.Terminal.*</c> and <c>Terminal.Ansi*</c> tokens: its
/// own background, text, cursor, selection and scroll thumb, and the 16 ANSI colors programs ask for by number, chosen
/// to read on that background. The rest of xterm's 256 colors and 24-bit colors are drawn as the program asks.
/// </summary>
/// <param name="Foreground">The default text color, as <c>0xRRGGBB</c>.</param>
/// <param name="Background">The default background.</param>
/// <param name="Cursor">The cursor.</param>
/// <param name="Selection">The selection's background.</param>
/// <param name="ScrollThumb">The scroll position marker.</param>
/// <param name="Ansi">The 16 ANSI colors: black, red, green, yellow, blue, magenta, cyan and white, then their bright forms.</param>
internal sealed record TerminalPalette(uint Foreground, uint Background, uint Cursor, uint Selection, uint ScrollThumb, IReadOnlyList<uint> Ansi)
{
    private const int AnsiCount = 16;

    private static readonly Dictionary<ThemeVariant, TerminalPalette> Palettes = [];

    /// <summary>The palette of a theme, read from the app's resources once.</summary>
    /// <param name="variant">The theme the Terminal is drawn in: light, or dark for any other.</param>
    public static TerminalPalette For(ThemeVariant variant)
    {
        var key = variant == ThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        if (!Palettes.TryGetValue(key, out var palette))
        {
            palette = new TerminalPalette(
                Rgb("Brush.Terminal.Foreground", key),
                Rgb("Brush.Terminal.Background", key),
                Rgb("Brush.Terminal.Cursor", key),
                Rgb("Brush.Terminal.Selection", key),
                Rgb("Brush.Terminal.ScrollThumb", key),
                [.. Enumerable.Range(0, AnsiCount).Select(index => Rgb("Terminal.Ansi" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), key))]);
            Palettes[key] = palette;
        }

        return palette;
    }

    /// <summary>A cell's color as drawn: the palette's own for the 16 ANSI colors, xterm's for the others.</summary>
    /// <param name="color">The color the program set.</param>
    /// <param name="fallback">The color for the default, as <c>0xRRGGBB</c>.</param>
    public uint Resolve(TerminalColor color, uint fallback) =>
        color.Kind == TerminalColorKind.Indexed && color.Value < AnsiCount ? Ansi[(int)color.Value] : color.ToRgb(fallback);

    private static uint Rgb(string token, ThemeVariant variant)
    {
        var color = ThemeBrushes.ColorOf(token, variant);
        return ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }
}
