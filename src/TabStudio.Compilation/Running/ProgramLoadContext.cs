using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Compilation.Running;

/// <summary>
/// Loads a program and the projects it references from memory, and its NuGet packages from disk. Everything
/// else, such as the .NET libraries, comes from the default context, which is the runtime TabStudio itself runs on.
/// </summary>
/// <remarks>
/// Assemblies a host shares with the program, such as Avalonia when the app preview shows an Avalonia app inside
/// TabStudio's own window, also come from the default context, even when a package brings its own copy.
/// </remarks>
internal sealed class ProgramLoadContext : AssemblyLoadContext
{
    private readonly Dictionary<string, CompiledAssembly> _dependencies;
    private readonly Dictionary<string, string> _packageAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, bool>? _sharedWithHost;

    // Uno navigation descriptors include the context name. Every build must have its own name,
    // including on Android where old contexts remain loaded after a restart.
    public ProgramLoadContext(string name, IReadOnlyList<CompiledAssembly> dependencies, IReadOnlyList<string> packageAssemblies, Func<string, bool>? sharedWithHost, bool unloadable = true)
        : base($"TabStudio program: {name} ({Guid.NewGuid():N})", isCollectible: unloadable && SupportsUnloading)
    {
        // A debug load context can live for the process lifetime. It needs images for lazy loads, never the
        // compiler graphs; only the active Hot Reload session should keep those alive.
        _dependencies = dependencies.Select(d => d with { HotReloadCompilation = null })
            .ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        _sharedWithHost = sharedWithHost;
        foreach (var path in packageAssemblies)
        {
            // The first assembly of a name wins, as in the restore's list.
            _packageAssemblies.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }
    }

    /// <summary>Mono, which runs .NET on Android, does not support unloading assemblies.</summary>
    private static bool SupportsUnloading => !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();

    public Assembly LoadFromImage(CompiledAssembly compiled)
    {
        using var image = new MemoryStream(compiled.Image, writable: false);
        using var symbols = compiled.Symbols is null ? null : new MemoryStream(compiled.Symbols, writable: false);
        var assembly = LoadFromStream(image, symbols);
        lock (_images)
        {
            // Asking for it by name, as Type.GetType("T, App") does, finds this one rather than loading it again.
            _images[compiled.Name] = assembly;
        }

        return assembly;
    }

    /// <summary>Loads every project and package assembly the program may use, rather than when its code first does.</summary>
    public void LoadAll()
    {
        foreach (var name in _dependencies.Keys.Concat(_packageAssemblies.Keys).ToList())
        {
            try
            {
                LoadFromAssemblyName(new AssemblyName(name));
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                // The program fails the same way if its code ever needs it.
            }
        }
    }

    public void UnloadIfSupported()
    {
        if (IsCollectible)
        {
            Unload();
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { } name || _sharedWithHost?.Invoke(name) == true)
        {
            return null;
        }

        lock (_images)
        {
            if (_images.TryGetValue(name, out var loaded))
            {
                return loaded;
            }
        }

        if (_dependencies.TryGetValue(name, out var compiled))
        {
            return LoadFromImage(compiled);
        }

        return _packageAssemblies.TryGetValue(name, out var path) ? LoadFromAssemblyPath(path) : null;
    }
}
