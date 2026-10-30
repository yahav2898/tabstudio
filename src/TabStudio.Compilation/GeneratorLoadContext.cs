using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Compilation;

/// <summary>
/// Loads a source generator built from a project of the workspace: its assembly and those of the projects it compiles
/// against, from memory. As with <see cref="AnalyzerLoader"/>, the compiler and the libraries its API exposes always come
/// from TabStudio, so the generator runs inside the Roslyn that hosts it. Anything else comes from the default context,
/// the runtime TabStudio runs on, as the compiler prefers its own copy of a library; only what the runtime does not have
/// is loaded from the package assemblies the projects compile against, in the global packages folder.
/// </summary>
/// <remarks>
/// Each build of a generator project gets a context of its own, so an edited generator replaces the one before. Like
/// the analyzer loader's, a context is never unloaded: Mono, which runs .NET on Android, cannot unload assemblies, and a
/// compilation Roslyn still keeps may run the generators of an earlier build. The host builds a generator project again
/// only when what it is made from changed.
/// </remarks>
internal sealed class GeneratorLoadContext : AssemblyLoadContext
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, byte[]> _images;
    private readonly Dictionary<string, string> _packageAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _loaded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the context of one build of a generator project.</summary>
    /// <param name="projectName">The generator project, for the context's name.</param>
    /// <param name="images">
    /// The emitted assemblies of the project and the projects it compiles against, by assembly name, which the context
    /// keeps; its keys compare without case, as assembly names do.
    /// </param>
    /// <param name="packageAssemblies">Full paths of the package assemblies they compile against.</param>
    public GeneratorLoadContext(string projectName, Dictionary<string, byte[]> images, IEnumerable<string> packageAssemblies)
        : base($"TabStudio generators: {projectName} ({Guid.NewGuid():N})", isCollectible: false)
    {
        _images = images;
        foreach (var path in packageAssemblies)
        {
            // The first assembly of a name wins, as in the restore's list.
            _packageAssemblies.TryAdd(Path.GetFileNameWithoutExtension(path), RuntimeAssemblyOf(path));
        }

        Resolving += OnResolving;
    }

    /// <summary>Loads one of the emitted project assemblies, such as the generator project's own.</summary>
    /// <param name="assemblyName">Its assembly name.</param>
    /// <exception cref="FileNotFoundException">No emitted assembly has that name.</exception>
    public Assembly LoadProject(string assemblyName) =>
        LoadImage(assemblyName) ?? throw new FileNotFoundException($"The generator assembly '{assemblyName}' was not emitted.", assemblyName);

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName) =>
        // The compiler's own assemblies, and anything else that is not a project of the workspace, come from the
        // default context next, then from OnResolving.
        assemblyName.Name is { } name && !AnalyzerLoader.IsHostAssembly(name) ? LoadImage(name) : null;

    /// <summary>An emitted project assembly, loaded once; <see langword="null"/> when there is none of that name.</summary>
    private Assembly? LoadImage(string name)
    {
        lock (_gate)
        {
            if (_loaded.TryGetValue(name, out var known))
            {
                return known;
            }

            if (!_images.TryGetValue(name, out var image))
            {
                return null;
            }

            using var stream = new MemoryStream(image, writable: false);
            var assembly = LoadFromStream(stream);
            _loaded[name] = assembly;
            return assembly;
        }
    }

    /// <summary>
    /// Called when neither this context nor the default one has an assembly: a package's, from its file. Never a
    /// second copy of the compiler, such as a newer Roslyn package than TabStudio's, whose types would not be the host's.
    /// </summary>
    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        lock (_gate)
        {
            if (assemblyName.Name is not { } name || AnalyzerLoader.IsHostAssembly(name)
                || !_packageAssemblies.TryGetValue(name, out var path) || !File.Exists(path))
            {
                return null;
            }

            if (!_loaded.TryGetValue(name, out var assembly))
            {
                assembly = LoadFromAssemblyPath(path);
                _loaded[name] = assembly;
            }

            return assembly;
        }
    }

    /// <summary>
    /// The assembly to run for a package assembly a project compiles against: the package's <c>lib/</c> build of a
    /// reference assembly in <c>ref/</c>, which only describes the API and cannot run, when the package has one.
    /// </summary>
    private static string RuntimeAssemblyOf(string compilePath)
    {
        var marker = Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar;
        var index = compilePath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return compilePath;
        }

        var libraryFolder = Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar;
        var library = string.Concat(compilePath.AsSpan(0, index), libraryFolder.AsSpan(), compilePath.AsSpan(index + marker.Length));
        return File.Exists(library) ? library : compilePath;
    }
}
