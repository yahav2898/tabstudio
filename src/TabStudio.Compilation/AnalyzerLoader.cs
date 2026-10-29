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

    /// <summary>Every analyzer and dependency path the loader knows, by simple assembly name.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void AddDependencyLocation(string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullPath);
        _known.GetOrAdd(Path.GetFileNameWithoutExtension(fullPath), _ => new(StringComparer.Ordinal)).TryAdd(fullPath, 0);
        ContextFor(fullPath);
    }

    /// <summary>
    /// Finds a dependency of an analyzer that is not in the analyzer's own folder among the other analyzer paths, as the
    /// compiler does: Vogen, for one, keeps <c>Vogen.dll</c> in <c>analyzers/dotnet/roslyn4.12/cs/</c> and the
    /// <c>Vogen.SharedTypes.dll</c> it needs in <c>analyzers/dotnet/cs/</c>. A path in the same package wins.
    /// </summary>
    private string? FindDependency(string simpleName, string requestingFolder)
    {
        if (!_known.TryGetValue(simpleName, out var paths) || paths.IsEmpty)
        {
            return null;
        }

        return paths.Keys
            .OrderByDescending(path => CommonPrefixLength(Path.GetDirectoryName(path) ?? string.Empty, requestingFolder))
            .ThenBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault(File.Exists);
    }

    private static int CommonPrefixLength(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < length && a[i] == b[i])
        {
            i++;
        }

        return i;
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
        return _contexts.GetOrAdd(folder, f => new FolderContext(this, f));
    }

    /// <summary>
    /// Whether an assembly is one an analyzer must share with TabStudio rather than load its own copy of: the compiler
    /// and the libraries its API exposes, so that a generator runs inside the Roslyn that hosts it.
    /// </summary>
    /// <param name="name">The simple assembly name.</param>
    internal static bool IsHostAssembly(string name) =>
        name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) || IsCompilerAssembly(name);

    /// <summary>
    /// The compiler's own assemblies and the libraries in its API, which an analyzer must share with the compiler that
    /// runs it. Another assembly named Microsoft.CodeAnalysis.*, such as Microsoft.CodeAnalysis.ResxSourceGenerator, may
    /// be a package's own.
    /// </summary>
    /// <param name="name">The simple assembly name.</param>
    internal static bool IsCompilerAssembly(string name) =>
        name is "Microsoft.CodeAnalysis" or "Microsoft.CodeAnalysis.CSharp" or "Microsoft.CodeAnalysis.VisualBasic"
            or "Microsoft.CodeAnalysis.Workspaces" or "Microsoft.CodeAnalysis.CSharp.Workspaces" or "Microsoft.CodeAnalysis.VisualBasic.Workspaces"
            or "System.Collections.Immutable" or "System.Reflection.Metadata" or "netstandard";

    /// <summary>The analyzers of one folder, and the libraries next to them.</summary>
    private sealed class FolderContext(AnalyzerLoader owner, string folder) : AssemblyLoadContext($"TabStudio analyzers: {folder}")
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
            if (assemblyName.Name is not { } name)
            {
                return null;
            }

            // The compiler and the libraries it shares with analyzers come from TabStudio itself. Another assembly
            // named Microsoft.CodeAnalysis.*, such as Microsoft.CodeAnalysis.ResxSourceGenerator beside its C#
            // generator, is the package's own and comes from its folder.
            var candidate = Path.Combine(folder, name + ".dll");
            if (IsCompilerAssembly(name) || (IsHostAssembly(name) && !File.Exists(candidate)))
            {
                return null;
            }

            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }

            // Loaded in its own folder's context, so every analyzer that uses it shares the same types.
            return owner.FindDependency(name, folder) is { } other ? owner.ContextFor(other).Load(other) : null;
        }
    }
}
