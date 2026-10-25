namespace TabStudio.Testing.Extensions;

/// <summary>
/// A small, purely declarative extension for tests, made like the ones on Open VSX: the made-up language Lumen
/// (<c>.lum</c> files) with a TextMate grammar, an injection grammar, embedded JSON, a language configuration,
/// snippets, a dark and a light color theme (the light one through an <c>include</c>), a README and an icon.
/// </summary>
public static class LumenExtension
{
    /// <summary>The extension's identifier.</summary>
    public const string Id = "tabstudio-tests.lumen";

    /// <summary>The publisher.</summary>
    public const string Publisher = "tabstudio-tests";

    /// <summary>The extension's name.</summary>
    public const string Name = "lumen";

    /// <summary>The display name, from <c>package.nls.json</c>.</summary>
    public const string DisplayName = "Lumen Language";

    /// <summary>The dark theme's label.</summary>
    public const string NightTheme = "Lumen Night";

    /// <summary>The light theme's label.</summary>
    public const string DayTheme = "Lumen Day";

    /// <summary>The dark theme's editor background.</summary>
    public const string NightBackground = "#0E1A24";

    /// <summary>The dark theme's keyword color.</summary>
    public const string NightKeyword = "#FF7AC6";

    /// <summary>The dark theme's string color.</summary>
    public const string NightString = "#9EE493";

    /// <summary>A 64-pixel PNG, the extension's icon and the image in its README.</summary>
    public static readonly byte[] IconPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAA90lEQVR42u3bUQrCMBBF0bcH9+gaXITrczsVhPZHFIuZzJvMLfRb7gG1TTLSyetyu2/Ot0Zf7sFhINXD/4JYLf4UwqrxPyGsHv8VoUv8R4TWAN3i3xAAAKAxQNf4AwEAAPI+fHtcX3dLgD0+G0EO8ZkIconPQpBTfAaC3OJnI8gxfiaCXONnIcg5fgaC3OOjEVQhPhJBVeKjEFQpPgJB1eJHI6hi/EgEVY0fhaDK8SMQAOArwI8gf4M8CPEozMsQr8MsiLAkxqIoy+JsjLA1xuYo2+MckOCIDACcFeSkKAAAMDPAxAhzQ0yOMTvYM57RWYane43PPwHexpw/EAks+AAAAABJRU5ErkJggg==");

    /// <summary>A Lumen program that uses every part of the grammar.</summary>
    public const string SampleProgram = """
        # Greets everyone. TODO: localize
        fn greet(name) {
            let count = 42
            if name {
                print("Hello, " + name)
            }
            return json`{"ok": true}`
        }
        """;

    /// <summary>The package.json.</summary>
    public static string PackageJson(string version = "1.0.0") => $$"""
        {
          // Comments and trailing commas are allowed, as VS Code allows them.
          "name": "{{Name}}",
          "publisher": "{{Publisher}}",
          "displayName": "%displayName%",
          "description": "%description%",
          "version": "{{version}}",
          "license": "MIT",
          "icon": "images/icon.png",
          "repository": { "type": "git", "url": "git+https://github.com/tabstudio-tests/lumen.git" },
          "engines": { "vscode": "^1.80.0" },
          "categories": ["Programming Languages", "Snippets", "Themes"],
          "contributes": {
            "languages": [
              {
                "id": "lumen",
                "aliases": ["Lumen", "lumen"],
                "extensions": [".lum", ".lumen"],
                "filenames": ["Lumenfile"],
                "configuration": "./language-configuration.json"
              }
            ],
            "grammars": [
              {
                "language": "lumen",
                "scopeName": "source.lumen",
                "path": "./syntaxes/lumen.tmLanguage.json",
                "embeddedLanguages": { "meta.embedded.block.json": "json" }
              },
              {
                "scopeName": "lumen.todo.injection",
                "path": "./syntaxes/todo.injection.json",
                "injectTo": ["source.lumen"]
              }
            ],
            "snippets": [
              { "language": "lumen", "path": "./snippets/lumen.code-snippets" }
            ],
            "themes": [
              { "label": "{{NightTheme}}", "uiTheme": "vs-dark", "path": "./themes/lumen-night.json" },
              { "label": "{{DayTheme}}", "uiTheme": "vs", "path": "./themes/lumen-day.json" },
            ],
          },
        }
        """;

    /// <summary>The package.nls.json.</summary>
    public const string PackageNls = """
        {
          "displayName": "Lumen Language",
          "description": { "message": "Syntax coloring, snippets and themes for Lumen.", "comment": ["For translators"] }
        }
        """;

    /// <summary>The grammar of Lumen.</summary>
    public const string Grammar = """
        {
          "name": "Lumen",
          "scopeName": "source.lumen",
          "patterns": [
            { "include": "#comments" },
            { "include": "#embedded-json" },
            { "include": "#strings" },
            { "include": "#keywords" },
            { "include": "#numbers" },
            { "include": "#functions" }
          ],
          "repository": {
            "comments": {
              "patterns": [
                { "name": "comment.block.lumen", "begin": "#\\[", "end": "\\]#" },
                { "name": "comment.line.number-sign.lumen", "match": "#.*$" }
              ]
            },
            "embedded-json": {
              "begin": "json`",
              "end": "`",
              "contentName": "meta.embedded.block.json",
              "patterns": [{ "include": "source.json" }]
            },
            "strings": {
              "name": "string.quoted.double.lumen",
              "begin": "\"",
              "end": "\"",
              "patterns": [{ "name": "constant.character.escape.lumen", "match": "\\\\." }]
            },
            "keywords": {
              "patterns": [
                { "name": "keyword.control.lumen", "match": "\\b(if|else|while|return)\\b" },
                { "name": "storage.type.lumen", "match": "\\b(let|fn)\\b" }
              ]
            },
            "numbers": { "name": "constant.numeric.lumen", "match": "\\b\\d+(\\.\\d+)?\\b" },
            "functions": {
              "match": "\\b([a-z_][a-z0-9_]*)\\s*(?=\\()",
              "captures": { "1": { "name": "entity.name.function.lumen" } }
            }
          }
        }
        """;

    /// <summary>An injection grammar that marks TODO in Lumen comments.</summary>
    public const string TodoInjection = """
        {
          "scopeName": "lumen.todo.injection",
          "injectionSelector": "L:comment.line.number-sign.lumen",
          "patterns": [{ "name": "keyword.todo.lumen", "match": "\\bTODO\\b" }]
        }
        """;

    /// <summary>The language configuration.</summary>
    public const string LanguageConfiguration = """
        {
          "comments": { "lineComment": "#", "blockComment": ["#[", "]#"] },
          "brackets": [["{", "}"], ["[", "]"], ["(", ")"]],
          "autoClosingPairs": [
            { "open": "{", "close": "}" },
            { "open": "[", "close": "]" },
            { "open": "(", "close": ")" },
            { "open": "\"", "close": "\"", "notIn": ["string", "comment"] }
          ],
          "surroundingPairs": [["{", "}"], ["[", "]"], ["(", ")"], ["\"", "\""]],
          "folding": {
            "markers": { "start": "^\\s*#\\s*region\\b", "end": "^\\s*#\\s*endregion\\b" }
          },
          "wordPattern": "[A-Za-z_][A-Za-z0-9_]*",
          "indentationRules": {
            "increaseIndentPattern": "^.*\\{[^}\"]*$",
            "decreaseIndentPattern": { "pattern": "^\\s*\\}" }
          },
          "onEnterRules": [
            {
              // Continue a comment, as VS Code's Rust configuration does.
              "beforeText": "^\\s*#\\s*\\S",
              "afterText": "^(?!\\s*$)",
              "action": { "indent": "none", "appendText": "# " }
            }
          ]
        }
        """;

    /// <summary>The snippets.</summary>
    public const string Snippets = """
        {
          "Function": {
            "prefix": "fn",
            "body": ["fn ${1:name}(${2:args}) {", "\t$0", "}"],
            "description": "A function"
          },
          "Print the file's name": {
            "prefix": ["pfile", "printfile"],
            "body": "print(\"${TM_FILENAME_BASE/(.*)/${1:/upcase}/}\")$0",
            "description": "Prints the name of this file"
          },
          "If": {
            "prefix": "ifs",
            "body": "if ${1|ready,done,failed|} {\n\t$2\n}"
          }
        }
        """;

    /// <summary>The dark theme.</summary>
    public const string NightThemeJson = $$"""
        {
          "name": "{{NightTheme}}",
          "type": "dark",
          "colors": {
            "editor.background": "{{NightBackground}}",
            "editor.foreground": "#DCE6F2",
            "editor.lineHighlightBackground": "#16283A",
            "editor.selectionBackground": "#2A4A6A",
            "editorCursor.foreground": "#FFCC66",
            "editorLineNumber.foreground": "#4F6A80",
            "editorLineNumber.activeForeground": "#A9C1D6",
          },
          "tokenColors": [
            { "settings": { "foreground": "#DCE6F2" } },
            { "scope": ["comment"], "settings": { "foreground": "#5C7A8C", "fontStyle": "italic" } },
            { "scope": "keyword, storage.type", "settings": { "foreground": "{{NightKeyword}}" } },
            { "scope": "string", "settings": { "foreground": "{{NightString}}" } },
            { "scope": "constant.numeric", "settings": { "foreground": "#F9C859" } },
            { "scope": "entity.name.function", "settings": { "foreground": "#7FD3FF" } },
            { "scope": "keyword.todo.lumen", "settings": { "foreground": "#FF5F5F", "fontStyle": "bold" } },
          ],
          "semanticHighlighting": true,
          "semanticTokenColors": {
            "class": "#FFD866",
            "method": { "foreground": "#7FD3FF", "italic": true },
            "*.static": { "bold": true },
          },
        }
        """;

    /// <summary>The light theme, which includes a base theme.</summary>
    public const string DayThemeJson = """
        {
          "name": "Lumen Day",
          "include": "./lumen-base.json",
          "colors": { "editor.background": "#FBFAF5" },
          "tokenColors": [
            { "scope": "keyword", "settings": { "foreground": "#A0216E" } }
          ]
        }
        """;

    /// <summary>The base theme the light theme includes.</summary>
    public const string DayBaseThemeJson = """
        {
          "name": "Lumen Base",
          "colors": { "editor.background": "#FFFFFF", "editor.foreground": "#20262E" },
          "tokenColors": [
            { "scope": "keyword", "settings": { "foreground": "#0000FF" } },
            { "scope": "string", "settings": { "foreground": "#1F7A1F" } }
          ]
        }
        """;

    /// <summary>The README.</summary>
    public const string Readme = """
        # Lumen for TabStudio

        Syntax coloring, snippets and two themes for **Lumen**, a tiny language.

        ![The Lumen logo](images/icon.png)

        ## Features

        - Coloring of keywords, strings and comments
        - Snippets: `fn`, `pfile` and `ifs`
        - The *Lumen Night* and *Lumen Day* themes

        ```lumen
        fn greet(name) {
            print("Hello, " + name)
        }
        ```

        See the [Lumen website](https://example.com/lumen) for more.
        """;

    /// <summary>A builder with every file of the extension, to add to or change.</summary>
    /// <param name="version">The version to give it.</param>
    public static VsixBuilder Builder(string version = "1.0.0") => new VsixBuilder()
        .Add("package.json", PackageJson(version))
        .Add("package.nls.json", PackageNls)
        .Add("syntaxes/lumen.tmLanguage.json", Grammar)
        .Add("syntaxes/todo.injection.json", TodoInjection)
        .Add("language-configuration.json", LanguageConfiguration)
        .Add("snippets/lumen.code-snippets", Snippets)
        .Add("themes/lumen-night.json", NightThemeJson)
        .Add("themes/lumen-day.json", DayThemeJson)
        .Add("themes/lumen-base.json", DayBaseThemeJson)
        .Add("README.md", Readme)
        .Add("images/icon.png", IconPng);

    /// <summary>The extension's package.</summary>
    /// <param name="version">The version to give it.</param>
    public static byte[] Build(string version = "1.0.0") => Builder(version).Build();
}
