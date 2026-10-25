namespace TabStudio.App.Editing;

/// <summary>How a file's tab badge is colored.</summary>
public enum BadgeKind
{
    /// <summary>C# and other .NET languages.</summary>
    CSharp,

    /// <summary>Data files such as JSON, YAML and TOML.</summary>
    Data,

    /// <summary>Markup such as XML, XAML, HTML and project files.</summary>
    Markup,

    /// <summary>Other programming languages.</summary>
    Script,

    /// <summary>Plain text and documentation.</summary>
    Text,
}

/// <summary>What TabStudio knows about a kind of file: how to label it and how to edit it.</summary>
/// <param name="DisplayName">The language name shown in the status bar, such as "C#" or "JSON".</param>
/// <param name="Badge">The short label on tabs and in the Solution Explorer, such as "C#" or "{ }".</param>
/// <param name="BadgeKind">How the badge is colored.</param>
/// <param name="LineComment">The line comment prefix, such as <c>//</c>, or <see langword="null"/>.</param>
/// <param name="BlockComment">The block comment delimiters, used when there is no line comment.</param>
/// <param name="HighlightingExtension">The extension whose TextMate grammar colors this file, or <see langword="null"/>.</param>
public sealed record FileType(
    string DisplayName,
    string Badge,
    BadgeKind BadgeKind,
    string? LineComment,
    (string Open, string Close)? BlockComment,
    string? HighlightingExtension)
{
    /// <summary>Whether Roslyn provides the language service for this file.</summary>
    public bool IsCSharp => BadgeKind == BadgeKind.CSharp && DisplayName == "C#";
}

/// <summary>Maps file names to <see cref="FileType"/>s.</summary>
public static class FileTypes
{
    private static readonly (string, string) XmlComment = ("<!--", "-->");
    private static readonly (string, string) CssComment = ("/*", "*/");

    /// <summary>A file TabStudio has no special support for.</summary>
    public static FileType PlainText { get; } = new("Plain Text", "TXT", BadgeKind.Text, null, null, null);

    private static readonly Dictionary<string, FileType> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = new("C#", "C#", BadgeKind.CSharp, "//", null, ".cs"),
        [".csx"] = new("C# Script", "C#", BadgeKind.CSharp, "//", null, ".cs"),
        [".vb"] = new("Visual Basic", "VB", BadgeKind.CSharp, "'", null, ".vb"),
        [".fs"] = new("F#", "F#", BadgeKind.CSharp, "//", null, ".fs"),
        [".fsx"] = new("F# Script", "F#", BadgeKind.CSharp, "//", null, ".fs"),
        [".razor"] = new("Razor", "@", BadgeKind.Markup, null, ("@*", "*@"), ".razor"),
        [".cshtml"] = new("Razor", "@", BadgeKind.Markup, null, ("@*", "*@"), ".cshtml"),
        [".json"] = new("JSON", "{ }", BadgeKind.Data, "//", null, ".json"),
        [".jsonc"] = new("JSON", "{ }", BadgeKind.Data, "//", null, ".json"),
        [".yml"] = new("YAML", "YML", BadgeKind.Data, "#", null, ".yml"),
        [".yaml"] = new("YAML", "YML", BadgeKind.Data, "#", null, ".yml"),
        [".toml"] = new("TOML", "TOML", BadgeKind.Data, "#", null, ".ini"),
        [".ini"] = new("INI", "INI", BadgeKind.Data, ";", null, ".ini"),
        [".editorconfig"] = new("EditorConfig", "CFG", BadgeKind.Data, "#", null, ".ini"),
        [".xml"] = new("XML", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".csproj"] = new("MSBuild", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".vbproj"] = new("MSBuild", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".fsproj"] = new("MSBuild", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".vcxproj"] = new("MSBuild", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".props"] = new("MSBuild", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".targets"] = new("MSBuild", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".slnx"] = new("Solution", "SLN", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".config"] = new("XML", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".resx"] = new("XML", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".nuspec"] = new("XML", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".manifest"] = new("XML", "XML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".xaml"] = new("XAML", "XAML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".axaml"] = new("XAML", "XAML", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".svg"] = new("SVG", "SVG", BadgeKind.Markup, null, XmlComment, ".xml"),
        [".html"] = new("HTML", "HTML", BadgeKind.Markup, null, XmlComment, ".html"),
        [".htm"] = new("HTML", "HTML", BadgeKind.Markup, null, XmlComment, ".html"),
        [".css"] = new("CSS", "CSS", BadgeKind.Markup, null, CssComment, ".css"),
        [".scss"] = new("SCSS", "CSS", BadgeKind.Markup, "//", null, ".scss"),
        [".less"] = new("Less", "CSS", BadgeKind.Markup, "//", null, ".less"),
        [".js"] = new("JavaScript", "JS", BadgeKind.Script, "//", null, ".js"),
        [".mjs"] = new("JavaScript", "JS", BadgeKind.Script, "//", null, ".js"),
        [".cjs"] = new("JavaScript", "JS", BadgeKind.Script, "//", null, ".js"),
        [".jsx"] = new("JavaScript", "JSX", BadgeKind.Script, "//", null, ".jsx"),
        [".ts"] = new("TypeScript", "TS", BadgeKind.Script, "//", null, ".ts"),
        [".tsx"] = new("TypeScript", "TSX", BadgeKind.Script, "//", null, ".tsx"),
        [".py"] = new("Python", "PY", BadgeKind.Script, "#", null, ".py"),
        [".rs"] = new("Rust", "RS", BadgeKind.Script, "//", null, ".rs"),
        [".go"] = new("Go", "GO", BadgeKind.Script, "//", null, ".go"),
        [".java"] = new("Java", "JAVA", BadgeKind.Script, "//", null, ".java"),
        [".kt"] = new("Kotlin", "KT", BadgeKind.Script, "//", null, ".java"),
        [".kts"] = new("Kotlin", "KT", BadgeKind.Script, "//", null, ".java"),
        [".c"] = new("C", "C", BadgeKind.Script, "//", null, ".c"),
        [".h"] = new("C", "H", BadgeKind.Script, "//", null, ".h"),
        [".cpp"] = new("C++", "C++", BadgeKind.Script, "//", null, ".cpp"),
        [".cc"] = new("C++", "C++", BadgeKind.Script, "//", null, ".cpp"),
        [".cxx"] = new("C++", "C++", BadgeKind.Script, "//", null, ".cpp"),
        [".hpp"] = new("C++", "H", BadgeKind.Script, "//", null, ".hpp"),
        [".swift"] = new("Swift", "SW", BadgeKind.Script, "//", null, ".swift"),
        [".dart"] = new("Dart", "DART", BadgeKind.Script, "//", null, ".dart"),
        [".rb"] = new("Ruby", "RB", BadgeKind.Script, "#", null, ".rb"),
        [".php"] = new("PHP", "PHP", BadgeKind.Script, "//", null, ".php"),
        [".lua"] = new("Lua", "LUA", BadgeKind.Script, "--", null, ".lua"),
        [".sh"] = new("Shell", "SH", BadgeKind.Script, "#", null, ".sh"),
        [".bash"] = new("Shell", "SH", BadgeKind.Script, "#", null, ".sh"),
        [".ps1"] = new("PowerShell", "PS", BadgeKind.Script, "#", null, ".ps1"),
        [".bat"] = new("Batch", "BAT", BadgeKind.Script, "REM ", null, ".bat"),
        [".cmd"] = new("Batch", "BAT", BadgeKind.Script, "REM ", null, ".bat"),
        [".sql"] = new("SQL", "SQL", BadgeKind.Script, "--", null, ".sql"),
        [".sol"] = new("Solidity", "SOL", BadgeKind.Script, "//", null, null),
        [".move"] = new("Move", "MOVE", BadgeKind.Script, "//", null, null),
        [".md"] = new("Markdown", "MD", BadgeKind.Text, null, XmlComment, ".md"),
        [".markdown"] = new("Markdown", "MD", BadgeKind.Text, null, XmlComment, ".md"),
        [".txt"] = new("Plain Text", "TXT", BadgeKind.Text, null, null, null),
        [".log"] = new("Log", "LOG", BadgeKind.Text, null, null, ".log"),
        [".diff"] = new("Diff", "DIFF", BadgeKind.Text, null, null, ".diff"),
        [".patch"] = new("Diff", "DIFF", BadgeKind.Text, null, null, ".diff"),
        [".gitignore"] = new("Ignore", "GIT", BadgeKind.Text, "#", null, null),
        [".gitattributes"] = new("Git", "GIT", BadgeKind.Text, "#", null, null),
        [".dockerignore"] = new("Ignore", "DOCK", BadgeKind.Text, "#", null, null),
        [".sln"] = new("Solution", "SLN", BadgeKind.Text, "#", null, null),
        [".dib"] = new("Notebook", "NB", BadgeKind.CSharp, "//", null, null),
        [".ipynb"] = new("Notebook", "NB", BadgeKind.Data, null, null, ".json"),
    };

    private static readonly Dictionary<string, FileType> ByFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = new("Dockerfile", "DOCK", BadgeKind.Script, "#", null, ".dockerfile"),
        ["Makefile"] = new("Makefile", "MAKE", BadgeKind.Script, "#", null, ".mak"),
        ["CMakeLists.txt"] = new("CMake", "CMAKE", BadgeKind.Script, "#", null, null),
        ["LICENSE"] = PlainText,
    };

    /// <summary>The file type for a path, by file name first and extension second.</summary>
    /// <param name="path">A file name or full path.</param>
    public static FileType For(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = Path.GetFileName(path);
        if (ByFileName.TryGetValue(name, out var byName))
        {
            return byName;
        }

        // Dot files such as ".gitignore" have no name before the extension.
        var extension = name.StartsWith('.') && name.IndexOf('.', 1) < 0 ? name : Path.GetExtension(name);
        return ByExtension.TryGetValue(extension, out var type) ? type : PlainText;
    }
}
