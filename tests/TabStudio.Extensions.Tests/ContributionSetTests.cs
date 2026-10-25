using TabStudio.Extensions.Contributions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Testing;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class ContributionSetTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ExtensionStore _store;

    public ContributionSetTests()
    {
        _store = new ExtensionStore(_temp.Resolve("extensions"));
    }

    [Fact]
    public async Task Files_get_the_language_an_extension_claims()
    {
        var set = ContributionSet.From([await InstallLumenAsync()]);

        Assert.Equal("lumen", set.FindLanguageForFile("/work/src/main.lum")?.Id);
        Assert.Equal("lumen", set.FindLanguageForFile("SAMPLE.LUMEN")?.Id);
        Assert.Equal("lumen", set.FindLanguageForFile("/work/Lumenfile")?.Id);
        Assert.Null(set.FindLanguageForFile("notes.txt"));
        var language = set.FindLanguage("lumen")!;
        Assert.Equal("Lumen", language.DisplayName);
        Assert.Equal(new[] { new ExtensionIdentifier(LumenExtension.Publisher, LumenExtension.Name) }, language.Owners);
    }

    [Fact]
    public async Task A_script_without_an_extension_is_recognized_by_its_first_line()
    {
        var package = LumenExtension.PackageJson().Replace(
            "\"filenames\": [\"Lumenfile\"],",
            "\"filenames\": [\"Lumenfile\"], \"firstLine\": \"^#!.*\\\\blumen\\\\b\",",
            StringComparison.Ordinal);
        var set = ContributionSet.From([await InstallAsync(LumenExtension.Builder().Add("package.json", package))]);

        Assert.Equal("lumen", set.FindLanguageForFirstLine("#!/usr/bin/env lumen")?.Id);
        Assert.Null(set.FindLanguageForFirstLine("#!/usr/bin/env python3"));
        Assert.Null(set.FindLanguageForFirstLine("lumen"));
        Assert.Null(ContributionSet.From([await InstallLumenAsync()]).FindLanguageForFirstLine("#!/usr/bin/env lumen"));
    }

    [Fact]
    public async Task Grammars_injections_configuration_and_snippets_are_found_by_language()
    {
        var set = ContributionSet.From([await InstallLumenAsync()]);

        var grammar = set.FindGrammarForLanguage("lumen")!;
        Assert.Equal("source.lumen", grammar.ScopeName);
        Assert.True(File.Exists(grammar.Path));
        Assert.Same(grammar, set.FindGrammar("source.lumen"));
        Assert.Equal(new[] { "lumen.todo.injection" }, set.FindInjections("source.lumen"));
        Assert.Equal("#", set.FindConfiguration("lumen")!.LineComment);
        Assert.Null(set.FindConfiguration("python"));
        Assert.Equal(3, set.FindSnippets("lumen").Count);
        Assert.Empty(set.FindSnippets("python"));
        Assert.Equal(new[] { LumenExtension.DayTheme, LumenExtension.NightTheme }, set.Themes.Select(t => t.Label));
        Assert.Equal($"{LumenExtension.Id}/{LumenExtension.NightTheme}", set.Themes[1].Key);
        Assert.Empty(set.Problems);
    }

    [Fact]
    public async Task A_disabled_extension_contributes_nothing()
    {
        var lumen = await InstallLumenAsync();

        var set = ContributionSet.From([lumen with { IsEnabled = false }]);

        Assert.Empty(set.Languages);
        Assert.Empty(set.Themes);
        Assert.Null(set.FindLanguageForFile("main.lum"));
    }

    [Fact]
    public async Task Extensions_that_contribute_to_one_language_are_merged()
    {
        var lumen = await InstallLumenAsync();
        var extra = await InstallAsync(new VsixBuilder()
            .Add("package.json", """
                {
                  "name": "lumen-extra", "publisher": "other", "version": "1.0.0",
                  "contributes": {
                    "languages": [{ "id": "lumen", "aliases": ["Lumen X"], "extensions": [".lmx"], "filenamePatterns": ["*.lumen.json"] }],
                    "snippets": [{ "language": "lumen", "path": "./more.json" }]
                  }
                }
                """)
            .Add("more.json", """{ "Loop": { "prefix": "while", "body": "while $1 {\n\t$0\n}" } }"""));

        var set = ContributionSet.From([lumen, extra]);

        var language = Assert.Single(set.Languages);
        Assert.Equal("Lumen", language.DisplayName);
        Assert.Equal(new[] { ".lum", ".lumen", ".lmx" }, language.Extensions);
        Assert.Equal(2, language.Owners.Count);
        Assert.Equal("lumen", set.FindLanguageForFile("a.lmx")?.Id);
        Assert.Equal("lumen", set.FindLanguageForFile("settings.lumen.json")?.Id);
        Assert.Equal(4, set.FindSnippets("lumen").Count);
        Assert.NotNull(set.FindConfiguration("lumen"));
    }

    [Fact]
    public async Task The_longest_extension_wins()
    {
        var types = await InstallAsync(new VsixBuilder().Add("package.json", """
            {
              "name": "types", "publisher": "p", "version": "1.0.0",
              "contributes": { "languages": [{ "id": "typescript", "extensions": [".ts"] }, { "id": "declarations", "extensions": [".d.ts"] }] }
            }
            """));

        var set = ContributionSet.From([types]);

        Assert.Equal("declarations", set.FindLanguageForFile("lib.d.ts")?.Id);
        Assert.Equal("typescript", set.FindLanguageForFile("app.ts")?.Id);
    }

    [Fact]
    public async Task Files_that_cannot_be_read_are_reported_once_and_contribute_nothing()
    {
        var broken = await InstallAsync(new VsixBuilder()
            .Add("package.json", """
                {
                  "name": "broken", "publisher": "p", "version": "1.0.0",
                  "contributes": {
                    "languages": [{ "id": "broken", "extensions": [".brk"], "configuration": "./config.json" }],
                    "snippets": [{ "language": "broken", "path": "./snippets.json" }]
                  }
                }
                """)
            .Add("config.json", "{ not json")
            .Add("snippets.json", "[1, 2"));

        var set = ContributionSet.From([broken]);

        Assert.Null(set.FindConfiguration("broken"));
        Assert.Null(set.FindConfiguration("broken"));
        Assert.Empty(set.FindSnippets("broken"));
        Assert.Equal(2, set.Problems.Count);
    }

    [Fact]
    public async Task Contributions_are_described_honestly()
    {
        var lumen = await InstallLumenAsync();
        var server = ManifestReader.Read("""
            {
              "name": "toml", "publisher": "p", "version": "1.0.0", "main": "./dist/extension.js",
              "activationEvents": ["onLanguage:toml"],
              "contributes": {
                "languages": [{ "id": "toml", "extensions": [".toml"] }],
                "commands": [{ "command": "toml.format", "title": "Format" }],
                "configuration": { "properties": {} }
              }
            }
            """);

        var declarative = ContributionSupport.Describe(lumen.Manifest);
        var withCode = ContributionSupport.Describe(server);
        var hosted = ContributionSupport.Describe(server, new FakeCodeHost());

        Assert.All(declarative, s => Assert.Equal(ContributionSupportLevel.Supported, s.Support));
        Assert.Contains(declarative, s => s.Title == "Language: Lumen" && s.Detail.StartsWith(".lum, .lumen, Lumenfile", StringComparison.Ordinal));
        Assert.Contains(declarative, s => s.Title == "Syntax coloring for Lumen");
        Assert.Contains(declarative, s => s.Title == "Syntax coloring added to source.lumen");
        Assert.Contains(declarative, s => s.Title == "Color theme: Lumen Day" && s.Detail == "Light editor colors");
        Assert.Equal(ContributionSupportLevel.Supported, withCode.Single(s => s.Point == "languages").Support);
        Assert.Equal(ContributionSupportLevel.NeedsCodeHost, withCode.Single(s => s.Point == "main").Support);
        Assert.Equal("./dist/extension.js · starts on onLanguage:toml", withCode.Single(s => s.Point == "main").Detail);
        Assert.Equal(ContributionSupportLevel.NeedsCodeHost, withCode.Single(s => s.Title == "Commands").Support);
        Assert.Equal(ContributionSupportLevel.NeedsCodeHost, withCode.Single(s => s.Title == "Settings").Support);
        Assert.All(hosted, s => Assert.Equal(ContributionSupportLevel.Supported, s.Support));
        Assert.Equal(ContributionSupportLevel.NotSupported, ContributionSupport.Describe(ManifestReader.Read("""
            { "name": "keys", "publisher": "p", "version": "1.0.0", "contributes": { "keybindings": [{ "key": "ctrl+k", "command": "x" }] } }
            """)).Single().Support);
    }

    public void Dispose()
    {
        _store.Dispose();
        _temp.Dispose();
    }

    private Task<InstalledExtension> InstallLumenAsync() => InstallAsync(LumenExtension.Builder());

    private Task<InstalledExtension> InstallAsync(VsixBuilder builder) =>
        _store.InstallAsync(builder.WriteTo(_temp.Path, Guid.NewGuid().ToString("N") + ".vsix"), new ExtensionSource(ExtensionOrigin.File));

    private sealed class FakeCodeHost : IExtensionCodeHost
    {
        public bool CanRun(ExtensionManifest manifest) => true;
    }
}
