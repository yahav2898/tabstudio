using TabStudio.Testing.Extensions;

namespace TabStudio.App.Tests.Extensions;

/// <summary>
/// A small file icon theme extension, as Material Icon Theme writes them: SVG icons with optimizer-compact paths, a
/// gradient and a CSS class, and a PNG.
/// </summary>
internal static class IconThemeExtension
{
    public const string Label = "Lumen Icons";

    public const string FileSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="#90a4ae" d="M13 9h5.5L13 3.5V9M6 2h8l6 6v12a2 2 0 01-2 2H6a2 2 0 01-2-2V4c0-1.11.89-2 2-2m9 16v-2H6v2h9m3-4v-2H6v2h12z"/></svg>
        """;

    public const string FolderSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="#ffca28" d="M10 4H4c-1.11 0-2 .89-2 2v12a2 2 0 002 2h16a2 2 0 002-2V8a2 2 0 00-2-2h-8l-2-2z"/></svg>
        """;

    public const string FolderOpenSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="#ffa000" d="M19 20H4a2 2 0 01-2-2V6c0-1.11.89-2 2-2h6l2 2h7a2 2 0 012 2H4v10l2.14-8h17.07l-2.28 8.5c-.23.87-1.01 1.5-1.93 1.5z"/></svg>
        """;

    public const string LumenSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 32 32">
          <defs>
            <linearGradient id="glow" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stop-color="#ffd54f"/>
              <stop offset="1" stop-color="#ff8f00"/>
            </linearGradient>
            <style>.ring { fill: none; stroke: #26a69a; stroke-width: 3 }</style>
          </defs>
          <circle cx="16" cy="16" r="9" fill="url(#glow)"/>
          <circle class="ring" cx="16" cy="16" r="13"/>
          <g transform="rotate(45 16 16)" opacity=".8"><rect x="14" y="2" width="4" height="4" rx="1" fill="#fff"/></g>
        </svg>
        """;

    public const string ThemeJson = """
        {
          "iconDefinitions": {
            "_file": { "iconPath": "./file.svg" },
            "_folder": { "iconPath": "./folder.svg" },
            "_folder_open": { "iconPath": "./folder-open.svg" },
            "_lumen": { "iconPath": "./lumen.svg" },
            "_text": { "iconPath": "./text.png" }
          },
          "file": "_file",
          "folder": "_folder",
          "folderExpanded": "_folder_open",
          "fileExtensions": { "lumen": "_lumen", "txt": "_text" }
        }
        """;

    public const string PackageJson = """
        {
          "name": "lumen-icons",
          "publisher": "tabstudio-tests",
          "displayName": "Lumen Icons",
          "description": "File icons for Lumen projects.",
          "version": "1.0.0",
          "engines": { "vscode": "^1.80.0" },
          "categories": ["Themes"],
          "contributes": {
            "iconThemes": [{ "id": "lumen-icons", "label": "Lumen Icons", "path": "./icons/lumen-icon-theme.json" }]
          }
        }
        """;

    /// <summary>The package.</summary>
    public static byte[] Build() => new VsixBuilder()
        .Add("package.json", PackageJson)
        .Add("icons/lumen-icon-theme.json", ThemeJson)
        .Add("icons/file.svg", FileSvg)
        .Add("icons/folder.svg", FolderSvg)
        .Add("icons/folder-open.svg", FolderOpenSvg)
        .Add("icons/lumen.svg", LumenSvg)
        .Add("icons/text.png", TestIcons.Create("text"))
        .Build();
}
