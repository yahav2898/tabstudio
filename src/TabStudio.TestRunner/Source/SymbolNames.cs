using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.TestRunner.Source;

/// <summary>Names types the way reflection does, from symbols or from syntax alone.</summary>
internal static class SymbolNames
{
    /// <summary>The name <see cref="Type.FullName"/> would give a type, such as <c>Ns.Outer+Inner`1</c>.</summary>
    /// <param name="type">The type symbol.</param>
    public static string MetadataFullName(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } containing)
        {
            return MetadataFullName(containing) + "+" + type.MetadataName;
        }

        var containingNamespace = type.ContainingNamespace;
        return containingNamespace is null || containingNamespace.IsGlobalNamespace
            ? type.MetadataName
            : containingNamespace.ToDisplayString() + "." + type.MetadataName;
    }

    /// <summary>The same name, computed from the declaration's syntax when there is no semantic model.</summary>
    /// <param name="declaration">The type declaration.</param>
    public static string MetadataFullName(BaseTypeDeclarationSyntax declaration)
    {
        var name = declaration.Identifier.ValueText + Arity(declaration);
        for (var parent = declaration.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case BaseTypeDeclarationSyntax outer:
                    name = outer.Identifier.ValueText + Arity(outer) + "+" + name;
                    break;
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    name = namespaceDeclaration.Name.ToString() + "." + name;
                    break;
            }
        }

        return name;
    }

    /// <summary>Splits a full name into its namespace and the rest.</summary>
    /// <param name="fullName">A full name such as <c>Ns.Outer+Inner</c>.</param>
    public static (string NamespaceName, string Name) Split(string fullName)
    {
        var outer = fullName.Split('+')[0];
        var dot = outer.LastIndexOf('.');
        return dot < 0 ? (string.Empty, fullName) : (outer[..dot], fullName[(dot + 1)..]);
    }

    private static string Arity(BaseTypeDeclarationSyntax declaration) =>
        declaration is TypeDeclarationSyntax { TypeParameterList.Parameters.Count: > 0 and var count }
            ? "`" + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
}
