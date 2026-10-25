using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation;

/// <summary>
/// The simple names of the assemblies in a set of framework references, such as <c>System.Runtime</c>.
/// </summary>
/// <remarks>
/// Package restore needs them to know what the framework already provides: packages named after these assemblies
/// are not downloaded, and assemblies with these names are never taken from a package.
/// </remarks>
public static class ReferenceAssemblyNames
{
    private static readonly Lazy<IReadOnlySet<string>> LazyNet100 = new(() => Read(ReferenceAssemblies.Net100));

    /// <summary>The names of the .NET 10 reference assemblies TabStudio compiles against. Computed once.</summary>
    public static IReadOnlySet<string> Net100 => LazyNet100.Value;

    /// <summary>Reads the assembly names of a set of references from their metadata.</summary>
    /// <param name="references">The references, such as <see cref="ReferenceAssemblies.Net100"/> or <see cref="ReferenceAssemblies.LoadMapped"/>.</param>
    /// <returns>The names, compared case-insensitively.</returns>
    public static IReadOnlySet<string> Read(IEnumerable<MetadataReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references.OfType<PortableExecutableReference>())
        {
            if (reference.GetMetadata() is not AssemblyMetadata assembly)
            {
                continue;
            }

            var modules = assembly.GetModules();
            if (modules.Length == 0)
            {
                continue;
            }

            var reader = modules[0].GetMetadataReader();
            if (reader.IsAssembly)
            {
                names.Add(reader.GetString(reader.GetAssemblyDefinition().Name));
            }
        }

        return names;
    }
}
