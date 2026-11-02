using System.Text.RegularExpressions;
using TabStudio.App.Notebooks;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services;

/// <summary>The starting content of a file created from the Solution Explorer.</summary>
public static partial class FileTemplates
{
    /// <summary>
    /// The name to create: the typed name, with <c>.cs</c> added when the folder belongs to a C# project (<c>.vb</c> for
    /// a Visual Basic one) and no extension was typed, as Visual Studio's Add Class does.
    /// </summary>
    /// <param name="typedName">What the user typed.</param>
    /// <param name="project">The project the new file belongs to, if any.</param>
    public static string FileNameFor(string typedName, ProjectInfo? project)
    {
        var name = typedName.Trim();
        return Path.HasExtension(name) || project is not { IsRoslynLanguage: true } ? name : name + (project.IsVisualBasic ? ".vb" : ".cs");
    }

    /// <summary>The content of a new file.</summary>
    /// <param name="filePath">Full path of the new file.</param>
    /// <param name="project">The project it belongs to, which decides the namespace of C# types.</param>
    public static string ContentFor(string filePath, ProjectInfo? project)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".cs" when project is not null => CSharpType(filePath, project, name),
            ".vb" when project is not null => VisualBasicType(name),
            ".json" => "{\n}\n",
            ".md" => $"# {name}\n",
            ".ipynb" or ".dib" => NotebookFiles.ContentFor(filePath),
            ".xml" or ".props" or ".targets" => "<Project>\n</Project>\n",
            _ => string.Empty,
        };
    }

    private static string CSharpType(string filePath, ProjectInfo project, string name)
    {
        var typeName = TypeName(name);
        var keyword = typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]) ? "interface" : "class";
        return $$"""
            namespace {{NamespaceFor(filePath, project)}};

            public {{keyword}} {{typeName}}
            {
            }

            """;
    }

    /// <summary>
    /// A new Visual Basic class or interface, as Visual Studio's Add Class writes it: no namespace, because Visual Basic
    /// puts every type in the project's root namespace.
    /// </summary>
    private static string VisualBasicType(string name)
    {
        var typeName = TypeName(name);
        var keyword = typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]) ? "Interface" : "Class";
        return $"Public {keyword} {typeName}\n\nEnd {keyword}\n";
    }

    /// <summary>The namespace Visual Studio would give a type in a folder: the root namespace plus the folders.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="project">The project.</param>
    public static string NamespaceFor(string filePath, ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var root = project.GetProperty("RootNamespace") ?? project.Name;
        var folder = Path.GetRelativePath(project.DirectoryPath, Path.GetDirectoryName(filePath)!);
        var parts = new List<string> { root };
        if (folder != ".")
        {
            parts.AddRange(folder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
        }

        return string.Join('.', parts.Select(p => IdentifierPattern().IsMatch(p) ? p : SanitizeIdentifier(p)));
    }

    // A type name is one identifier: "Order.Validation.cs" declares Order_Validation, as Visual Studio's safe item name
    // does, not "class Order.Validation", which does not compile.
    private static string TypeName(string name)
    {
        var typeName = SanitizeIdentifier(name).Replace('.', '_');
        return IdentifierPattern().IsMatch(typeName) ? typeName : "NewClass";
    }

    private static string SanitizeIdentifier(string text)
    {
        var cleaned = new string(text.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' ? c : '_').ToArray());
        return cleaned.Length == 0 || char.IsDigit(cleaned[0]) ? "_" + cleaned : cleaned;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex IdentifierPattern();
}
