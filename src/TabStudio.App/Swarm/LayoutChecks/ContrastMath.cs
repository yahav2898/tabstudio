using Avalonia.Media;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// The contrast between text and its background, as WCAG 2.2 defines it: the relative luminance of each sRGB color,
/// and their ratio, from 1:1 for the same color to 21:1 for black on white.
/// </summary>
public static class ContrastMath
{
    /// <summary>The contrast normal text needs for WCAG AA.</summary>
    public const double NormalTextMinimum = 4.5;

    /// <summary>The contrast large text needs for WCAG AA.</summary>
    public const double LargeTextMinimum = 3;

    /// <summary>The size from which text is large, 18 points, in device-independent pixels.</summary>
    public const double LargeTextSize = 24;

    /// <summary>The size from which bold text is large, 14 points, in device-independent pixels.</summary>
    public const double LargeBoldTextSize = 18.66;

    /// <summary>The relative luminance of a color, from 0 for black to 1 for white; its alpha is ignored.</summary>
    /// <param name="color">The color.</param>
    public static double RelativeLuminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    /// <summary>The contrast ratio of two opaque colors, from 1 to 21, whichever is lighter.</summary>
    /// <param name="first">One color.</param>
    /// <param name="second">The other.</param>
    public static double Ratio(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>A color drawn over another: the opaque color a person sees.</summary>
    /// <param name="top">The color drawn on top, with its alpha.</param>
    /// <param name="bottom">The opaque color under it.</param>
    public static Color Over(Color top, Color bottom)
    {
        var alpha = top.A / 255.0;
        return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));

        byte Mix(byte over, byte under) => (byte)Math.Round((over * alpha) + (under * (1 - alpha)));
    }

    /// <summary>A color with its alpha multiplied by an opacity, such as a brush's or an element's.</summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity, from 0 to 1.</param>
    public static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    /// <summary>Whether text counts as large for WCAG: 18 points, or 14 points when bold.</summary>
    /// <param name="fontSize">Its size, in device-independent pixels.</param>
    /// <param name="weight">Its weight.</param>
    public static bool IsLargeText(double fontSize, FontWeight weight) =>
        fontSize >= LargeTextSize || (fontSize >= LargeBoldTextSize && weight >= FontWeight.Bold);

    /// <summary>The contrast text needs for WCAG AA.</summary>
    /// <param name="isLargeText">Whether it is large text.</param>
    public static double Minimum(bool isLargeText) => isLargeText ? LargeTextMinimum : NormalTextMinimum;

    /// <summary>One sRGB channel, linearized.</summary>
    private static double Linear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
