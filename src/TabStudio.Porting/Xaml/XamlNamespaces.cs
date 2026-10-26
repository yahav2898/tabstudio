using System.Xml.Linq;

namespace TabStudio.Porting.Xaml;

/// <summary>The XML namespaces of WPF and Avalonia XAML, and how a WPF CLR namespace mapping becomes Avalonia's.</summary>
internal static class XamlNamespaces
{
    /// <summary>WPF's controls.</summary>
    public static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>The XAML language namespace, <c>x:</c>, the same in both.</summary>
    public static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Avalonia's controls.</summary>
    public static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    /// <summary>Design-time properties, <c>d:</c>.</summary>
    public static readonly XNamespace Blend = "http://schemas.microsoft.com/expression/blend/2008";

    /// <summary>Markup compatibility, <c>mc:</c>.</summary>
    public static readonly XNamespace Compatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    /// <summary>The CLR namespace a WPF mapping names: <c>clr-namespace:App.Views;assembly=App</c> is <c>App.Views</c>.</summary>
    /// <param name="mapping">The xmlns value.</param>
    /// <returns>The namespace, or <see langword="null"/> when the value is not a CLR mapping.</returns>
    public static string? ClrNamespace(string mapping)
    {
        if (mapping.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var rest = mapping["clr-namespace:".Length..];
            var semicolon = rest.IndexOf(';', StringComparison.Ordinal);
            return (semicolon < 0 ? rest : rest[..semicolon]).Trim();
        }

        return mapping.StartsWith("using:", StringComparison.Ordinal) ? mapping["using:".Length..].Trim() : null;
    }

    /// <summary>The assembly a WPF mapping names, or <see langword="null"/> for the project's own.</summary>
    /// <param name="mapping">The xmlns value.</param>
    public static string? ClrAssembly(string mapping)
    {
        var index = mapping.IndexOf(";assembly=", StringComparison.Ordinal);
        return index < 0 ? null : mapping[(index + ";assembly=".Length)..].Trim();
    }

    /// <summary>Avalonia's form of a CLR namespace mapping: <c>using:App.Views</c>.</summary>
    /// <param name="clrNamespace">The CLR namespace.</param>
    public static XNamespace Using(string clrNamespace) => "using:" + clrNamespace;
}
