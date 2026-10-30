using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Packages;
using TabStudio.Wpf.Building;

namespace TabStudio.App.Services;

/// <summary>
/// The parts of the C# toolchain that are shared by every workspace: the reference assemblies, a warm
/// Roslyn and the NuGet package restorer. Warming up happens in the background at startup, so the first file
/// opens with colors and the first F5 is fast.
/// </summary>
public sealed class CompilerServices
{
    private readonly AppPaths _paths;
    private readonly Lazy<Task<IReadOnlyList<MetadataReference>>> _references;
    private Task? _warmUp;

    /// <summary>Creates the services. Nothing is loaded until it is first needed or <see cref="WarmUpAsync"/> runs.</summary>
    /// <param name="paths">Where the reference assemblies are cached and packages are kept.</param>
    /// <param name="http">The HTTP client for downloading packages; a new one when <see langword="null"/>.</param>
    public CompilerServices(AppPaths paths, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
        _references = new Lazy<Task<IReadOnlyList<MetadataReference>>>(
            () => Task.Run(() => ReferenceAssemblies.LoadMapped(Path.Combine(_paths.Cache, "ReferenceAssemblies"))));
        Packages = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = paths.Packages,
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
                CompilerVersion = CompilerVersion,
            },
            http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
    }

    /// <summary>Restores NuGet packages into the app's packages folder, shared by every workspace.</summary>
    public PackageRestorer Packages { get; }

    /// <summary>The version of Roslyn inside TabStudio, which decides which analyzers of a package it can run.</summary>
    public static Version CompilerVersion { get; } = typeof(Microsoft.CodeAnalysis.Compilation).Assembly.GetName().Version is { } version
        ? new Version(version.Major, version.Minor)
        : new Version(5, 0);

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
        var host = new RoslynWorkspaceHost(references);
        host.SetWpfXamlCompiler(WpfXamlCompiler);
        host.SetWindowsFormsReferences(WindowsFormsReferences.References);
        host.SetVisualBasicReferences(VisualBasicReferences.References);
        return host;
    }

    /// <summary>OpenSilver's XAML compiler for WPF projects, which reads .NET's reference assemblies from the cache folder.</summary>
    private OpenSilverWpfCompiler WpfXamlCompiler => field ??= new OpenSilverWpfCompiler(ReferenceAssemblies.ExtractTo(Path.Combine(_paths.Cache, "ReferenceAssemblies")));

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
