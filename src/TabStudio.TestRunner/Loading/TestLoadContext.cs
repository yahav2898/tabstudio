using System.Reflection;
using System.Runtime.Loader;
using TabStudio.Compilation;

namespace TabStudio.TestRunner.Loading;

/// <summary>
/// Loads a test project into one <see cref="AssemblyLoadContext"/>: the user's assemblies from memory and the
/// test framework's assemblies, such as <c>xunit.core.dll</c>, from the files NuGet restored.
/// </summary>
/// <remarks>
/// <para>
/// Assemblies are found by simple name. A name that is neither a compiled assembly nor a given runtime path is
/// looked up next to the runtime paths, then left to the default context, which is the .NET runtime TabStudio
/// itself runs on.
/// </para>
/// <para>
/// The context is not collectible, for two reasons. Mono, which runs .NET on Android, cannot unload assemblies.
/// And <see cref="DispatchProxy"/>, which implements xUnit's <c>ITestOutputHelper</c>, generates its proxy
/// types in a non-collectible dynamic assembly that may not reference a collectible one.
/// </para>
/// </remarks>
internal sealed class TestLoadContext : AssemblyLoadContext
{
    private readonly Dictionary<string, CompiledAssembly> _compiled;
    private readonly Dictionary<string, string> _runtimePaths;
    private readonly List<string> _probingDirectories;
    private readonly Dictionary<string, Assembly> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <summary>Creates a context for a test project.</summary>
    /// <param name="name">A name for diagnostics, usually the test project's name.</param>
    /// <param name="compiled">The assemblies compiled in memory.</param>
    /// <param name="runtimeAssemblyPaths">The framework and package assemblies, as files.</param>
    public TestLoadContext(string name, IReadOnlyList<CompiledAssembly> compiled, IReadOnlyList<string> runtimeAssemblyPaths)
        : base($"TabStudio tests: {name}", isCollectible: false)
    {
        _compiled = new Dictionary<string, CompiledAssembly>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in compiled)
        {
            _compiled[assembly.Name] = assembly;
        }

        _runtimePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in runtimeAssemblyPaths)
        {
            _ = _runtimePaths.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        _probingDirectories = runtimeAssemblyPaths
            .Select(path => Path.GetDirectoryName(path))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Loads one of the compiled assemblies, with its symbols so stack traces have line numbers. Loading the same
    /// assembly again returns the instance already loaded.
    /// </summary>
    /// <param name="compiled">The assembly.</param>
    public Assembly LoadCompiled(CompiledAssembly compiled)
    {
        lock (_gate)
        {
            if (_loaded.TryGetValue(compiled.Name, out var existing))
            {
                return existing;
            }

            using var image = new MemoryStream(compiled.Image, writable: false);
            using var symbols = compiled.Symbols is null ? null : new MemoryStream(compiled.Symbols, writable: false);
            var assembly = LoadFromStream(image, symbols);
            _loaded[compiled.Name] = assembly;
            return assembly;
        }
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { } name)
        {
            return null;
        }

        if (_compiled.TryGetValue(name, out var compiled))
        {
            return LoadCompiled(compiled);
        }

        if (_runtimePaths.TryGetValue(name, out var path))
        {
            return LoadFromAssemblyPath(path);
        }

        foreach (var directory in _probingDirectories)
        {
            var candidate = Path.Combine(directory, name + ".dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
        }

        return null;
    }
}
