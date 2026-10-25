using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TabStudio.App.Views.Notebooks;

/// <summary>Looks up the theme's brushes and fonts for views that build their content in code.</summary>
internal static class ThemeResources
{
    /// <summary>A brush from the theme, or a fallback color when the theme is not loaded (as in some tests).</summary>
    /// <param name="key">The resource key, such as <c>Brush.TextMuted</c>.</param>
    /// <param name="fallback">The color to use when the key is missing.</param>
    public static IBrush Brush(string key, string fallback) =>
        Find(key) as IBrush ?? new ImmutableSolidColorBrush(Color.Parse(fallback));

    /// <summary>The code font.</summary>
    public static FontFamily CodeFont => Find("CodeFont") as FontFamily ?? FontFamily.Default;

    private static object? Find(string key) =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value : null;
}
