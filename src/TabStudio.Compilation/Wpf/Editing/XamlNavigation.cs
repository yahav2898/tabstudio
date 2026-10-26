using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Features;
using TabStudio.Compilation.Wpf.Xaml;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>A place in a XAML file a name stands for, such as the element of a resource or of a named element.</summary>
/// <param name="FilePath">The file.</param>
/// <param name="Start">The offset of the name there, such as an <c>x:Key</c>'s value.</param>
/// <param name="Length">Its length.</param>
/// <param name="Kind">What it is, such as <c>resource</c>.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">What it is, as Quick Info says it, such as <c>SolidColorBrush in App.xaml</c>.</param>
internal sealed record XamlPlace(string FilePath, int Start, int Length, string Kind, string Name, string Description);

/// <summary>What a name in XAML stands for: a type or member of the project's compilation, or a place in XAML.</summary>
/// <param name="SpanStart">The start of the name in the file.</param>
/// <param name="SpanLength">Its length.</param>
/// <param name="Symbol">The type or member, or <see langword="null"/>.</param>
/// <param name="Place">The place in XAML, or <see langword="null"/>.</param>
/// <param name="Attached">Whether the symbol is the setter of an attached property or event, such as <c>Grid.SetRow</c>.</param>
internal sealed record XamlTarget(int SpanStart, int SpanLength, ISymbol? Symbol, XamlPlace? Place, bool Attached = false);

/// <summary>
/// What a name in WPF XAML stands for, for Go to Definition and Quick Info: an element's type, an attribute's
/// property or event, an event handler in the code-behind, the class of <c>x:Class</c>, a type in a value, a
/// binding's path, a resource key and an element name.
/// </summary>
internal static class XamlNavigation
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat MemberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>What the name at the caret stands for, or <see langword="null"/>.</summary>
    public static XamlTarget? TargetAt(XamlSemantics semantics, XamlCaret caret, CancellationToken cancellationToken)
    {
        var element = caret.Element;
        switch (caret.Kind)
        {
            case XamlCaretKind.ElementName or XamlCaretKind.EndTagName when element is not null:
                var symbol = element.IsPropertyElement ? semantics.PropertyElement(element)?.Member.Symbol : semantics.TypeOf(element);
                return symbol is null ? null : new XamlTarget(caret.WordStart, caret.WordEnd - caret.WordStart, symbol, null);

            case XamlCaretKind.AttributeName when element is not null && caret.Attribute is { } attribute:
                return semantics.MemberOf(element, attribute) is { } member
                    ? new XamlTarget(attribute.NameStart, attribute.NameEnd - attribute.NameStart, member.Symbol, null, member.Kind is XamlMemberKind.AttachedProperty or XamlMemberKind.AttachedEvent)
                    : null;

            case XamlCaretKind.AttributeValue when element is not null && caret.Attribute is { } attribute:
                return InValue(semantics, element, attribute, caret.OffsetInValue, cancellationToken);

            default:
                return null;
        }
    }

    private static XamlTarget? InValue(XamlSemantics semantics, XamlElement element, XamlAttributeNode attribute, int offset, CancellationToken cancellationToken)
    {
        var start = attribute.ValueStart;
        var value = attribute.Value;
        var trimmed = value.Trim();
        var whole = (start + (attribute.RawValue.Length - attribute.RawValue.TrimStart().Length), trimmed.Length);
        if (attribute.IsNamespaceDeclaration)
        {
            return null;
        }

        if (XamlSemantics.IsDirective(element, attribute))
        {
            return attribute.LocalName == "Class" && semantics.Class is { } @class ? Symbol(whole, @class) : null;
        }

        if (XamlMarkupExtension.At(attribute.RawValue, offset) is { } extension)
        {
            return InExtension(semantics, element, attribute, extension, offset, cancellationToken);
        }

        if (semantics.MemberOf(element, attribute) is { } member)
        {
            if (member.IsEvent)
            {
                return semantics.Class?.GetMembers(trimmed).OfType<IMethodSymbol>().FirstOrDefault() is { } handler ? Symbol(whole, handler) : null;
            }

            if (member.Type.TypeKind == TypeKind.Enum && member.Type.GetMembers(trimmed).FirstOrDefault() is { } field)
            {
                return Symbol(whole, field);
            }

            if (attribute.LocalName is "TargetType" or "DataType" || member.Type.ToDisplayString() == "System.Type")
            {
                return semantics.TypeValue(element, trimmed) is { } type ? Symbol(whole, type) : null;
            }
        }

        if (element.LocalName == "Setter")
        {
            if (attribute.LocalName == "Property" && semantics.TargetTypeFor(element) is { } target && semantics.Types.FindMember(target, trimmed) is { } property)
            {
                return Symbol(whole, property.Symbol);
            }

            if (attribute.LocalName == "TargetName" && Named(semantics, trimmed) is { } place)
            {
                return new XamlTarget(whole.Item1, whole.Item2, null, place);
            }
        }

        return null;
    }

    private static XamlTarget? InExtension(
        XamlSemantics semantics, XamlElement element, XamlAttributeNode attribute, XamlMarkupExtension extension, int offset, CancellationToken cancellationToken)
    {
        var start = attribute.ValueStart;
        var type = element.NamespaceOf(extension.Prefix) is { } ns ? semantics.Types.FindType(ns, extension.Name) : null;
        if (offset <= extension.NameEnd)
        {
            return type is null ? null : new XamlTarget(start + extension.NameStart, extension.NameEnd - extension.NameStart, type, null);
        }

        for (var i = 0; i < extension.Arguments.Count; i++)
        {
            var argument = extension.Arguments[i];
            var valueSpan = (start + argument.ValueStart, argument.Value.Length);
            if (argument.Name is { } name && offset >= argument.NameStart && offset <= argument.NameStart + name.Length)
            {
                return type is not null && semantics.Types.FindMember(type, name) is { } member
                    ? new XamlTarget(start + argument.NameStart, name.Length, member.Symbol, null)
                    : null;
            }

            if (offset < argument.ValueStart || offset > argument.ValueStart + argument.Value.Length)
            {
                continue;
            }

            var key = argument.Name ?? (i == 0 ? Positional(extension) : null);
            switch (key)
            {
                case "ResourceKey":
                    return Resource(semantics, argument.Value) is { } resource ? new XamlTarget(valueSpan.Item1, valueSpan.Item2, null, resource) : null;
                case "ElementName" or "Reference":
                    return Named(semantics, argument.Value) is { } named ? new XamlTarget(valueSpan.Item1, valueSpan.Item2, null, named) : null;
                case "Path":
                    return PathSegment(semantics, element, argument, offset, start, cancellationToken);
                case "TypeName" or "AncestorType":
                    return semantics.TypeNamed(element, argument.Value) is { } named2 ? new XamlTarget(valueSpan.Item1, valueSpan.Item2, named2, null) : null;
                case "Property":
                    return semantics.TargetTypeFor(element) is { } target && semantics.Types.FindMember(target, argument.Value) is { } property
                        ? new XamlTarget(valueSpan.Item1, valueSpan.Item2, property.Symbol, null)
                        : null;
            }

            return null;
        }

        return null;
    }

    /// <summary>What a markup extension's first argument without a name sets.</summary>
    private static string? Positional(XamlMarkupExtension extension) => extension.Name switch
    {
        "Binding" => "Path",
        "StaticResource" or "DynamicResource" => "ResourceKey",
        "Type" => "TypeName",
        "Reference" => "Reference",
        "TemplateBinding" => "Property",
        _ => null,
    };

    private static XamlTarget? PathSegment(XamlSemantics semantics, XamlElement element, XamlExtensionArgument argument, int offset, int valueStart, CancellationToken cancellationToken)
    {
        if (semantics.DataTypeAt(element, cancellationToken) is not { } dataType)
        {
            return null;
        }

        ITypeSymbol? current = dataType;
        var segmentStart = argument.ValueStart;
        foreach (var segment in argument.Value.Split('.'))
        {
            var property = Properties(current).FirstOrDefault(p => p.Name == segment.Trim());
            if (offset <= segmentStart + segment.Length)
            {
                return property is null ? null : new XamlTarget(valueStart + segmentStart, segment.Length, property, null);
            }

            current = property?.Type;
            segmentStart += segment.Length + 1;
        }

        return null;
    }

    private static IEnumerable<IPropertySymbol> Properties(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && property.DeclaredAccessibility == Accessibility.Public)
                {
                    yield return property;
                }
            }
        }
    }

    private static XamlTarget Symbol((int Start, int Length) span, ISymbol symbol) => new(span.Start, span.Length, symbol, null);

    /// <summary>The resource with a key, in the file or the application.</summary>
    public static XamlPlace? Resource(XamlSemantics semantics, string key)
    {
        var resource = semantics.Resources().FirstOrDefault(r => r.Key == key.Trim());
        return resource is null
            ? null
            : new XamlPlace(
                resource.FilePath,
                resource.KeyAttribute.ValueStart,
                resource.KeyAttribute.ValueEnd - resource.KeyAttribute.ValueStart,
                "resource",
                resource.Key,
                $"{resource.Type?.Name ?? resource.Element.LocalName} in {Path.GetFileName(resource.FilePath)}");
    }

    private static XamlPlace? Named(XamlSemantics semantics, string name)
    {
        var found = semantics.NamedElements().FirstOrDefault(n => n.Name == name.Trim());
        return found.Element is null
            ? null
            : new XamlPlace(semantics.File.FilePath, found.Attribute.ValueStart, found.Attribute.ValueEnd - found.Attribute.ValueStart, "element", found.Name, found.Element.Name);
    }

    /// <summary>Where a target is defined, for Go to Definition.</summary>
    public static DefinitionResult Definition(XamlSemantics semantics, XamlTarget target)
    {
        if (target.Place is { } place)
        {
            var text = semantics.File.ProjectXaml.GetValueOrDefault(place.FilePath) ?? semantics.File.Text;
            var (line, column) = XamlDocument.Parse(text).LineAndColumn(place.Start);
            return new DefinitionResult($"{place.Kind} {place.Name}", SymbolGlyph.Field, [new SourceLocation(place.FilePath, place.Start, place.Length, line, column)], null);
        }

        var symbol = target.Symbol!;
        var locations = symbol.Locations
            .Where(l => l.SourceTree is { } tree && !IsGenerated(semantics, tree))
            .Select(SymbolResolver.ToSourceLocation)
            .OfType<SourceLocation>()
            .ToList();
        if (locations.Count == 0 && symbol is INamedTypeSymbol type && XamlOf(semantics, type) is { } xaml)
        {
            locations.Add(xaml);
        }

        return new DefinitionResult(symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), GlyphMapper.FromSymbol(symbol), locations, symbol.ContainingAssembly?.Name);
    }

    /// <summary>The Quick Info of a target: its declaration and summary, and whether OpenSilver implements it.</summary>
    public static QuickInfoResult QuickInfo(XamlSemantics semantics, XamlTarget target, CancellationToken cancellationToken)
    {
        if (target.Place is { } place)
        {
            List<TaggedRun> runs = [new(place.Kind, TaggedTextKind.Keyword), new(" ", TaggedTextKind.Space), new(place.Name, TaggedTextKind.Field), new(" · " + place.Description, TaggedTextKind.Text)];
            return new QuickInfoResult(target.SpanStart, target.SpanLength, SymbolGlyph.Field, [new QuickInfoSection(QuickInfoSectionKind.Description, runs)]);
        }

        var symbol = target.Symbol!;
        var sections = new List<QuickInfoSection> { new(QuickInfoSectionKind.Description, Describe(symbol, target.Attached)) };
        if (XamlDocumentation.Summary(symbol, semantics.Types.Compilation, cancellationToken) is { Length: > 0 } summary)
        {
            sections.Add(new QuickInfoSection(QuickInfoSectionKind.Documentation, [new TaggedRun(summary, TaggedTextKind.Text)]));
        }

        if (WpfDiagnostics.IsNotImplemented(symbol))
        {
            sections.Add(new QuickInfoSection(QuickInfoSectionKind.Remarks, [new TaggedRun("OpenSilver declares this but does not implement it, so it does nothing in the web preview.", TaggedTextKind.Text)]));
        }

        return new QuickInfoResult(target.SpanStart, target.SpanLength, GlyphMapper.FromSymbol(symbol), sections);
    }

    /// <summary>The declaration of a symbol as Quick Info and completion tooltips show it.</summary>
    public static List<TaggedRun> Describe(ISymbol symbol, bool attached = false)
    {
        if (attached && symbol is IMethodSymbol { Parameters.Length: > 0 } setter)
        {
            var name = setter.Name.StartsWith("Add", StringComparison.Ordinal) ? setter.Name[3..^"Handler".Length] : setter.Name[3..];
            var type = setter.Name.StartsWith("Get", StringComparison.Ordinal) ? setter.ReturnType : setter.Parameters[^1].Type;
            return
            [
                new("attached", TaggedTextKind.Keyword), new(" ", TaggedTextKind.Space),
                .. TaggedTextMapper.Map(type.ToDisplayParts(SymbolDisplayFormat.MinimallyQualifiedFormat)), new(" ", TaggedTextKind.Space),
                new(setter.ContainingType.Name, TaggedTextKind.Class), new(".", TaggedTextKind.Punctuation), new(name, TaggedTextKind.Property),
            ];
        }

        return TaggedTextMapper.Map(symbol.ToDisplayParts(symbol is INamedTypeSymbol ? TypeFormat : MemberFormat));
    }

    private static bool IsGenerated(XamlSemantics semantics, SyntaxTree tree) =>
        tree.FilePath.Contains(nameof(WpfXamlGenerator), StringComparison.Ordinal) || WpfProject.SecondPassSource(semantics.File.Project, tree.FilePath) is not null;

    /// <summary>The root of the XAML file whose <c>x:Class</c> is a type, for a class declared in XAML alone.</summary>
    private static SourceLocation? XamlOf(XamlSemantics semantics, INamedTypeSymbol type)
    {
        var name = type.ToDisplayString();
        foreach (var (path, text) in semantics.File.ProjectXaml)
        {
            var document = XamlDocument.Parse(text);
            if (document.Root?.Attribute(XamlNamespaces.Language, "Class") is { } attribute && attribute.Value.Trim() == name)
            {
                var (line, column) = document.LineAndColumn(document.Root.NameStart);
                return new SourceLocation(path, document.Root.NameStart, document.Root.NameEnd - document.Root.NameStart, line, column);
            }
        }

        return null;
    }
}
