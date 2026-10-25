using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// The load context of one kernel session: the notebook host, every cell's assembly, and the assemblies that
/// <c>#r</c> references brought in.
/// </summary>
/// <remarks>
/// Each cell compiles to its own assembly that references the assemblies of earlier cells by name, so this
/// context hands those back from memory. The .NET libraries come from the default context, which is the
/// runtime TabStudio itself runs on. Assemblies from <c>#r</c> references are loaded from their files only when
/// the default context does not have them, so a package cannot replace a library the app depends on.
/// </remarks>
internal sealed class SubmissionLoadContext : AssemblyLoadContext
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Assembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _runtimeAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);

    public SubmissionLoadContext(string name)
        : base(name, isCollectible: SupportsUnloading)
    {
        Resolving += OnResolving;
    }

    /// <summary>Mono, which runs .NET on Android, does not support unloading assemblies.</summary>
    private static bool SupportsUnloading => !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();

    /// <summary>Loads an assembly from memory and makes it available to later cells by name.</summary>
    public Assembly LoadImage(byte[] image, byte[]? symbols)
    {
        using var imageStream = new MemoryStream(image, writable: false);
        using var symbolStream = symbols is null ? null : new MemoryStream(symbols, writable: false);
        var assembly = LoadFromStream(imageStream, symbolStream);
        lock (_gate)
        {
            _assemblies[assembly.GetName().Name ?? string.Empty] = assembly;
        }

        return assembly;
    }

    /// <summary>Registers an implementation assembly to load from a file when running code needs it.</summary>
    public void AddRuntimeAssembly(string path)
    {
        var name = System.Reflection.AssemblyName.GetAssemblyName(path).Name ?? Path.GetFileNameWithoutExtension(path);
        lock (_gate)
        {
            _runtimeAssemblyPaths[name] = path;
        }
    }

    /// <summary>Unloads the session's assemblies where the runtime supports it, once nothing uses them any more.</summary>
    public void UnloadIfSupported()
    {
        if (IsCollectible)
        {
            Unload();
        }
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        lock (_gate)
        {
            return assemblyName.Name is { } name && _assemblies.TryGetValue(name, out var assembly) ? assembly : null;
        }
    }

    /// <summary>Called when neither this context nor the default one has an assembly.</summary>
    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        string? path;
        lock (_gate)
        {
            if (assemblyName.Name is not { } name || !_runtimeAssemblyPaths.TryGetValue(name, out path))
            {
                return null;
            }
        }

        var assembly = LoadFromAssemblyPath(path);
        lock (_gate)
        {
            _assemblies[assembly.GetName().Name ?? string.Empty] = assembly;
        }

        return assembly;
    }
}
