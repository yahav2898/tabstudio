using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation;

/// <summary>
/// Loads analyzer and source generator assemblies from NuGet packages, the way the compiler does: each package
/// folder gets its own load context, so two packages can bring different versions of a helper library, and the
/// compiler's own assemblies always come from TabStudio, so generators run inside the Roslyn that hosts them.
/// </summary>
internal sealed class AnalyzerLoader : IAnalyzerAssemblyLoader
{
    /// <summary>One loader for the process: an analyzer assembly is loaded once, however many projects use it.</summary>
    public static AnalyzerLoader Shared { get; } = new();

    private readonly ConcurrentDictionary<string, FolderContext> _contexts = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void AddDependencyLocation(string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullPath);
        ContextFor(fullPath);
    }

    /// <inheritdoc />
    public Assembly LoadFromPath(string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullPath);
        return ContextFor(fullPath).Load(fullPath);
    }

    private FolderContext ContextFor(string fullPath)
    {
        var folder = Path.GetDirectoryName(fullPath) ?? string.Empty;
        return _contexts.GetOrAdd(folder, f => new FolderContext(f));
    }

    /// <summary>The analyzers of one folder, and the libraries next to them.</summary>
    private sealed class FolderContext(string folder) : AssemblyLoadContext($"TabStudio analyzers: {folder}")
    {
        private readonly Lock _gate = new();

        public Assembly Load(string fullPath)
        {
            lock (_gate)
            {
                var name = AssemblyName.GetAssemblyName(fullPath);
                return Assemblies.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(name, a.GetName()))
                    ?? LoadFromAssemblyPath(fullPath);
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // The compiler and the libraries it shares with analyzers come from TabStudio itself.
            if (assemblyName.Name is not { } name || IsHostAssembly(name))
            {
                return null;
            }

            var candidate = Path.Combine(folder, name + ".dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }

        private static bool IsHostAssembly(string name) =>
            name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
            || name is "System.Collections.Immutable" or "System.Reflection.Metadata" or "netstandard";
    }
}
