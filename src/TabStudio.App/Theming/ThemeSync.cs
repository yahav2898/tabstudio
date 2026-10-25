using Avalonia;
using Avalonia.Styling;
using TabStudio.App.Editing;
using TabStudio.App.Editing.Extensions;

namespace TabStudio.App.Theming;

/// <summary>
/// Keeps what code draws in step with the app's theme: recolors <see cref="ThemeBrushes"/> and gives the editors
/// TabStudio's light colors while the IDE is light. Views follow by themselves, through their dynamic resources.
/// </summary>
public static class ThemeSync
{
    private static Application? _application;

    /// <summary>Follows an application's theme from now on. Call once, on the UI thread, when the app starts.</summary>
    /// <param name="application">The application.</param>
    public static void Start(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (ReferenceEquals(_application, application))
        {
            return;
        }

        if (_application is not null)
        {
            _application.ActualThemeVariantChanged -= OnThemeChanged;
        }

        _application = application;
        application.ActualThemeVariantChanged += OnThemeChanged;
        Sync();
    }

    private static void OnThemeChanged(object? sender, EventArgs e) => Sync();

    private static void Sync()
    {
        EditorExtensions.UseBuiltInTheme(ThemeBrushes.AppVariant == ThemeVariant.Light ? BuiltInEditorThemes.Light : null);
        ThemeBrushes.Refresh();
    }
}
