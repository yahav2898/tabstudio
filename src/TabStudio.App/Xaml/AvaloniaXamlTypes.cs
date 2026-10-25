using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace TabStudio.App.Xaml;

/// <summary>A property the Properties panel offers for an element.</summary>
/// <param name="Name">Its XAML name, such as <c>Width</c> or <c>Grid.Row</c>.</param>
/// <param name="TypeName">Its type, for people: <c>double</c>, <c>Thickness</c>, <c>HorizontalAlignment</c>.</param>
/// <param name="Choices">The values it can take, for enums and booleans; otherwise <see langword="null"/>.</param>
internal sealed record XamlPropertyInfo(string Name, string TypeName, IReadOnlyList<string>? Choices);

/// <summary>
/// The controls in Avalonia's XML namespace and the properties they can set in XAML, read from Avalonia's own
/// assemblies, so the Properties panel knows what a <c>&lt;Button&gt;</c> or a <c>&lt;StackPanel&gt;</c> has. UI thread
/// only, as Avalonia registers properties there.
/// </summary>
internal static class AvaloniaXamlTypes
{
    /// <summary>Avalonia's XML namespace, the default one of its XAML files.</summary>
    public const string Namespace = "https://github.com/avaloniaui";

    private static readonly string[] BooleanChoices = ["True", "False"];
    private static readonly Dictionary<Type, IReadOnlyList<XamlPropertyInfo>> PropertiesByType = [];
    private static Dictionary<string, Type>? _types;

    /// <summary>The type an element of Avalonia's namespace creates, such as <c>Avalonia.Controls.Button</c> for <c>Button</c>.</summary>
    /// <param name="localName">The element's name without prefix.</param>
    /// <returns>The type, or <see langword="null"/> when Avalonia has no such element.</returns>
    public static Type? Find(string localName)
    {
        ArgumentNullException.ThrowIfNull(localName);
        _types ??= IndexTypes();
        return _types.GetValueOrDefault(localName);
    }

    /// <summary>
    /// The properties a type can set as attributes, by name: every Avalonia property that is not read-only, and the
    /// plain properties written as text, such as a Grid's <c>ColumnDefinitions</c>.
    /// </summary>
    /// <param name="type">The type, such as <c>Button</c>.</param>
    public static IReadOnlyList<XamlPropertyInfo> PropertiesOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (PropertiesByType.TryGetValue(type, out var known))
        {
            return known;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var properties = new List<XamlPropertyInfo>();
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(type))
        {
            if (!property.IsReadOnly && !property.IsAttached && !typeof(Delegate).IsAssignableFrom(property.PropertyType) && seen.Add(property.Name))
            {
                properties.Add(Describe(property.Name, property.PropertyType));
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0
                && IsWrittenAsText(property.PropertyType) && seen.Add(property.Name))
            {
                properties.Add(Describe(property.Name, property.PropertyType));
            }
        }

        properties.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        PropertiesByType[type] = properties;
        return properties;
    }

    /// <summary>An attached property by its XAML name, such as <c>Grid.Row</c> or <c>ToolTip.Tip</c>.</summary>
    /// <param name="name">The owner's name, a dot, and the property's name.</param>
    /// <returns>The property, or <see langword="null"/> when Avalonia has no such attached property.</returns>
    public static XamlPropertyInfo? Attached(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || Find(name[..dot]) is not { } owner)
        {
            return null;
        }

        var field = owner.GetField(name[(dot + 1)..] + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        return field?.GetValue(null) is AvaloniaProperty property ? Describe(name, property.PropertyType) : null;
    }

    /// <summary>Describes a property of a given type for the Properties panel.</summary>
    /// <param name="name">Its XAML name.</param>
    /// <param name="type">Its type.</param>
    public static XamlPropertyInfo Describe(string name, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var valueType = Nullable.GetUnderlyingType(type) ?? type;
        IReadOnlyList<string>? choices = valueType.IsEnum ? Enum.GetNames(valueType) : valueType == typeof(bool) ? BooleanChoices : null;
        return new XamlPropertyInfo(name, FriendlyName(type), choices);
    }

    /// <summary>Whether XAML writes values of a type as attribute text: numbers, text, enums, and types that parse it.</summary>
    private static bool IsWrittenAsText(Type type)
    {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;
        return valueType.IsPrimitive || valueType.IsEnum || valueType == typeof(string) || valueType == typeof(decimal)
            || valueType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]) is not null;
    }

    private static string FriendlyName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return FriendlyName(underlying) + "?";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        return tick > 0 ? type.Name[..tick] : type.Name;
    }

    /// <summary>Indexes the element types of Avalonia's namespace, from the namespaces its assemblies map to it.</summary>
    private static Dictionary<string, Type> IndexTypes()
    {
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var assembly in new[] { typeof(Control).Assembly, typeof(AvaloniaObject).Assembly })
        {
            var namespaces = assembly.GetCustomAttributes<XmlnsDefinitionAttribute>()
                .Where(definition => definition.XmlNamespace == Namespace)
                .Select(definition => definition.ClrNamespace)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type is { IsNested: false, IsGenericTypeDefinition: false } && type.Namespace is { } name && namespaces.Contains(name)
                    && typeof(AvaloniaObject).IsAssignableFrom(type))
                {
                    types.TryAdd(type.Name, type);
                }
            }
        }

        return types;
    }
}
