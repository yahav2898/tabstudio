using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.Themes;
using TabStudio.Testing;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class ThemeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_json_theme_is_read_with_its_colors_rules_and_semantic_styles()
    {
        var path = _temp.Write("themes/night.json", LumenExtension.NightThemeJson);

        var theme = ColorThemeReader.Read(path, ColorThemeKind.Dark);

        Assert.Equal(LumenExtension.NightTheme, theme.Name);
        Assert.True(theme.IsDark);
        Assert.Equal(LumenExtension.NightBackground, theme.DefaultBackground);
        Assert.Equal("#DCE6F2", theme.DefaultForeground);
        Assert.Equal("#FFCC66", theme.Color("editorCursor.foreground"));
        Assert.Null(theme.Color("editor.missing"));
        Assert.Equal(7, theme.TokenColors.Count);
        Assert.Empty(theme.TokenColors[0].Scopes);
        Assert.Equal(new[] { "keyword", "storage.type" }, theme.TokenColors[2].Scopes);
        Assert.Equal("italic", theme.TokenColors[1].FontStyle);
        Assert.True(theme.SemanticHighlighting);
        Assert.Equal("#FFD866", theme.SemanticTokenColors["class"].Foreground);
        Assert.True(theme.SemanticTokenColors["method"].Italic);
    }

    [Fact]
    public void An_included_theme_comes_first_and_the_including_one_overrides_it()
    {
        _temp.Write("themes/lumen-base.json", LumenExtension.DayBaseThemeJson);
        var path = _temp.Write("themes/lumen-day.json", LumenExtension.DayThemeJson);

        var theme = ColorThemeReader.Read(path, ColorThemeKind.Light, root: _temp.Path);

        Assert.Equal(LumenExtension.DayTheme, theme.Name);
        Assert.False(theme.IsDark);
        Assert.Equal("#FBFAF5", theme.DefaultBackground);
        Assert.Equal("#20262E", theme.DefaultForeground);
        Assert.Equal(new[] { "#0000FF", "#1F7A1F", "#A0216E" }, theme.TokenColors.Select(r => r.Foreground));
        Assert.False(theme.SemanticHighlighting);
    }

    [Fact]
    public void Includes_outside_the_extension_and_include_cycles_are_ignored()
    {
        _temp.Write("secret.json", """{ "colors": { "editor.background": "#BAD" } }""");
        var outside = _temp.Write("ext/themes/outside.json", """{ "name": "Outside", "include": "../../secret.json", "colors": { "editor.foreground": "#111111" } }""");
        var cycle = _temp.Write("ext/themes/a.json", """{ "name": "A", "include": "./a.json", "tokenColors": [{ "scope": "x", "settings": { "foreground": "#123456" } }] }""");

        var first = ColorThemeReader.Read(outside, ColorThemeKind.Dark, root: _temp.Resolve("ext"));
        var second = ColorThemeReader.Read(cycle, ColorThemeKind.Dark, root: _temp.Resolve("ext"));

        Assert.Null(first.Color("editor.background"));
        Assert.Equal("#111111", first.DefaultForeground);
        Assert.Single(second.TokenColors);
    }

    [Fact]
    public void A_textmate_theme_is_read_from_its_property_list()
    {
        var path = _temp.Write("Classic.tmTheme", """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>name</key><string>Classic</string>
              <key>settings</key>
              <array>
                <dict>
                  <key>settings</key>
                  <dict>
                    <key>background</key><string>#272822</string>
                    <key>foreground</key><string>#F8F8F2</string>
                    <key>caret</key><string>#F8F8F0</string>
                    <key>lineHighlight</key><string>#3E3D32</string>
                    <key>selection</key><string>#49483E</string>
                  </dict>
                </dict>
                <dict>
                  <key>name</key><string>Comment</string>
                  <key>scope</key><string>comment, punctuation.definition.comment</string>
                  <key>settings</key>
                  <dict><key>foreground</key><string>#75715E</string><key>fontStyle</key><string>italic</string></dict>
                </dict>
                <dict><key>scope</key><string>keyword</string><key>settings</key><dict><key>foreground</key><string>#F92672</string></dict></dict>
              </array>
              <key>uuid</key><string>D8D5E82E-3D5B-46B5-B38E-8C841C21347D</string>
              <key>semanticClass</key><true/>
            </dict>
            </plist>
            """);

        var theme = ColorThemeReader.Read(path, ColorThemeKind.Dark);

        Assert.Equal("Classic", theme.Name);
        Assert.Equal("#272822", theme.DefaultBackground);
        Assert.Equal("#F8F8F2", theme.DefaultForeground);
        Assert.Equal("#3E3D32", theme.Color("editor.lineHighlightBackground"));
        Assert.Equal("#49483E", theme.Color("editor.selectionBackground"));
        Assert.Equal("#F8F8F0", theme.Color("editorCursor.foreground"));
        Assert.Equal(new[] { "comment", "punctuation.definition.comment" }, theme.TokenColors[1].Scopes);
        Assert.Equal("#F92672", theme.TokenColors[2].Foreground);
    }

    [Fact]
    public void Damaged_themes_are_reported_as_format_errors()
    {
        var json = _temp.Write("bad.json", "{ \"colors\": ");
        var xml = _temp.Write("bad.tmTheme", "<plist><dict><key>x</key>");

        Assert.Throws<FormatException>(() => ColorThemeReader.Read(json, ColorThemeKind.Dark));
        Assert.Throws<FormatException>(() => ColorThemeReader.Read(xml, ColorThemeKind.Dark));
    }

    [Fact]
    public void Semantic_styles_take_each_part_from_the_most_specific_selector()
    {
        var theme = ColorThemeReader.Read(_temp.Write("night.json", LumenExtension.NightThemeJson), ColorThemeKind.Dark);

        Assert.Equal(new SemanticTokenStyle("#FFD866"), SemanticTokenStyles.Resolve(theme, "class", [], "csharp"));
        Assert.Equal(new SemanticTokenStyle("#7FD3FF", Bold: true, Italic: true), SemanticTokenStyles.Resolve(theme, "method", ["static"], "csharp"));
        Assert.Equal(new SemanticTokenStyle(null, Bold: true), SemanticTokenStyles.Resolve(theme, "property", ["static"], null));
        Assert.Null(SemanticTokenStyles.Resolve(theme, "variable", [], null));
    }

    [Theory]
    [InlineData("variable", "variable", 100)]
    [InlineData("variable.readonly", "variable", 200)]
    [InlineData("*.readonly", "variable", 100)]
    [InlineData("variable:csharp", "variable", 110)]
    [InlineData("variable:python", "variable", -1)]
    [InlineData("class", "variable", -1)]
    [InlineData("variable.static", "variable", -1)]
    public void Semantic_selectors_score_by_how_much_they_name(string selector, string type, int expected)
    {
        Assert.Equal(expected, SemanticTokenStyles.Score(selector, type, ["readonly"], "csharp"));
    }

    [Fact]
    public void File_icons_are_found_by_name_extension_language_and_folder()
    {
        _temp.Write("icons/file.svg", "<svg/>");
        _temp.Write("icons/json.svg", "<svg/>");
        _temp.Write("icons/test.svg", "<svg/>");
        var path = _temp.Write("icon-theme.json", """
            {
              "iconDefinitions": {
                "_file": { "iconPath": "./icons/file.svg" },
                "_json": { "iconPath": "./icons/json.svg" },
                "_test": { "iconPath": "./icons/test.svg" },
                "_folder": { "fontCharacter": "\\E001", "fontColor": "#cbcb41" },
                "_src": { "fontCharacter": "\\E002" },
                "_outside": { "iconPath": "../outside.svg" }
              },
              "file": "_file",
              "folder": "_folder",
              "fileExtensions": { "json": "_json", "test.json": "_test" },
              "fileNames": { "Package.json": "_test" },
              "languageIds": { "lumen": "_json" },
              "folderNames": { "src": "_src" }
            }
            """);

        var theme = FileIconTheme.Read(path, "Test Icons", _temp.Path);

        Assert.EndsWith("json.svg", theme.ForFile("data.json", null)!.ImagePath, StringComparison.Ordinal);
        Assert.EndsWith("test.svg", theme.ForFile("unit.test.json", null)!.ImagePath, StringComparison.Ordinal);
        Assert.EndsWith("test.svg", theme.ForFile("package.json", null)!.ImagePath, StringComparison.Ordinal);
        Assert.EndsWith("json.svg", theme.ForFile("main.lum", "lumen")!.ImagePath, StringComparison.Ordinal);
        Assert.EndsWith("file.svg", theme.ForFile("notes.txt", null)!.ImagePath, StringComparison.Ordinal);
        Assert.Equal("\\E002", theme.ForFolder("SRC", expanded: true)!.FontCharacter);
        Assert.Equal("#cbcb41", theme.ForFolder("docs", expanded: false)!.FontColor);
        Assert.True(theme.UsesImages);
    }

    public void Dispose() => _temp.Dispose();
}
