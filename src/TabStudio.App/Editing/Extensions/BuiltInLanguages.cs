namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// The VS Code language identifiers of the file types TabStudio knows itself, so an extension's snippets and settings
/// for <c>python</c> or <c>rust</c> apply to the files TabStudio already colors.
/// </summary>
public static class BuiltInLanguages
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".csx"] = "csharp",
        [".vb"] = "vb",
        [".fs"] = "fsharp",
        [".fsx"] = "fsharp",
        [".razor"] = "razor",
        [".cshtml"] = "razor",
        [".json"] = "json",
        [".jsonc"] = "jsonc",
        [".yml"] = "yaml",
        [".yaml"] = "yaml",
        [".toml"] = "toml",
        [".ini"] = "ini",
        [".editorconfig"] = "editorconfig",
        [".xml"] = "xml",
        [".csproj"] = "xml",
        [".vbproj"] = "xml",
        [".myapp"] = "xml",
        [".fsproj"] = "xml",
        [".vcxproj"] = "xml",
        [".props"] = "xml",
        [".targets"] = "xml",
        [".slnx"] = "xml",
        [".config"] = "xml",
        [".resx"] = "xml",
        [".nuspec"] = "xml",
        [".manifest"] = "xml",
        [".xaml"] = "xml",
        [".axaml"] = "xml",
        [".svg"] = "xml",
        [".html"] = "html",
        [".htm"] = "html",
        [".css"] = "css",
        [".scss"] = "scss",
        [".less"] = "less",
        [".js"] = "javascript",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",
        [".jsx"] = "javascriptreact",
        [".ts"] = "typescript",
        [".tsx"] = "typescriptreact",
        [".py"] = "python",
        [".rs"] = "rust",
        [".go"] = "go",
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin",
        [".c"] = "c",
        [".h"] = "c",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".cxx"] = "cpp",
        [".hpp"] = "cpp",
        [".swift"] = "swift",
        [".dart"] = "dart",
        [".rb"] = "ruby",
        [".php"] = "php",
        [".phtml"] = "php",
        [".lua"] = "lua",
        [".sh"] = "shellscript",
        [".bash"] = "shellscript",
        [".ps1"] = "powershell",
        [".bat"] = "bat",
        [".cmd"] = "bat",
        [".sql"] = "sql",
        [".sol"] = "solidity",
        [".move"] = "move",
        [".md"] = "markdown",
        [".markdown"] = "markdown",
        [".txt"] = "plaintext",
        [".log"] = "log",
        [".diff"] = "diff",
        [".patch"] = "diff",
        [".gitignore"] = "ignore",
        [".dockerignore"] = "ignore",
        [".gitattributes"] = "gitattributes",
        [".ipynb"] = "jupyter",
        [".dib"] = "polyglot-notebook",
    };

    private static readonly Dictionary<string, string> ByFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = "dockerfile",
        ["Makefile"] = "makefile",
        ["CMakeLists.txt"] = "cmake",
    };

    /// <summary>The language identifier of a file TabStudio knows itself, or <see langword="null"/>.</summary>
    /// <param name="path">The file's path or name.</param>
    public static string? For(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = Path.GetFileName(path);
        if (ByFileName.TryGetValue(name, out var byName))
        {
            return byName;
        }

        var extension = name.StartsWith('.') && name.IndexOf('.', 1) < 0 ? name : Path.GetExtension(name);
        return ByExtension.GetValueOrDefault(extension);
    }
}
