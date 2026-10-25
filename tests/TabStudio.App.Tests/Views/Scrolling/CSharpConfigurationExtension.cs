using TabStudio.Testing.Extensions;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// An extension that gives C# the language configuration of VS Code's built-in C# extension: comments, brackets,
/// auto-closing pairs and folding by indentation and <c>#region</c> markers, as a user who installed a C# extension
/// from Open VSX has it. Roslyn keeps coloring the file.
/// </summary>
internal static class CSharpConfigurationExtension
{
    /// <summary>The package's file name.</summary>
    public const string FileName = "csharp-configuration-1.0.0.vsix";

    private const string PackageJson = """
        {
          "name": "csharp-configuration",
          "publisher": "tabstudio-tests",
          "displayName": "C# Language Configuration",
          "description": "Brackets, comments and folding for C#.",
          "version": "1.0.0",
          "license": "MIT",
          "engines": { "vscode": "^1.80.0" },
          "categories": ["Programming Languages"],
          "contributes": {
            "languages": [
              { "id": "csharp", "aliases": ["C#", "csharp"], "configuration": "./language-configuration.json" }
            ]
          }
        }
        """;

    private const string LanguageConfiguration = """
        {
          "comments": { "lineComment": "//", "blockComment": ["/*", "*/"] },
          "brackets": [["{", "}"], ["[", "]"], ["(", ")"]],
          "autoClosingPairs": [
            { "open": "'", "close": "'", "notIn": ["string", "comment"] },
            { "open": "\"", "close": "\"", "notIn": ["string", "comment"] },
            { "open": "{", "close": "}" },
            { "open": "[", "close": "]" },
            { "open": "(", "close": ")" }
          ],
          "surroundingPairs": [["{", "}"], ["[", "]"], ["(", ")"], ["<", ">"], ["'", "'"], ["\"", "\""]],
          "folding": {
            "markers": { "start": "^\\s*#region\\b", "end": "^\\s*#endregion\\b" }
          }
        }
        """;

    /// <summary>The package's bytes.</summary>
    public static byte[] Build() => new VsixBuilder()
        .Add("package.json", PackageJson)
        .Add("language-configuration.json", LanguageConfiguration)
        .Build();
}
