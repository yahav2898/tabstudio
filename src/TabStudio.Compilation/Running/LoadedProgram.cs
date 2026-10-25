using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Compilation.Running;

/// <summary>
/// A program loaded into its own context without running it, for hosts that call into it themselves, such as
/// the app preview, which creates an Avalonia app's views. Disposing it unloads the program where the runtime can.
/// </summary>
public sealed class LoadedProgram : IDisposable
{
    private readonly ProgramLoadContext _context;

    private LoadedProgram(ProgramLoadContext context, Assembly entryAssembly)
    {
        _context = context;
        EntryAssembly = entryAssembly;
    }

    /// <summary>The program's assembly.</summary>
    public Assembly EntryAssembly { get; }

    /// <summary>Loads a program.</summary>
    /// <param name="entryAssembly">The program's assembly.</param>
    /// <param name="dependencies">Assemblies of referenced projects, loaded on demand.</param>
    /// <param name="packageAssemblies">Full paths of the package assemblies the program may load.</param>
    /// <param name="sharedWithHost">
    /// Names the assemblies that come from TabStudio itself even when a package brings them, so the program and the
    /// host use the same types, such as Avalonia's; <see langword="null"/> shares nothing beyond .NET.
    /// </param>
    /// <param name="unloadable">
    /// Whether the program can be unloaded when it is disposed. Code the host generates at run time against the
    /// program's types, such as XAML Avalonia compiles as it loads, needs a program that stays loaded.
    /// </param>
    public static LoadedProgram Load(
        CompiledAssembly entryAssembly,
        IReadOnlyList<CompiledAssembly> dependencies,
        IReadOnlyList<string> packageAssemblies,
        Func<string, bool>? sharedWithHost = null,
        bool unloadable = true)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(packageAssemblies);
        var context = new ProgramLoadContext(entryAssembly.Name, dependencies, packageAssemblies, sharedWithHost, unloadable);
        return new LoadedProgram(context, context.LoadFromImage(entryAssembly));
    }

    /// <summary>
    /// Loads every assembly the program may use now, rather than when its code first needs one, for hosts that look
    /// through the loaded assemblies, as Avalonia's XAML loader does for the types XAML names.
    /// </summary>
    public void LoadAll() => _context.LoadAll();

    /// <summary>The program's assemblies loaded so far: its own, its projects' and its packages'.</summary>
    public IEnumerable<Assembly> Assemblies => _context.Assemblies;

    /// <summary>Whether an assembly is part of this program.</summary>
    /// <param name="assembly">The assembly.</param>
    public bool Contains(Assembly assembly) => AssemblyLoadContext.GetLoadContext(assembly) == _context;

    /// <summary>Whether an assembly belongs to a program TabStudio loaded, such as an earlier run of an app, rather than to TabStudio.</summary>
    /// <param name="assembly">The assembly.</param>
    public static bool IsProgramAssembly(Assembly assembly) => AssemblyLoadContext.GetLoadContext(assembly) is ProgramLoadContext;

    /// <inheritdoc />
    public void Dispose() => _context.UnloadIfSupported();
}
