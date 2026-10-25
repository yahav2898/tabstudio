using TabStudio.Compilation.Wpf.Xaml;

namespace TabStudio.Wpf.Building;

/// <summary>
/// What a WPF window's XAML sets that OpenSilver's <c>Window</c> may not have, and what TabStudio does with it in the
/// web preview, where the app's window fills the page: the background is painted behind the window's content, and
/// the settings that place or size a desktop window have nothing to act on.
/// </summary>
internal static class WindowXamlSettings
{
    /// <summary>The window's background, which TabStudio paints behind its content.</summary>
    public const string Background = "Background";

    /// <summary>The settings of a desktop window that mean nothing in a page, each with why.</summary>
    public static IReadOnlyDictionary<string, string> WithoutEffect { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["WindowStartupLocation"] = "the window fills the web preview, so it has no place to start at",
        ["ResizeMode"] = "the window fills the web preview and is sized with it",
        ["SizeToContent"] = "the window fills the web preview and is sized with it",
        ["WindowState"] = "the window fills the web preview, as if maximized",
        ["WindowStyle"] = "TabStudio draws the window's title bar itself",
        ["AllowsTransparency"] = "the window is drawn in the page",
        ["Topmost"] = "the window is the only one in the web preview",
        ["ShowInTaskbar"] = "the web preview has no taskbar",
        ["ShowActivated"] = "the window is always active in the web preview",
        ["Icon"] = "TabStudio draws the window's title bar itself",
        ["Left"] = "the window fills the web preview",
        ["Top"] = "the window fills the web preview",
    };

    /// <summary>The settings OpenSilver's <c>Window</c> lacks that its content shows in WPF, which go on the controls instead.</summary>
    public static IReadOnlySet<string> OnContent { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Foreground", "FontFamily", "FontSize", "FontWeight", "FontStyle", "FontStretch", "Padding", "BorderBrush", "BorderThickness",
        "HorizontalContentAlignment", "VerticalContentAlignment",
    };

    /// <summary>The background a window's XAML sets as a color the page can paint, such as <c>#FFF0F0F0</c> or <c>Beige</c>.</summary>
    /// <param name="root">The window's root element.</param>
    /// <returns>A CSS color, or <see langword="null"/> when there is none or it is not a plain color.</returns>
    public static string? BackgroundColor(XamlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root.Attribute(Background)?.Value.Trim() is { Length: > 0 } value ? CssColor(value) : null;
    }

    /// <summary>A XAML color as CSS: named colors as they are, <c>#AARRGGBB</c> as <c>rgba()</c>.</summary>
    internal static string? CssColor(string value)
    {
        if (value.StartsWith('{'))
        {
            return null;
        }

        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (!hex.All(Uri.IsHexDigit))
            {
                return null;
            }

            return hex.Length switch
            {
                3 or 6 => "#" + hex,
                8 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"rgba({Convert.ToInt32(hex[2..4], 16)}, {Convert.ToInt32(hex[4..6], 16)}, {Convert.ToInt32(hex[6..8], 16)}, {Convert.ToInt32(hex[..2], 16) / 255.0:0.###})"),
                4 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"rgba({Convert.ToInt32(new string(hex[1], 2), 16)}, {Convert.ToInt32(new string(hex[2], 2), 16)}, {Convert.ToInt32(new string(hex[3], 2), 16)}, {Convert.ToInt32(new string(hex[0], 2), 16) / 255.0:0.###})"),
                _ => null,
            };
        }

        return value.All(char.IsAsciiLetter) ? value : null;
    }
}
