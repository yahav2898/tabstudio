using Avalonia;
using Avalonia.Media;
using TabStudio.App.Theming;

namespace TabStudio.App.Views.Notebooks;

/// <summary>Looks up the theme's brushes and fonts for views that build their content in code.</summary>
internal static class ThemeResources
{
    /// <summary>A brush from the theme, which changes color with it.</summary>
    /// <param name="key">The resource key, such as <c>Brush.TextMuted</c>.</param>
    public static IBrush Brush(string key) => ThemeBrushes.Get(key);

    /// <summary>The code font.</summary>
    public static FontFamily CodeFont => Find("CodeFont") as FontFamily ?? FontFamily.Default;

    private static object? Find(string key) =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value : null;
}
