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
/// Only what the restored OpenSilver lacks is left out, so a release that adds it gets it. What its compiler then still
/// cannot compile is left out too, as <see cref="XamlLeftOut"/> finds it.
/// </remarks>
internal static partial class XamlSanitizer
{
    /// <summary>The XAML of a project as OpenSilver's compiler gets it.</summary>
    /// <param name="items">The project's XAML files.</param>
    /// <param name="types">The XAML types of the project's compilation.</param>
    /// <param name="copyDirectory">Where copies go, each under its path in the project.</param>
    /// <param name="projectName">The project, for the diagnostics.</param>
    /// <param name="leftOut">What OpenSilver's compiler could not compile, which the copies leave out (see <see cref="XamlLeftOut"/>).</param>
    public static (IReadOnlyList<XamlCompilerInput> Inputs, IReadOnlyList<BuildDiagnostic> Diagnostics) Prepare(
        IReadOnlyList<XamlItem> items,
        XamlTypeSystem types,
        string copyDirectory,
        string projectName,
        XamlLeftOut? leftOut = null)
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
            var spans = leftOut?.SpansOf(item.FilePath) ?? [];
            var rewritten = document.Root is { } top ? TextContentRewrites(top, types, text) : [];
            var (setters, insertions) = QualifiedSetters(document, types, text, spans);
            rewritten.AddRange(setters);
            DataGridRowHeaders(document, spans, removed, insertions);
            if (removed.Count == 0 && rewritten.Count == 0 && spans.Count == 0)
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

            foreach (var (start, end) in spans)
            {
                for (var i = start; i < end && i < chars.Length; i++)
                {
                    if (chars[i] is not ('\n' or '\r'))
                    {
                        chars[i] = ' ';
                    }
                }
            }

            // Text added to a start tag moves only what follows it on its line, so it goes in last, from the end.
            var sanitized = new string(chars);
            foreach (var (start, insertion) in insertions.OrderByDescending(i => i.Start))
            {
                sanitized = sanitized.Insert(start, insertion);
            }

            var copy = Path.Combine(copyDirectory, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.WriteAllText(copy, sanitized);
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
            if (value.Length == 0 || value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal)
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

    /// <summary>
    /// A style's setters that name their property through a type, as <c>Property="Label.FontSize"</c> in a style without
    /// a <c>TargetType</c>: WPF looks the property up on that type, where OpenSilver's compiler looks for an attached
    /// property. Each becomes <c>Property="FontSize"</c>, in as many characters, and a style without a target type gets
    /// that type as its <c>TargetType</c>, added to its start tag.
    /// </summary>
    private static (List<(int Start, string Replacement)> Rewrites, List<(int Start, string Insertion)> Insertions) QualifiedSetters(
        XamlDocument document, XamlTypeSystem types, string text, IReadOnlyList<(int Start, int End)> leftOut)
    {
        var rewrites = new List<(int Start, string Replacement)>();
        var insertions = new List<(int Start, string Insertion)>();
        foreach (var style in document.Elements.Where(e => !e.IsPropertyElement && e.LocalName is "Style" or "ControlTemplate" && e.XmlNamespace == XamlNamespaces.Presentation))
        {
            if (leftOut.Any(span => span.Start <= style.Start && style.Start < span.End))
            {
                continue;
            }

            // TargetType="{x:Type Label}" becomes TargetType="Label": OpenSilver's compiler reads a style's setters from the
            // attribute's text, which it no longer has once it turned the markup extension into an element.
            if (style.Attribute("TargetType") is { HasValue: true, IsValueClosed: true } targetType && XTypeArgument().Match(targetType.RawValue) is { Success: true } typeName)
            {
                var length = targetType.End - targetType.ValueStart;
                var name = typeName.Groups["type"].Value;
                rewrites.Add((targetType.ValueStart, name + text[targetType.ValueEnd] + new string(' ', length - name.Length - 1)));
            }

            var owners = new HashSet<string>(StringComparer.Ordinal);
            var setters = style.Children.Where(c => c.LocalName == "Setter")
                .Concat(style.Children.Where(c => c.LocalName == "Style.Setters").SelectMany(c => c.Children.Where(s => s.LocalName == "Setter")));
            foreach (var setter in setters)
            {
                if (setter.Attribute("Property") is not { HasValue: true, IsValueClosed: true } property
                    || leftOut.Any(span => span.Start <= setter.Start && setter.Start < span.End)
                    || property.RawValue.Trim() is not { } value || value.StartsWith('(') || value.IndexOf('.', StringComparison.Ordinal) is var dot && dot <= 0
                    || types.FindType(XamlNamespaces.Presentation, value[..dot]) is not { } owner)
                {
                    continue;
                }

                // A property of the type itself, inherited or its own, as FrameworkElement.FontSize is Label's: not an attached one.
                var name = value[(dot + 1)..];
                owners.Add(value[..dot]);
                if (types.FindMember(owner, name) is null)
                {
                    continue;
                }

                var length = property.End - property.ValueStart;
                rewrites.Add((property.ValueStart, name + text[property.ValueEnd] + new string(' ', length - name.Length - 1)));
            }

            if (owners.Count == 1 && style.Attribute("TargetType") is null)
            {
                insertions.Add((style.NameEnd, $" TargetType=\"{owners.Single()}\""));
            }
        }

        return (rewrites, insertions);
    }

    /// <summary>
    /// A DataGrid whose <c>RowHeaderWidth</c> is under 4, as WPF's <c>RowHeaderWidth="0"</c> that hides the row headers:
    /// OpenSilver's DataGrid throws for it, so the attribute is left out and the headers are hidden with
    /// <c>HeadersVisibility="Column"</c>, as WPF shows it.
    /// </summary>
    private static void DataGridRowHeaders(XamlDocument document, IReadOnlyList<(int Start, int End)> leftOut, List<XamlAttributeNode> removed, List<(int Start, string Insertion)> insertions)
    {
        foreach (var grid in document.Elements.Where(e => !e.IsPropertyElement && e.LocalName == "DataGrid" && e.XmlNamespace == XamlNamespaces.Presentation))
        {
            if (leftOut.Any(span => span.Start <= grid.Start && grid.Start < span.End)
                || grid.Attribute("RowHeaderWidth") is not { } width
                || !double.TryParse(width.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value >= 4)
            {
                continue;
            }

            removed.Add(width);
            if (grid.Attribute("HeadersVisibility") is null)
            {
                insertions.Add((grid.NameEnd, " HeadersVisibility=\"Column\""));
            }
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*\{\s*x:Type\s+(TypeName\s*=\s*)?(?<type>[\w:.]+)\s*\}\s*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex XTypeArgument();

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
