using System.Diagnostics;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.WindowsCompatibility.Building.Wpf;

namespace TabStudio.WindowsCompatibility.Building;

/// <summary>How to build a Windows desktop app.</summary>
public sealed record WindowsBuildOptions
{
    /// <summary>The configuration, which names the output folder: <c>Debug</c> or <c>Release</c>.</summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>The runtime assemblies of a project's NuGet packages, to copy next to the app, by project file.</summary>
    public Func<string, IReadOnlyList<string>> PackageRuntimeAssemblies { get; init; } = _ => [];
}

/// <summary>The outcome of building a Windows desktop app: the build, and where the app is.</summary>
/// <param name="Build">The build's diagnostics and assemblies.</param>
/// <param name="OutputFolder">The app's folder, <c>bin/&lt;configuration&gt;/&lt;target framework&gt;</c>, when the build succeeded.</param>
/// <param name="EntryAssembly">The app's assembly in it.</param>
public sealed record WindowsBuildResult(BuildResult Build, string? OutputFolder, string? EntryAssembly)
{
    /// <summary>Whether the app was built.</summary>
    public bool Success => Build.Success && EntryAssembly is not null;
}

/// <summary>
/// Builds Windows Forms and WPF projects on the tablet the way the Windows Desktop SDK does, for the real .NET
/// Windows Desktop runtime to run under Wine: against the Microsoft.WindowsDesktop.App reference assemblies, with
/// WPF's markup compiler for the XAML, into <c>bin/&lt;configuration&gt;/&lt;target framework&gt;</c> with the
/// app's <c>runtimeconfig.json</c>, its project references and its packages.
/// </summary>
/// <remarks>
/// <para>
/// Each project of the build order compiles from the editor's live compilation (so unsaved edits and generated
/// code are in), with its references replaced: .NET's own, the Windows Desktop profile the project uses, its
/// packages, and its project references as just built. A WPF project first runs MarkupCompilePass1, whose partial
/// classes join the compilation and the editor's IntelliSense; pages that use the project's own types are compiled
/// by MarkupCompilePass2 with an assembly built from that first pass; then the pages and <c>Resource</c> items are
/// packed into <c>&lt;assembly&gt;.g.resources</c>.
/// </para>
/// <para>
/// Diagnostics land in the Error List with their files and lines: XAML errors from the markup compiler where they
/// are, and C# errors in the generated code on the XAML lines it came from.
/// </para>
/// </remarks>
public sealed class WindowsDesktopBuilder
{
    /// <summary>The symbols the SDK defines for a <c>-windows</c> target framework.</summary>
    public static readonly IReadOnlyList<string> WindowsSymbols = ["WINDOWS", "WINDOWS7_0_OR_GREATER"];

    private readonly RoslynWorkspaceHost _host;
    private readonly IWindowsReferencePack _pack;
    private readonly IReadOnlyList<string> _netReferences;

    /// <summary>Creates the builder.</summary>
    /// <param name="host">The editor's Roslyn workspace.</param>
    /// <param name="pack">Gets the Windows Desktop reference assemblies.</param>
    /// <param name="netReferences">.NET's own reference assemblies as files, which the markup compiler resolves types in.</param>
    public WindowsDesktopBuilder(RoslynWorkspaceHost host, IWindowsReferencePack pack, IReadOnlyList<string> netReferences)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(netReferences);
        _host = host;
        _pack = pack;
        _netReferences = netReferences;
    }

    /// <summary>Builds a project and the projects it references.</summary>
    /// <param name="project">The project, a Windows Forms or WPF app.</param>
    /// <param name="options">The configuration and the packages' assemblies.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public async Task<WindowsBuildResult> BuildAsync(ProjectInfo project, WindowsBuildOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        var clock = Stopwatch.StartNew();
        var diagnostics = new List<BuildDiagnostic>();
        var built = new List<CompiledAssembly>();
        CompiledAssembly? entry = null;
        foreach (var current in _host.GetBuildOrder(project.FilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await BuildOneAsync(current, built, options, diagnostics, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return Failed(project, diagnostics, built, clock);
            }

            if (current.FilePath == project.FilePath)
            {
                entry = result;
            }
            else
            {
                built.Add(result);
            }
        }

        if (entry is null)
        {
            diagnostics.Add(new BuildDiagnostic(ProjectBuilder.UnsupportedLanguageId, DiagnosticSeverityLevel.Error, $"'{project.Name}' is not loaded, so it cannot be built.", null, project.Name));
            return Failed(project, diagnostics, built, clock);
        }

        var desktop = WindowsDesktopProject.From(project);
        var output = desktop?.OutputFolder(options.Configuration) ?? Path.Combine(project.DirectoryPath, "bin", options.Configuration, "net10.0-windows");
        var references = desktop is null ? null : await _pack.ResolveAsync(desktop, cancellationToken).ConfigureAwait(false);
        var entryPath = WriteOutput(project, desktop, entry, built, output, options, references?.Assemblies ?? []);
        return new WindowsBuildResult(new BuildResult(project.Name, Sort(diagnostics), entry, built, clock.Elapsed), output, entryPath);
    }

    /// <summary>
    /// Runs WPF's first markup pass for a project, so the editor knows its pages' partial classes
    /// (<c>InitializeComponent</c> and the named elements) before the project is built.
    /// </summary>
    /// <param name="project">The project; nothing happens for one that does not use WPF.</param>
    /// <param name="configuration">The configuration, which names the intermediate folder.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The markup compiler's diagnostics.</returns>
    public async Task<IReadOnlyList<BuildDiagnostic>> UpdateMarkupAsync(ProjectInfo project, string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (WindowsDesktopProject.From(project) is not { UsesWpf: true } desktop)
        {
            return [];
        }

        var references = await _pack.ResolveAsync(desktop, cancellationToken).ConfigureAwait(false);
        if (!references.Success)
        {
            return references.Diagnostics;
        }

        var input = MarkupInput(desktop, configuration, References(project, references.Assemblies, []));
        var pass1 = await RunMarkupAsync(() => WpfMarkupCompiler.CompilePass1Async(input, cancellationToken), project).ConfigureAwait(false);
        if (pass1.Success)
        {
            _host.SetGeneratedSources(project.FilePath, ReadSources(pass1.CodeFiles));
        }

        return pass1.Diagnostics;
    }

    private async Task<CompiledAssembly?> BuildOneAsync(ProjectInfo project, List<CompiledAssembly> built, WindowsBuildOptions options, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var desktop = WindowsDesktopProject.From(project);
        IReadOnlyList<string> pack = [];
        if (desktop is not null)
        {
            var resolved = await _pack.ResolveAsync(desktop, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(resolved.Diagnostics);
            if (!resolved.Success)
            {
                return null;
            }

            pack = resolved.Assemblies;
        }

        var references = References(project, pack, [], includeFramework: false);
        var variant = new CompilationVariant
        {
            References = references,
            ProjectReferences = built,
            PreprocessorSymbols = desktop is null ? [] : WindowsSymbols,
        };

        if (desktop is not { UsesWpf: true })
        {
            return Emitted(await _host.EmitVariantAsync(project.FilePath, variant, cancellationToken).ConfigureAwait(false), diagnostics, null);
        }

        // WPF: the first markup pass, then the assembly, with a second pass first when pages use the project's own types.
        var folder = Path.Combine(project.DirectoryPath, desktop.IntermediateFolder(options.Configuration));
        var referenceFiles = WriteReferenceImages(built, Path.Combine(folder, "refs"));
        var input = MarkupInput(desktop, options.Configuration, References(project, pack, referenceFiles));
        var pass1 = await RunMarkupAsync(() => WpfMarkupCompiler.CompilePass1Async(input, cancellationToken), project).ConfigureAwait(false);
        diagnostics.AddRange(pass1.Diagnostics);
        if (!pass1.Success)
        {
            return null;
        }

        var sources = ReadSources(pass1.CodeFiles);
        _host.SetGeneratedSources(project.FilePath, sources);
        var generated = sources.ToDictionary(s => s.Path, s => s.Text, StringComparer.Ordinal);
        variant = variant with { Sources = sources };
        var baml = pass1.BamlFiles.ToList();
        if (pass1.RequiresPass2)
        {
            var temporary = Emitted(await _host.EmitVariantAsync(project.FilePath, variant, cancellationToken).ConfigureAwait(false), diagnostics, generated);
            if (temporary is null)
            {
                return null;
            }

            var localTypes = Path.Combine(folder, desktop.AssemblyName + "_wpftmp.dll");
            await File.WriteAllBytesAsync(localTypes, temporary.Image, cancellationToken).ConfigureAwait(false);
            var pass2 = await RunMarkupAsync(() => WpfMarkupCompiler.CompilePass2Async(input, localTypes, cancellationToken), project).ConfigureAwait(false);
            diagnostics.AddRange(pass2.Diagnostics);
            File.Delete(localTypes);
            if (!pass2.Success)
            {
                return null;
            }

            baml.AddRange(pass2.BamlFiles);
        }

        var resources = await WpfMarkupCompiler.PackResourcesAsync(input, baml.Distinct(StringComparer.Ordinal).ToList(), cancellationToken).ConfigureAwait(false);
        diagnostics.AddRange(resources.Diagnostics);
        if (resources is { Name: { } name, Content: { } content })
        {
            variant = variant with { Resources = [new VariantResource(name, content)] };
        }

        return Emitted(await _host.EmitVariantAsync(project.FilePath, variant, cancellationToken).ConfigureAwait(false), diagnostics, generated);
    }

    /// <summary>The assemblies a project compiles against: .NET's when asked, then Windows', then its packages, then its built project references.</summary>
    private List<string> References(ProjectInfo project, IReadOnlyList<string> pack, IReadOnlyList<string> projectReferences, bool includeFramework = true)
    {
        var windows = pack.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A package may carry an assembly Windows Desktop also has; the framework's wins, as the SDK's conflict resolution decides.
        var packages = _host.GetPackageReferencePaths(project.FilePath).Where(path => !windows.Contains(Path.GetFileName(path)));
        return [.. includeFramework ? _netReferences : [], .. pack, .. packages, .. projectReferences];
    }

    private static WpfMarkupInput MarkupInput(WindowsDesktopProject desktop, string configuration, IReadOnlyList<string> references)
    {
        var items = desktop.ReadWpfItems();
        return new WpfMarkupInput
        {
            ProjectFile = desktop.Project.FilePath,
            ProjectName = desktop.Project.Name,
            AssemblyName = desktop.AssemblyName,
            RootNamespace = desktop.RootNamespace,
            OutputType = desktop.OutputType,
            IntermediateDirectory = desktop.IntermediateFolder(configuration),
            ApplicationMarkup = desktop.IsApp ? items.ApplicationDefinition : null,
            PageMarkup = desktop.IsApp || items.ApplicationDefinition is null ? items.Pages : [.. items.Pages, items.ApplicationDefinition],
            Resources = items.Resources,
            SourceFiles = [.. ProjectSourceFiles.Enumerate(desktop.Project).Select(path => Path.GetRelativePath(desktop.Directory, path))],
            References = references,
            DefineConstants = string.Join(';', ["TRACE", configuration.ToUpperInvariant(), "NET", .. WindowsSymbols]),
            XamlDebuggingInformation = string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Runs a markup pass, reporting a failure of the tasks themselves as an error rather than letting it escape.</summary>
    private static async Task<WpfMarkupResult> RunMarkupAsync(Func<Task<WpfMarkupResult>> pass, ProjectInfo project)
    {
        try
        {
            return await pass().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return new WpfMarkupResult(false, [], [], false,
                [new BuildDiagnostic("MC1000", DiagnosticSeverityLevel.Error, $"The WPF markup compiler stopped: {ex.Message}", null, project.Name)]);
        }
    }

    private static CompiledAssembly? Emitted(CompilationResult result, List<BuildDiagnostic> diagnostics, IReadOnlyDictionary<string, string>? generated)
    {
        diagnostics.AddRange(generated is null ? result.Diagnostics : GeneratedCodeMapper.Map(result.Diagnostics, generated));
        return result.Assembly;
    }

    private static List<GeneratedSource> ReadSources(IReadOnlyList<string> files) =>
        [.. files.Where(File.Exists).Select(file => new GeneratedSource(file, File.ReadAllText(file)))];

    /// <summary>Writes the project references just built where the markup compiler can load them.</summary>
    private static List<string> WriteReferenceImages(List<CompiledAssembly> built, string folder)
    {
        if (built.Count == 0)
        {
            return [];
        }

        Directory.CreateDirectory(folder);
        return [.. built.Select(assembly =>
        {
            var path = Path.Combine(folder, assembly.Name + ".dll");
            File.WriteAllBytes(path, assembly.Image);
            return path;
        })];
    }

    /// <summary>Lays out the app's folder: its assembly, its project references and packages, and its runtime configuration.</summary>
    private static string WriteOutput(ProjectInfo project, WindowsDesktopProject? desktop, CompiledAssembly entry, List<CompiledAssembly> built, string output, WindowsBuildOptions options, IReadOnlyList<string> pack)
    {
        Directory.CreateDirectory(output);
        foreach (var assembly in built.Append(entry))
        {
            File.WriteAllBytes(Path.Combine(output, assembly.Name + ".dll"), assembly.Image);
            if (assembly.Symbols is { } symbols)
            {
                File.WriteAllBytes(Path.Combine(output, assembly.Name + ".pdb"), symbols);
            }
        }

        // Package assemblies Windows Desktop itself has stay out, so the app gets the framework's own.
        var windows = pack.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projects = built.Select(a => a.Name + ".dll").Append(entry.Name + ".dll").ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in options.PackageRuntimeAssemblies(project.FilePath))
        {
            var name = Path.GetFileName(file);
            if (!windows.Contains(name) && !projects.Contains(name) && File.Exists(file))
            {
                File.Copy(file, Path.Combine(output, name), overwrite: true);
            }
        }

        var entryPath = Path.Combine(output, entry.Name + ".dll");
        if (project.IsExecutable)
        {
            File.WriteAllText(Path.Combine(output, entry.Name + ".runtimeconfig.json"), RuntimeConfig.Create(RuntimeConfig.MajorVersion(desktop?.TargetFramework ?? "net10.0")));
        }

        return entryPath;
    }

    private static WindowsBuildResult Failed(ProjectInfo project, List<BuildDiagnostic> diagnostics, List<CompiledAssembly> built, Stopwatch clock) =>
        new(new BuildResult(project.Name, Sort(diagnostics), null, built, clock.Elapsed), null, null);

    private static List<BuildDiagnostic> Sort(List<BuildDiagnostic> diagnostics) =>
        [.. diagnostics.Distinct().OrderByDescending(d => d.Severity)];
}
