using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;
using TabStudio.Wpf.Compiler;

namespace TabStudio.Wpf.Building;

/// <summary>
/// Gets a WPF project's XAML ready for OpenSilver's compiler, which stops at a member OpenSilver does not have: the
/// settings TabStudio takes care of itself are left out of a copy of the file, in place, so every line and column stays
/// where it is, and each one is reported with what happens to it.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>StartupUri</c> on the application: TabStudio's browser host opens that window (OpenSilver's
///   <c>Application</c> has no <c>StartupUri</c>).</item>
///   <item>On a window, the desktop settings OpenSilver's <c>Window</c> lacks: they have no effect in the web preview,
///   where the window fills the page; its <c>Background</c> is painted behind its content.</item>
///   <item>Text inside an element whose WPF type takes it as a property, as <c>&lt;TextBox&gt;00&lt;/TextBox&gt;</c> sets
///   <c>Text</c>, where OpenSilver's type has no content property: the text becomes that attribute, in the same
///   characters, as <c>&lt;TextBox Text="00"     /&gt;</c>.</item>
/// </list>
/// Only what the restored OpenSilver lacks is left out, so a release that adds it gets it.
/// </remarks>
internal static class XamlSanitizer
{
    /// <summary>The XAML of a project as OpenSilver's compiler gets it.</summary>
    /// <param name="items">The project's XAML files.</param>
    /// <param name="types">The XAML types of the project's compilation.</param>
    /// <param name="copyDirectory">Where copies go, each under its path in the project.</param>
    /// <param name="projectName">The project, for the diagnostics.</param>
    public static (IReadOnlyList<XamlCompilerInput> Inputs, IReadOnlyList<BuildDiagnostic> Diagnostics) Prepare(
        IReadOnlyList<XamlItem> items,
        XamlTypeSystem types,
        string copyDirectory,
        string projectName)
    {
        var inputs = new List<XamlCompilerInput>(items.Count);
        var diagnostics = new List<BuildDiagnostic>();
        foreach (var item in items)
        {
            string text;
            try
            {
                text = File.ReadAllText(item.FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new BuildDiagnostic(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"{item.RelativePath} could not be read: {ex.Message}", null, projectName));
                continue;
            }

            var document = XamlDocument.Parse(text);
            var removed = document.Root is { } root ? Removals(item, root, types, document, diagnostics, projectName) : [];
            var rewritten = document.Root is { } top ? TextContentRewrites(top, types, text) : [];
            if (removed.Count == 0 && rewritten.Count == 0)
            {
                inputs.Add(new XamlCompilerInput(item));
                continue;
            }

            var chars = text.ToCharArray();
            foreach (var (start, replacement) in rewritten)
            {
                replacement.CopyTo(0, chars, start, replacement.Length);
            }

            foreach (var attribute in removed)
            {
                for (var i = attribute.NameStart; i < attribute.End && i < chars.Length; i++)
                {
                    if (chars[i] is not ('\n' or '\r'))
                    {
                        chars[i] = ' ';
                    }
                }
            }

            var copy = Path.Combine(copyDirectory, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.WriteAllText(copy, new string(chars));
            inputs.Add(new XamlCompilerInput(item, copy));
        }

        return (inputs, diagnostics);
    }

    /// <summary>The property WPF's types take their text content as, where OpenSilver's may have no content property.</summary>
    private static readonly Dictionary<string, string> WpfTextContentProperties = new(StringComparer.Ordinal)
    {
        ["TextBox"] = "Text",
        ["TextBlock"] = "Text",
        ["Run"] = "Text",
        ["AccessText"] = "Text",
    };

    /// <summary>
    /// The text contents that become attributes: for each element with only text in it whose type has no content
    /// property but has the property WPF's type takes text as, the start of its start tag's <c>&gt;</c> and what replaces
    /// it to the end of its end tag, in exactly as many characters, so the rest of the file keeps its lines and columns.
    /// </summary>
    private static List<(int Start, string Replacement)> TextContentRewrites(XamlElement root, XamlTypeSystem types, string text)
    {
        var rewrites = new List<(int, string)>();
        var pending = new Stack<XamlElement>();
        pending.Push(root);
        while (pending.TryPop(out var element))
        {
            foreach (var child in element.Children)
            {
                pending.Push(child);
            }

            if (element.IsPropertyElement || element.IsSelfClosing || element.Children.Count > 0 || element.StartTagEnd < 1 || element.EndTagStart < element.StartTagEnd
                || element.End < 0 || element.End > text.Length || !WpfTextContentProperties.TryGetValue(element.LocalName, out var property)
                || element.Attribute(property) is not null
                || types.FindType(element.XmlNamespace, element.LocalName) is not { } type
                || XamlTypeSystem.ContentProperty(type) is not null || types.FindMember(type, property) is null)
            {
                continue;
            }

            var content = text[element.StartTagEnd..element.EndTagStart];
            var value = content.Trim();
            if (value.Length == 0 || value.Contains('
', StringComparison.Ordinal) || value.Contains('', StringComparison.Ordinal)
                || value.Contains('<', StringComparison.Ordinal) || value.Contains("<![CDATA[", StringComparison.Ordinal))
            {
                continue;
            }

            // From the start tag's '>' to the end tag's '>': ' Text="00"' and spaces, then '/>'.
            var start = element.StartTagEnd - 1;
            var attribute = $" {property}=\"{value.Replace("\"", "&quot;", StringComparison.Ordinal)}\"";
            var length = element.End - start;
            if (attribute.Length + 2 > length)
            {
                continue;
            }

            rewrites.Add((start, attribute + new string(' ', length - attribute.Length - 2) + "/>"));
        }

        return rewrites;
    }

    private static List<XamlAttributeNode> Removals(XamlItem item, XamlElement root, XamlTypeSystem types, XamlDocument document, List<BuildDiagnostic> diagnostics, string projectName)
    {
        var removed = new List<XamlAttributeNode>();
        var rootType = types.FindType(root.XmlNamespace, root.LocalName);
        if (item.Kind == XamlItemKind.ApplicationDefinition && root.Attribute("StartupUri") is { } startup
            && types.Application is { } application && types.FindMember(application, "StartupUri") is null)
        {
            removed.Add(startup);
        }

        if (types.Window is not { } window || !XamlTypeSystem.DerivesFrom(rootType, window))
        {
            return removed;
        }

        foreach (var attribute in root.Attributes)
        {
            if (attribute.Prefix.Length > 0 || attribute.IsNamespaceDeclaration || attribute.LocalName.Contains('.', StringComparison.Ordinal)
                || types.FindMember(rootType!, attribute.LocalName) is not null)
            {
                continue;
            }

            if (WpfWindowSettings.Note(attribute.LocalName, root) is not { } note)
            {
                continue;
            }

            removed.Add(attribute);
            var (line, column) = document.LineAndColumn(attribute.NameStart);
            diagnostics.Add(new BuildDiagnostic(
                WpfDiagnosticIds.WindowSettingIgnored,
                note.Severity,
                note.Message,
                new DiagnosticLocation(item.FilePath, attribute.NameStart, attribute.NameEnd - attribute.NameStart, line, column),
                projectName));
        }

        return removed;
    }
}
