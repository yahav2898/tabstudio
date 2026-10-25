using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TabStudio.App.Theming;

/// <summary>
/// The palette's brushes for code that draws or converts colors itself: editor margins and renderers, converters and
/// view models. Views use <c>{DynamicResource Brush.…}</c> instead. Every brush comes from Colors.axaml by its token
/// key, and changes color in place when the theme changes, so text and shapes drawn with it earlier follow at once.
/// </summary>
/// <remarks>
/// There are two sets: the app's (<see cref="Get"/>), for the IDE's chrome, and the editors' (<see cref="Editor"/>),
/// for what is drawn on code. They differ when an extension's color theme gives the editor a dark background in a
/// light IDE, or the other way around. Call on the UI thread; elsewhere the methods return a fixed snapshot.
/// </remarks>
public static class ThemeBrushes
{
    private static readonly Dictionary<string, SolidColorBrush> AppBrushes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SolidColorBrush> EditorBrushes = new(StringComparer.Ordinal);
    private static ThemeVariant? _editorVariant;

    /// <summary>Raised on the UI thread after the brushes changed color, when the app's or the editors' theme changed.</summary>
    public static event EventHandler? Changed;

    /// <summary>The app's theme now: <see cref="ThemeVariant.Dark"/> or <see cref="ThemeVariant.Light"/>.</summary>
    public static ThemeVariant AppVariant =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark;

    /// <summary>
    /// The editors' theme: that of the color theme they use (an extension's, or TabStudio's light one), or the app's
    /// while they use TabStudio's dark colors.
    /// </summary>
    public static ThemeVariant EditorVariant => _editorVariant ?? AppVariant;

    /// <summary>A brush of the app's theme, such as <c>Brush.Error</c>; it changes color with the theme.</summary>
    /// <param name="key">The token's resource key.</param>
    public static IBrush Get(string key) => Find(AppBrushes, key, AppVariant);

    /// <summary>A brush of the editors' theme, such as <c>Editor.ErrorSquiggle</c>; it changes color with the editors' theme.</summary>
    /// <param name="key">The token's resource key.</param>
    public static IBrush Editor(string key) => Find(EditorBrushes, key, EditorVariant);

    /// <summary>
    /// A resource of the app's theme, for converters: a brush token as a brush that follows the theme (see
    /// <see cref="Get"/>), anything else (icons, fonts) as it is; <see langword="null"/> when there is no such resource.
    /// </summary>
    /// <typeparam name="T">The resource's type, such as <see cref="IBrush"/> or <see cref="Geometry"/>.</typeparam>
    /// <param name="key">The resource key.</param>
    public static T? Find<T>(string key)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (Application.Current?.TryGetResource(key, AppVariant, out var value) != true)
        {
            return null;
        }

        return value is ISolidColorBrush && typeof(T).IsAssignableFrom(typeof(SolidColorBrush)) ? Get(key) as T : value as T;
    }

    /// <summary>A token's color in a theme, or transparent when the palette has no such token.</summary>
    /// <param name="key">The token's resource key.</param>
    /// <param name="variant">The theme.</param>
    public static Color ColorOf(string key, ThemeVariant variant)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(variant);
        object? value = null;
        if (Application.Current?.TryGetResource(key, variant, out value) != true)
        {
            return Colors.Transparent;
        }

        return value switch
        {
            ISolidColorBrush brush => brush.Color,
            Color color => color,
            _ => Colors.Transparent,
        };
    }

    /// <summary>
    /// Sets the editors' theme from the color theme they use; <see langword="null"/> follows the app's. Recolors the
    /// editors' brushes when it changes.
    /// </summary>
    /// <param name="variant">Dark or light, or <see langword="null"/>.</param>
    public static void SetEditorVariant(ThemeVariant? variant)
    {
        if (_editorVariant == variant)
        {
            return;
        }

        _editorVariant = variant;
        Refresh();
    }

    /// <summary>Recolors every brush for the current themes. Called when the app's theme changes.</summary>
    public static void Refresh()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Refresh);
            return;
        }

        Recolor(AppBrushes, AppVariant);
        Recolor(EditorBrushes, EditorVariant);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static IBrush Find(Dictionary<string, SolidColorBrush> brushes, string key, ThemeVariant variant)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return new ImmutableSolidColorBrush(ColorOf(key, variant));
        }

        if (!brushes.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(ColorOf(key, variant));
            brushes[key] = brush;
        }

        return brush;
    }

    private static void Recolor(Dictionary<string, SolidColorBrush> brushes, ThemeVariant variant)
    {
        foreach (var (key, brush) in brushes)
        {
            brush.Color = ColorOf(key, variant);
        }
    }
}
