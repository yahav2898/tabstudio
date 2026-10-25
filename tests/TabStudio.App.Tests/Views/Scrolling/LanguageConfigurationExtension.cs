using TabStudio.Testing.Extensions;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// Extensions that give C# and Python the language configurations of VS Code's built-in language extensions: comments,
/// brackets, auto-closing pairs and folding (by indentation and <c>#region</c> markers), as a user who installed those
/// languages' extensions from Open VSX has them. Roslyn, TextMate and Pyright keep coloring the files.
/// </summary>
internal static class LanguageConfigurationExtension
{
    /// <summary>C#'s configuration.</summary>
    public static readonly (string FileName, byte[] Package) CSharp = Build("csharp", "C#", """
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
        """);

    /// <summary>Python's configuration.</summary>
    public static readonly (string FileName, byte[] Package) Python = Build("python", "Python", """
        {
          "comments": { "lineComment": "#", "blockComment": ["\"\"\"", "\"\"\""] },
          "brackets": [["{", "}"], ["[", "]"], ["(", ")"]],
          "autoClosingPairs": [
            { "open": "{", "close": "}" },
            { "open": "[", "close": "]" },
            { "open": "(", "close": ")" },
            { "open": "\"", "close": "\"", "notIn": ["string"] },
            { "open": "'", "close": "'", "notIn": ["string", "comment"] }
          ],
          "surroundingPairs": [["{", "}"], ["[", "]"], ["(", ")"], ["\"", "\""], ["'", "'"]],
          "folding": {
            "offSide": true,
            "markers": { "start": "^\\s*#\\s*region\\b", "end": "^\\s*#\\s*endregion\\b" }
          }
        }
        """);

    private static (string FileName, byte[] Package) Build(string language, string name, string configuration)
    {
        var packageJson = $$"""
            {
              "name": "{{language}}-configuration",
              "publisher": "tabstudio-tests",
              "displayName": "{{name}} Language Configuration",
              "description": "Brackets, comments and folding for {{name}}.",
              "version": "1.0.0",
              "license": "MIT",
              "engines": { "vscode": "^1.80.0" },
              "categories": ["Programming Languages"],
              "contributes": {
                "languages": [
                  { "id": "{{language}}", "aliases": ["{{name}}", "{{language}}"], "configuration": "./language-configuration.json" }
                ]
              }
            }
            """;
        var package = new VsixBuilder()
            .Add("package.json", packageJson)
            .Add("language-configuration.json", configuration)
            .Build();
        return ($"{language}-configuration-1.0.0.vsix", package);
    }
}
