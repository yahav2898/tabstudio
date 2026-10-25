using TabStudio.Compilation;
using TabStudio.Debugging;

namespace TabStudio.Swarm.Device;

/// <summary>An app the tablet sent to a device, ready to show.</summary>
public sealed record SwarmApp
{
    /// <summary>The run it belongs to.</summary>
    public required long RunId { get; init; }

    /// <summary>The app's name, such as <c>HelloAvalonia</c>.</summary>
    public required string Name { get; init; }

    /// <summary>How the device shows it.</summary>
    public required SwarmAppKind Kind { get; init; }

    /// <summary>The app's assembly, for an Avalonia app.</summary>
    public CompiledAssembly? Entry { get; init; }

    /// <summary>The assemblies of the projects it references.</summary>
    public IReadOnlyList<CompiledAssembly> Dependencies { get; init; } = [];

    /// <summary>Full paths of its package assemblies, in the device's cache.</summary>
    public IReadOnlyList<string> PackageAssemblies { get; init; } = [];

    /// <summary>
    /// The debugger, when the tablet debugs the app on this device: attach it to the app's assemblies once they are
    /// loaded, and set its wait pump so the screen keeps drawing while the app is paused.
    /// </summary>
    public DebugSession? Debugger { get; init; }

    /// <summary>For a web app: the page the web view opens, on the device's loopback gateway.</summary>
    public Uri? Address { get; init; }
}
