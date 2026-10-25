using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.TestRunner.Loading;

/// <summary>
/// Package assemblies, such as <c>xunit.core.dll</c>, loaded once and shared by every test session that runs with
/// the same packages, when <see cref="TestRunOptions.ShareRuntimeAssemblies"/> asks for it.
/// </summary>
/// <remarks>
/// <para>
/// Test sessions cannot be unloaded (see <see cref="TestLoadContext"/>), so each one keeps what it loaded for as
/// long as TabStudio runs. Live Unit Testing loads a session after every edit that changes code; sharing the
/// framework's assemblies leaves only the user's own small assemblies to each session.
/// </para>
/// <para>
/// One context exists per set of package files, so projects that use different versions of a package never share
/// an assembly. The contexts are not collectible either, for the same reasons as test sessions.
/// </para>
/// </remarks>
internal sealed class PackageLoadContext : AssemblyLoadContext
{
    private static readonly Lock SharedGate = new();
    private static readonly Dictionary<string, PackageLoadContext> Shared = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _paths;
    private readonly List<string> _probingDirectories;
    private readonly Dictionary<string, Assembly> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    private PackageLoadContext(IReadOnlyList<string> runtimeAssemblyPaths)
        : base("TabStudio test packages", isCollectible: false)
    {
        _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in runtimeAssemblyPaths)
        {
            _ = _paths.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        _probingDirectories = runtimeAssemblyPaths
            .Select(path => Path.GetDirectoryName(path))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The shared context for a set of package files, created the first time the set is used.</summary>
    /// <param name="runtimeAssemblyPaths">The package files.</param>
    public static PackageLoadContext For(IReadOnlyList<string> runtimeAssemblyPaths)
    {
        var key = string.Join('\n', runtimeAssemblyPaths.Order(StringComparer.Ordinal));
        lock (SharedGate)
        {
            if (!Shared.TryGetValue(key, out var context))
            {
                context = new PackageLoadContext(runtimeAssemblyPaths);
                Shared[key] = context;
            }

            return context;
        }
    }

    /// <summary>Loads a package assembly by name, once.</summary>
    /// <param name="assemblyName">The assembly's name.</param>
    /// <returns>The assembly, or <see langword="null"/> when it is not one of the packages' files.</returns>
    public Assembly? Find(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { } name)
        {
            return null;
        }

        lock (_gate)
        {
            if (_loaded.TryGetValue(name, out var loaded))
            {
                return loaded;
            }

            var path = _paths.TryGetValue(name, out var listed)
                ? listed
                : _probingDirectories.Select(directory => Path.Combine(directory, name + ".dll")).FirstOrDefault(File.Exists);
            if (path is null)
            {
                return null;
            }

            var assembly = LoadFromAssemblyPath(path);
            _loaded[name] = assembly;
            return assembly;
        }
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName) => Find(assemblyName);
}
