using System.Text.RegularExpressions;
using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Wpf.Building;

/// <summary>
/// What of a WPF project's XAML is left out because OpenSilver's XAML compiler cannot compile it, found by the compiler
/// itself: each error it reports in a file leaves out the attribute it is in, or the resource or element it is in, and
/// the build compiles the XAML again without it. A reference to a resource left out is left out with it. Each one is a
/// warning that says what is missing in the web preview, so the window still shows, as Microsoft's ExpenseIt, whose
/// pages read XML with <c>XmlDataProvider</c> and whose window style uses WPF's Aero2 theme, does.
/// </summary>
internal sealed class XamlLeftOut(IReadOnlyList<XamlItem> items, string projectName)
{
    /// <summary>How many times a build compiles the XAML again at most.</summary>
    public const int MaxRounds = 40;

    private readonly Dictionary<string, List<(int Start, int End)>> _spans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XamlDocument> _documents = new(StringComparer.Ordinal);
    private readonly List<BuildDiagnostic> _notes = [];

    /// <summary>The warnings for what was left out.</summary>
    public IReadOnlyList<BuildDiagnostic> Notes => _notes;

    /// <summary>The spans of a file that are left out, in its text.</summary>
    /// <param name="filePath">The XAML file.</param>
    public IReadOnlyList<(int Start, int End)> SpansOf(string filePath) => _spans.TryGetValue(filePath, out var spans) ? spans : [];

    /// <summary>Leaves out what each error is in.</summary>
    /// <param name="errors">The XAML compiler's errors, in the project's files.</param>
    /// <returns>Whether anything more was left out, so compiling again can get further.</returns>
    public bool LeaveOut(IEnumerable<BuildDiagnostic> errors)
    {
        var progress = false;
        foreach (var error in errors)
        {
            if (error.Severity != DiagnosticSeverityLevel.Error || error.Location is not { FilePath: var file } location || Document(file) is not { } document)
            {
                continue;
            }

            var offset = Math.Clamp(location.Start, 0, document.Text.Length);
            if (location.Column <= 1)
            {
                // A line without a column: the first thing on it.
                while (offset < document.Text.Length && document.Text[offset] is ' ' or '\t')
                {
                    offset++;
                }
            }

            var element = document.StartTagAt(offset + (offset < document.Text.Length && document.Text[offset] == '<' ? 1 : 0)) ?? document.ElementAt(offset);
            if (element is null)
            {
                continue;
            }

            var attribute = element.Attributes.FirstOrDefault(a => !a.IsNamespaceDeclaration && offset >= a.NameStart && offset < a.End)
                ?? element.Attributes.FirstOrDefault(a => !a.IsNamespaceDeclaration && a.Prefix.Length == 0 && Names(error.Message, a.LocalName));
            if (attribute is not null)
            {
                progress |= Add(file, attribute.NameStart, attribute.End, $"{element.LocalName}.{attribute.LocalName}", error.Message, location);
                continue;
            }

            var (target, key) = ResourceOrElement(element);
            if (target is null || ReferenceEquals(target, document.Root))
            {
                continue;
            }

            if (Add(file, target.Start, target.End, key is null ? $"<{target.Name}>" : $"The resource {key}", error.Message, location))
            {
                progress = true;
                if (key is not null)
                {
                    LeaveOutReferencesTo(key);
                }
            }
        }

        return progress;
    }

    /// <summary>Whether an error message names an attribute, as in <c>'XPath'</c> or <c>XPath</c> on its own.</summary>
    private static bool Names(string message, string name) =>
        message.Contains("'" + name + "'", StringComparison.Ordinal) || message.Contains("\"" + name + "\"", StringComparison.Ordinal)
        || Regex.IsMatch(message, @"(?<![\w.])" + Regex.Escape(name) + @"(?![\w])");

    /// <summary>The resource an element is in, with its key, or the element itself when it is in none.</summary>
    private static (XamlElement? Target, string? Key) ResourceOrElement(XamlElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.Parent is { } parent && !current.IsPropertyElement
                && (parent.LocalName.EndsWith(".Resources", StringComparison.Ordinal) || (parent.LocalName == "ResourceDictionary" && !parent.IsPropertyElement)))
            {
                var key = current.Attribute(XamlNamespaces.Language, "Key")?.Value.Trim();
                return (current, key is { Length: > 0 } ? key : null);
            }
        }

        return (element, null);
    }

    /// <summary>Leaves out the attributes that use a resource that was left out, as <c>{StaticResource Key}</c>.</summary>
    private void LeaveOutReferencesTo(string key)
    {
        var pattern = new Regex(@"\{\s*(Static|Dynamic)Resource\s+(ResourceKey\s*=\s*)?" + Regex.Escape(key) + @"\s*\}", RegexOptions.CultureInvariant);
        foreach (var item in items)
        {
            if (Document(item.FilePath) is not { } document)
            {
                continue;
            }

            foreach (var element in document.Elements)
            {
                foreach (var attribute in element.Attributes)
                {
                    if (!attribute.IsNamespaceDeclaration && pattern.IsMatch(attribute.RawValue))
                    {
                        var (line, column) = document.LineAndColumn(attribute.NameStart);
                        var location = new DiagnosticLocation(item.FilePath, attribute.NameStart, attribute.NameEnd - attribute.NameStart, line, column);
                        Add(item.FilePath, attribute.NameStart, attribute.End, $"{element.LocalName}.{attribute.LocalName}", $"it uses the resource {key}, which was left out", location);
                    }
                }
            }
        }
    }

    private bool Add(string file, int start, int end, string what, string reason, DiagnosticLocation location)
    {
        if (!_spans.TryGetValue(file, out var spans))
        {
            _spans[file] = spans = [];
        }

        if (end <= start || spans.Exists(s => s.Start <= start && end <= s.End))
        {
            return false;
        }

        spans.Add((start, end));
        _notes.Add(new BuildDiagnostic(
            WpfDiagnosticIds.LeftOut,
            DiagnosticSeverityLevel.Warning,
            $"{what} is left out in the web preview: OpenSilver cannot compile it ({reason.TrimEnd('.')}). It works when the project runs on your PC through the Windows Bridge.",
            location with { Start = start, Length = Math.Min(end - start, Math.Max(1, location.Length)) },
            projectName));
        return true;
    }

    private XamlDocument? Document(string file)
    {
        if (_documents.TryGetValue(file, out var document))
        {
            return document;
        }

        if (!items.Any(i => string.Equals(i.FilePath, file, StringComparison.Ordinal)))
        {
            return null;
        }

        try
        {
            return _documents[file] = XamlDocument.Parse(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
