using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Wpf.Xaml;

/// <summary>What a XAML member is.</summary>
public enum XamlMemberKind
{
    /// <summary>A property of the element's type, set by an attribute or a property element.</summary>
    Property,

    /// <summary>An event of the element's type, whose attribute names a handler in the code-behind.</summary>
    Event,

    /// <summary>A property another type defines for any element, such as <c>Grid.Row</c>.</summary>
    AttachedProperty,

    /// <summary>An event another type defines for any element.</summary>
    AttachedEvent,
}

/// <summary>A property or event XAML can set.</summary>
/// <param name="Name">The name, such as <c>Content</c> or <c>Row</c>.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Symbol">The property, event or attached member's setter.</param>
/// <param name="Type">The type of its values, or the handler's delegate type for an event.</param>
/// <param name="DeclaringType">The type that declares it.</param>
public sealed record XamlMember(string Name, XamlMemberKind Kind, ISymbol Symbol, ITypeSymbol Type, INamedTypeSymbol DeclaringType)
{
    /// <summary>Whether it is an event.</summary>
    public bool IsEvent => Kind is XamlMemberKind.Event or XamlMemberKind.AttachedEvent;
}

/// <summary>A .NET namespace a XAML namespace stands for.</summary>
/// <param name="Namespace">The .NET namespace, such as <c>System.Windows.Controls</c>.</param>
/// <param name="Assembly">The assembly it is in, or <see langword="null"/> for any assembly the project compiles against.</param>
public sealed record XamlClrNamespace(string Namespace, IAssemblySymbol? Assembly);

/// <summary>
/// The types and members of XAML, from the project's compilation: which .NET namespaces a XAML namespace stands for
/// (the <c>[XmlnsDefinition]</c> attributes of the assemblies, <c>clr-namespace:</c> and <c>using:</c>), the type of
/// an element, and the properties, events and attached members its attributes can set.
/// </summary>
/// <remarks>One instance per compilation, from <see cref="For"/>; lookups are cached, and safe from any thread.</remarks>
public sealed class XamlTypeSystem
{
    private static readonly ConditionalWeakTable<RoslynCompilation, XamlTypeSystem> Instances = new();

    private readonly Dictionary<string, List<XamlClrNamespace>> _definitions;
    private readonly ConcurrentDictionary<(string, string), INamedTypeSymbol?> _types = new();
    private readonly ConcurrentDictionary<INamedTypeSymbol, IReadOnlyList<XamlMember>> _members = new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<INamedTypeSymbol, IReadOnlyList<XamlMember>> _attached = new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<string, IReadOnlyList<INamedTypeSymbol>> _namespaceTypes = new(StringComparer.Ordinal);

    private XamlTypeSystem(RoslynCompilation compilation)
    {
        Compilation = compilation;
        _definitions = ReadDefinitions(compilation);
        DependencyObject = compilation.GetTypeByMetadataName("System.Windows.DependencyObject");
        UIElement = compilation.GetTypeByMetadataName("System.Windows.UIElement");
        MarkupExtension = compilation.GetTypeByMetadataName("System.Windows.Markup.MarkupExtension")
            ?? compilation.GetTypeByMetadataName("System.Xaml.MarkupExtension");
        Window = compilation.GetTypeByMetadataName("System.Windows.Window");
        Application = compilation.GetTypeByMetadataName("System.Windows.Application");
    }

    /// <summary>The compilation the types come from.</summary>
    public RoslynCompilation Compilation { get; }

    /// <summary><c>System.Windows.DependencyObject</c>, or <see langword="null"/> when the project does not reference WPF's API.</summary>
    public INamedTypeSymbol? DependencyObject { get; }

    /// <summary><c>System.Windows.UIElement</c>.</summary>
    public INamedTypeSymbol? UIElement { get; }

    /// <summary>The base class of markup extensions such as <c>Binding</c> and <c>StaticResource</c>.</summary>
    public INamedTypeSymbol? MarkupExtension { get; }

    /// <summary><c>System.Windows.Window</c>.</summary>
    public INamedTypeSymbol? Window { get; }

    /// <summary><c>System.Windows.Application</c>.</summary>
    public INamedTypeSymbol? Application { get; }

    /// <summary>The XAML namespaces the referenced assemblies define, such as the presentation namespace.</summary>
    public IReadOnlyCollection<string> DefinedNamespaces => _definitions.Keys;

    /// <summary>The type system of a compilation, made once per compilation.</summary>
    /// <param name="compilation">The project's compilation.</param>
    public static XamlTypeSystem For(RoslynCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        return Instances.GetValue(compilation, static c => new XamlTypeSystem(c));
    }

    /// <summary>The .NET namespaces a XAML namespace stands for.</summary>
    /// <param name="xmlNamespace">The XAML namespace, such as the presentation namespace or <c>clr-namespace:WpfApp1</c>.</param>
    public IReadOnlyList<XamlClrNamespace> ClrNamespaces(string xmlNamespace)
    {
        ArgumentNullException.ThrowIfNull(xmlNamespace);
        if (xmlNamespace.StartsWith(XamlNamespaces.ClrNamespacePrefix, StringComparison.Ordinal))
        {
            var value = xmlNamespace[XamlNamespaces.ClrNamespacePrefix.Length..];
            var semicolon = value.IndexOf(';', StringComparison.Ordinal);
            var ns = (semicolon < 0 ? value : value[..semicolon]).Trim();
            var assembly = semicolon < 0 ? null : AssemblyNamed(value[(semicolon + 1)..]);
            return [new XamlClrNamespace(ns, assembly ?? (semicolon < 0 ? Compilation.Assembly : null))];
        }

        if (xmlNamespace.StartsWith(XamlNamespaces.UsingPrefix, StringComparison.Ordinal))
        {
            return [new XamlClrNamespace(xmlNamespace[XamlNamespaces.UsingPrefix.Length..].Trim(), null)];
        }

        return _definitions.TryGetValue(xmlNamespace, out var namespaces) ? namespaces : [];
    }

    /// <summary>The type of an element, from its XAML namespace and name; for a markup extension, its name with or without <c>Extension</c>.</summary>
    /// <param name="xmlNamespace">The element's XAML namespace.</param>
    /// <param name="localName">Its name, such as <c>Button</c>.</param>
    public INamedTypeSymbol? FindType(string xmlNamespace, string localName)
    {
        ArgumentNullException.ThrowIfNull(xmlNamespace);
        ArgumentNullException.ThrowIfNull(localName);
        return _types.GetOrAdd((xmlNamespace, localName), key =>
        {
            foreach (var ns in ClrNamespaces(key.Item1))
            {
                if (Find(ns, key.Item2) is { } type)
                {
                    return type;
                }
            }

            return null;
        });
    }

    /// <summary>The types an element of a XAML namespace can be: public, non-abstract types XAML can create, by name.</summary>
    /// <param name="xmlNamespace">The XAML namespace.</param>
    public IReadOnlyList<INamedTypeSymbol> TypesIn(string xmlNamespace)
    {
        ArgumentNullException.ThrowIfNull(xmlNamespace);
        return _namespaceTypes.GetOrAdd(xmlNamespace, key =>
        {
            var types = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            foreach (var ns in ClrNamespaces(key))
            {
                foreach (var assembly in AssembliesOf(ns))
                {
                    if (NamespaceSymbol(assembly, ns.Namespace) is not { } symbol)
                    {
                        continue;
                    }

                    foreach (var type in symbol.GetTypeMembers())
                    {
                        if (IsCreatable(type, assembly))
                        {
                            types.TryAdd(type.Name, type);
                        }
                    }
                }
            }

            return types.Values.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        });
    }

    /// <summary>The properties and events of a type XAML can set, its own and inherited ones, by name.</summary>
    /// <param name="type">The type.</param>
    public IReadOnlyList<XamlMember> Members(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _members.GetOrAdd(type, static t =>
        {
            var members = new Dictionary<string, XamlMember>(StringComparer.Ordinal);
            for (var current = t; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic || members.ContainsKey(member.Name))
                    {
                        continue;
                    }

                    switch (member)
                    {
                        case IPropertySymbol { IsIndexer: false, GetMethod: not null } property
                            when property.SetMethod is { DeclaredAccessibility: Accessibility.Public } || IsCollection(property.Type):
                            members[property.Name] = new XamlMember(property.Name, XamlMemberKind.Property, property, property.Type, current);
                            break;
                        case IEventSymbol @event:
                            members[@event.Name] = new XamlMember(@event.Name, XamlMemberKind.Event, @event, @event.Type, current);
                            break;
                    }
                }
            }

            return members.Values.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        });
    }

    /// <summary>A property or event of a type, by name, or <see langword="null"/>.</summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The member's name.</param>
    public XamlMember? FindMember(INamedTypeSymbol type, string name) =>
        Members(type).FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// The attached properties and events a type defines, as its static <c>SetRow</c>, <c>GetRow</c> and
    /// <c>AddClickHandler</c> methods declare them, by name.
    /// </summary>
    /// <param name="owner">The type, such as <c>Grid</c>.</param>
    public IReadOnlyList<XamlMember> AttachedMembers(INamedTypeSymbol owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _attached.GetOrAdd(owner, static t =>
        {
            var members = new Dictionary<string, XamlMember>(StringComparer.Ordinal);
            for (var current = t; current is not null; current = current.BaseType)
            {
                foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
                {
                    if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Parameters.Length == 0)
                    {
                        continue;
                    }

                    if (method.Name.StartsWith("Set", StringComparison.Ordinal) && method.Name.Length > 3 && method.Parameters.Length == 2)
                    {
                        members.TryAdd(method.Name[3..], new XamlMember(method.Name[3..], XamlMemberKind.AttachedProperty, method, method.Parameters[1].Type, current));
                    }
                    else if (method.Name.StartsWith("Get", StringComparison.Ordinal) && method.Name.Length > 3 && method.Parameters.Length == 1 && IsCollection(method.ReturnType))
                    {
                        members.TryAdd(method.Name[3..], new XamlMember(method.Name[3..], XamlMemberKind.AttachedProperty, method, method.ReturnType, current));
                    }
                    else if (method.Name.StartsWith("Add", StringComparison.Ordinal) && method.Name.EndsWith("Handler", StringComparison.Ordinal)
                        && method.Name.Length > "AddHandler".Length && method.Parameters.Length == 2)
                    {
                        var name = method.Name[3..^"Handler".Length];
                        members.TryAdd(name, new XamlMember(name, XamlMemberKind.AttachedEvent, method, method.Parameters[1].Type, current));
                    }
                }
            }

            return members.Values.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        });
    }

    /// <summary>An attached property or event of a type, by name, or <see langword="null"/>.</summary>
    /// <param name="owner">The type that defines it.</param>
    /// <param name="name">Its name.</param>
    public XamlMember? FindAttachedMember(INamedTypeSymbol owner, string name) =>
        AttachedMembers(owner).FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));

    /// <summary>The property an element's content sets, from <c>[ContentProperty]</c> on the type or a base type.</summary>
    /// <param name="type">The type.</param>
    public string? ContentProperty(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.Name == "ContentPropertyAttribute" && attribute.ConstructorArguments is [{ Value: string name }])
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>Whether a type is, or derives from, another.</summary>
    /// <param name="type">The type.</param>
    /// <param name="baseType">The base type, or <see langword="null"/>, which nothing derives from.</param>
    public static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a type is a markup extension, such as <c>Binding</c> or <c>StaticResourceExtension</c>.</summary>
    /// <param name="type">The type.</param>
    public bool IsMarkupExtension(ITypeSymbol? type) => DerivesFrom(type, MarkupExtension);

    /// <summary>Whether XAML adds items to a member of this type rather than setting it: a list or dictionary.</summary>
    /// <param name="type">The member's type.</param>
    public static bool IsCollection(ITypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.SpecialType != SpecialType.System_String
            && type.AllInterfaces.Any(i => i.Name is "IList" or "IDictionary" or "ICollection" && i.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal));
    }

    private INamedTypeSymbol? Find(XamlClrNamespace ns, string localName)
    {
        foreach (var assembly in AssembliesOf(ns))
        {
            foreach (var name in (string[])[localName, localName + "Extension"])
            {
                if (assembly.GetTypeByMetadataName($"{ns.Namespace}.{name}") is { } type
                    && (type.DeclaredAccessibility == Accessibility.Public || SymbolEqualityComparer.Default.Equals(assembly, Compilation.Assembly)))
                {
                    if (name == localName || IsMarkupExtension(type))
                    {
                        return type;
                    }
                }
            }
        }

        return null;
    }

    private bool IsCreatable(INamedTypeSymbol type, IAssemblySymbol assembly) =>
        type.TypeKind == TypeKind.Class
        && !type.IsAbstract
        && !type.IsStatic
        && !type.IsGenericType
        && (type.DeclaredAccessibility == Accessibility.Public || SymbolEqualityComparer.Default.Equals(assembly, Compilation.Assembly))
        && !type.Name.StartsWith("INTERNAL_", StringComparison.Ordinal)
        && !type.GetAttributes().Any(a => a.AttributeClass?.Name == "EditorBrowsableAttribute" && a.ConstructorArguments is [{ Value: 1 }])
        && (DerivesFrom(type, DependencyObject) || IsMarkupExtension(type) || type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public));

    private IEnumerable<IAssemblySymbol> AssembliesOf(XamlClrNamespace ns) => ns.Assembly is { } one ? [one] : AllAssemblies();

    private IEnumerable<IAssemblySymbol> AllAssemblies() =>
        Compilation.SourceModule.ReferencedAssemblySymbols.Prepend(Compilation.Assembly);

    private IAssemblySymbol? AssemblyNamed(string assemblyPart)
    {
        var name = assemblyPart.Trim();
        if (name.StartsWith("assembly=", StringComparison.Ordinal))
        {
            name = name["assembly=".Length..].Trim();
        }

        return AllAssemblies().FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static INamespaceSymbol? NamespaceSymbol(IAssemblySymbol assembly, string name)
    {
        var current = assembly.GlobalNamespace;
        foreach (var part in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current.GetNamespaceMembers().FirstOrDefault(n => n.Name == part);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>The <c>[XmlnsDefinition]</c> attributes of the project and every assembly it references.</summary>
    private static Dictionary<string, List<XamlClrNamespace>> ReadDefinitions(RoslynCompilation compilation)
    {
        var definitions = new Dictionary<string, List<XamlClrNamespace>>(StringComparer.Ordinal);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols.Prepend(compilation.Assembly))
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.Name != "XmlnsDefinitionAttribute" || attribute.ConstructorArguments is not [{ Value: string xml }, { Value: string clr }])
                {
                    continue;
                }

                if (!definitions.TryGetValue(xml, out var namespaces))
                {
                    namespaces = [];
                    definitions[xml] = namespaces;
                }

                if (!namespaces.Exists(n => n.Namespace == clr && SymbolEqualityComparer.Default.Equals(n.Assembly, assembly)))
                {
                    namespaces.Add(new XamlClrNamespace(clr, assembly));
                }
            }
        }

        return definitions;
    }
}
