using Microsoft.CodeAnalysis;

namespace TabStudio.TestRunner.Source;

/// <summary>Names types the way reflection does, from their symbols.</summary>
/// <remarks>Names from syntax alone come from each language's <see cref="SourceLanguage"/>.</remarks>
internal static class SymbolNames
{
    /// <summary>The name <see cref="Type.FullName"/> would give a type, such as <c>Ns.Outer+Inner`1</c>.</summary>
    /// <param name="type">The type symbol.</param>
    /// <remarks>
    /// Visual Basic's root namespace is part of the symbol's namespace, so the name is the one the compiled type has.
    /// </remarks>
    public static string MetadataFullName(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } containing)
        {
            return MetadataFullName(containing) + "+" + type.MetadataName;
        }

        var namespaceName = NamespaceName(type.ContainingNamespace);
        return namespaceName.Length == 0 ? type.MetadataName : namespaceName + "." + type.MetadataName;
    }

    /// <summary>Splits a full name into its namespace and the rest.</summary>
    /// <param name="fullName">A full name such as <c>Ns.Outer+Inner</c>.</param>
    public static (string NamespaceName, string Name) Split(string fullName)
    {
        var outer = fullName.Split('+')[0];
        var dot = outer.LastIndexOf('.');
        return dot < 0 ? (string.Empty, fullName) : (outer[..dot], fullName[(dot + 1)..]);
    }

    /// <summary>A namespace's dotted name as metadata writes it, without the escaping a language's display adds.</summary>
    private static string NamespaceName(INamespaceSymbol? symbol)
    {
        var parts = new List<string>();
        for (var current = symbol; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            parts.Add(current.MetadataName);
        }

        parts.Reverse();
        return string.Join('.', parts);
    }
}
