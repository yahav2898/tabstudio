using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TabStudio.App.Previews;

/// <summary>
/// An app's theme around its views inside TabStudio: the theme variant, styles, data templates and resources of the
/// app's <c>App</c>, and a background like the window an app's views normally sit on.
/// </summary>
/// <remarks>
/// They are moved from the app's <c>Application</c>, which TabStudio never makes current, into a
/// <see cref="ThemeVariantScope"/>, so they apply to the app's views and never to the IDE around them. The IDE's
/// own styles of built-in controls are undone inside it first (<c>AppPreviewStyles.axaml</c>).
/// </remarks>
internal sealed class AppTheme
{
    /// <summary>Styles that undo TabStudio's own sizes of built-in controls inside an app's views.</summary>
    private static readonly Uri PreviewStyles = new("avares://TabStudio.App/Previews/AppPreviewStyles.axaml");

    private readonly Border _background = new();

    /// <summary>Takes the theme of an app.</summary>
    /// <param name="app">The app's started <c>App</c>, or <see langword="null"/> for Avalonia's defaults.</param>
    public AppTheme(Application? app)
    {
        Root = new ThemeVariantScope { RequestedThemeVariant = app?.RequestedThemeVariant ?? ThemeVariant.Default };
        Root.Styles.Add((IStyle)AvaloniaXamlLoader.Load(PreviewStyles));
        if (app is not null)
        {
            foreach (var style in app.Styles.ToList())
            {
                app.Styles.Remove(style);
                Root.Styles.Add(style);
            }

            foreach (var template in app.DataTemplates.ToList())
            {
                app.DataTemplates.Remove(template);
                Root.DataTemplates.Add(template);
            }

            Root.Resources = MoveResources(app.Resources);
        }

        // The IDE's text size and touch targets are the IDE's: an app's views get Fluent's own sizes back.
        Accessibility.AccessibilityFeatures.RestoreFluentDefaults(Root.Resources);

        // Apps expect a window behind their views; this panel plays that part in the app's own theme, with the
        // window's text color, so an app's light theme in TabStudio's dark window still has dark text.
        _background.Bind(Border.BackgroundProperty, _background.GetResourceObservable("SystemControlBackgroundAltHighBrush"));
        _background.Bind(TextElement.ForegroundProperty, _background.GetResourceObservable("SystemControlForegroundBaseHighBrush"));
        Root.Child = _background;
    }

    /// <summary>The control to put in the IDE: the theme, with the view inside.</summary>
    public ThemeVariantScope Root { get; }

    /// <summary>The app's view shown in the theme.</summary>
    public Control? Content
    {
        get => _background.Child;
        set => _background.Child = value;
    }

    /// <summary>The app's view inside a theme's root, as <see cref="Root"/> holds it.</summary>
    /// <param name="root">A theme's root.</param>
    /// <returns>The view, or <see langword="null"/> when the root is not a theme's or holds none.</returns>
    public static Control? ContentOf(Control root) => root is ThemeVariantScope { Child: Border { Child: { } content } } ? content : null;

    private static ResourceDictionary MoveResources(IResourceDictionary from)
    {
        var to = new ResourceDictionary();
        foreach (var (key, value) in from)
        {
            to[key] = value;
        }

        foreach (var merged in from.MergedDictionaries.ToList())
        {
            from.MergedDictionaries.Remove(merged);
            to.MergedDictionaries.Add(merged);
        }

        foreach (var (variant, dictionary) in from.ThemeDictionaries.ToList())
        {
            from.ThemeDictionaries.Remove(variant);
            to.ThemeDictionaries[variant] = dictionary;
        }

        return to;
    }
}
