using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Features;
using TabStudio.Compilation.Wpf.Xaml;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>
/// The completion lists of WPF XAML, as Visual Studio has them: the elements a file can use after <c>&lt;</c> (the
/// types of every namespace it declares, and the parent's property elements), the closing tag after <c>&lt;/</c>, an
/// element's properties, events, attached properties and directives in its tag, and in a value what fits there: an
/// enum's or a bool's values, colors, fonts and the project's images, types, the code-behind's event handlers, markup
/// extensions, a binding's properties and paths, resource keys and element names.
/// </summary>
internal static class XamlCompletion
{
    private static readonly string[] FontFamilies = ["Segoe UI", "Arial", "Calibri", "Consolas", "Courier New", "Georgia", "Tahoma", "Times New Roman", "Verdana"];

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".ico"];

    /// <summary>The list at the caret, and the span it replaces.</summary>
    public static (IReadOnlyList<XamlCompletionItem> Items, int SpanStart, int SpanEnd) Complete(XamlSemantics semantics, XamlCaret caret, CancellationToken cancellationToken) =>
        caret.Kind switch
        {
            XamlCaretKind.ElementName => (Elements(semantics, caret), caret.WordStart, caret.WordEnd),
            XamlCaretKind.EndTagName => (EndTag(caret), caret.WordStart, caret.WordEnd),
            XamlCaretKind.AttributeName => (Attributes(semantics, caret), caret.WordStart, caret.WordEnd),
            XamlCaretKind.AttributeValue => Values(semantics, caret, cancellationToken),
            _ => ([], caret.Offset, caret.Offset),
        };

    private static List<XamlCompletionItem> Elements(XamlSemantics semantics, XamlCaret caret)
    {
        var element = caret.Element!;
        var typed = caret.Typed;
        var items = new List<XamlCompletionItem>();
        var dot = typed.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            if (semantics.TypeNamed(element, typed[..dot]) is { } owner)
            {
                items.AddRange(PropertyElements(semantics, owner, typed[..dot]));
            }

            return items;
        }

        foreach (var (prefix, ns) in element.NamespacesInScope())
        {
            if (ns is XamlNamespaces.Language or XamlNamespaces.MarkupCompatibility or XamlNamespaces.Designer || semantics.IsDesignOnly(element, prefix))
            {
                continue;
            }

            foreach (var type in semantics.Types.TypesIn(ns))
            {
                var name = prefix.Length == 0 ? type.Name : $"{prefix}:{type.Name}";
                items.Add(new XamlCompletionItem(name, GlyphOf(type), name) { Description = type.ContainingNamespace.ToDisplayString(), Symbol = type });
            }
        }

        if (element.Parent is { IsPropertyElement: false } parent && semantics.TypeOf(parent) is { } parentType)
        {
            items.AddRange(PropertyElements(semantics, parentType, parent.Name));
        }

        return items;
    }

    private static IEnumerable<XamlCompletionItem> PropertyElements(XamlSemantics semantics, INamedTypeSymbol owner, string ownerName) =>
        semantics.Types.Members(owner)
            .Where(m => m.Kind == XamlMemberKind.Property)
            .Select(m => new XamlCompletionItem($"{ownerName}.{m.Name}", SymbolGlyph.Property, $"{ownerName}.{m.Name}")
            {
                Description = Display(m.Type),
                Symbol = m.Symbol,
            });

    private static List<XamlCompletionItem> EndTag(XamlCaret caret)
    {
        if (caret.Element is not { } element)
        {
            return [];
        }

        var text = caret.Document.Text;
        var closed = caret.WordEnd < text.Length && text[caret.WordEnd] == '>';
        return [new XamlCompletionItem(element.Name, SymbolGlyph.Class, closed ? element.Name : element.Name + ">") { IsPreselected = true }];
    }

    private static List<XamlCompletionItem> Attributes(XamlSemantics semantics, XamlCaret caret)
    {
        var element = caret.Element!;
        if (element.IsPropertyElement || element.LocalName.Length == 0)
        {
            return [];
        }

        var typed = caret.Typed;
        var items = new List<XamlCompletionItem>();
        var set = element.Attributes.Where(a => !ReferenceEquals(a, caret.Attribute)).Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        var dot = typed.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            if (semantics.TypeNamed(element, typed[..dot]) is { } owner)
            {
                items.AddRange(semantics.Types.AttachedMembers(owner).Select(m => Attribute($"{typed[..dot]}.{m.Name}", m)));
            }

            return items;
        }

        if (semantics.TypeOf(element) is { } type)
        {
            foreach (var member in semantics.Types.Members(type))
            {
                if (!set.Contains(member.Name) && (member.IsEvent || member.Symbol is IPropertySymbol { SetMethod.DeclaredAccessibility: Accessibility.Public }))
                {
                    items.Add(Attribute(member.Name, member));
                }
            }
        }

        if (element.Parent is { IsPropertyElement: false } parent && semantics.TypeOf(parent) is { } parentType)
        {
            foreach (var member in semantics.Types.AttachedMembers(parentType))
            {
                var name = $"{parent.Name}.{member.Name}";
                if (member.Kind == XamlMemberKind.AttachedProperty && !set.Contains(name))
                {
                    items.Add(Attribute(name, member));
                }
            }
        }

        var x = semantics.LanguagePrefix;
        var isRoot = ReferenceEquals(element, semantics.Root);
        var named = set.Contains($"{x}:Name") || set.Contains("Name");
        AddDirective(items, set, x, "Name", !named, "names the element, which makes it a field of the class");
        AddDirective(items, set, x, "Key", IsInResources(element), "the key the resource is found by");
        AddDirective(items, set, x, "Class", isRoot, "the class the file's XAML is part of");
        AddDirective(items, set, x, "ClassModifier", isRoot, "the access of the class");
        AddDirective(items, set, x, "FieldModifier", !isRoot && named, "the access of the element's field");
        AddDirective(items, set, x, "Uid", true, "an id for localization");
        if (semantics.PrefixOf(XamlNamespaces.Designer) is { } d)
        {
            AddDirective(items, set, d, "DataContext", true, "the data the designer binds to");
            AddDirective(items, set, d, "DesignWidth", isRoot, "the width the designer shows the root at");
            AddDirective(items, set, d, "DesignHeight", isRoot, "the height the designer shows the root at");
        }

        if (isRoot && semantics.PrefixOf(XamlNamespaces.MarkupCompatibility) is { } mc)
        {
            AddDirective(items, set, mc, "Ignorable", true, "the prefixes compilers skip, such as d");
        }

        return items;
    }

    private static XamlCompletionItem Attribute(string name, XamlMember member) =>
        new(name, member.IsEvent ? SymbolGlyph.Event : SymbolGlyph.Property, name)
        {
            Description = member.Kind is XamlMemberKind.AttachedProperty or XamlMemberKind.AttachedEvent ? $"attached · {Display(member.Type)}" : Display(member.Type),
            Symbol = member.Symbol,
            IsAttributeName = true,
        };

    private static void AddDirective(List<XamlCompletionItem> items, HashSet<string> set, string prefix, string name, bool applies, string description)
    {
        var qualified = $"{prefix}:{name}";
        if (applies && !set.Contains(qualified))
        {
            items.Add(new XamlCompletionItem(qualified, SymbolGlyph.Keyword, qualified) { Description = description, IsAttributeName = true });
        }
    }

    private static bool IsInResources(XamlElement element) =>
        element.Parent is { } parent && (parent.LocalName.EndsWith(".Resources", StringComparison.Ordinal) || parent.LocalName == "ResourceDictionary");

    private static (IReadOnlyList<XamlCompletionItem>, int, int) Values(XamlSemantics semantics, XamlCaret caret, CancellationToken cancellationToken)
    {
        var element = caret.Element!;
        var attribute = caret.Attribute!;
        var start = attribute.ValueStart;
        var end = attribute.ValueEnd;
        var raw = attribute.RawValue;
        if (attribute.IsNamespaceDeclaration)
        {
            return (NamespaceUris(semantics), start, end);
        }

        if (XamlSemantics.IsDirective(element, attribute))
        {
            return (DirectiveValues(semantics, attribute), start, end);
        }

        if (attribute.Prefix.Length > 0 && element.NamespaceOf(attribute.Prefix) == XamlNamespaces.MarkupCompatibility)
        {
            // mc:Ignorable is a list of prefixes: complete the one at the caret.
            var wordStart = caret.Offset;
            while (wordStart > start && !char.IsWhiteSpace(caret.Document.Text[wordStart - 1]))
            {
                wordStart--;
            }

            var listed = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            return ([.. element.NamespacesInScope().Keys.Where(p => p.Length > 0 && !listed.Contains(p)).Select(p => new XamlCompletionItem(p, SymbolGlyph.Namespace, p) { Word = XamlWordKind.Name })], wordStart, caret.Offset);
        }

        if (XamlMarkupExtension.At(raw, caret.OffsetInValue) is { } extension)
        {
            return Extension(semantics, element, attribute, extension, caret.OffsetInValue, cancellationToken);
        }

        if (semantics.IsDesignOnly(element, attribute.Prefix))
        {
            return ([], start, end);
        }

        var member = semantics.MemberOf(element, attribute);
        if (member is { IsEvent: true })
        {
            return (Handlers(semantics, element, member), start, end);
        }

        ITypeSymbol? valueType = member?.Type;
        if (element.LocalName == "Setter" && semantics.TypeOf(element) is not null)
        {
            if (attribute.LocalName == "Property")
            {
                return (semantics.TargetTypeFor(element) is { } propertyTarget ? TargetProperties(semantics, propertyTarget) : [], start, end);
            }

            if (attribute.LocalName == "Value" && semantics.TargetTypeFor(element) is { } target
                && element.Attribute("Property")?.Value.Trim() is { Length: > 0 } property
                && semantics.Types.FindMember(target, property) is { } setterMember)
            {
                valueType = setterMember.Type;
            }
            else if (attribute.LocalName == "TargetName")
            {
                return (NamedElements(semantics, XamlWordKind.Value), start, end);
            }
        }

        var items = new List<XamlCompletionItem>();
        if (attribute.LocalName is "TargetType" or "DataType" || valueType?.ToDisplayString() == "System.Type")
        {
            items.AddRange(TypeNames(semantics, element, XamlWordKind.Value));
        }
        else if (valueType is not null)
        {
            items.AddRange(ValuesOf(semantics, valueType, attribute.LocalName));
        }

        items.AddRange(ExtensionStarters(semantics, element));
        return (items, start, end);
    }

    /// <summary>What can go in a value of a type: an enum's names, a bool's, named colors, fonts, images.</summary>
    private static List<XamlCompletionItem> ValuesOf(XamlSemantics semantics, ITypeSymbol type, string attributeName)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] })
        {
            type = underlying;
        }

        var items = new List<XamlCompletionItem>();
        var compilation = semantics.Types.Compilation;
        if (type.TypeKind == TypeKind.Enum)
        {
            items.AddRange(type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)
                .Select(f => new XamlCompletionItem(f.Name, SymbolGlyph.EnumMember, f.Name) { Symbol = f, Word = XamlWordKind.Value }));
            return items;
        }

        if (type.SpecialType == SpecialType.System_Boolean)
        {
            return [Keyword("True"), Keyword("False")];
        }

        var name = type.ToDisplayString();
        if (name is "System.Windows.Media.Color" || XamlTypeSystem.DerivesFrom(type, compilation.GetTypeByMetadataName("System.Windows.Media.Brush")))
        {
            if (compilation.GetTypeByMetadataName("System.Windows.Media.Colors") is { } colors)
            {
                items.AddRange(colors.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsStatic && p.DeclaredAccessibility == Accessibility.Public)
                    .Select(p => new XamlCompletionItem(p.Name, SymbolGlyph.Constant, p.Name) { Symbol = p, Word = XamlWordKind.Value }));
            }

            return items;
        }

        // A type with a class of its named values beside it: FontWeight and FontWeights, Cursor and Cursors.
        if (type is INamedTypeSymbol named && compilation.GetTypeByMetadataName($"{named.ContainingNamespace.ToDisplayString()}.{named.Name}s") is { } values)
        {
            items.AddRange(values.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && SymbolEqualityComparer.Default.Equals(p.Type, type))
                .Select(p => new XamlCompletionItem(p.Name, SymbolGlyph.Constant, p.Name) { Symbol = p, Word = XamlWordKind.Value }));
        }

        switch (type.Name)
        {
            case "FontFamily":
                items.AddRange(FontFamilies.Select(f => new XamlCompletionItem(f, SymbolGlyph.Constant, f) { Word = XamlWordKind.Value }));
                break;
            case "ImageSource" or "BitmapSource" or "BitmapImage":
                items.AddRange(Images(semantics));
                break;
            case "GridLength":
                items.AddRange([Keyword("Auto"), Keyword("*")]);
                break;
            case "Double" when attributeName is "Width" or "Height":
                items.Add(Keyword("Auto"));
                break;
        }

        return items;
    }

    private static XamlCompletionItem Keyword(string value) => new(value, SymbolGlyph.Keyword, value) { Word = XamlWordKind.Value };

    /// <summary>The image files of the project, by their paths in it, for an image's <c>Source</c>.</summary>
    private static List<XamlCompletionItem> Images(XamlSemantics semantics)
    {
        var directory = semantics.File.Project.DirectoryPath;
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => Path.GetRelativePath(directory, f).Replace('\\', '/'))
                .Where(f => !f.StartsWith("bin/", StringComparison.Ordinal) && !f.StartsWith("obj/", StringComparison.Ordinal))
                .Take(200)
                .Select(f => new XamlCompletionItem(f, SymbolGlyph.Other, f) { Description = "image", Word = XamlWordKind.Value })
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The markup extensions a value can start with, such as <c>{Binding}</c>.</summary>
    private static IEnumerable<XamlCompletionItem> ExtensionStarters(XamlSemantics semantics, XamlElement element) =>
        ExtensionNames(semantics, element).Select(name => new XamlCompletionItem(
            $"{{{name}}}",
            SymbolGlyph.Snippet,
            name.EndsWith(":Null", StringComparison.Ordinal) ? $"{{{name}}}" : $"{{{name} }}")
        {
            CaretFromEnd = name.EndsWith(":Null", StringComparison.Ordinal) ? 0 : 1,
            SortText = "~" + name,
            Description = "markup extension",
            Word = XamlWordKind.Value,
        });

    /// <summary>The markup extensions XAML here can use: Binding, the resources, x:Null and x:Type, and the project's own.</summary>
    private static List<string> ExtensionNames(XamlSemantics semantics, XamlElement element)
    {
        var names = new List<string> { "Binding", "StaticResource" };
        var presentation = element.NamespaceOf(string.Empty) ?? XamlNamespaces.Presentation;
        if (semantics.Types.FindType(presentation, "DynamicResource") is not null)
        {
            names.Add("DynamicResource");
        }

        if (IsInTemplate(element))
        {
            names.Add("TemplateBinding");
        }

        names.Add("RelativeSource");
        var x = semantics.LanguagePrefix;
        names.AddRange([$"{x}:Null", $"{x}:Type", $"{x}:Static"]);
        foreach (var (prefix, ns) in element.NamespacesInScope())
        {
            if (prefix.Length > 0 && ns.StartsWith(XamlNamespaces.ClrNamespacePrefix, StringComparison.Ordinal))
            {
                names.AddRange(semantics.Types.TypesIn(ns).Where(t => semantics.Types.IsMarkupExtension(t))
                    .Select(t => $"{prefix}:{(t.Name.EndsWith("Extension", StringComparison.Ordinal) ? t.Name[..^"Extension".Length] : t.Name)}"));
            }
        }

        return names;
    }

    private static bool IsInTemplate(XamlElement element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (current.LocalName is "ControlTemplate")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Inside a markup extension: its name, its arguments' names, or an argument's value.</summary>
    private static (IReadOnlyList<XamlCompletionItem>, int, int) Extension(
        XamlSemantics semantics, XamlElement element, XamlAttributeNode attribute, XamlMarkupExtension extension, int offset, CancellationToken cancellationToken)
    {
        var start = attribute.ValueStart;
        var raw = attribute.RawValue;
        if (offset <= extension.NameEnd)
        {
            // An extension without its closing brace yet gets one, with the caret before it.
            var open = extension.End == raw.Length && !raw.TrimEnd().EndsWith('}');
            var items = ExtensionNames(semantics, element).Select(name =>
            {
                var isNull = name.EndsWith(":Null", StringComparison.Ordinal);
                return new XamlCompletionItem(name, SymbolGlyph.Snippet, !open ? name : isNull ? name + "}" : name + " }")
                {
                    CaretFromEnd = open && !isNull ? 1 : 0,
                    Description = "markup extension",
                    Word = XamlWordKind.ExtensionPart,
                };
            }).ToList();
            return (items, start + extension.NameStart, start + extension.NameEnd);
        }

        var type = ExtensionType(semantics, element, extension);
        var index = -1;
        XamlExtensionArgument? argument = null;
        for (var i = 0; i < extension.Arguments.Count; i++)
        {
            var candidate = extension.Arguments[i];
            if (offset >= candidate.NameStart && offset <= candidate.ValueStart + candidate.Value.Length)
            {
                (argument, index) = (candidate, i);
                break;
            }
        }

        if (argument is { Name: { } name } && offset >= argument.ValueStart)
        {
            var (items, segmentStart) = ArgumentValues(semantics, element, extension, type, name, argument, offset - argument.ValueStart, cancellationToken);
            return (items, start + argument.ValueStart + segmentStart, start + argument.ValueStart + argument.Value.Length);
        }

        if (argument is null)
        {
            // Before any argument, as in "{Binding |}": a path, a key or a type, and for a binding its properties too.
            if (extension.Arguments.All(a => a.NameStart > offset))
            {
                var empty = new XamlExtensionArgument(null, offset, string.Empty, offset);
                var (first, _) = PositionalValues(semantics, element, extension, empty, 0, cancellationToken);
                return ([.. first, .. extension.Name == "Binding" || first.Count == 0 ? ArgumentNames(semantics, type, extension) : []], start + offset, start + offset);
            }

            return (ArgumentNames(semantics, type, extension), start + offset, start + offset);
        }

        var spanStart = start + argument.NameStart;
        var spanEnd = start + argument.NameStart + (argument.Name ?? argument.Value).Length;
        if (index == 0)
        {
            var (positional, segmentStart) = PositionalValues(semantics, element, extension, argument, offset - argument.ValueStart, cancellationToken);
            if (segmentStart > 0)
            {
                return (positional, spanStart + segmentStart, spanEnd);
            }

            return ([.. positional, .. extension.Name == "Binding" ? ArgumentNames(semantics, type, extension) : []], spanStart, spanEnd);
        }

        return (ArgumentNames(semantics, type, extension), spanStart, spanEnd);
    }

    private static INamedTypeSymbol? ExtensionType(XamlSemantics semantics, XamlElement element, XamlMarkupExtension extension) =>
        element.NamespaceOf(extension.Prefix) is { } ns ? semantics.Types.FindType(ns, extension.Name) : null;

    /// <summary>The settable properties of a markup extension, each with <c>=</c>.</summary>
    private static List<XamlCompletionItem> ArgumentNames(XamlSemantics semantics, INamedTypeSymbol? type, XamlMarkupExtension extension)
    {
        if (type is null)
        {
            return [];
        }

        var set = extension.Arguments.Where(a => a.Name is not null).Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);
        return [.. semantics.Types.Members(type)
            .Where(m => m.Symbol is IPropertySymbol { SetMethod.DeclaredAccessibility: Accessibility.Public } && !set.Contains(m.Name))
            .Select(m => new XamlCompletionItem(m.Name, SymbolGlyph.Property, m.Name + "=") { Description = Display(m.Type), Symbol = m.Symbol, Word = XamlWordKind.ExtensionPart })];
    }

    /// <summary>A markup extension's first argument without a name: a binding's path, a resource key, a type.</summary>
    private static (List<XamlCompletionItem> Items, int SegmentStart) PositionalValues(
        XamlSemantics semantics, XamlElement element, XamlMarkupExtension extension, XamlExtensionArgument argument, int offset, CancellationToken cancellationToken)
    {
        switch (extension.Name)
        {
            case "Binding":
                return Paths(semantics, element, argument.Value, offset, cancellationToken);
            case "StaticResource" or "DynamicResource":
                return (ResourceKeys(semantics, wrap: false, filter: null), 0);
            case "Type" when element.NamespaceOf(extension.Prefix) == XamlNamespaces.Language:
                return (TypeNames(semantics, element, XamlWordKind.ExtensionPart), 0);
            case "Reference" when element.NamespaceOf(extension.Prefix) == XamlNamespaces.Language:
                return (NamedElements(semantics, XamlWordKind.ExtensionPart), 0);
            case "TemplateBinding":
                return (semantics.TargetTypeFor(element) is { } target ? [.. TargetProperties(semantics, target).Select(i => i with { Word = XamlWordKind.ExtensionPart })] : [], 0);
            case "RelativeSource":
                return ([.. ((string[])["Self", "TemplatedParent", "FindAncestor"]).Select(m => new XamlCompletionItem(m, SymbolGlyph.EnumMember, m) { Word = XamlWordKind.ExtensionPart })], 0);
            default:
                return ([], 0);
        }
    }

    /// <summary>The value of a markup extension's named argument.</summary>
    private static (List<XamlCompletionItem> Items, int SegmentStart) ArgumentValues(
        XamlSemantics semantics, XamlElement element, XamlMarkupExtension extension, INamedTypeSymbol? type, string name, XamlExtensionArgument argument, int offset, CancellationToken cancellationToken)
    {
        switch (name)
        {
            case "Path":
                return Paths(semantics, element, argument.Value, offset, cancellationToken);
            case "ElementName":
                return (NamedElements(semantics, XamlWordKind.ExtensionPart), 0);
            case "Converter":
                return (ResourceKeys(semantics, wrap: true, filter: "System.Windows.Data.IValueConverter"), 0);
            case "ConverterParameter" or "Source" or "FallbackValue" or "TargetNullValue":
                return (ResourceKeys(semantics, wrap: true, filter: null), 0);
            case "RelativeSource":
                return ([.. ((string[])["{RelativeSource Self}", "{RelativeSource TemplatedParent}", "{RelativeSource FindAncestor, AncestorType=}"])
                    .Select(v => new XamlCompletionItem(v, SymbolGlyph.Snippet, v) { CaretFromEnd = v.EndsWith("=}", StringComparison.Ordinal) ? 1 : 0, Word = XamlWordKind.ExtensionPart })], 0);
            case "AncestorType":
                return (TypeNames(semantics, element, XamlWordKind.ExtensionPart), 0);
        }

        return type is not null && semantics.Types.FindMember(type, name) is { } member
            ? ([.. ValuesOf(semantics, member.Type, name).Select(i => i with { Word = XamlWordKind.ExtensionPart })], 0)
            : ([], 0);
    }

    /// <summary>The properties a binding's path can name at the caret: of the data type, then of each property along it.</summary>
    private static (List<XamlCompletionItem> Items, int SegmentStart) Paths(XamlSemantics semantics, XamlElement element, string path, int offset, CancellationToken cancellationToken)
    {
        if (semantics.DataTypeAt(element, cancellationToken) is not { } dataType)
        {
            return ([], 0);
        }

        var typedPath = path[..Math.Clamp(offset, 0, path.Length)];
        var lastDot = typedPath.LastIndexOf('.');
        ITypeSymbol? current = dataType;
        if (lastDot >= 0)
        {
            foreach (var segment in typedPath[..lastDot].Split('.'))
            {
                current = PublicProperties(current).FirstOrDefault(p => p.Name == segment.Trim())?.Type;
                if (current is null)
                {
                    return ([], 0);
                }
            }
        }

        var items = PublicProperties(current)
            .Select(p => new XamlCompletionItem(p.Name, SymbolGlyph.Property, p.Name) { Description = Display(p.Type), Symbol = p, Word = XamlWordKind.Name })
            .ToList();
        return (items, lastDot + 1);
    }

    private static IEnumerable<IPropertySymbol> PublicProperties(ITypeSymbol? type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public && seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    /// <summary>The resource keys of the file and the application, as <c>Key</c> or <c>{StaticResource Key}</c>.</summary>
    private static List<XamlCompletionItem> ResourceKeys(XamlSemantics semantics, bool wrap, string? filter)
    {
        var wanted = filter is null ? null : semantics.Types.Compilation.GetTypeByMetadataName(filter);
        return [.. semantics.Resources()
            .Where(r => wanted is null || r.Type is null || r.Type.AllInterfaces.Contains(wanted, SymbolEqualityComparer.Default) || XamlTypeSystem.DerivesFrom(r.Type, wanted))
            .DistinctBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new XamlCompletionItem(wrap ? $"{{StaticResource {r.Key}}}" : r.Key, SymbolGlyph.Field, wrap ? $"{{StaticResource {r.Key}}}" : r.Key)
            {
                Description = $"{r.Type?.Name ?? r.Element.LocalName} · {Path.GetFileName(r.FilePath)}",
                FilterText = r.Key,
                Word = XamlWordKind.ExtensionPart,
            })];
    }

    private static List<XamlCompletionItem> NamedElements(XamlSemantics semantics, XamlWordKind word) =>
        [.. semantics.NamedElements().DistinctBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => new XamlCompletionItem(n.Name, SymbolGlyph.Field, n.Name) { Description = n.Element.Name, Word = word })];

    /// <summary>The types XAML here can name, with their prefixes, for <c>TargetType</c>, <c>DataType</c> and <c>x:Type</c>.</summary>
    private static List<XamlCompletionItem> TypeNames(XamlSemantics semantics, XamlElement element, XamlWordKind word)
    {
        var items = new List<XamlCompletionItem>();
        foreach (var (prefix, ns) in element.NamespacesInScope())
        {
            if (ns is XamlNamespaces.Language or XamlNamespaces.MarkupCompatibility or XamlNamespaces.Designer)
            {
                continue;
            }

            foreach (var type in semantics.Types.TypesIn(ns))
            {
                var name = prefix.Length == 0 ? type.Name : $"{prefix}:{type.Name}";
                items.Add(new XamlCompletionItem(name, GlyphOf(type), name) { Description = type.ContainingNamespace.ToDisplayString(), Symbol = type, Word = word });
            }
        }

        return items;
    }

    /// <summary>The dependency properties a style for a type can set.</summary>
    private static List<XamlCompletionItem> TargetProperties(XamlSemantics semantics, INamedTypeSymbol target) =>
        [.. semantics.Types.Members(target)
            .Where(m => m.Kind == XamlMemberKind.Property && m.Symbol is IPropertySymbol { SetMethod.DeclaredAccessibility: Accessibility.Public })
            .Select(m => new XamlCompletionItem(m.Name, SymbolGlyph.Property, m.Name) { Description = Display(m.Type), Symbol = m.Symbol, Word = XamlWordKind.Value })];

    /// <summary>The code-behind's methods that can handle an event, and a new handler's usual name.</summary>
    private static List<XamlCompletionItem> Handlers(XamlSemantics semantics, XamlElement element, XamlMember member)
    {
        var items = new List<XamlCompletionItem>();
        if (semantics.Class is not { } @class)
        {
            return items;
        }

        var parameters = (member.Type as INamedTypeSymbol)?.DelegateInvokeMethod?.Parameters.Length ?? 2;
        foreach (var method in @class.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind == MethodKind.Ordinary && method.ReturnsVoid && !method.IsStatic && method.Parameters.Length == parameters && !method.IsImplicitlyDeclared)
            {
                items.Add(new XamlCompletionItem(method.Name, SymbolGlyph.Method, method.Name)
                {
                    Description = string.Join(", ", method.Parameters.Select(p => p.Type.Name)),
                    Symbol = method,
                    Word = XamlWordKind.Value,
                });
            }
        }

        var owner = (element.Attribute(XamlNamespaces.Language, "Name") ?? element.Attribute("Name"))?.Value.Trim() is { Length: > 0 } name ? name : element.LocalName;
        var suggested = $"{owner}_{member.Name}";
        if (!items.Exists(i => i.DisplayText == suggested))
        {
            items.Add(new XamlCompletionItem(suggested, SymbolGlyph.Method, suggested) { Description = "new handler", IsPreselected = items.Count == 0, Word = XamlWordKind.Value });
        }

        return items;
    }

    /// <summary>The namespaces an <c>xmlns</c> can declare: XAML's own, the presentation one and the project's.</summary>
    private static List<XamlCompletionItem> NamespaceUris(XamlSemantics semantics)
    {
        var uris = new List<string>(semantics.Types.DefinedNamespaces) { XamlNamespaces.Presentation, XamlNamespaces.Language, XamlNamespaces.Designer, XamlNamespaces.MarkupCompatibility };
        var items = uris.Distinct(StringComparer.Ordinal).Select(u => new XamlCompletionItem(u, SymbolGlyph.Namespace, u) { Word = XamlWordKind.Value }).ToList();
        var pending = new Stack<INamespaceSymbol>([semantics.Types.Compilation.Assembly.GlobalNamespace]);
        while (pending.TryPop(out var ns))
        {
            if (!ns.IsGlobalNamespace && ns.GetTypeMembers().Length > 0)
            {
                var value = XamlNamespaces.ClrNamespacePrefix + ns.ToDisplayString();
                items.Add(new XamlCompletionItem(value, SymbolGlyph.Namespace, value) { Description = "this project", Word = XamlWordKind.Value });
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                pending.Push(child);
            }
        }

        return items;
    }

    private static List<XamlCompletionItem> DirectiveValues(XamlSemantics semantics, XamlAttributeNode attribute)
    {
        switch (attribute.LocalName)
        {
            case "Class":
                var project = semantics.File.Project;
                var relative = Path.GetRelativePath(project.DirectoryPath, semantics.File.FilePath);
                var folders = Path.GetDirectoryName(relative)?.Replace(Path.DirectorySeparatorChar, '.').Replace('/', '.') is { Length: > 0 } f ? "." + f : string.Empty;
                var name = $"{project.GetProperty("RootNamespace") ?? project.Name}{folders}.{Path.GetFileNameWithoutExtension(relative)}";
                return [new XamlCompletionItem(name, SymbolGlyph.Class, name) { IsPreselected = true, Word = XamlWordKind.Value }];
            case "FieldModifier":
                return [.. ((string[])["private", "internal", "protected", "public"]).Select(Keyword)];
            case "ClassModifier":
                return [.. ((string[])["public", "internal"]).Select(Keyword)];
            default:
                return [];
        }
    }

    private static SymbolGlyph GlyphOf(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Struct => SymbolGlyph.Struct,
        TypeKind.Enum => SymbolGlyph.Enum,
        TypeKind.Interface => SymbolGlyph.Interface,
        TypeKind.Delegate => SymbolGlyph.Delegate,
        _ => SymbolGlyph.Class,
    };

    private static string Display(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
}
