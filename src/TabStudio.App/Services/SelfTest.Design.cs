using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's design part, with the device's own fonts and theme setting (docs/design/README.md): single lines of
/// text in a fixed height are centered in it, in the IDE and on the Start screen (on a real tablet, 0.8's template tags
/// sat at the top of their pills, which the desktop's fonts hid); the light theme recolors the views, the code-drawn
/// brushes and the editor at once; and Follow the system takes the device's dark theme setting.
/// </summary>
public static partial class SelfTest
{
    /// <summary>How much taller than its text a single line may be before the text visibly sits high.</summary>
    private const double StretchTolerance = 1.5;

    /// <summary>How long a theme may take to reach every view and brush.</summary>
    private static readonly TimeSpan ThemeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Looks at the IDE as the Windows Forms part left it (the designer, its Toolbox and its Properties), then at the
    /// Start screen, each in the light and the dark theme, and then follows the system; saves a picture of each look in
    /// <paramref name="screenshots"/> when it is given. Leaves the Start screen shown, in the saved theme.
    /// </summary>
    /// <returns>Whether every line of text was centered and every theme applied, with a one-line summary.</returns>
    private static async Task<SelfTestResult> CheckDesignAsync(MainViewModel main, string? screenshots, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        SelfTestResult Fail(string message) => new(false, $"Design: {message}", clock.Elapsed);
        if (Application.Current is not { } app || RootView(app) is not { } root || main.Workspace is not { } workspace)
        {
            return Fail("the IDE is not shown.");
        }

        var lines = 0;
        var stretched = new List<string>();
        async Task<bool> LookAsync(string name, IdeTheme theme)
        {
            var variant = AppearanceService.VariantOf(theme);
            var window = ThemeBrushes.ColorOf("Brush.Window", variant);
            AppearanceService.Preview(theme);
            if (!await WaitForAsync(() => IsShown(app, variant, window), ThemeTimeout, cancellationToken))
            {
                return false;
            }

            // A frame for the views to take the theme's resources and lay out again.
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            var look = FindStretchedText(root);
            lines += look.Lines;
            stretched.AddRange(look.Stretched.Select(text => $"{name}: {text}"));
            if (screenshots is not null && TopLevel.GetTopLevel(root) is { } top)
            {
                Directory.CreateDirectory(screenshots);
                Capture(top, Path.Combine(screenshots, name + ".png"));
            }

            return true;
        }

        try
        {
            if (!await LookAsync("design-01-forms-designer-dark", IdeTheme.Dark) || !await LookAsync("design-02-forms-designer-light", IdeTheme.Light))
            {
                return Fail($"the theme did not reach the IDE: it is {app.ActualThemeVariant}, the editor's {ThemeBrushes.EditorVariant}.");
            }

            await workspace.CloseWorkspaceCommand.ExecuteAsync(null);
            if (!await WaitForAsync(() => main.CurrentPage is StartViewModel && root.GetVisualDescendants().OfType<StartView>().Any(view => view.IsEffectivelyVisible), ThemeTimeout, cancellationToken))
            {
                return Fail("the Start screen did not show when the solution closed.");
            }

            if (!await LookAsync("design-03-start-light", IdeTheme.Light) || !await LookAsync("design-04-start-dark", IdeTheme.Dark))
            {
                return Fail($"the theme did not reach the Start screen: it is {app.ActualThemeVariant}.");
            }

            var system = app.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
            AppearanceService.Preview(IdeTheme.System);
            if (!await WaitForAsync(() => IsShown(app, system, ThemeBrushes.ColorOf("Brush.Window", system)), ThemeTimeout, cancellationToken))
            {
                return Fail($"Follow the system shows {app.ActualThemeVariant}, the device's setting is {system}.");
            }

            if (stretched.Count > 0)
            {
                return Fail($"{stretched.Count} of {lines} lines of text sit high in their height: {string.Join("; ", stretched.Distinct().Take(6))}");
            }

            return new SelfTestResult(
                true,
                string.Create(CultureInfo.InvariantCulture, $"Design: {lines} lines of text centered in the Forms designer and on the Start screen, in the light and dark themes; Follow the system is {system}"),
                clock.Elapsed);
        }
        finally
        {
            // Back to the theme the user chose, which the self test does not change.
            if ((app as TabStudioApplication)?.Services?.GetService<AppearanceService>() is { } appearance)
            {
                appearance.Apply();
            }
            else
            {
                AppearanceService.Preview(IdeTheme.Dark);
            }
        }
    }

    /// <summary>Whether a theme reached the app, its code-drawn brushes and the editor.</summary>
    private static bool IsShown(Application app, ThemeVariant variant, Color window) =>
        app.ActualThemeVariant == variant
        && ThemeBrushes.Get("Brush.Window") is ISolidColorBrush brush && brush.Color == window
        && ThemeBrushes.EditorVariant == variant;

    /// <summary>The app's root view: the single view on a tablet, the main window on a desktop.</summary>
    private static Control? RootView(Application app) => app.ApplicationLifetime switch
    {
        ISingleViewApplicationLifetime single => single.MainView,
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
        _ => null,
    };

    /// <summary>
    /// The single lines of text under <paramref name="root"/> that are stretched to a height taller than their text,
    /// and so drawn at its top, rather than centered in it; and how many single lines were looked at.
    /// </summary>
    private static (int Lines, List<string> Stretched) FindStretchedText(Visual root)
    {
        var lines = 0;
        var stretched = new List<string>();
        foreach (var text in root.GetVisualDescendants().OfType<TextBlock>())
        {
            var hasText = !string.IsNullOrEmpty(text.Text) || text.Inlines is { Count: > 0 };
            if (!text.IsEffectivelyVisible || !hasText || text.TextWrapping != TextWrapping.NoWrap || text.MaxLines > 1)
            {
                continue;
            }

            lines++;
            var extra = text.Bounds.Height - (text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom);
            if (text.VerticalAlignment == VerticalAlignment.Stretch && extra > StretchTolerance)
            {
                var parent = text.GetVisualParent() is { } visual ? visual.GetType().Name : "nothing";
                stretched.Add(string.Create(CultureInfo.InvariantCulture, $"\"{text.Text}\" in a {parent}, {extra:0.#} px too tall"));
            }
        }

        return (lines, stretched);
    }
}
