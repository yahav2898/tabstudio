using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;

namespace TabStudio.Uno.Hosting;

/// <summary>
/// Compiles TabStudio's Uno Platform host (the sources under <c>Host/</c>, carried as resources) on the device, against
/// the Uno Platform assemblies a project restored, so the host always matches the Uno version it runs.
/// </summary>
/// <remarks>
/// <para>
/// A host plugs into Uno through Uno's internal hosting API (windows, input, rendering, the dispatcher), which Uno's
/// own hosts reach through <c>InternalsVisibleTo</c>. TabStudio's host is compiled the way C#'s scripting and the
/// debugger's expression evaluator compile against non-public members: Roslyn imports every member of the references
/// and binds without accessibility checks, and the host's <c>IgnoresAccessChecksTo</c> attributes tell the runtime
/// to allow the access, which CoreCLR and Mono both honor.
/// </para>
/// <para>
/// The result is kept on disk next to a key made of the host's sources and the identities of the assemblies it was
/// compiled against, so a device compiles it once per Uno version.
/// </para>
/// </remarks>
public static class UnoHostCompiler
{
    /// <summary>The host assembly's name.</summary>
    public const string AssemblyName = "TabStudio.UnoHost";

    /// <summary>The host's entry type, whose static members TabStudio calls.</summary>
    public const string HostTypeName = "TabStudio.UnoHost.PreviewHost";

    private const string ResourcePrefix = "UnoHost/";

    /// <summary>The libraries the host and TabStudio share, in TabStudio's copy, when the app's packages do not bring them.</summary>
    private static readonly string[] SharedWithTabStudio = ["SkiaSharp", "HarfBuzzSharp"];

    /// <summary>The host's source files, by file name.</summary>
    public static IReadOnlyList<(string Name, string Text)> Sources { get; } = LoadSources();

    /// <summary>Compiles the host into a folder, unless the one there was compiled from the same sources against the same assemblies.</summary>
    /// <param name="unoAssemblies">The Uno Platform assemblies the app runs with, Skia's implementations.</param>
    /// <param name="outputDirectory">The folder to put <c>TabStudio.UnoHost.dll</c> in.</param>
    /// <param name="cancellationToken">Cancels the compilation.</param>
    /// <returns>The path of the host assembly.</returns>
    /// <exception cref="UnoHostException">The host does not compile against these assemblies, as for an Uno version whose hosting API changed.</exception>
    public static string Compile(IReadOnlyList<string> unoAssemblies, string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unoAssemblies);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);

        var references = CreateReferences(unoAssemblies);
        var key = ComputeKey(references);
        var path = Path.Combine(outputDirectory, AssemblyName + ".dll");
        var keyPath = path + ".key";
        if (File.Exists(path) && File.Exists(keyPath) && File.ReadAllText(keyPath) == key)
        {
            return path;
        }

        var compilation = CreateCompilation(references.Select(r => r.Reference), cancellationToken);
        using var image = new MemoryStream();
        var result = compilation.Emit(image, cancellationToken: cancellationToken);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(10)
                .Select(d => d.ToString())
                .ToList();
            throw new UnoHostException(
                $"TabStudio's app preview cannot host this version of Uno Platform: its hosting API is not the one TabStudio supports.{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        Directory.CreateDirectory(outputDirectory);
        var staging = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllBytes(staging, image.ToArray());
        File.Move(staging, path, overwrite: true);
        File.WriteAllText(keyPath, key);
        return path;
    }

    /// <summary>Creates the host's compilation, which binds to Uno's internal members.</summary>
    /// <param name="references">The assemblies to compile against.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    public static CSharpCompilation CreateCompilation(IEnumerable<MetadataReference> references, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(references);
        var parse = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: ["HAS_UNO", "HAS_UNO_WINUI", "__SKIA__", "__UNO_SKIA__"]);
        var trees = Sources
            .Select(source => CSharpSyntaxTree.ParseText(SourceText.From(source.Text, Encoding.UTF8), parse, path: source.Name, cancellationToken: cancellationToken))
            .ToList();
        var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: true,
                deterministic: true,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal)
                {
                    // Uno's assemblies reference older versions of .NET's.
                    ["CS1701"] = ReportDiagnostic.Suppress,
                    ["CS1702"] = ReportDiagnostic.Suppress,
                })
            .WithMetadataImportOptions(MetadataImportOptions.All);
        return CSharpCompilation.Create(AssemblyName, trees, [.. ReferenceAssemblies.Net100, .. references], WithoutAccessibilityChecks(options));
    }

    /// <summary>
    /// The options Roslyn binds with when accessibility does not matter, as its scripting and expression evaluator use:
    /// the internal <c>BinderFlags.IgnoreAccessibility</c> of the Roslyn TabStudio pins, which a test keeps available.
    /// </summary>
    public static CSharpCompilationOptions WithoutAccessibilityChecks(CSharpCompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var method = typeof(CSharpCompilationOptions).GetMethod("WithTopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new UnoHostException("This build of Roslyn has no WithTopLevelBinderFlags; the Uno Platform host cannot be compiled.");
        var flags = typeof(CSharpCompilationOptions).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.BinderFlags")
            ?? throw new UnoHostException("This build of Roslyn has no BinderFlags; the Uno Platform host cannot be compiled.");
        var ignoreAccessibility = Enum.Parse(flags, "IgnoreAccessibility");
        return (CSharpCompilationOptions)method.Invoke(options, [ignoreAccessibility])!;
    }

    /// <summary>
    /// The references: Uno's assemblies, and SkiaSharp, which the host draws with in the same copy as TabStudio: the one
    /// already loaded, whose metadata is read in memory (inside an Android app, assemblies have no file of their own).
    /// </summary>
    private static List<(MetadataReference Reference, Guid Identity)> CreateReferences(IReadOnlyList<string> unoAssemblies)
    {
        var references = new List<(MetadataReference, Guid)>();
        foreach (var path in unoAssemblies)
        {
            references.Add((MetadataReference.CreateFromFile(path), ReadModuleVersionId(path)));
        }

        foreach (var shared in SharedWithTabStudio)
        {
            if (unoAssemblies.Any(path => string.Equals(Path.GetFileNameWithoutExtension(path), shared, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (LoadedReference(shared) is { } loaded)
            {
                references.Add(loaded);
            }
        }

        return references;
    }

    /// <summary>A reference to an assembly TabStudio itself has loaded, from its metadata in memory.</summary>
    private static unsafe (MetadataReference, Guid)? LoadedReference(string name)
    {
        Assembly assembly;
        try
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }

        if (!assembly.TryGetRawMetadata(out var blob, out var length))
        {
            return null;
        }

        // The metadata belongs to the loaded assembly, which stays loaded for the life of the process.
        var module = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
        return (AssemblyMetadata.Create(module).GetReference(display: name), assembly.ManifestModule.ModuleVersionId);
    }

    private static Guid ReadModuleVersionId(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new System.Reflection.PortableExecutable.PEReader(stream);
        var metadata = reader.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }

    private static string ComputeKey(List<(MetadataReference Reference, Guid Identity)> references)
    {
        var text = new StringBuilder();
        foreach (var (name, source) in Sources)
        {
            text.Append(name).Append('\n').Append(source).Append('\n');
        }

        foreach (var identity in references.Select(r => r.Identity).Order())
        {
            text.Append(identity.ToString("N")).Append('\n');
        }

        text.Append(typeof(CSharpCompilation).Assembly.ManifestModule.ModuleVersionId.ToString("N"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static List<(string, string)> LoadSources()
    {
        var assembly = typeof(UnoHostCompiler).Assembly;
        var sources = new List<(string, string)>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            sources.Add((resource[ResourcePrefix.Length..], reader.ReadToEnd()));
        }

        return sources;
    }
}
