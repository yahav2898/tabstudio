using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using TabStudio.App.Editing;
using TabStudio.App.Editing.Extensions;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Theming;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Theming;

/// <summary>The IDE's theme: chosen, remembered, applied at once to views, code-drawn colors and the editor.</summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class AppearanceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [AvaloniaFact]
    public void The_light_theme_recolors_the_palette_the_editor_and_code_drawn_brushes_at_once()
    {
        var appearance = new AppearanceService(new AppearanceSettingsStore(new AppPaths(_temp.Path)));
        var window = ThemeBrushes.Get("Brush.Window");
        var keyword = SyntaxTheme.For("keyword")!;
        var squiggle = ThemeBrushes.Editor("Editor.ErrorSquiggle");
        Assert.Equal(Color.Parse("#1B1D23"), Solid(window));
        Assert.Null(EditorExtensions.Theme);

        appearance.SetTheme(AppTheme.Light);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeVariant.Light, Application.Current!.ActualThemeVariant);
        Assert.Equal(Colors.White, Solid(window));
        Assert.Equal(ThemeBrushes.ColorOf("Syntax.Keyword", ThemeVariant.Light), Solid(keyword));
        Assert.Equal(ThemeBrushes.ColorOf("Editor.ErrorSquiggle", ThemeVariant.Light), Solid(squiggle));
        Assert.Same(BuiltInEditorThemes.Light, EditorExtensions.Theme);
        Assert.Equal(ThemeVariant.Light, ThemeBrushes.EditorVariant);

        appearance.SetTheme(AppTheme.Dark);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeVariant.Dark, Application.Current.ActualThemeVariant);
        Assert.Equal(Color.Parse("#1B1D23"), Solid(window));
        Assert.Null(EditorExtensions.Theme);
    }

    [AvaloniaFact]
    public void The_theme_is_remembered_and_Follow_the_system_asks_the_platform()
    {
        var paths = new AppPaths(_temp.Path);
        var appearance = new AppearanceService(new AppearanceSettingsStore(paths));
        Assert.Equal(AppTheme.Dark, appearance.Theme);

        try
        {
            appearance.SetTheme(AppTheme.System);

            Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
            Assert.Equal(AppTheme.System, new AppearanceService(new AppearanceSettingsStore(paths)).Theme);
            Assert.Contains("system", File.ReadAllText(Path.Combine(paths.State, "appearance.json")), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppearanceService.Preview(AppTheme.Dark);
        }
    }

    [AvaloniaFact]
    public void A_damaged_settings_file_means_the_dark_theme()
    {
        var paths = new AppPaths(_temp.Path);
        File.WriteAllText(Path.Combine(paths.State, "appearance.json"), "{ not json");

        Assert.Equal(AppearanceSettings.Default, new AppearanceSettingsStore(paths).Load());
    }

    public void Dispose() => _temp.Dispose();

    private static Color Solid(IBrush brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
