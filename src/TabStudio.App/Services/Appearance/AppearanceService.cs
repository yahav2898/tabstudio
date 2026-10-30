using Avalonia;
using Avalonia.Styling;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Services.Appearance;

/// <summary>
/// The IDE's theme (Dark, Light or Follow the system) and text size (Small to Very large, or Android's font size),
/// remembered across sessions and applied at once to the whole app: views through their dynamic resources, the editors
/// and code-drawn colors through <see cref="Theming.ThemeSync"/>, text through <see cref="TypeScale"/>.
/// </summary>
/// <remarks>Call on the UI thread.</remarks>
public sealed class AppearanceService
{
    private readonly AppearanceSettingsStore _store;

    private readonly SystemAccessibility _system;
    private bool _followsSystem;

    /// <summary>Reads the saved settings.</summary>
    /// <param name="store">Where the settings are kept.</param>
    /// <param name="system">The device's accessibility settings; <see cref="SystemAccessibility.Current"/> by default.</param>
    public AppearanceService(AppearanceSettingsStore store, SystemAccessibility? system = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _system = system ?? SystemAccessibility.Current;
        Settings = store.Load();
    }

    /// <summary>Raised after the theme or the text size changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The chosen text size.</summary>
    public TextSize TextSize => Settings.TextSize;

    /// <summary>The text size's factor now: the chosen size's, or the system's font scale.</summary>
    public double TextFactor => TextSizes.Factor(TextSize, _system.FontScale);

    /// <summary>The user's settings.</summary>
    public AppearanceSettings Settings { get; private set; }

    /// <summary>The chosen theme.</summary>
    public IdeTheme Theme => Settings.Theme;

    /// <summary>Whether the IDE is light now: chosen, or followed from the system.</summary>
    public static bool IsLight => Application.Current?.ActualThemeVariant == ThemeVariant.Light;

    /// <summary>The theme's display name, as in the theme picker.</summary>
    /// <param name="theme">The theme.</param>
    public static string DisplayName(IdeTheme theme) => theme switch
    {
        IdeTheme.Light => "Light",
        IdeTheme.System => "Follow the system",
        _ => "Dark",
    };

    /// <summary>The theme variant Avalonia is asked for: dark, light, or the platform's.</summary>
    /// <param name="theme">The theme.</param>
    public static ThemeVariant VariantOf(IdeTheme theme) => theme switch
    {
        IdeTheme.Light => ThemeVariant.Light,
        IdeTheme.System => ThemeVariant.Default,
        _ => ThemeVariant.Dark,
    };

    /// <summary>Applies the saved theme and text size, as the app starts, and follows the system's font scale from then on.</summary>
    public void Apply()
    {
        Show(Theme);
        TypeScale.Apply(TextFactor);
        if (!_followsSystem)
        {
            _followsSystem = true;
            _system.Changed += OnSystemChanged;
        }
    }

    /// <summary>Uses a text size from now on, and remembers it. The whole IDE and its editors change at once.</summary>
    /// <param name="size">The size.</param>
    public void SetTextSize(TextSize size)
    {
        if (size != TextSize)
        {
            Settings = Settings with { TextSize = size };
            _store.Save(Settings);
        }

        TypeScale.Apply(TextFactor);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Steps the text size up or down from the one in use, as Make Text Larger and Smaller do.</summary>
    /// <param name="larger">Whether to make the text larger.</param>
    public void StepTextSize(bool larger) => SetTextSize(TextSizes.Step(TextFactor, larger));

    private void OnSystemChanged(object? sender, EventArgs e)
    {
        if (TextSize == TextSize.System)
        {
            TypeScale.Apply(TextFactor);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Uses a theme from now on, and remembers it.</summary>
    /// <param name="theme">The theme.</param>
    public void SetTheme(IdeTheme theme)
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
    public static void Preview(IdeTheme theme) => Show(theme);

    private static void Show(IdeTheme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = VariantOf(theme);
        }
    }
}
