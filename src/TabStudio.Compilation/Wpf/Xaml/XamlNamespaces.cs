namespace TabStudio.Compilation.Wpf.Xaml;

/// <summary>The XML namespaces WPF and OpenSilver XAML is written with.</summary>
public static class XamlNamespaces
{
    /// <summary>WPF's controls, the default namespace of every WPF XAML file.</summary>
    public const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>The XAML language itself, usually <c>x:</c>: <c>x:Class</c>, <c>x:Name</c>, <c>x:Key</c>…</summary>
    public const string Language = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Markup compatibility, usually <c>mc:</c>, whose <c>Ignorable</c> lists the prefixes compilers skip.</summary>
    public const string MarkupCompatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    /// <summary>The designer's namespace, usually <c>d:</c>, for design-time values only.</summary>
    public const string Designer = "http://schemas.microsoft.com/expression/blend/2008";

    /// <summary>The prefix of a namespace that names a .NET namespace directly: <c>clr-namespace:WpfApp1.Views</c>.</summary>
    public const string ClrNamespacePrefix = "clr-namespace:";

    /// <summary>The prefix of a namespace that names a .NET namespace the UWP way: <c>using:WpfApp1.Views</c>.</summary>
    public const string UsingPrefix = "using:";

    /// <summary>The XAML language's directives, which go on any element.</summary>
    public static IReadOnlyList<string> Directives { get; } = ["Name", "Key", "Class", "FieldModifier", "ClassModifier", "Uid", "Shared", "Subclass", "TypeArguments"];

    /// <summary>The prefixes a file asks compilers to skip, from its root's <c>mc:Ignorable</c>.</summary>
    /// <param name="root">The root element.</param>
    public static IReadOnlySet<string> IgnorablePrefixes(XamlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var ignorable = root.Attribute(MarkupCompatibility, "Ignorable")?.Value;
        return ignorable is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(ignorable.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
    }
}
