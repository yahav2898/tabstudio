using TabStudio.Compilation;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Swarm.Coordinator;

/// <summary>A build of the app, as the tablet sends it to every device of the swarm.</summary>
public sealed record SwarmBuild
{
    /// <summary>The app's name, such as <c>HelloAvalonia</c>.</summary>
    public required string AppName { get; init; }

    /// <summary>How the devices run it.</summary>
    public required SwarmAppKind Kind { get; init; }

    /// <summary>The app's assembly.</summary>
    public required CompiledAssembly Entry { get; init; }

    /// <summary>The assemblies of the projects it references.</summary>
    public IReadOnlyList<CompiledAssembly> Dependencies { get; init; } = [];

    /// <summary>
    /// Full paths of the package assemblies it loads, as NuGet restored them on the tablet. Leave out what every device
    /// already has, such as Avalonia, which TabStudio itself brings.
    /// </summary>
    public IReadOnlyList<string> PackageAssemblies { get; init; } = [];

    /// <summary>Command-line arguments, for console apps.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>How to debug it on the devices, or <see langword="null"/> to run it without the debugger.</summary>
    public SwarmDebugOptions? Debug { get; init; }

    /// <summary>For a web app: where the tablet serves it and which page to open.</summary>
    public SwarmWebTarget? Web { get; init; }
}

/// <summary>How the devices debug a build.</summary>
/// <param name="DebugInfo">The debugger's map of the instrumented build.</param>
/// <param name="BreakOnUnhandledExceptions">Whether an exception about to go unhandled pauses the app.</param>
/// <param name="PauseAtStart">Whether the app pauses at its first statement, as F10 and F11 start debugging.</param>
public sealed record SwarmDebugOptions(DebugInfoMap DebugInfo, bool BreakOnUnhandledExceptions = true, bool PauseAtStart = false);

/// <summary>A web app the tablet serves, which every device opens through the swarm's tunnel.</summary>
/// <param name="Address">Where the tablet serves it, on its loopback address, such as <c>http://127.0.0.1:52713/</c>.</param>
/// <param name="LaunchPath">The page to open first, such as <c>/</c> or <c>/swagger</c>.</param>
public sealed record SwarmWebTarget(Uri Address, string LaunchPath = "/");
