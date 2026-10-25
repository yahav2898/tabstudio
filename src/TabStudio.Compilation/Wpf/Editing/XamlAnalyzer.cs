using System.Xml;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>
/// The live errors of a WPF XAML file, as it is typed: XAML that is not well-formed, prefixes not declared, types and
/// members not found (with what was probably meant), WPF API OpenSilver does not have
/// (<see cref="WpfDiagnosticIds.UnsupportedApi"/>) or does not implement (<see cref="WpfDiagnosticIds.NotImplementedApi"/>),
/// event handlers the code-behind lacks, values an enum does not have, resources not found, names used twice, and the
/// window settings that have no effect in the web preview.
/// </summary>
internal static class XamlAnalyzer
{
    private static readonly HashSet<string> LanguageExtensions = new(StringComparer.Ordinal) { "Null", "Type", "Static", "Reference", "Array", "Bind" };

    /// <summary>The file's diagnostics, most severe first.</summary>
    public static List<BuildDiagnostic> Analyze(XamlSemantics semantics, CancellationToken cancellationToken)
    {
        var context = new Context(semantics);
        var wellFormed = CheckWellFormed(context);
        if (semantics.Root is null)
        {
            return context.Diagnostics;
        }

        var names = new Dictionary<string, XamlAttributeNode>(StringComparer.Ordinal);
        foreach (var element in semantics.Document.Elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName.Length > 0)
            {
                CheckElement(context, element, wellFormed, names, cancellationToken);
            }
        }

        return [.. context.Diagnostics.OrderByDescending(d => d.Severity).ThenBy(d => d.Location?.Start ?? 0)];
    }

    /// <summary>Reads the text as XML, reporting where it first stops being well-formed.</summary>
    private static bool CheckWellFormed(Context context)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(context.Semantics.File.Text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            while (reader.Read())
            {
            }

            return true;
        }
        catch (XmlException ex)
        {
            var offset = OffsetOf(context.Semantics.File.Text, ex.LineNumber, ex.LinePosition);
            var message = ex.Message;
            var at = message.LastIndexOf(" Line ", StringComparison.Ordinal);
            if (at > 0)
            {
                message = message[..at];
            }

            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"The XAML is not well-formed: {message}", offset, offset < context.Semantics.File.Text.Length ? 1 : 0);
            return false;
        }
    }

    private static void CheckElement(Context context, XamlElement element, bool wellFormed, Dictionary<string, XamlAttributeNode> names, CancellationToken cancellationToken)
    {
        var semantics = context.Semantics;
        var nameLength = element.NameEnd - element.NameStart;
        if (element.NamespaceOf(element.Prefix) is not { } ns)
        {
            if (wellFormed || element.Prefix.Length == 0)
            {
                context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, element.Prefix.Length == 0
                    ? $"{element.Name} has no XAML namespace: declare xmlns=\"{XamlNamespaces.Presentation}\" on the root element."
                    : $"The prefix '{element.Prefix}' is not declared: declare it on the root element, such as xmlns:{element.Prefix}=\"clr-namespace:{RootNamespace(semantics)}\".", element.NameStart, nameLength);
            }

            return;
        }

        if (ns == XamlNamespaces.Language || semantics.IsDesignOnly(element, element.Prefix))
        {
            return;
        }

        if (element.IsPropertyElement)
        {
            CheckPropertyElement(context, element, ns);
            return;
        }

        if (semantics.TypeOf(element) is not { } type)
        {
            ReportMissingType(context, element.LocalName, ns, element.NameStart, nameLength, semantics.Types.TypesIn(ns).Select(t => t.Name));
            return;
        }

        if (WpfDiagnostics.IsNotImplemented(type))
        {
            context.Add(WpfDiagnosticIds.NotImplementedApi, DiagnosticSeverityLevel.Warning, WpfDiagnostics.NotImplementedMessage(type.Name), element.NameStart, nameLength);
        }

        foreach (var attribute in element.Attributes)
        {
            CheckAttribute(context, element, type, attribute, names, cancellationToken);
        }
    }

    private static void CheckPropertyElement(Context context, XamlElement element, string ns)
    {
        var semantics = context.Semantics;
        var dot = element.LocalName.IndexOf('.', StringComparison.Ordinal);
        var ownerName = element.LocalName[..dot];
        var name = element.LocalName[(dot + 1)..];
        if (semantics.Types.FindType(ns, ownerName) is not { } owner)
        {
            ReportMissingType(context, ownerName, ns, element.NameStart, ownerName.Length + element.Prefix.Length + (element.Prefix.Length > 0 ? 1 : 0), semantics.Types.TypesIn(ns).Select(t => t.Name));
            return;
        }

        if (semantics.PropertyElement(element) is null)
        {
            ReportMissingMember(context, owner, name, element.NameStart, element.NameEnd - element.NameStart, attached: false);
        }
    }

    private static void CheckAttribute(Context context, XamlElement element, INamedTypeSymbol type, XamlAttributeNode attribute, Dictionary<string, XamlAttributeNode> names, CancellationToken cancellationToken)
    {
        var semantics = context.Semantics;
        var nameLength = attribute.NameEnd - attribute.NameStart;
        if (attribute.IsNamespaceDeclaration || attribute.Prefix == "xml" || attribute.LocalName.Length == 0)
        {
            return;
        }

        if (attribute.Prefix.Length > 0 && element.NamespaceOf(attribute.Prefix) is null)
        {
            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"The prefix '{attribute.Prefix}' is not declared: declare it on the root element.", attribute.NameStart, nameLength);
            return;
        }

        if (semantics.IsDesignOnly(element, attribute.Prefix))
        {
            return;
        }

        if (XamlSemantics.IsDirective(element, attribute))
        {
            CheckDirective(context, element, attribute, names);
            return;
        }

        if (attribute.LocalName == "Name" && attribute.Prefix.Length == 0 && attribute.HasValue)
        {
            CheckName(context, attribute, names);
        }

        var member = semantics.MemberOf(element, attribute);
        var isRootWindow = ReferenceEquals(element, semantics.Root) && XamlTypeSystem.DerivesFrom(type, semantics.Types.Window);
        if (isRootWindow && attribute.Prefix.Length == 0 && !attribute.LocalName.Contains('.', StringComparison.Ordinal)
            && (member is null || WpfDiagnostics.IsNotImplemented(member.Symbol)) && WpfWindowSettings.Note(attribute.LocalName, element) is { } note)
        {
            context.Add(WpfDiagnosticIds.WindowSettingIgnored, note.Severity, note.Message, attribute.NameStart, nameLength);
            return;
        }

        if (member is null)
        {
            var dot = attribute.LocalName.IndexOf('.', StringComparison.Ordinal);
            if (dot < 0)
            {
                ReportMissingMember(context, type, attribute.LocalName, attribute.NameStart, nameLength, attached: false);
                return;
            }

            var ownerName = attribute.Prefix.Length == 0 ? attribute.LocalName[..dot] : $"{attribute.Prefix}:{attribute.LocalName[..dot]}";
            if (semantics.TypeNamed(element, ownerName) is { } owner)
            {
                ReportMissingMember(context, owner, attribute.LocalName[(dot + 1)..], attribute.NameStart, nameLength, attached: true);
            }
            else if (element.NamespaceOf(attribute.Prefix) is { } ownerNamespace)
            {
                ReportMissingType(context, attribute.LocalName[..dot], ownerNamespace, attribute.NameStart, dot + (attribute.Prefix.Length > 0 ? attribute.Prefix.Length + 1 : 0), semantics.Types.TypesIn(ownerNamespace).Select(t => t.Name));
            }

            return;
        }

        if (WpfDiagnostics.IsNotImplemented(member.Symbol))
        {
            context.Add(WpfDiagnosticIds.NotImplementedApi, DiagnosticSeverityLevel.Warning, WpfDiagnostics.NotImplementedMessage($"{member.DeclaringType.Name}.{member.Name}"), attribute.NameStart, nameLength);
        }

        if (!attribute.HasValue)
        {
            return;
        }

        var value = attribute.Value.Trim();
        if (XamlMarkupExtension.IsExtension(attribute.RawValue))
        {
            CheckExtensions(context, element, attribute);
            return;
        }

        if (member.IsEvent)
        {
            CheckHandler(context, attribute, value);
        }
        else if (value.Length > 0)
        {
            CheckValue(context, member, attribute, value);
        }
    }

    private static void CheckDirective(Context context, XamlElement element, XamlAttributeNode attribute, Dictionary<string, XamlAttributeNode> names)
    {
        var nameLength = attribute.NameEnd - attribute.NameStart;
        if (!XamlNamespaces.Directives.Contains(attribute.LocalName))
        {
            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"{attribute.Name} is not a directive of the XAML language.", attribute.NameStart, nameLength);
        }
        else if (attribute.LocalName == "Name" && attribute.HasValue)
        {
            CheckName(context, attribute, names);
        }
        else if (attribute.LocalName is "Class" or "ClassModifier" && !ReferenceEquals(element, context.Semantics.Root))
        {
            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"{attribute.Name} goes on the root element only.", attribute.NameStart, nameLength);
        }
    }

    private static void CheckName(Context context, XamlAttributeNode attribute, Dictionary<string, XamlAttributeNode> names)
    {
        var name = attribute.Value.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (!names.TryAdd(name, attribute))
        {
            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"The name '{name}' is already used by another element in this file.", attribute.ValueStart, attribute.ValueEnd - attribute.ValueStart);
        }
        else if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name))
        {
            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"'{name}' is not a name C# can use for the element's field.", attribute.ValueStart, attribute.ValueEnd - attribute.ValueStart);
        }
    }

    private static void CheckHandler(Context context, XamlAttributeNode attribute, string handler)
    {
        if (handler.Length == 0 || context.Semantics.Class is not { } @class || HasMethod(@class, handler))
        {
            return;
        }

        var codeBehind = @class.Locations.FirstOrDefault(l => l.SourceTree?.FilePath is { Length: > 0 } path && !path.Contains(nameof(WpfXamlGenerator), StringComparison.Ordinal))?.SourceTree?.FilePath;
        var where = codeBehind is null ? @class.Name : Path.GetFileName(codeBehind);
        context.Add(
            WpfDiagnosticIds.MissingEventHandler,
            DiagnosticSeverityLevel.Error,
            $"{attribute.LocalName}=\"{handler}\" names a method {@class.Name} does not have: add it to {where}, or use the light bulb.",
            attribute.ValueStart,
            attribute.ValueEnd - attribute.ValueStart);
    }

    private static bool HasMethod(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(name).OfType<IMethodSymbol>().Any())
            {
                return true;
            }
        }

        return false;
    }

    private static void CheckValue(Context context, XamlMember member, XamlAttributeNode attribute, string value)
    {
        var type = member.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] } ? underlying : member.Type;
        var (start, length) = (attribute.ValueStart, attribute.ValueEnd - attribute.ValueStart);
        if (type.TypeKind == TypeKind.Enum && !type.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute"))
        {
            var values = type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name).ToList();
            if (!values.Contains(value, StringComparer.OrdinalIgnoreCase) && !int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"'{value}' is not a value of {type.Name}: use {string.Join(", ", values.Take(8))}{(values.Count > 8 ? ", …" : string.Empty)}.", start, length);
            }
        }
        else if (type.SpecialType == SpecialType.System_Boolean && !bool.TryParse(value, out _))
        {
            context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"'{value}' is not True or False.", start, length);
        }
    }

    /// <summary>The markup extensions of a value: their names, and the resources they look up.</summary>
    private static void CheckExtensions(Context context, XamlElement element, XamlAttributeNode attribute)
    {
        var semantics = context.Semantics;
        var raw = attribute.RawValue;
        for (var i = raw.IndexOf('{', StringComparison.Ordinal); i >= 0 && i < raw.Length; i = raw.IndexOf('{', i + 1))
        {
            var extension = XamlMarkupExtension.Parse(raw, i);
            if (extension.Name.Length == 0)
            {
                continue;
            }

            var start = attribute.ValueStart + extension.NameStart;
            var length = extension.NameEnd - extension.NameStart;
            if (element.NamespaceOf(extension.Prefix) is not { } ns)
            {
                context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"The prefix '{extension.Prefix}' is not declared: declare it on the root element.", start, length);
                continue;
            }

            if (ns == XamlNamespaces.Language ? !LanguageExtensions.Contains(extension.Name) : semantics.Types.FindType(ns, extension.Name) is null)
            {
                if (ns != XamlNamespaces.Language && WpfApiCatalog.Shipped.MissingTypesNamed(extension.Name + "Extension").Concat(WpfApiCatalog.Shipped.MissingTypesNamed(extension.Name)).Any())
                {
                    context.Add(WpfDiagnosticIds.UnsupportedApi, DiagnosticSeverityLevel.Error, WpfUnsupportedApi.Message(extension.Name), start, length);
                }
                else
                {
                    context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"The markup extension '{extension.QualifiedName}' is not found.", start, length);
                }

                continue;
            }

            if (extension.Name is "StaticResource" or "DynamicResource" && extension.Arguments.FirstOrDefault(a => a.Name is null or "ResourceKey") is { Value.Length: > 0 } key
                && !key.Value.StartsWith('{') && XamlNavigation.Resource(semantics, key.Value) is null && !semantics.HasOtherResources())
            {
                context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Warning, $"No resource has the key '{key.Value}' in this file or the application's.", attribute.ValueStart + key.ValueStart, key.Value.Length);
            }
        }
    }

    private static void ReportMissingType(Context context, string name, string ns, int start, int length, IEnumerable<string> known)
    {
        if (!ns.StartsWith(XamlNamespaces.ClrNamespacePrefix, StringComparison.Ordinal)
            && WpfApiCatalog.Shipped.MissingTypesNamed(name).Any(t => t.StartsWith("System.Windows", StringComparison.Ordinal)))
        {
            context.Add(WpfDiagnosticIds.UnsupportedApi, DiagnosticSeverityLevel.Error, WpfUnsupportedApi.Message(name), start, length);
            return;
        }

        var where = ns == XamlNamespaces.Presentation ? "WPF's controls" : ns;
        context.Add(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"The type '{name}' is not found in {where}.{Suggestion(name, known)}", start, length);
    }

    private static void ReportMissingMember(Context context, INamedTypeSymbol type, string name, int start, int length, bool attached)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var full = current.ContainingNamespace is { IsGlobalNamespace: false } ns ? $"{ns.ToDisplayString()}.{current.MetadataName}" : current.MetadataName;
            if (WpfApiCatalog.Shipped.FindMissingMember(full, name) is { } declaring)
            {
                context.Add(WpfDiagnosticIds.UnsupportedApi, DiagnosticSeverityLevel.Error, WpfUnsupportedApi.Message($"{WpfApiCatalog.SimpleName(declaring)}.{name}"), start, length);
                return;
            }
        }

        var members = attached ? context.Semantics.Types.AttachedMembers(type) : context.Semantics.Types.Members(type);
        context.Add(
            WpfDiagnosticIds.InvalidXaml,
            DiagnosticSeverityLevel.Error,
            $"{type.Name} has no {(attached ? "attached property or event" : "property or event")} named '{name}'.{Suggestion(name, members.Select(m => m.Name))}",
            start,
            length);
    }

    /// <summary>What a misspelled name probably meant: the known name closest to it, when it is close.</summary>
    private static string Suggestion(string name, IEnumerable<string> known)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in known)
        {
            var distance = Distance(name, candidate);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (candidate, distance);
            }
        }

        return best is not null && bestDistance <= Math.Max(1, name.Length / 4) ? $" Did you mean '{best}'?" : string.Empty;
    }

    /// <summary>The edit distance of two names, ignoring case.</summary>
    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToUpperInvariant(a[i - 1]) == char.ToUpperInvariant(b[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    private static string RootNamespace(XamlSemantics semantics) =>
        semantics.File.Project.GetProperty("RootNamespace") ?? semantics.File.Project.Name;

    private static int OffsetOf(string text, int line, int column)
    {
        var offset = 0;
        for (var current = 1; current < line && offset < text.Length; offset++)
        {
            if (text[offset] == '\n')
            {
                current++;
            }
        }

        return Math.Clamp(offset + Math.Max(0, column - 1), 0, text.Length);
    }

    /// <summary>The file and the diagnostics found in it so far.</summary>
    private sealed class Context(XamlSemantics semantics)
    {
        public XamlSemantics Semantics { get; } = semantics;

        public List<BuildDiagnostic> Diagnostics { get; } = [];

        public void Add(string id, DiagnosticSeverityLevel severity, string message, int start, int length)
        {
            var (line, column) = Semantics.Document.LineAndColumn(start);
            Diagnostics.Add(new BuildDiagnostic(id, severity, message, new DiagnosticLocation(Semantics.File.FilePath, start, length, line, column), Semantics.File.Project.Name));
        }
    }
}
