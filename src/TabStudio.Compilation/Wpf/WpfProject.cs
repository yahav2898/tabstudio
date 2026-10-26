using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Wpf;

/// <summary>How a project's XAML file is built, as its MSBuild item type says.</summary>
public enum XamlItemKind
{
    /// <summary>A <c>Page</c>: a window, page, user control or resource dictionary.</summary>
    Page,

    /// <summary>The <c>ApplicationDefinition</c>: the app's <c>App.xaml</c>, with its resources and startup.</summary>
    ApplicationDefinition,
}

/// <summary>A XAML file of a WPF project.</summary>
/// <param name="FilePath">The file's full path.</param>
/// <param name="RelativePath">Its path from the project folder with forward slashes, such as <c>Views/CustomerView.xaml</c>.</param>
/// <param name="Kind">How it is built.</param>
public sealed record XamlItem(string FilePath, string RelativePath, XamlItemKind Kind);

/// <summary>
/// What the Windows Desktop SDK gives a project with <c>&lt;UseWPF&gt;true&lt;/UseWPF&gt;</c>, which TabStudio gives it
/// itself: its XAML files as <c>ApplicationDefinition</c> and <c>Page</c> items, the implicit usings without
/// <c>System.IO</c> and <c>System.Net.Http</c>, and, because it compiles against OpenSilver, the <c>OPENSILVER</c>
/// symbol OpenSilver's build defines.
/// </summary>
/// <remarks>
/// A WPF project compiles against OpenSilver's implementation of the WPF API whatever its <c>TargetFramework</c>
/// says, such as <c>net10.0-windows</c>: see <c>TabStudio.Wpf</c>.
/// </remarks>
public static class WpfProject
{
    /// <summary>The symbol OpenSilver's build targets define, so code can tell OpenSilver from WPF: <c>#if OPENSILVER</c>.</summary>
    public const string OpenSilverSymbol = "OPENSILVER";

    /// <summary>The implicit usings the Windows Desktop SDK takes away for WPF, whose own types share their names (<c>Path</c>).</summary>
    public static IReadOnlyList<string> RemovedImplicitUsings { get; } = ["System.IO", "System.Net.Http"];

    /// <summary>Whether a project uses WPF.</summary>
    /// <param name="project">The project.</param>
    public static bool UsesWpf(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return IsTrue(project.GetProperty("UseWPF"));
    }

    /// <summary>The implicit usings a project's SDK takes away: WPF's, for a WPF project, none otherwise.</summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> RemovedUsings(TsProject project) => UsesWpf(project) ? RemovedImplicitUsings : [];

    /// <summary>A project's parse options with the <c>OPENSILVER</c> symbol when it is a WPF project.</summary>
    /// <param name="project">The project, or <see langword="null"/>.</param>
    /// <param name="options">The options without it.</param>
    public static CSharpParseOptions ParseOptions(TsProject? project, CSharpParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return project is not null && UsesWpf(project) && !options.PreprocessorSymbolNames.Contains(OpenSilverSymbol, StringComparer.Ordinal)
            ? options.WithPreprocessorSymbols(options.PreprocessorSymbolNames.Append(OpenSilverSymbol))
            : options;
    }

    /// <summary>
    /// Where the second pass of OpenSilver's XAML compiler writes the C# of each XAML file, under its path in the
    /// project: <c>MainWindow.xaml</c> becomes <c>MainWindow.xaml.g.cs</c> in this folder.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string SecondPassDirectory(TsProject project) => Path.Combine(OutputDirectory(project), "xaml");

    /// <summary>The XAML file the second pass generated a C# file from, or <see langword="null"/> for any other file.</summary>
    /// <param name="project">The project.</param>
    /// <param name="generatedFilePath">A source file of the project's build.</param>
    public static string? SecondPassSource(TsProject project, string generatedFilePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(generatedFilePath);
        var directory = SecondPassDirectory(project) + Path.DirectorySeparatorChar;
        return generatedFilePath.StartsWith(directory, StringComparison.Ordinal) && generatedFilePath.EndsWith(".xaml.g.cs", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(project.DirectoryPath, generatedFilePath[directory.Length..^".g.cs".Length])
            : null;
    }

    /// <summary>The folder of a WPF project where TabStudio writes what it builds, as the SDK writes to <c>obj</c>.</summary>
    /// <param name="project">The project.</param>
    public static string OutputDirectory(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Path.Combine(project.DirectoryPath, "obj", "TabStudio", "wpf");
    }

    /// <summary>
    /// The XAML files of a project, as the Windows Desktop SDK's default items have them: <c>App.xaml</c> beside the
    /// project file is the <c>ApplicationDefinition</c>, unless <c>EnableDefaultApplicationDefinition</c> is
    /// <c>false</c>, and every other <c>.xaml</c> file is a <c>Page</c>, unless <c>EnableDefaultPageItems</c> is
    /// <c>false</c>; the project file's own <c>ApplicationDefinition</c> and <c>Page</c> items add to them, and its
    /// <c>Remove</c> items take files away. Files in <c>bin</c> and <c>obj</c> are never items.
    /// </summary>
    /// <param name="project">A WPF project.</param>
    /// <returns>The files, the <c>ApplicationDefinition</c> first, then the pages by path.</returns>
    public static IReadOnlyList<XamlItem> XamlItems(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var directory = project.DirectoryPath;
        var declared = ReadDeclaredItems(project.FilePath);
        var items = new Dictionary<string, XamlItemKind>(StringComparer.Ordinal);

        if (!IsFalse(project.GetProperty("EnableDefaultItems")) && !IsFalse(project.GetProperty("EnableDefaultPageItems")))
        {
            foreach (var path in EnumerateXaml(directory))
            {
                items[path] = XamlItemKind.Page;
            }
        }

        var app = Path.Combine(directory, "App.xaml");
        if (!IsFalse(project.GetProperty("EnableDefaultApplicationDefinition")) && File.Exists(app))
        {
            items[app] = XamlItemKind.ApplicationDefinition;
        }

        foreach (var (kind, include, isRemove) in declared)
        {
            foreach (var path in Expand(directory, include))
            {
                if (isRemove)
                {
                    if (items.TryGetValue(path, out var existing) && existing == kind)
                    {
                        items.Remove(path);
                    }
                }
                else
                {
                    items[path] = kind;
                }
            }
        }

        // One file is one item: an ApplicationDefinition is not also a page.
        return items
            .Select(item => new XamlItem(item.Key, Path.GetRelativePath(directory, item.Key).Replace('\\', '/'), item.Value))
            .OrderBy(item => item.Kind == XamlItemKind.ApplicationDefinition ? 0 : 1)
            .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The <c>.xaml</c> files under a folder, without those in <c>bin</c>, <c>obj</c> and hidden folders.</summary>
    private static IEnumerable<string> EnumerateXaml(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        return Directory.EnumerateFiles(directory, "*.xaml", options)
            .Where(path => !Path.GetRelativePath(directory, path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" || part.StartsWith('.')));
    }

    /// <summary>The files an item's <c>Include</c> names: files, or wildcards such as <c>Views\**\*.xaml</c>.</summary>
    private static IEnumerable<string> Expand(string directory, string include)
    {
        foreach (var part in include.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pattern = part.Replace('\\', '/');
            if (!pattern.Contains('*', StringComparison.Ordinal))
            {
                var path = Path.GetFullPath(Path.Combine(directory, pattern.Replace('/', Path.DirectorySeparatorChar)));
                if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                {
                    yield return path;
                }

                continue;
            }

            var glob = Glob(pattern);
            foreach (var path in EnumerateXaml(directory))
            {
                if (glob.IsMatch(Path.GetRelativePath(directory, path).Replace('\\', '/')))
                {
                    yield return path;
                }
            }
        }
    }

    /// <summary>An MSBuild wildcard as a regular expression: <c>**/</c> is any folders, <c>*</c> any part of a name.</summary>
    private static Regex Glob(string pattern)
    {
        var expression = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                var slash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                expression.Append(slash ? "(?:.*/)?" : ".*");
                i += slash ? 2 : 1;
            }
            else
            {
                expression.Append(pattern[i] == '*' ? "[^/]*" : Regex.Escape(pattern[i..(i + 1)]));
            }
        }

        return new Regex(expression.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    /// <summary>The project file's <c>ApplicationDefinition</c> and <c>Page</c> items, in order, with their <c>Remove</c> items.</summary>
    private static List<(XamlItemKind Kind, string Include, bool IsRemove)> ReadDeclaredItems(string projectFilePath)
    {
        var items = new List<(XamlItemKind, string, bool)>();
        XDocument document;
        try
        {
            document = XDocument.Load(projectFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            // The workspace already reports a project file it cannot read.
            return items;
        }

        foreach (var element in document.Descendants())
        {
            var kind = element.Name.LocalName switch
            {
                "Page" => XamlItemKind.Page,
                "ApplicationDefinition" => XamlItemKind.ApplicationDefinition,
                _ => (XamlItemKind?)null,
            };
            if (kind is null || element.Parent?.Name.LocalName != "ItemGroup")
            {
                continue;
            }

            if (element.Attribute("Include")?.Value is { Length: > 0 } include)
            {
                items.Add((kind.Value, include, false));
            }
            else if (element.Attribute("Remove")?.Value is { Length: > 0 } remove)
            {
                items.Add((kind.Value, remove, true));
            }
        }

        return items;
    }

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsFalse(string? value) => string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
}
