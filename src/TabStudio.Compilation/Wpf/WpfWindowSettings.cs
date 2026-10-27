using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Wpf;

/// <summary>
/// What a WPF window's XAML sets that OpenSilver's <c>Window</c> may not have, and what TabStudio does with it in the
/// web preview, where the page shows the app's window whole (Fit), at its size (100%) or filling the pane (Fill): the
/// background is painted behind the window's content, and the settings that place or size a desktop window have
/// nothing to act on.
/// </summary>
public static class WpfWindowSettings
{
    /// <summary>The window's background, which TabStudio paints behind its content.</summary>
    public const string Background = "Background";

    /// <summary>The settings of a desktop window that mean nothing in a page, each with why.</summary>
    public static IReadOnlyDictionary<string, string> WithoutEffect { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["WindowStartupLocation"] = "the web preview centers the window, or makes it fill the pane in Fill",
        ["ResizeMode"] = "the web preview sizes the window: its Width and Height in Fit and 100%, the pane's size in Fill",
        ["SizeToContent"] = "the web preview sizes the window: its Width and Height in Fit and 100%, the pane's size in Fill",
        ["WindowState"] = "the web preview shows the window whole (Fit), at its size (100%) or as if maximized (Fill)",
        ["WindowStyle"] = "TabStudio draws the window's title bar itself",
        ["AllowsTransparency"] = "the window is drawn in the page",
        ["Topmost"] = "the window is the only one in the web preview",
        ["ShowInTaskbar"] = "the web preview has no taskbar",
        ["ShowActivated"] = "the window is always active in the web preview",
        ["Icon"] = "TabStudio draws the window's title bar itself",
        ["Left"] = "the web preview places the window",
        ["Top"] = "the web preview places the window",
    };

    /// <summary>The settings OpenSilver's <c>Window</c> lacks that its content shows in WPF, which go on the controls instead.</summary>
    public static IReadOnlySet<string> OnContent { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Foreground", "FontFamily", "FontSize", "FontWeight", "FontStyle", "FontStretch", "Padding", "BorderBrush", "BorderThickness",
        "HorizontalContentAlignment", "VerticalContentAlignment",
    };

    /// <summary>
    /// What happens in the web preview to an attribute of a window's root element that OpenSilver's <c>Window</c> does
    /// not have, as the Error List says it; <see langword="null"/> for one TabStudio does nothing about.
    /// </summary>
    /// <param name="attribute">The attribute's name, such as <c>WindowStartupLocation</c>.</param>
    /// <param name="root">The window's root element, whose background TabStudio paints when it is a plain color.</param>
    public static (DiagnosticSeverityLevel Severity, string Message)? Note(string attribute, XamlElement root)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(root);
        if (attribute == Background)
        {
            return (DiagnosticSeverityLevel.Info, BackgroundColor(root) is not null
                ? "OpenSilver's Window has no Background, so TabStudio paints it behind the window's content."
                : "OpenSilver's Window has no Background, and TabStudio paints only a plain color behind the window's content: set the brush on the window's root panel instead.");
        }

        if (WithoutEffect.TryGetValue(attribute, out var why))
        {
            return (DiagnosticSeverityLevel.Info, $"{attribute} has no effect in the web preview: {why}.");
        }

        return OnContent.Contains(attribute)
            ? (DiagnosticSeverityLevel.Warning, $"OpenSilver's Window has no {attribute}, so the window's content does not get it: set it on the controls, or on a style for them.")
            : null;
    }

    /// <summary>The background a window's XAML sets as a color the page can paint, such as <c>#FFF0F0F0</c> or <c>Beige</c>.</summary>
    /// <param name="root">The window's root element.</param>
    /// <returns>A CSS color, or <see langword="null"/> when there is none or it is not a plain color.</returns>
    public static string? BackgroundColor(XamlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root.Attribute(Background)?.Value.Trim() is { Length: > 0 } value ? CssColor(value) : null;
    }

    /// <summary>A XAML color as CSS: named colors as they are, <c>#AARRGGBB</c> as <c>rgba()</c>.</summary>
    /// <param name="value">The color as XAML writes it.</param>
    /// <returns>The CSS color, or <see langword="null"/> for a value that is not a plain color.</returns>
    public static string? CssColor(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
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
