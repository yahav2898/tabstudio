using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Compilation.Wpf.Xaml;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>
/// What the names in one XAML file of a WPF project stand for: the type of each element, the member each attribute
/// sets, the class its <c>x:Class</c> names, its named elements and the resources it and <c>App.xaml</c> define.
/// </summary>
internal sealed class XamlSemantics
{
    private XamlSemantics(WpfXamlFile file, XamlDocument document)
    {
        File = file;
        Document = document;
        Types = file.Types;
        Root = document.Root;
        LanguagePrefix = PrefixOf(XamlNamespaces.Language) ?? "x";
        Ignorable = Root is null ? new HashSet<string>(StringComparer.Ordinal) : XamlNamespaces.IgnorablePrefixes(Root);
    }

    /// <summary>The file.</summary>
    public WpfXamlFile File { get; }

    /// <summary>Its parsed text.</summary>
    public XamlDocument Document { get; }

    /// <summary>The types of its project.</summary>
    public XamlTypeSystem Types { get; }

    /// <summary>Its root element.</summary>
    public XamlElement? Root { get; }

    /// <summary>The prefix the file gives the XAML language namespace, <c>x</c> as a rule.</summary>
    public string LanguagePrefix { get; }

    /// <summary>The prefixes <c>mc:Ignorable</c> names, such as <c>d</c>.</summary>
    public IReadOnlySet<string> Ignorable { get; }

    /// <summary>Reads a file.</summary>
    public static XamlSemantics For(WpfXamlFile file) => new(file, XamlDocument.Parse(file.Text));

    /// <summary>The prefix the root declares for a namespace, or <see langword="null"/>.</summary>
    public string? PrefixOf(string xmlNamespace) =>
        Root?.NamespacesInScope().FirstOrDefault(p => p.Value == xmlNamespace && p.Key.Length > 0).Key;

    /// <summary>Whether a prefix's namespace is one compilers skip or one that is not about types, such as <c>d</c> or <c>mc</c>.</summary>
    public bool IsDesignOnly(XamlElement scope, string prefix) =>
        prefix.Length > 0 && (Ignorable.Contains(prefix) || scope.NamespaceOf(prefix) is XamlNamespaces.MarkupCompatibility or XamlNamespaces.Designer);

    /// <summary>The type of an element, or <see langword="null"/> for a property element, a directive or a name that is not found.</summary>
    public INamedTypeSymbol? TypeOf(XamlElement element)
    {
        if (element.IsPropertyElement || element.LocalName.Length == 0 || element.NamespaceOf(element.Prefix) is not { } ns || ns == XamlNamespaces.Language)
        {
            return null;
        }

        return Types.FindType(ns, element.LocalName);
    }

    /// <summary>A type named in XAML, such as <c>Button</c> or <c>local:ContactCard</c>, as seen from an element.</summary>
    public INamedTypeSymbol? TypeNamed(XamlElement scope, string qualifiedName)
    {
        var name = qualifiedName.Trim();
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        var prefix = colon < 0 ? string.Empty : name[..colon];
        var local = colon < 0 ? name : name[(colon + 1)..];
        return local.Length > 0 && scope.NamespaceOf(prefix) is { } ns && ns != XamlNamespaces.Language ? Types.FindType(ns, local) : null;
    }

    /// <summary>
    /// The member a property element sets, such as <c>Grid.RowDefinitions</c>, with the type that owns it.
    /// </summary>
    public (INamedTypeSymbol Owner, XamlMember Member)? PropertyElement(XamlElement element)
    {
        if (!element.IsPropertyElement)
        {
            return null;
        }

        var dot = element.LocalName.IndexOf('.', StringComparison.Ordinal);
        if (TypeNamed(element, element.Prefix.Length == 0 ? element.LocalName[..dot] : $"{element.Prefix}:{element.LocalName[..dot]}") is not { } owner)
        {
            return null;
        }

        var name = element.LocalName[(dot + 1)..];
        return (Types.FindMember(owner, name) ?? Types.FindAttachedMember(owner, name)) is { } member ? (owner, member) : null;
    }

    /// <summary>
    /// The member an attribute sets: a property or event of the element's type, or an attached one such as
    /// <c>Grid.Row</c>; <see langword="null"/> for directives, namespace declarations and names not found.
    /// </summary>
    public XamlMember? MemberOf(XamlElement element, XamlAttributeNode attribute)
    {
        if (attribute.IsNamespaceDeclaration || attribute.LocalName.Length == 0 || attribute.Prefix == "xml" || IsDesignOnly(element, attribute.Prefix)
            || (attribute.Prefix.Length > 0 && element.NamespaceOf(attribute.Prefix) == XamlNamespaces.Language))
        {
            return null;
        }

        var dot = attribute.LocalName.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            var ownerName = attribute.Prefix.Length == 0 ? attribute.LocalName[..dot] : $"{attribute.Prefix}:{attribute.LocalName[..dot]}";
            var name = attribute.LocalName[(dot + 1)..];
            return TypeNamed(element, ownerName) is { } owner ? Types.FindAttachedMember(owner, name) ?? Types.FindMember(owner, name) : null;
        }

        return TypeOf(element) is { } type ? Types.FindMember(type, attribute.LocalName) : null;
    }

    /// <summary>Whether an attribute is a directive of the XAML language, such as <c>x:Name</c>.</summary>
    public static bool IsDirective(XamlElement element, XamlAttributeNode attribute) =>
        attribute.Prefix.Length > 0 && element.NamespaceOf(attribute.Prefix) == XamlNamespaces.Language;

    /// <summary>The class the root's <c>x:Class</c> names, which holds the event handlers.</summary>
    public INamedTypeSymbol? Class =>
        Root?.Attribute(XamlNamespaces.Language, "Class")?.Value is { Length: > 0 } name ? Types.Compilation.GetTypeByMetadataName(name.Trim()) : null;

    /// <summary>The type a style, template or setter in it is for, from the <c>TargetType</c> of it or an element around it.</summary>
    public INamedTypeSymbol? TargetTypeFor(XamlElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.Attribute("TargetType")?.Value is { Length: > 0 } target)
            {
                return TypeValue(current, target);
            }
        }

        return null;
    }

    /// <summary>A type an attribute's value names, as <c>Button</c>, <c>local:Card</c> or <c>{x:Type Button}</c>.</summary>
    public INamedTypeSymbol? TypeValue(XamlElement scope, string value)
    {
        var trimmed = value.Trim();
        if (XamlMarkupExtension.IsExtension(trimmed))
        {
            var extension = XamlMarkupExtension.Parse(trimmed, trimmed.IndexOf('{', StringComparison.Ordinal));
            return extension.Name is "Type" && extension.Arguments is [{ } argument, ..] ? TypeNamed(scope, argument.Name is "TypeName" or null ? argument.Value : string.Empty) : null;
        }

        return TypeNamed(scope, trimmed);
    }

    /// <summary>The elements with an <c>x:Name</c> or a <c>Name</c>, by name.</summary>
    public IEnumerable<(string Name, XamlElement Element, XamlAttributeNode Attribute)> NamedElements()
    {
        foreach (var element in Document.Elements)
        {
            if ((element.Attribute(XamlNamespaces.Language, "Name") ?? element.Attribute("Name")) is { HasValue: true } attribute && attribute.Value.Trim() is { Length: > 0 } name)
            {
                yield return (name, element, attribute);
            }
        }
    }

    /// <summary>
    /// The resources this file and the project's application file define: every element with an <c>x:Key</c>, and a
    /// style without one under its target type.
    /// </summary>
    public IEnumerable<XamlResource> Resources()
    {
        foreach (var resource in ResourcesIn(File.FilePath, Document))
        {
            yield return resource;
        }

        foreach (var (path, text) in File.ProjectXaml)
        {
            if (!string.Equals(path, File.FilePath, StringComparison.Ordinal) && WpfProject.XamlItems(File.Project).Any(i => i.Kind == XamlItemKind.ApplicationDefinition && string.Equals(i.FilePath, path, StringComparison.Ordinal)))
            {
                foreach (var resource in ResourcesIn(path, XamlDocument.Parse(text)))
                {
                    yield return resource;
                }
            }
        }
    }

    /// <summary>Whether a resource could also come from somewhere TabStudio does not read: a merged or external dictionary.</summary>
    public bool HasOtherResources() =>
        File.ProjectXaml.Values.Any(text => text.Contains("MergedDictionaries", StringComparison.Ordinal) || text.Contains("ResourceDictionary Source", StringComparison.Ordinal));

    /// <summary>
    /// The data type bindings at an element read from: a <c>DataTemplate</c>'s <c>DataType</c>, a <c>d:DataContext</c>
    /// or a <c>DataContext</c> property element, or outside templates what the code-behind sets <c>DataContext</c> to.
    /// </summary>
    public INamedTypeSymbol? DataTypeAt(XamlElement element, CancellationToken cancellationToken)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.LocalName == "DataTemplate" && current.Attribute("DataType")?.Value is { Length: > 0 } dataType)
            {
                return TypeValue(current, dataType);
            }

            foreach (var attribute in current.Attributes)
            {
                if (attribute.LocalName == "DataContext" && attribute.Prefix.Length > 0 && IsDesignOnly(current, attribute.Prefix) && XamlMarkupExtension.IsExtension(attribute.Value))
                {
                    var extension = XamlMarkupExtension.Parse(attribute.Value, attribute.Value.IndexOf('{', StringComparison.Ordinal));
                    if (extension.Name == "DesignInstance" && extension.Arguments.FirstOrDefault(a => a.Name is null or "Type") is { } type)
                    {
                        return TypeNamed(current, type.Value);
                    }
                }
            }

            foreach (var child in current.Children)
            {
                if (child.IsPropertyElement && child.LocalName.EndsWith(".DataContext", StringComparison.Ordinal) && child.Children is [{ } instance, ..] && TypeOf(instance) is { } type)
                {
                    return type;
                }
            }

            if (current.LocalName is "DataTemplate" or "ControlTemplate" or "HierarchicalDataTemplate")
            {
                return null;
            }
        }

        return CodeBehindDataContext(cancellationToken);
    }

    /// <summary>What the code-behind sets <c>DataContext</c> to, as in <c>DataContext = new MainViewModel();</c>.</summary>
    private INamedTypeSymbol? CodeBehindDataContext(CancellationToken cancellationToken)
    {
        if (Class is not { } @class)
        {
            return null;
        }

        foreach (var reference in @class.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);
            foreach (var assignment in syntax.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var target = assignment.Left switch
                {
                    IdentifierNameSyntax name => name.Identifier.ValueText,
                    MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText,
                    _ => null,
                };
                if (target == "DataContext" && assignment.Right is BaseObjectCreationExpressionSyntax creation
                    && Types.Compilation.GetSemanticModel(reference.SyntaxTree).GetTypeInfo(creation, cancellationToken).Type is INamedTypeSymbol type)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private IEnumerable<XamlResource> ResourcesIn(string path, XamlDocument document)
    {
        foreach (var element in document.Elements)
        {
            if (element.Attribute(XamlNamespaces.Language, "Key") is { HasValue: true } key && key.Value.Trim() is { Length: > 0 } name)
            {
                yield return new XamlResource(name, path, element, key, TypeOf(element));
            }
        }
    }
}

/// <summary>A resource a XAML file defines.</summary>
/// <param name="Key">Its key.</param>
/// <param name="FilePath">The file it is in.</param>
/// <param name="Element">Its element.</param>
/// <param name="KeyAttribute">Its <c>x:Key</c>.</param>
/// <param name="Type">Its type, or <see langword="null"/> when it is not found.</param>
internal sealed record XamlResource(string Key, string FilePath, XamlElement Element, XamlAttributeNode KeyAttribute, INamedTypeSymbol? Type);
