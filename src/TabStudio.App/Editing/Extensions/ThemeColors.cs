using System.Globalization;
using Avalonia.Media;

namespace TabStudio.App.Editing.Extensions;

/// <summary>Reads the colors VS Code themes write (<c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>) into Avalonia colors.</summary>
public static class ThemeColors
{
    /// <summary>Reads a theme color.</summary>
    /// <param name="text">The color, with the alpha last as VS Code writes it.</param>
    /// <param name="color">The color.</param>
    /// <returns>Whether the text is a color.</returns>
    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        if (text is not { Length: > 1 } || text[0] != '#')
        {
            return false;
        }

        var hex = text[1..];
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = hex.Length == 6
            ? Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : Color.FromArgb((byte)value, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8));
        return true;
    }

    /// <summary>A theme color as a brush, or <see langword="null"/> when the text is not a color.</summary>
    /// <param name="text">The color.</param>
    public static IBrush? Brush(string? text) => TryParse(text, out var color) ? new Avalonia.Media.Immutable.ImmutableSolidColorBrush(color) : null;

    /// <summary>Mixes two colors.</summary>
    /// <param name="color">The first color.</param>
    /// <param name="other">The second color.</param>
    /// <param name="amount">How much of the second color, from 0 to 1.</param>
    public static Color Mix(Color color, Color other, double amount) => Color.FromArgb(
        (byte)Math.Round(color.A + ((other.A - color.A) * amount)),
        (byte)Math.Round(color.R + ((other.R - color.R) * amount)),
        (byte)Math.Round(color.G + ((other.G - color.G) * amount)),
        (byte)Math.Round(color.B + ((other.B - color.B) * amount)));
}
