using Microsoft.CodeAnalysis;
using TabStudio.Compilation;

namespace TabStudio.App.Services;

/// <summary>
/// The parts of the C# toolchain that are shared by every workspace: the reference assemblies and a warm
/// Roslyn. Warming up happens in the background at startup, so the first file opens with colors and the
/// first F5 is fast.
/// </summary>
public sealed class CompilerServices
{
    private readonly AppPaths _paths;
    private readonly Lazy<Task<IReadOnlyList<MetadataReference>>> _references;
    private Task? _warmUp;

    /// <summary>Creates the services. Nothing is loaded until it is first needed or <see cref="WarmUpAsync"/> runs.</summary>
    /// <param name="paths">Where the reference assemblies are cached.</param>
    public CompilerServices(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
        _references = new Lazy<Task<IReadOnlyList<MetadataReference>>>(
            () => Task.Run(() => ReferenceAssemblies.LoadMapped(Path.Combine(_paths.Cache, "ReferenceAssemblies"))));
    }

    /// <summary>Raised on a background thread when Roslyn finishes warming up.</summary>
    public event EventHandler? Warmed;

    /// <summary>Whether Roslyn is loaded and ready.</summary>
    public bool IsWarm { get; private set; }

    /// <summary>The .NET reference assemblies, memory-mapped from the cache folder.</summary>
    public Task<IReadOnlyList<MetadataReference>> GetReferencesAsync() => _references.Value;

    /// <summary>Creates a language service host for a new workspace.</summary>
    public async Task<RoslynWorkspaceHost> CreateHostAsync()
    {
        var references = await GetReferencesAsync().ConfigureAwait(false);
        return new RoslynWorkspaceHost(references);
    }

    /// <summary>
    /// Loads the reference assemblies, composes Roslyn's services and compiles a tiny program, so the code
    /// paths the editor needs are ready. Safe to call more than once.
    /// </summary>
    public Task WarmUpAsync() => _warmUp ??= Task.Run(async () =>
    {
        var references = await GetReferencesAsync().ConfigureAwait(false);
        using var host = new RoslynWorkspaceHost(references);
        SyntaxClassifier.Classify("var greeting = \"Hello\"; System.Console.WriteLine(greeting);");
        new CSharpCompiler(references).Compile(new CompilationRequest
        {
            AssemblyName = "TabStudioWarmUp",
            Documents = [new SourceDocument("WarmUp.cs", "System.Console.WriteLine(\"warm\");")],
        });

        IsWarm = true;
        Warmed?.Invoke(this, EventArgs.Empty);
    });
}
