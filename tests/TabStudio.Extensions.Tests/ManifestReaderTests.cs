using TabStudio.Extensions.Manifests;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class ManifestReaderTests
{
    [Fact]
    public void Reads_the_fixture_manifest_with_its_localized_strings()
    {
        var manifest = ManifestReader.Read(LumenExtension.PackageJson("1.2.3"), LumenExtension.PackageNls);

        Assert.Equal(new ExtensionIdentifier(LumenExtension.Publisher, LumenExtension.Name), manifest.Id);
        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal(LumenExtension.DisplayName, manifest.DisplayName);
        Assert.Equal("Syntax coloring, snippets and themes for Lumen.", manifest.Description);
        Assert.Equal("^1.80.0", manifest.EngineVersion);
        Assert.Equal("images/icon.png", manifest.Icon);
        Assert.Equal(new Uri("https://github.com/tabstudio-tests/lumen"), manifest.Repository);
        Assert.False(manifest.HasCode);

        var contributes = manifest.Contributes;
        var language = Assert.Single(contributes.Languages);
        Assert.Equal("lumen", language.Id);
        Assert.Equal(new[] { ".lum", ".lumen" }, language.Extensions);
        Assert.Equal(new[] { "Lumenfile" }, language.Filenames);
        Assert.Equal("./language-configuration.json", language.Configuration);
        Assert.Equal(2, contributes.Grammars.Count);
        Assert.Equal("json", contributes.Grammars[0].EmbeddedLanguages["meta.embedded.block.json"]);
        Assert.Equal(new[] { "source.lumen" }, contributes.Grammars[1].InjectTo);
        Assert.Null(contributes.Grammars[1].Language);
        Assert.Equal("lumen", Assert.Single(contributes.Snippets).Language);
        Assert.Equal(new[] { LumenExtension.NightTheme, LumenExtension.DayTheme }, contributes.Themes.Select(t => t.Label));
        Assert.Equal(new[] { ColorThemeKind.Dark, ColorThemeKind.Light }, contributes.Themes.Select(t => t.Kind));
        Assert.Equal(new[] { "languages", "grammars", "snippets", "themes" }, contributes.Points.Select(p => p.Name));
        Assert.Equal(2, contributes.Points.Single(p => p.Name == "themes").Count);
    }

    [Fact]
    public void Strings_without_a_translation_are_kept_as_they_are()
    {
        var manifest = ManifestReader.Read("""{ "name": "x", "publisher": "p", "version": "1.0.0", "displayName": "%missing%", "description": "100%" }""");

        Assert.Equal("%missing%", manifest.DisplayName);
        Assert.Equal("100%", manifest.Description);
    }

    [Fact]
    public void The_name_stands_in_for_a_missing_display_name()
    {
        var manifest = ManifestReader.Read("""{ "name": "plain", "publisher": "p", "version": "0.1.0" }""");

        Assert.Equal("plain", manifest.DisplayName);
        Assert.Same(ExtensionContributions.None, manifest.Contributes);
    }

    [Theory]
    [InlineData("""{ "publisher": "p", "version": "1.0.0" }""")]
    [InlineData("""{ "name": "x", "version": "1.0.0" }""")]
    [InlineData("""{ "name": "x", "publisher": "p" }""")]
    [InlineData("""{ "name": "x", "publisher": "p", "version": "" }""")]
    [InlineData("""[1, 2, 3]""")]
    [InlineData("""not json""")]
    public void A_manifest_without_identity_is_rejected(string json)
    {
        Assert.Throws<ExtensionPackageException>(() => ManifestReader.Read(json));
    }

    [Fact]
    public void Code_entry_points_and_their_activation_are_read()
    {
        var manifest = ManifestReader.Read("""
            {
              "name": "server", "publisher": "p", "version": "2.0.0",
              "main": "./out/extension.js",
              "activationEvents": ["onLanguage:toml", "onCommand:x.run"],
              "extensionKind": "workspace",
              "contributes": { "commands": [{ "command": "x.run", "title": "Run" }, { "command": "x.stop", "title": "Stop" }], "configuration": { "title": "X" } }
            }
            """);

        Assert.True(manifest.HasCode);
        Assert.Equal("./out/extension.js", manifest.Main);
        Assert.Equal(new[] { "onLanguage:toml", "onCommand:x.run" }, manifest.ActivationEvents);
        Assert.Equal(new[] { "workspace" }, manifest.ExtensionKind);
        Assert.Equal(new[] { ("commands", 2), ("configuration", 1) }, manifest.Contributes.Points.Select(p => (p.Name, p.Count)));
    }

    [Fact]
    public void Strings_where_arrays_are_expected_and_entries_of_the_wrong_kind_are_tolerated()
    {
        var manifest = ManifestReader.Read("""
            {
              "name": "odd", "publisher": "p", "version": "1.0.0",
              "categories": "Themes",
              "contributes": {
                "languages": [{ "id": "odd", "extensions": ".odd", "aliases": "Odd" }, { "aliases": ["no id"] }, 42],
                "grammars": [{ "language": "odd", "path": "./g.json" }, { "scopeName": "source.odd", "path": "./odd.json" }],
                "themes": [{ "path": "./themes/plain.json" }]
              }
            }
            """);

        Assert.Equal(new[] { "Themes" }, manifest.Categories);
        var language = Assert.Single(manifest.Contributes.Languages);
        Assert.Equal(new[] { ".odd" }, language.Extensions);
        Assert.Equal(new[] { "Odd" }, language.Aliases);
        Assert.Equal("source.odd", Assert.Single(manifest.Contributes.Grammars).ScopeName);
        var theme = Assert.Single(manifest.Contributes.Themes);
        Assert.Equal(("plain", "plain", ColorThemeKind.Dark), (theme.Id, theme.Label, theme.Kind));
    }

    [Theory]
    [InlineData("github:someone/thing", "https://github.com/someone/thing")]
    [InlineData("someone/thing", "https://github.com/someone/thing")]
    [InlineData("git+https://gitlab.com/a/b.git", "https://gitlab.com/a/b")]
    [InlineData("git://github.com/a/b.git", "https://github.com/a/b")]
    public void Repository_addresses_in_npm_forms_become_web_pages(string repository, string expected)
    {
        var manifest = ManifestReader.Read($$"""{ "name": "x", "publisher": "p", "version": "1.0.0", "repository": "{{repository}}" }""");

        Assert.Equal(new Uri(expected), manifest.Repository);
    }

    [Fact]
    public void Identifiers_compare_without_case()
    {
        Assert.True(ExtensionIdentifier.TryParse("VSCode.Perl", out var id));
        Assert.Equal(new ExtensionIdentifier("vscode", "perl"), id);
        Assert.Equal(new ExtensionIdentifier("vscode", "perl").GetHashCode(), id.GetHashCode());
        Assert.Equal("VSCode.Perl", id.ToString());
        Assert.False(ExtensionIdentifier.TryParse("noperiod", out _));
        Assert.False(ExtensionIdentifier.TryParse(".name", out _));
        Assert.False(ExtensionIdentifier.TryParse("a/b.c", out _));
    }

    [Theory]
    [InlineData("1.10.0", "1.9.2", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("2.0.0-beta", "2.0.0", -1)]
    [InlineData("2.0.0-beta.2", "2.0.0-beta.10", -1)]
    [InlineData("2.0.0-alpha", "2.0.0-beta", -1)]
    [InlineData("1.2", "1.2.0", 0)]
    [InlineData("v3.0.0", "2.9.9", 1)]
    [InlineData("1.0.0+build.5", "1.0.0", 0)]
    public void Versions_compare_as_semantic_versions(string left, string right, int expected)
    {
        Assert.Equal(expected, Math.Sign(ExtensionVersions.Compare(left, right)));
    }
}
