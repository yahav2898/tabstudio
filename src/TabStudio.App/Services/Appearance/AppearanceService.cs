using Avalonia;
using Avalonia.Styling;

namespace TabStudio.App.Services.Appearance;

/// <summary>
/// The IDE's theme: Dark, Light or Follow the system, remembered across sessions and applied at once to the whole app
/// (views through their dynamic resources, the editors and code-drawn colors through <see cref="Theming.ThemeSync"/>).
/// </summary>
/// <remarks>Call on the UI thread.</remarks>
public sealed class AppearanceService
{
    private readonly AppearanceSettingsStore _store;

    /// <summary>Reads the saved settings.</summary>
    /// <param name="store">Where the settings are kept.</param>
    public AppearanceService(AppearanceSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        Settings = store.Load();
    }

    /// <summary>Raised after the theme changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The user's settings.</summary>
    public AppearanceSettings Settings { get; private set; }

    /// <summary>The chosen theme.</summary>
    public AppTheme Theme => Settings.Theme;

    /// <summary>Whether the IDE is light now: chosen, or followed from the system.</summary>
    public static bool IsLight => Application.Current?.ActualThemeVariant == ThemeVariant.Light;

    /// <summary>The theme's display name, as in the theme picker.</summary>
    /// <param name="theme">The theme.</param>
    public static string DisplayName(AppTheme theme) => theme switch
    {
        AppTheme.Light => "Light",
        AppTheme.System => "Follow the system",
        _ => "Dark",
    };

    /// <summary>The theme variant Avalonia is asked for: dark, light, or the platform's.</summary>
    /// <param name="theme">The theme.</param>
    public static ThemeVariant VariantOf(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.System => ThemeVariant.Default,
        _ => ThemeVariant.Dark,
    };

    /// <summary>Applies the saved theme, as the app starts.</summary>
    public void Apply() => Show(Theme);

    /// <summary>Uses a theme from now on, and remembers it.</summary>
    /// <param name="theme">The theme.</param>
    public void SetTheme(AppTheme theme)
    {
        if (theme != Theme)
        {
            Settings = Settings with { Theme = theme };
            _store.Save(Settings);
        }

        Show(theme);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a theme without remembering it, as the theme picker does while the user browses.</summary>
    /// <param name="theme">The theme to show; <see cref="Apply"/> goes back to the chosen one.</param>
    public static void Preview(AppTheme theme) => Show(theme);

    private static void Show(AppTheme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = VariantOf(theme);
        }
    }
}
