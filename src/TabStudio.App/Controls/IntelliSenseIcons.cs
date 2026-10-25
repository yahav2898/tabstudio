using Avalonia.Media;

namespace TabStudio.App.Controls;

/// <summary>Stroke icons for IntelliSense, on the same 24 by 24 grid as the icons in <c>Theme/Icons.axaml</c>.</summary>
public static class IntelliSenseIcons
{
    /// <summary>The light bulb of quick fixes.</summary>
    public static Geometry LightBulb { get; } = Geometry.Parse(
        "M9 18 L15 18 M10 21 L14 21 M12 3 A6 6 0 0 0 8.4 13.8 C9.1 14.5 9.5 15.3 9.5 16 L14.5 16 C14.5 15.3 14.9 14.5 15.6 13.8 A6 6 0 0 0 12 3 Z");

    /// <summary>The screwdriver of refactorings.</summary>
    public static Geometry Screwdriver { get; } = Geometry.Parse(
        "M3.5 20.5 L9.5 14.5 M8 13 L11 16 L19.5 7.5 L16.5 4.5 Z M16.5 4.5 L18.5 2.5 L21.5 5.5 L19.5 7.5");
}
