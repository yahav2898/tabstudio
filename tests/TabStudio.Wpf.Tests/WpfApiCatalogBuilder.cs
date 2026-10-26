using System.IO.Compression;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// Makes the catalog of what WPF has and OpenSilver does not (<c>Wpf/MissingInOpenSilver.txt</c> in
/// TabStudio.Compilation) by comparing WPF's reference assemblies, from the Microsoft.WindowsDesktop.App.Ref pack on
/// nuget.org, with OpenSilver's assemblies and .NET's: the namespaces and types OpenSilver lacks, the members its
/// types lack, and WPF's base types, so a member is looked up along WPF's hierarchy.
/// </summary>
internal static class WpfApiCatalogBuilder
{
    /// <summary>The version of WPF the catalog describes.</summary>
    public const string WindowsDesktopVersion = "10.0.0";

    /// <summary>WPF's assemblies, and the ones WPF apps use with it, such as the Ribbon and Windows Forms hosting.</summary>
    private static readonly string[] WpfAssemblies =
    [
        "PresentationCore", "PresentationFramework", "WindowsBase", "System.Xaml", "ReachFramework", "System.Printing",
        "System.Windows.Controls.Ribbon", "WindowsFormsIntegration", "UIAutomationClient", "UIAutomationTypes",
        "UIAutomationProvider", "System.Windows.Input.Manipulations", "PresentationFramework.Aero2",
    ];

    /// <summary>Downloads WPF's reference assemblies and returns them as references.</summary>
    public static async Task<IReadOnlyList<MetadataReference>> DownloadWpfAsync(HttpClient http, string directory)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/microsoft.windowsdesktop.app.ref/{WindowsDesktopVersion}/microsoft.windowsdesktop.app.ref.{WindowsDesktopVersion}.nupkg";
        var package = await http.GetByteArrayAsync(url);
        using var zip = new ZipArchive(new MemoryStream(package));
        var references = new List<MetadataReference>();
        Directory.CreateDirectory(directory);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.StartsWith("ref/net10.0/", StringComparison.Ordinal) && entry.FullName.EndsWith(".dll", StringComparison.Ordinal))
            {
                var path = Path.Combine(directory, entry.Name);
                entry.ExtractToFile(path, overwrite: true);
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return references;
    }

    /// <summary>Compares WPF with OpenSilver and writes the catalog's lines.</summary>
    /// <param name="wpf">WPF's reference assemblies.</param>
    /// <param name="openSilver">A compilation of a WPF project built on OpenSilver, with its references.</param>
    /// <param name="openSilverVersion">The version of OpenSilver, for the header.</param>
    public static string Build(IReadOnlyList<MetadataReference> wpf, Microsoft.CodeAnalysis.Compilation openSilver, string openSilverVersion)
    {
        var wpfCompilation = CSharpCompilation.Create("Wpf", references: [.. ReferenceAssemblies.Net100, .. wpf]);
        var openSilverNamespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in openSilver.SourceModule.ReferencedAssemblySymbols)
        {
            CollectNamespaces(assembly.GlobalNamespace, openSilverNamespaces);
        }

        var lines = new SortedSet<string>(StringComparer.Ordinal);
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var present = new List<(INamedTypeSymbol Wpf, INamedTypeSymbol OpenSilver)>();
        var types = new List<INamedTypeSymbol>();
        foreach (var assembly in wpfCompilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!WpfAssemblies.Contains(assembly.Name, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var type in PublicTypes(assembly.GlobalNamespace))
            {
                types.Add(type);
                var name = FullName(type);
                var ns = type.ContainingNamespace.ToDisplayString();
                if (!openSilverNamespaces.Contains(ns))
                {
                    lines.Add($"N {ns}");
                }

                if (FindPublic(openSilver, name) is { } counterpart)
                {
                    present.Add((type, counterpart));
                }
                else
                {
                    missing.Add(name);
                    lines.Add($"T {name}");
                }
            }
        }

        foreach (var type in types)
        {
            if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType && baseType.TypeKind == TypeKind.Class)
            {
                lines.Add($"B {FullName(type)} {FullName(baseType)}");
            }
        }

        foreach (var (wpfType, counterpart) in present)
        {
            var have = MemberNames(counterpart);
            foreach (var name in DeclaredMemberNames(wpfType))
            {
                if (!have.Contains(name))
                {
                    lines.Add($"M {FullName(wpfType)} {name}");
                }
            }

            // A base type OpenSilver does not have, such as Visual: its members are missing from every type under it.
            for (var current = wpfType.BaseType; current is not null; current = current.BaseType)
            {
                if (missing.Contains(FullName(current)))
                {
                    foreach (var name in DeclaredMemberNames(current))
                    {
                        lines.Add($"M {FullName(current)} {name}");
                    }
                }
            }
        }

        var text = new StringBuilder();
        text.Append("# What WPF ").Append(WindowsDesktopVersion).Append(" has that OpenSilver ").Append(openSilverVersion).Append(" does not, made by WpfApiCatalogTests.\n");
        text.Append("# N namespace: no type of it is in OpenSilver. T type: not in OpenSilver. M type member: the type is, the member is not.\n");
        text.Append("# B type base: WPF's base type, along which members are looked up.\n");
        foreach (var line in lines)
        {
            text.Append(line).Append('\n');
        }

        return text.ToString();
    }

    private static void CollectNamespaces(INamespaceSymbol ns, HashSet<string> names)
    {
        if (ns.GetTypeMembers().Any(t => t.DeclaredAccessibility == Accessibility.Public))
        {
            names.Add(ns.ToDisplayString());
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectNamespaces(child, names);
        }
    }

    private static IEnumerable<INamedTypeSymbol> PublicTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (type.DeclaredAccessibility == Accessibility.Public)
            {
                yield return type;
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in PublicTypes(child))
            {
                yield return type;
            }
        }
    }

    private static INamedTypeSymbol? FindPublic(Microsoft.CodeAnalysis.Compilation compilation, string metadataName) =>
        compilation.GetTypesByMetadataName(metadataName).FirstOrDefault(t => t.DeclaredAccessibility == Accessibility.Public);

    private static string FullName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns ? $"{ns.ToDisplayString()}.{type.MetadataName}" : type.MetadataName;

    /// <summary>The members code can use from outside a type or from a class deriving from it: public and protected.</summary>
    private static IEnumerable<string> DeclaredMemberNames(INamedTypeSymbol type) =>
        type.GetMembers()
            .Where(m => m.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal
                && !m.IsImplicitlyDeclared
                && m is not INamedTypeSymbol
                && m is not IMethodSymbol { MethodKind: not MethodKind.Ordinary })
            .Select(m => m.Name)
            .Distinct(StringComparer.Ordinal);

    private static HashSet<string> MemberNames(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            names.UnionWith(current.GetMembers().Select(m => m.Name));
        }

        foreach (var @interface in type.AllInterfaces)
        {
            names.UnionWith(@interface.GetMembers().Select(m => m.Name));
        }

        return names;
    }
}
