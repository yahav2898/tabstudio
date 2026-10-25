using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// The Razor components a Razor file can use, as the Razor compiler sees them: the types that implement
/// <c>IComponent</c>, in the file's project and in the assemblies it references, whose namespace the file imports
/// (with <c>@using</c>, an <c>_Imports.razor</c>, or by being in it).
/// </summary>
internal static class RazorComponents
{
    private const string ComponentInterface = "Microsoft.AspNetCore.Components.IComponent";
    private const string ParameterAttribute = "Microsoft.AspNetCore.Components.ParameterAttribute";
    private const string ComponentsAssembly = "Microsoft.AspNetCore.Components";

    private static readonly ConditionalWeakTable<RoslynCompilation, IReadOnlyList<INamedTypeSymbol>> Cache = new();

    /// <summary>Every component type of a compilation and its references, by simple name.</summary>
    /// <param name="compilation">The compilation of the Razor file's project.</param>
    public static IReadOnlyList<INamedTypeSymbol> All(RoslynCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        return Cache.GetValue(compilation, Discover);
    }

    /// <summary>The components a generated file can name without a namespace: those in the namespaces it imports or is in.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="generated">The C# the Razor compiler generated for the file.</param>
    public static IReadOnlyList<INamedTypeSymbol> InScope(RoslynCompilation compilation, SyntaxTree generated)
    {
        var namespaces = NamespacesInScope(generated);
        return [.. All(compilation).Where(c => namespaces.Contains(c.ContainingNamespace.ToDisplayString()))];
    }

    /// <summary>The component a tag names, such as <c>Counter</c> or <c>Shared.Counter</c>, or <see langword="null"/>.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="generated">The C# generated for the file the tag is in.</param>
    /// <param name="tagName">The tag's name.</param>
    public static INamedTypeSymbol? Find(RoslynCompilation compilation, SyntaxTree generated, string tagName)
    {
        ArgumentNullException.ThrowIfNull(tagName);
        var name = tagName.Split(':')[0];
        if (name.Contains('.', StringComparison.Ordinal))
        {
            return All(compilation).FirstOrDefault(c => string.Equals(c.ToDisplayString(), name, StringComparison.Ordinal)
                || c.ToDisplayString().EndsWith("." + name, StringComparison.Ordinal));
        }

        return InScope(compilation, generated).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
    }

    /// <summary>A component's parameters: its public properties with <c>[Parameter]</c>, its own and its base types'.</summary>
    /// <param name="component">The component type.</param>
    public static IReadOnlyList<IPropertySymbol> Parameters(INamedTypeSymbol component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var parameters = new List<IPropertySymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var type = component; type is not null; type = type.BaseType)
        {
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility == Accessibility.Public
                    && property.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ParameterAttribute)
                    && seen.Add(property.Name))
                {
                    parameters.Add(property);
                }
            }
        }

        return parameters;
    }

    /// <summary>Whether a parameter is an event callback, such as <c>EventCallback&lt;int&gt; CountChanged</c>.</summary>
    /// <param name="parameter">The parameter.</param>
    public static bool IsEventCallback(IPropertySymbol parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return parameter.Type is INamedTypeSymbol { Name: "EventCallback" } type
            && type.ContainingNamespace.ToDisplayString() == ComponentsAssembly;
    }

    /// <summary>The namespaces a generated file's code sees: its using directives, and its namespace and the ones containing it.</summary>
    private static HashSet<string> NamespacesInScope(SyntaxTree generated)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var root = generated.GetRoot();
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (directive.Alias is null && directive.StaticKeyword.IsKind(SyntaxKind.None) && directive.Name is { } name)
            {
                namespaces.Add(name.ToString().Replace("global::", string.Empty, StringComparison.Ordinal));
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            var parts = declaration.Name.ToString().Replace("global::", string.Empty, StringComparison.Ordinal).Split('.');
            for (var i = 1; i <= parts.Length; i++)
            {
                namespaces.Add(string.Join('.', parts[..i]));
            }
        }

        return namespaces;
    }

    private static IReadOnlyList<INamedTypeSymbol> Discover(RoslynCompilation compilation)
    {
        if (compilation.GetTypeByMetadataName(ComponentInterface) is not { } component)
        {
            return [];
        }

        var found = new List<INamedTypeSymbol>();
        Collect(compilation.Assembly.GlobalNamespace, component, found, includeInternal: true);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            // Only assemblies that can have components: Blazor's, and those that reference it.
            if (assembly.Name == ComponentsAssembly || assembly.Modules.Any(m => m.ReferencedAssemblies.Any(r => r.Name == ComponentsAssembly)))
            {
                Collect(assembly.GlobalNamespace, component, found, includeInternal: false);
            }
        }

        return found;
    }

    private static void Collect(INamespaceSymbol namespaceSymbol, INamedTypeSymbol component, List<INamedTypeSymbol> found, bool includeInternal)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.TypeKind == TypeKind.Class
                && !type.IsAbstract
                && (type.DeclaredAccessibility == Accessibility.Public || (includeInternal && type.DeclaredAccessibility == Accessibility.Internal))
                && !type.Name.StartsWith('_')
                && type.AllInterfaces.Contains(component, SymbolEqualityComparer.Default))
            {
                found.Add(type);
            }
        }

        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            Collect(child, component, found, includeInternal);
        }
    }
}
