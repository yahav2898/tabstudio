using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using TabStudio.App.Editing;
using TabStudio.App.Theming;

namespace TabStudio.App.Tests.Theming;

/// <summary>The palette: both themes complete, readable (WCAG AA), and the same colors wherever they are used.</summary>
public sealed class ThemePaletteTests
{
    private static readonly string[] Surfaces =
        ["Brush.Window", "Brush.Panel", "Brush.Toolbar", "Brush.MenuBar", "Brush.Popup", "Brush.Control", "Brush.Hover"];

    private static readonly string[] TextOnSurfaces =
        ["Brush.TextBright", "Brush.Text", "Brush.TextSecondary", "Brush.TextLabel", "Brush.TextMuted", "Brush.Link", "Brush.AccentText",
         "Brush.Error", "Brush.Success", "Brush.Info", "Brush.Warning"];

    // Text (4.5:1) and shapes such as icons and squiggles (3:1) on the backgrounds they are drawn on.
    private static readonly (string Foreground, string Background, double Minimum)[] Pairs =
    [
        ("Brush.LineNumber", "Brush.Window", 4.5),
        ("Brush.TextMuted", "Brush.Selection", 4.5),
        ("Brush.SelectionText", "Brush.Selection", 4.5),
        ("Brush.TextMuted", "Brush.SelectionInactive", 4.5),
        ("Brush.OnAccent", "Brush.Accent", 4.5),
        ("Brush.OnAccent", "Brush.AccentHover", 4.5),
        ("Brush.OnAccent", "Brush.AccentPressed", 4.5),
        ("Brush.OnAccent", "Brush.Debugging", 4.5),
        ("Brush.OnAccent", "Brush.Destructive", 4.5),
        ("Brush.OnAccent", "Brush.Badge.Count", 4.5),
        ("Brush.AccentText", "Brush.AccentSoft", 4.5),
        ("Brush.TextBright", "Brush.AccentSoft", 4.5),
        ("Brush.Success", "Brush.SuccessSoft", 4.5),
        ("Brush.Badge.CSharp", "Brush.Badge.CSharpBackground", 4.5),
        ("Brush.Badge.Notebook", "Brush.Badge.NotebookBackground", 4.5),
        ("Brush.TextSecondary", "Brush.Pill", 4.5),
        ("Brush.Error", "Brush.Notice.Error", 4.5),
        ("Brush.Warning", "Brush.Notice.Warning", 4.5),
        ("Brush.Info", "Brush.Notice.Info", 4.5),
        ("Brush.TextMuted", "Brush.ActivityBar", 4.5),
        ("Brush.ActivityIndicator", "Brush.ActivityBar", 3),
        ("Brush.Error", "Brush.ExceptionHeader", 4.5),
        ("Brush.Run.Failed", "Brush.Run.FailedSoft", 4.5),
        ("Brush.Run.Passed", "Brush.Run.PassedSoft", 4.5),
        ("Brush.Review.Claude", "Brush.Review.ClaudeSoft", 4.5),
        ("Brush.Review.Pending", "Brush.Review.PendingSoft", 4.5),
        ("Brush.Terminal.Foreground", "Brush.Terminal.Background", 4.5),
        ("Brush.Log.Error", "Brush.Window", 4.5),
        ("Brush.Log.Warning", "Brush.Window", 4.5),
        ("Brush.Output.Error", "Brush.Window", 4.5),
        ("Brush.Output.Warning", "Brush.Window", 4.5),
        ("Brush.Output.Success", "Brush.Window", 4.5),
        ("Editor.InlayHint", "Brush.Window", 4.5),
        ("Editor.DiffAddedText", "Editor.DiffAddedLine", 4.5),
        ("Editor.DiffRemovedText", "Editor.DiffRemovedLine", 4.5),
        ("Editor.ErrorSquiggle", "Brush.Window", 3),
        ("Editor.WarningSquiggle", "Brush.Window", 3),
        ("Editor.MessageSquiggle", "Brush.Window", 3),
        ("Brush.Breakpoint", "Brush.Window", 3),
        ("Brush.Folder", "Brush.Panel", 3),
        ("Brush.Solution", "Brush.Panel", 3),
        ("Symbol.Purple", "Brush.Popup", 3),
        ("Symbol.Blue", "Brush.Popup", 3),
        ("Symbol.Gold", "Brush.Popup", 3),
        ("Brush.TextBright", "Brush.Overlay", 4.5),
        ("Brush.TextMuted", "Brush.Overlay", 4.5),
        ("Brush.Info", "Brush.Overlay", 3),
        ("Brush.Swarm.Highlight", "Brush.Panel", 3),
        ("TextControlPlaceholderForeground", "TextControlBackground", 4.5),
        ("TextControlPlaceholderForegroundPointerOver", "TextControlBackground", 4.5),
        ("TextControlPlaceholderForegroundFocused", "TextControlBackground", 4.5),
    ];

    private static readonly string[] SyntaxTokens =
        ["Syntax.Text", "Syntax.Keyword", "Syntax.ControlKeyword", "Syntax.Type", "Syntax.Method", "Syntax.Variable", "Syntax.String",
         "Syntax.StringEscape", "Syntax.Number", "Syntax.Comment", "Syntax.Muted"];

    [AvaloniaFact]
    public void Both_themes_have_every_token()
    {
        var palette = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://TabStudio.App/Theme/Colors.axaml"));
        var dark = Keys(palette, ThemeVariant.Default);
        var light = Keys(palette, ThemeVariant.Light);

        Assert.True(dark.Count > 200, $"Only {dark.Count} dark tokens.");
        Assert.Empty(dark.Except(light));
        Assert.Empty(light.Except(dark));

        static HashSet<string> Keys(ResourceDictionary palette, ThemeVariant variant) =>
            [.. ((ResourceDictionary)palette.ThemeDictionaries[variant]).Keys.OfType<string>()];
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Text_and_shapes_pass_WCAG_AA_on_the_surfaces_they_are_drawn_on(string theme)
    {
        var variant = theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        var pairs = Pairs
            .Concat(TextOnSurfaces.SelectMany(text => Surfaces.Select(surface => (text, surface, 4.5))))
            .Concat(SyntaxTokens.SelectMany(syntax => new[] { (syntax, "Brush.Window", 4.5), (syntax, "Brush.CurrentLine", 4.5), (syntax, "Brush.Popup", 4.5) }));

        var failures = pairs
            .Select(pair => (pair.Item1, pair.Item2, pair.Item3, Ratio: Contrast(pair.Item1, pair.Item2, variant)))
            .Where(pair => pair.Ratio < pair.Item3)
            .Select(pair => $"{pair.Item1} on {pair.Item2}: {pair.Ratio:0.00} (needs {pair.Item3})")
            .ToList();

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [AvaloniaFact]
    public void A_placeholder_is_drawn_at_full_strength_so_its_color_keeps_its_contrast()
    {
        // Fluent's template draws it at half opacity: the light placeholder measured 3.5:1 on white.
        var box = new TextBox { Watermark = "Search Solution Explorer", Width = 300 };
        var window = new Window { Width = 400, Height = 100, Content = box };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var watermark = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(box).OfType<TextBlock>().Single(t => t.Name == "PART_Watermark");
        Assert.True(watermark.IsEffectivelyVisible);
        Assert.Equal(1, watermark.Opacity);

        box.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, watermark.Opacity);
        window.Close();
    }

    [AvaloniaFact]
    public void The_dark_TextMate_theme_has_the_palettes_syntax_colors()
    {
        using var stream = typeof(TextMateHighlighting).Assembly.GetManifestResourceStream("Themes/TabStudioDark.json")!;
        using var json = JsonDocument.Parse(stream);
        var rules = json.RootElement.GetProperty("tokenColors").EnumerateArray().ToList();

        Assert.Equal(BuiltInEditorThemes.ScopeRules.Count, rules.Count);
        for (var i = 0; i < rules.Count; i++)
        {
            var (scopes, token, fontStyle) = BuiltInEditorThemes.ScopeRules[i];
            var settings = rules[i].GetProperty("settings");
            var jsonScopes = rules[i].TryGetProperty("scope", out var scope) ? scope.EnumerateArray().Select(s => s.GetString()!).ToArray() : [];
            Assert.Equal(scopes, jsonScopes);
            Assert.Equal(token is null ? null : BuiltInEditorThemes.ToVsCodeHex(ThemeBrushes.ColorOf(token, ThemeVariant.Dark)),
                settings.TryGetProperty("foreground", out var foreground) ? foreground.GetString() : null);
            Assert.Equal(fontStyle, settings.TryGetProperty("fontStyle", out var style) ? style.GetString() : null);
        }
    }

    [AvaloniaFact]
    public void TabStudio_Light_colors_code_as_the_light_palette_does()
    {
        var theme = BuiltInEditorThemes.Light;

        Assert.False(theme.Source.IsDark);
        Assert.Equal(ThemeBrushes.ColorOf("Brush.Window", ThemeVariant.Light), Solid(theme.Background));
        Assert.Equal(ThemeBrushes.ColorOf("Syntax.Text", ThemeVariant.Light), Solid(theme.Foreground));
        Assert.Equal(ThemeBrushes.ColorOf("Editor.Caret", ThemeVariant.Light), Solid(theme.Caret));
        foreach (var classification in new[] { "keyword", "keyword - control", "class name", "method name", "local name", "string", "number", "comment", "excluded code" })
        {
            Assert.Equal(SyntaxTheme.ColorFor(classification, ThemeVariant.Light), Solid(theme.ClassificationBrush(classification)!));
        }

        Assert.Null(theme.ClassificationBrush("operator"));
        Assert.Equal(BuiltInEditorThemes.ToVsCodeHex(ThemeBrushes.ColorOf("Syntax.Keyword", ThemeVariant.Light)), theme.ScopeColor("keyword.other.using"));
        Assert.Equal(BuiltInEditorThemes.ToVsCodeHex(ThemeBrushes.ColorOf("Syntax.String", ThemeVariant.Light)), theme.ScopeColor("string.quoted.double.python"));
    }

    [AvaloniaFact]
    public void Colors_are_written_as_VS_Code_writes_them()
    {
        Assert.Equal("#0F7A70", BuiltInEditorThemes.ToVsCodeHex(Color.Parse("#0F7A70")));
        Assert.Equal("#ADD6FF99", BuiltInEditorThemes.ToVsCodeHex(Color.Parse("#99ADD6FF")));
    }

    private static Color Solid(IBrush brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    /// <summary>The WCAG contrast ratio of a token on another, with a translucent foreground or background blended over the window.</summary>
    private static double Contrast(string foreground, string background, ThemeVariant variant)
    {
        var window = ThemeBrushes.ColorOf("Brush.Window", variant);
        var back = Blend(ThemeBrushes.ColorOf(background, variant), window);
        var fore = Blend(ThemeBrushes.ColorOf(foreground, variant), back);
        var (lighter, darker) = (Math.Max(Luminance(fore), Luminance(back)), Math.Min(Luminance(fore), Luminance(back)));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static Color Blend(Color color, Color under)
    {
        var alpha = color.A / 255.0;
        return Color.FromRgb(Mix(color.R, under.R), Mix(color.G, under.G), Mix(color.B, under.B));

        byte Mix(byte top, byte bottom) => (byte)Math.Round((top * alpha) + (bottom * (1 - alpha)));
    }

    private static double Luminance(Color color) =>
        (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
