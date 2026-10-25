using TabStudio.Compilation;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Coordinator;

/// <summary>
/// A build as the devices receive it: every file named by the hash of its bytes, so each device is sent only the files
/// it lacks, and the run request that refers to them.
/// </summary>
internal sealed class SwarmPayload
{
    private readonly Dictionary<string, Func<byte[]>> _contents = new(StringComparer.Ordinal);
    private readonly List<SwarmFileEntry> _files = [];
    private readonly SwarmBuild _build;
    private SwarmAssemblyEntry _entry = null!;
    private List<SwarmAssemblyEntry> _dependencies = [];
    private List<SwarmPackageEntry> _packages = [];

    private SwarmPayload(SwarmBuild build) => _build = build;

    /// <summary>Every distinct file of the build.</summary>
    public IReadOnlyList<SwarmFileEntry> Files => _files;

    /// <summary>Names the files of a build.</summary>
    /// <param name="build">The build.</param>
    /// <param name="hashes">The known hashes of package assemblies.</param>
    /// <exception cref="IOException">A package assembly cannot be read.</exception>
    public static SwarmPayload Create(SwarmBuild build, SwarmPackageHashes hashes)
    {
        var payload = new SwarmPayload(build);
        payload._entry = payload.AddAssembly(build.Entry);
        payload._dependencies = [.. build.Dependencies.Select(payload.AddAssembly)];
        payload._packages = [.. build.PackageAssemblies.Select(path =>
        {
            var (hash, length) = hashes.Of(path);
            payload.Add(hash, length, () => File.ReadAllBytes(path));
            return new SwarmPackageEntry(Path.GetFileName(path), hash);
        })];
        return payload;
    }

    /// <summary>The bytes of a file.</summary>
    /// <param name="hash">The file's hash.</param>
    public byte[] Read(string hash) => _contents[hash]();

    /// <summary>The run request, once the device's missing files are known.</summary>
    /// <param name="run">The run.</param>
    /// <param name="attached">The hashes of the files attached to the request, in order.</param>
    /// <param name="breakpoints">The breakpoints now.</param>
    /// <param name="restart">Whether the device starts the app again when it runs this run already.</param>
    public SwarmRunRequest CreateRequest(SwarmRun run, IReadOnlyList<string> attached, IReadOnlyList<SwarmBreakpoint> breakpoints, bool restart)
    {
        var debug = _build.Debug is { } options
            ? new SwarmDebugSetup(options.DebugInfo.Statements, options.DebugInfo.Methods, breakpoints, options.BreakOnUnhandledExceptions, options.PauseAtStart)
            : null;
        var web = _build.Web is { } target && run.WebSecret is { } secret ? new SwarmWebSetup(secret, target.LaunchPath) : null;
        return new SwarmRunRequest(run.Id, _build.AppName, _build.Kind, _entry, _dependencies, _packages, attached, _build.Arguments, debug, web, restart);
    }

    private SwarmAssemblyEntry AddAssembly(CompiledAssembly assembly)
    {
        var image = SwarmFiles.Hash(assembly.Image);
        Add(image, assembly.Image.Length, () => assembly.Image);
        string? symbols = null;
        if (assembly.Symbols is { Length: > 0 } pdb)
        {
            symbols = SwarmFiles.Hash(pdb);
            Add(symbols, pdb.Length, () => pdb);
        }

        return new SwarmAssemblyEntry(assembly.Name, image, symbols);
    }

    private void Add(string hash, long length, Func<byte[]> content)
    {
        if (_contents.TryAdd(hash, content))
        {
            _files.Add(new SwarmFileEntry(hash, length));
        }
    }
}
