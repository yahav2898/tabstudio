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
internal sealed partial class XamlLeftOut(IReadOnlyList<XamlItem> items, string projectName)
{
    /// <summary>How many times a build compiles the XAML again at most.</summary>
    public const int MaxRounds = 60;

    /// <summary>The namespaces WPF's types that XAML names by their simple name are in.</summary>
    private static readonly string[] WpfNamespaces =
    [
        "System.Windows", "System.Windows.Automation", "System.Windows.Controls", "System.Windows.Controls.Primitives",
        "System.Windows.Data", "System.Windows.Documents", "System.Windows.Input", "System.Windows.Media",
        "System.Windows.Navigation", "System.Windows.Shapes",
    ];

    private static readonly string[] ClrPrefixes = [XamlNamespaces.ClrNamespacePrefix, XamlNamespaces.UsingPrefix];

    private readonly Dictionary<string, List<(int Start, int End)>> _spans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XamlDocument> _documents = new(StringComparer.Ordinal);
    private readonly List<BuildDiagnostic> _notes = [];

    /// <summary>The warnings for what was left out.</summary>
    public IReadOnlyList<BuildDiagnostic> Notes => _notes;

    /// <summary>The spans of a file that are left out, in its text.</summary>
    /// <param name="filePath">The XAML file.</param>
    public IReadOnlyList<(int Start, int End)> SpansOf(string filePath) => _spans.TryGetValue(filePath, out var spans) ? spans : [];

    /// <summary>
    /// Leaves out the style setters for a property the style's type has in WPF and not in OpenSilver, as
    /// <c>FocusVisualStyle</c>: OpenSilver's compiler does not check a setter's property, and the C# it writes for one
    /// does not compile. A setter for a property neither has stays, and stays an error.
    /// </summary>
    /// <param name="types">The XAML types of the project's compilation.</param>
    public void LeaveOutMissingSetters(XamlTypeSystem types)
    {
        var catalog = WpfApiCatalog.Shipped;
        foreach (var item in items)
        {
            if (Document(item.FilePath) is not { } document)
            {
                continue;
            }

            foreach (var style in document.Elements.Where(e => !e.IsPropertyElement && e.LocalName == "Style" && e.XmlNamespace == XamlNamespaces.Presentation).ToList())
            {
                var target = style.Attribute("TargetType")?.Value.Trim() is { Length: > 0 } text ? TypeName(text) : null;
                var setters = style.Children.Where(c => c.LocalName == "Setter")
                    .Concat(style.Children.Where(c => c.LocalName == "Style.Setters").SelectMany(c => c.Children.Where(s => s.LocalName == "Setter")));
                foreach (var setter in setters.ToList())
                {
                    if (setter.Attribute("Property")?.Value.Trim() is not { Length: > 0 } property || property.StartsWith('('))
                    {
                        continue;
                    }

                    var dot = property.LastIndexOf('.');
                    var owner = dot > 0 ? property[..dot] : target;
                    var name = property[(dot + 1)..];
                    if (owner is null || owner.Contains(':', StringComparison.Ordinal) || types.FindType(XamlNamespaces.Presentation, owner) is not { } type
                        || types.FindMember(type, name) is not null || types.FindAttachedMember(type, name) is not null)
                    {
                        continue;
                    }

                    var full = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + type.MetadataName : type.MetadataName;
                    if (catalog.IsMissingType(full) || IsWpfOnlyMember(full, name))
                    {
                        LeaveOutElement(item.FilePath, document, setter, $"OpenSilver's {type.Name} has no {name}");
                    }
                }
            }
        }
    }

    /// <summary>The type a <c>TargetType</c> names, as <c>Label</c> or <c>{x:Type Label}</c>.</summary>
    private static string TypeName(string text)
    {
        var match = XTypeName().Match(text);
        return match.Success ? match.Groups["type"].Value : text;
    }

    /// <summary>Leaves out what each error is about, or is in.</summary>
    /// <param name="errors">The XAML compiler's errors, in the project's files.</param>
    /// <returns>Whether anything more was left out, so compiling again can get further.</returns>
    /// <remarks>
    /// OpenSilver's compiler stops at the first error of a file and says what it did not find, often without a line:
    /// <c>Type not found: {namespace}XmlDataProvider</c>, <c>Member "XPath" not found in type "System.Windows.Data.Binding"</c>.
    /// Every use of what it names in the file is left out at once; an error with a line leaves out what is there.
    /// </remarks>
    public bool LeaveOut(IEnumerable<BuildDiagnostic> errors)
    {
        var progress = false;
        foreach (var error in errors)
        {
            if (error.Severity != DiagnosticSeverityLevel.Error || error.Location is not { FilePath: var file } location || Document(file) is not { } document)
            {
                continue;
            }

            // Only what WPF has and OpenSilver does not is left out; anything else, such as a misspelt type, stays an error.
            var reason = Reason(error.Message);
            if (TypeNotFound().Match(error.Message) is { Success: true } type && IsWpfOnly(type.Groups["ns"].Value, type.Groups["name"].Value))
            {
                progress |= LeaveOutType(file, document, type.Groups["ns"].Value, type.Groups["name"].Value, reason, location);
            }
            else if (MemberNotFound().Match(error.Message) is { Success: true } member
                && IsWpfOnlyMember(member.Groups["type"].Value, member.Groups["member"].Value[(member.Groups["member"].Value.LastIndexOf('.') + 1)..]))
            {
                progress |= LeaveOutMember(file, document, member.Groups["member"].Value, member.Groups["type"].Value, reason, location);
            }
            else if (AccessorNotFound().Match(error.Message) is { Success: true } accessor
                && IsWpfOnlyMember(accessor.Groups["type"].Value, accessor.Groups["member"].Value))
            {
                // An attached property OpenSilver's type lacks, as ScrollViewer.PanningMode, whose GetPanningMode the compiler looked for.
                var accessorType = accessor.Groups["type"].Value;
                progress |= LeaveOutMember(file, document, accessorType[(accessorType.LastIndexOf('.') + 1)..] + "." + accessor.Groups["member"].Value, accessorType, reason, location);
            }
            else if (NestedResourceKey().IsMatch(error.Message))
            {
                // OpenSilver has no resources keyed by a type or a system key, so WPF's are left out with what uses them.
                progress |= LeaveOutNestedResourceKeys(file, document, "OpenSilver's resources are keyed by name, not by a markup extension such as {x:Type} or {x:Static}");
            }
            else if (error.Message.Contains("Style must declare a TargetType", StringComparison.Ordinal))
            {
                // WPF applies a style without a target type to any element; OpenSilver needs one. The sanitizer gives a style
                // whose setters all name one type that type; one it cannot is left out.
                foreach (var style in document.Elements.Where(e => !e.IsPropertyElement && e.LocalName == "Style" && e.Attribute("TargetType") is null).ToList())
                {
                    progress |= LeaveOutElement(file, document, style, "OpenSilver needs a style's TargetType");
                }
            }
            else if (PropertyNotFound().Match(error.Message) is { Success: true } attached
                && WpfOwners(attached.Groups["owner"].Value).FirstOrDefault(owner => IsWpfOnlyMember(owner, attached.Groups["member"].Value)) is { } ownerType)
            {
                progress |= LeaveOutMember(file, document, attached.Groups["owner"].Value + "." + attached.Groups["member"].Value, ownerType, reason, location);
            }
        }

        return progress;
    }

    /// <summary>Whether a type the compiler did not find is one of WPF's that OpenSilver does not have, as the catalog says.</summary>
    private static bool IsWpfOnly(string xmlNamespace, string name)
    {
        var catalog = WpfApiCatalog.Shipped;
        if (xmlNamespace == XamlNamespaces.Presentation)
        {
            return catalog.MissingTypesNamed(name).Any();
        }

        // clr-namespace:Microsoft.Windows.Themes;assembly=PresentationFramework.Aero2, which the compiler names {Microsoft.Windows.Themes}.
        var clr = ClrNamespace(xmlNamespace);
        return clr.Length > 0 && (catalog.IsMissingType(clr + "." + name) || catalog.IsMissingNamespace(clr));
    }

    /// <summary>Whether a member of a WPF type is one OpenSilver's type lacks; an attached property counts by its field or accessor.</summary>
    private static bool IsWpfOnlyMember(string type, string member)
    {
        var catalog = WpfApiCatalog.Shipped;
        return catalog.FindMissingMember(type, member) is not null
            || catalog.FindMissingMember(type, member + "Property") is not null
            || catalog.FindMissingMember(type, "Get" + member) is not null;
    }

    /// <summary>The full names a WPF type an attribute names by its simple name can have.</summary>
    private static IEnumerable<string> WpfOwners(string name) => WpfNamespaces.Select(ns => ns + "." + name);

    /// <summary>The CLR namespace of an XML namespace: of <c>clr-namespace:X;assembly=Y</c> and <c>using:X</c>, or itself when it is one.</summary>
    private static string ClrNamespace(string xmlNamespace)
    {
        foreach (var prefix in ClrPrefixes)
        {
            if (xmlNamespace.StartsWith(prefix, StringComparison.Ordinal))
            {
                return xmlNamespace[prefix.Length..].Split(';')[0].Trim();
            }
        }

        return xmlNamespace.Contains(':', StringComparison.Ordinal) || xmlNamespace.Contains('/', StringComparison.Ordinal) ? string.Empty : xmlNamespace;
    }

    /// <summary>Every element of a type the compiler did not find, or every attribute whose markup extension names it.</summary>
    private bool LeaveOutType(string file, XamlDocument document, string xmlNamespace, string name, string reason, DiagnosticLocation location)
    {
        var any = false;
        var clr = ClrNamespace(xmlNamespace);
        foreach (var element in document.Elements.Where(e => !e.IsPropertyElement && e.LocalName == name
            && (e.XmlNamespace == xmlNamespace || (clr.Length > 0 && ClrNamespace(e.XmlNamespace) == clr))).ToList())
        {
            any |= LeaveOutElement(file, document, element, reason);
        }

        if (any)
        {
            return true;
        }

        // A type in a markup extension, such as {x:Static SystemParameters.MenuPopupAnimationKey}, or the owner of an attached property.
        var used = new Regex(@"(?<![\w])(\w+:)?" + Regex.Escape(name) + @"(?![\w])", RegexOptions.CultureInvariant);
        foreach (var element in document.Elements.ToList())
        {
            foreach (var attribute in element.Attributes.Where(a => !a.IsNamespaceDeclaration).ToList())
            {
                if (attribute.LocalName.StartsWith(name + ".", StringComparison.Ordinal)
                    || (attribute.RawValue.TrimStart().StartsWith('{') && used.IsMatch(attribute.RawValue))
                    || (element.LocalName is "Setter" or "Condition" && attribute.LocalName == "Property" && attribute.RawValue.StartsWith(name + ".", StringComparison.Ordinal)))
                {
                    any |= LeaveOutAttribute(file, document, element, attribute, reason);
                }
            }
        }

        return any || LeaveOutAt(file, document, location, name, reason);
    }

    /// <summary>Every use of a member the compiler did not find: as an attribute, a markup extension's argument, a property element or a setter's property.</summary>
    private bool LeaveOutMember(string file, XamlDocument document, string member, string type, string reason, DiagnosticLocation location)
    {
        var simple = member[(member.LastIndexOf('.') + 1)..];
        var owner = type[(type.LastIndexOf('.') + 1)..];
        var argument = new Regex(@"[{,\s]" + Regex.Escape(simple) + @"\s*=", RegexOptions.CultureInvariant);
        var any = false;
        foreach (var element in document.Elements.ToList())
        {
            if (element.IsPropertyElement && element.LocalName.EndsWith("." + simple, StringComparison.Ordinal))
            {
                any |= LeaveOutElement(file, document, element, reason);
                continue;
            }

            foreach (var attribute in element.Attributes.Where(a => !a.IsNamespaceDeclaration).ToList())
            {
                var property = attribute.LocalName == "Property" && element.LocalName is "Setter" or "Condition" ? attribute.RawValue.Trim() : null;
                if (attribute.LocalName == simple || attribute.LocalName == owner + "." + simple
                    || (attribute.RawValue.TrimStart().StartsWith('{') && argument.IsMatch(attribute.RawValue))
                    || property == simple || property == owner + "." + simple)
                {
                    any |= LeaveOutAttribute(file, document, element, attribute, reason);
                }
            }
        }

        return any || LeaveOutAt(file, document, location, member, reason);
    }

    /// <summary>
    /// Every use of a resource whose key is a markup extension, as <c>{StaticResource {x:Type ListBoxItem}}</c> or
    /// <c>{DynamicResource {x:Static SystemColors.WindowBrushKey}}</c>: OpenSilver's compiler takes only a name as the key.
    /// </summary>
    private bool LeaveOutNestedResourceKeys(string file, XamlDocument document, string reason)
    {
        var any = false;
        foreach (var element in document.Elements.ToList())
        {
            foreach (var attribute in element.Attributes.Where(a => !a.IsNamespaceDeclaration && ResourceWithExtensionKey().IsMatch(a.RawValue)).ToList())
            {
                any |= LeaveOutAttribute(file, document, element, attribute, reason);
            }
        }

        return any;
    }

    /// <summary>What is at an error's line and column: the attribute, or the resource or element, it is in.</summary>
    private bool LeaveOutAt(string file, XamlDocument document, DiagnosticLocation location, string message, string reason)
    {
        if (location.Line <= 1 && location.Column <= 1)
        {
            return false;
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
            return false;
        }

        var attribute = element.Attributes.FirstOrDefault(a => !a.IsNamespaceDeclaration && offset >= a.NameStart && offset < a.End)
            ?? element.Attributes.FirstOrDefault(a => !a.IsNamespaceDeclaration && a.Prefix.Length == 0 && Names(message, a.LocalName));
        if (attribute is not null)
        {
            return LeaveOutAttribute(file, document, element, attribute, reason);
        }

        var (target, _) = ResourceOrElement(element);
        return target is not null && LeaveOutElement(file, document, target, reason);
    }

    /// <summary>Leaves out an attribute; a setter or a trigger's condition without its property or value is left out whole.</summary>
    private bool LeaveOutAttribute(string file, XamlDocument document, XamlElement element, XamlAttributeNode attribute, string reason)
    {
        // A setter or condition is nothing without it, and a style or template is nothing without the type it is for.
        if (element.LocalName is "Setter" or "Condition" or "EventSetter" || attribute.LocalName is "TargetType" or "DataType")
        {
            return LeaveOutElement(file, document, element, reason);
        }

        var (line, column) = document.LineAndColumn(attribute.NameStart);
        var location = new DiagnosticLocation(file, attribute.NameStart, attribute.NameEnd - attribute.NameStart, line, column);
        return Add(file, attribute.NameStart, attribute.End, $"{element.LocalName}.{attribute.LocalName}", reason, location);
    }

    /// <summary>Leaves out an element with what is in it, and every reference to it when it is a keyed resource.</summary>
    private bool LeaveOutElement(string file, XamlDocument document, XamlElement element, string reason)
    {
        // A template or style that is a setter's value goes with the setter, which cannot be left without one.
        if (element.Parent is { IsPropertyElement: true, LocalName: "Setter.Value", Parent: { LocalName: "Setter" } setter })
        {
            element = setter;
        }

        if (ReferenceEquals(element, document.Root) || element.End <= element.Start)
        {
            return false;
        }

        var key = element.Attribute(XamlNamespaces.Language, "Key")?.Value.Trim();
        var (line, column) = document.LineAndColumn(element.Start);
        var location = new DiagnosticLocation(file, element.NameStart, element.NameEnd - element.NameStart, line, column);
        if (!Add(file, element.Start, element.End, key is { Length: > 0 } ? $"The resource {key} ({element.LocalName})" : $"<{element.Name}>", reason, location))
        {
            return false;
        }

        if (key is { Length: > 0 })
        {
            LeaveOutReferencesTo(key);
        }

        return true;
    }

    /// <summary>The compiler's message without the stack trace an exception adds.</summary>
    private static string Reason(string message)
    {
        var text = message.StartsWith("Exception: ", StringComparison.Ordinal) ? message["Exception: ".Length..] : message;
        var stack = text.IndexOf(" at OpenSilver.", StringComparison.Ordinal);
        return (stack > 0 ? text[..stack] : text).Trim();
    }

    [GeneratedRegex(@"^\{\s*x:Type\s+(TypeName\s*=\s*)?(?<type>[\w:.]+)\s*\}$", RegexOptions.CultureInvariant)]
    private static partial Regex XTypeName();

    [GeneratedRegex(@"Type not found:\s*\{(?<ns>[^}]*)\}(?<name>[\w.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeNotFound();

    [GeneratedRegex(@"Member\s+""(?<member>[\w.]+)""\s+not found in type\s+""(?<type>[\w.`+]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex MemberNotFound();

    [GeneratedRegex(@"Method\s+""(Get|Set)(?<member>\w+)""\s+not found in type\s+""(?<type>[\w.`+]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex AccessorNotFound();

    [GeneratedRegex(@"The property '(?<owner>\w+)\.(?<member>\w+)' does not exist", RegexOptions.CultureInvariant)]
    private static partial Regex PropertyNotFound();

    [GeneratedRegex(@"""_placeHolderForDefaultValue"" not found in type ""System\.Windows\.Markup\.(Static|Dynamic)ResourceExtension""", RegexOptions.CultureInvariant)]
    private static partial Regex NestedResourceKey();

    [GeneratedRegex(@"\{\s*(Static|Dynamic)Resource\s+(ResourceKey\s*=\s*)?\{", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceWithExtensionKey();

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
