using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Compiles C# to an assembly in memory, with the same defaults the .NET SDK uses for a new project.
/// </summary>
/// <remarks>
/// An instance is immutable and safe to share. It is used directly for one-off compilations, and its static
/// members give <see cref="RoslynWorkspaceHost"/> the options every project in a workspace is compiled with.
/// </remarks>
public sealed class CSharpCompiler
{
    private readonly IReadOnlyList<MetadataReference> _references;

    /// <summary>Creates a compiler that references the .NET 10 libraries.</summary>
    public CSharpCompiler()
        : this(ReferenceAssemblies.Net100)
    {
    }

    /// <summary>Creates a compiler with a custom set of framework references.</summary>
    /// <param name="frameworkReferences">The reference assemblies every compilation sees.</param>
    public CSharpCompiler(IReadOnlyList<MetadataReference> frameworkReferences)
    {
        ArgumentNullException.ThrowIfNull(frameworkReferences);
        _references = frameworkReferences;
    }

    /// <summary>The namespaces the SDK imports when <c>ImplicitUsings</c> is enabled for a console project.</summary>
    public static IReadOnlyList<string> ImplicitUsingNamespaces { get; } =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    /// <summary>The extra global usings of the Web SDK (<c>Microsoft.NET.Sdk.Web</c>), as it generates them.</summary>
    public static IReadOnlyList<string> WebImplicitUsingNamespaces { get; } =
    [
        "System.Net.Http.Json",
        "Microsoft.AspNetCore.Builder",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Logging",
    ];

    /// <summary>The extra global usings of the Worker SDK (<c>Microsoft.NET.Sdk.Worker</c>), as it generates them.</summary>
    public static IReadOnlyList<string> WorkerImplicitUsingNamespaces { get; } =
    [
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Logging",
    ];

    /// <summary>
    /// The extra global usings of the Blazor WebAssembly SDK (<c>Microsoft.NET.Sdk.BlazorWebAssembly</c>), as its
    /// <c>Sdk.props</c> lists them.
    /// </summary>
    public static IReadOnlyList<string> BlazorWebAssemblyImplicitUsingNamespaces { get; } =
    [
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Logging",
    ];

    private static readonly string[] CommonSymbols =
    [
        "TRACE", "NET", "NETCOREAPP", "NET10_0", "NET10_0_OR_GREATER", "NET9_0_OR_GREATER", "NET8_0_OR_GREATER",
        "NET7_0_OR_GREATER", "NET6_0_OR_GREATER", "NET5_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER", "NETCOREAPP3_0_OR_GREATER",
        "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP2_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP1_0_OR_GREATER",
        "TABSTUDIO",
    ];

    private static readonly EmitOptions EmitOptions = new(debugInformationFormat: DebugInformationFormat.PortablePdb);

    /// <summary>
    /// The warnings the SDK's default <c>NoWarn</c> turns off in every C# project: CS1701 and CS1702 report that a
    /// reference to an older framework assembly, as packages built for .NET Standard have, was unified with the
    /// version the project targets, which is expected.
    /// </summary>
    private static readonly Dictionary<string, ReportDiagnostic> SdkNoWarn = new(StringComparer.Ordinal)
    {
        ["CS1701"] = ReportDiagnostic.Suppress,
        ["CS1702"] = ReportDiagnostic.Suppress,
    };

    /// <summary>The namespaces interceptors are allowed in when the project is not known.</summary>
    private static readonly string[] DefaultInterceptorsNamespaces = [Xaml.AvaloniaXamlGenerator.GeneratedNamespace];

    /// <summary>
    /// Parse options for a project. Generators may put interceptors in the given namespaces; by default, only in that
    /// of TabStudio's generated XAML loader, which redirects Avalonia's <c>AvaloniaXamlLoader.Load(this)</c>.
    /// </summary>
    /// <param name="optimize">Whether this is a release build, which defines <c>RELEASE</c> instead of <c>DEBUG</c>.</param>
    /// <param name="interceptorsNamespaces">The namespaces interceptors are allowed in, as the SDK's <c>InterceptorsNamespaces</c> property lists them.</param>
    /// <param name="languageVersion">The C# version, such as a project's <c>LangVersion</c>; the latest by default.</param>
    public static CSharpParseOptions CreateParseOptions(bool optimize = false, IEnumerable<string>? interceptorsNamespaces = null, LanguageVersion languageVersion = LanguageVersion.Latest) => new CSharpParseOptions(
        languageVersion: languageVersion,
        documentationMode: DocumentationMode.Parse,
        kind: SourceCodeKind.Regular,
        preprocessorSymbols: [.. CommonSymbols, optimize ? "RELEASE" : "DEBUG"])
        .WithFeatures([new("InterceptorsNamespaces", string.Join(';', interceptorsNamespaces ?? DefaultInterceptorsNamespaces))]);

    /// <summary>
    /// The symbols a project's <c>DefineConstants</c> adds, as csc reads it: separated by semicolons or commas, without
    /// a part that still names an MSBuild property (<c>$(DefineConstants)</c>, which the SDK's own symbols stand for).
    /// Without them a project's <c>#if</c> took the wrong branch: Humanizer's tests reached their <c>#error</c>.
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/> for none.</param>
    public static IReadOnlyList<string> DefinedSymbols(TsProject? project) =>
        project?.GetProperty("DefineConstants") is { Length: > 0 } defines
            ? [.. defines.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(symbol => !symbol.Contains("$(", StringComparison.Ordinal) && SyntaxFacts.IsValidIdentifier(symbol))
                .Distinct(StringComparer.Ordinal)]
            : [];

    /// <summary>
    /// The C# version a project compiles with: <c>preview</c> when its <c>LangVersion</c> asks for it, otherwise the
    /// latest. Source generators check it, and some only generate for preview features. A lower version than the latest
    /// is not applied, because the code TabStudio generates for a project (XAML loaders, interceptors) uses current C#.
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/> for the latest.</param>
    public static LanguageVersion LanguageVersionOf(TsProject? project) =>
        project?.GetProperty("LangVersion") is { Length: > 0 } text
        && LanguageVersionFacts.TryParse(text.Trim(), out var version)
        && version == LanguageVersion.Preview
            ? LanguageVersion.Preview
            : LanguageVersion.Latest;

    /// <summary>
    /// Whether a project compiles deterministically, as MSBuild decides: its <c>Deterministic</c> property, else
    /// <see langword="true"/> for an SDK-style project and <see langword="false"/> for a .NET Framework one, whose
    /// <c>AssemblyInfo</c> often has a wildcard version (<c>[assembly: AssemblyVersion("1.0.*")]</c>), which a
    /// deterministic build rejects (CS8357, BC37330: VBDev2Dev's Dots).
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/> for code outside one.</param>
    public static bool IsDeterministic(TsProject? project) =>
        project?.GetProperty("Deterministic")?.Trim() is { Length: > 0 } value
            ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            : project is null || project.Sdk is not null || project.Sdks.Count > 0;

    /// <summary>Compilation options for a project.</summary>
    /// <param name="kind">Whether to produce a program or a library.</param>
    /// <param name="nullable">Whether nullable reference types are enabled.</param>
    /// <param name="optimize">Whether to optimize.</param>
    /// <param name="allowUnsafe">Whether <c>unsafe</c> code is allowed.</param>
    /// <param name="concurrentBuild">Whether Roslyn may compile on several threads. Turned off while the device is hot.</param>
    public static CSharpCompilationOptions CreateCompilationOptions(
        AssemblyKind kind,
        bool nullable,
        bool optimize = false,
        bool allowUnsafe = false,
        bool concurrentBuild = true) => new(
        outputKind: kind == AssemblyKind.ConsoleApplication ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
        optimizationLevel: optimize ? OptimizationLevel.Release : OptimizationLevel.Debug,
        allowUnsafe: allowUnsafe,
        deterministic: true,
        concurrentBuild: concurrentBuild,
        specificDiagnosticOptions: SdkNoWarn);

    /// <summary>
    /// Applies a project's <c>NoWarn</c>, <c>WarningsAsErrors</c> and <c>WarningsNotAsErrors</c>, as csc reads them:
    /// codes separated by semicolons or commas, a bare number meaning that C# warning (<c>649</c> is CS0649). Without
    /// them AutoMapper's tests showed 48 CS0649 warnings their project turns off. <c>TreatWarningsAsErrors</c> is left
    /// out: a warning TabStudio reports that a computer's build does not would fail a build that passes there.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="project">The project.</param>
    public static CSharpCompilationOptions WithProjectWarnings(CSharpCompilationOptions options, TsProject project)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(project);
        var specific = new Dictionary<string, ReportDiagnostic>(options.SpecificDiagnosticOptions, StringComparer.Ordinal);
        foreach (var code in WarningCodes(project.GetProperty("WarningsAsErrors")))
        {
            specific[code] = ReportDiagnostic.Error;
        }

        foreach (var code in WarningCodes(project.GetProperty("WarningsNotAsErrors")))
        {
            specific[code] = ReportDiagnostic.Warn;
        }

        foreach (var code in WarningCodes(project.GetProperty("NoWarn")))
        {
            specific[code] = ReportDiagnostic.Suppress;
        }

        return options.WithSpecificDiagnosticOptions(specific);
    }

    /// <summary>The warning codes of a property such as <c>$(NoWarn);649;CS0618;NU1701</c>.</summary>
    private static IEnumerable<string> WarningCodes(string? value) =>
        (value ?? string.Empty).Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(code => !code.Contains("$(", StringComparison.Ordinal) && code.All(char.IsAsciiLetterOrDigit))
            .Select(code => code.All(char.IsAsciiDigit) ? "CS" + code.PadLeft(4, '0') : code.ToUpperInvariant());

    /// <summary>The source of the implicit global usings file, as the base SDK would generate it.</summary>
    public static string CreateGlobalUsingsSource()
    {
        var builder = new StringBuilder("// <auto-generated/>\n");
        foreach (var name in ImplicitUsingNamespaces)
        {
            builder.Append("global using global::").Append(name).Append(";\n");
        }

        return builder.ToString();
    }

    /// <summary>The implicit global usings of an SDK: the base SDK's, plus those the Web, Worker and Blazor WebAssembly SDKs add.</summary>
    /// <param name="sdk">The project's SDK, such as <c>Microsoft.NET.Sdk.Web</c>, or <see langword="null"/> for the base SDK.</param>
    public static IEnumerable<string> ImplicitUsingsFor(string? sdk)
    {
        IEnumerable<string> extra = sdk switch
        {
            _ when string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) => WebImplicitUsingNamespaces,
            _ when string.Equals(sdk, "Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase) => WorkerImplicitUsingNamespaces,
            _ when string.Equals(sdk, "Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase) => BlazorWebAssemblyImplicitUsingNamespaces,
            _ => [],
        };
        return ImplicitUsingNamespaces.Concat(extra);
    }

    /// <summary>
    /// The source of a project's global usings file, as the SDK generates it: the implicit usings of its SDK when
    /// <c>ImplicitUsings</c> is enabled, with those of Windows Forms when it uses it, without those its
    /// <c>Using Remove</c> items take away, then its <c>Using Include</c> items.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The source, or <see langword="null"/> when the project has no global usings.</returns>
    public static string? CreateGlobalUsingsSource(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var implicitUsings = project.ImplicitUsings
            ? ImplicitUsingsFor(project.Sdk).Concat(WindowsForms.WindowsFormsProject.ImplicitUsings(project))
                .Except([.. project.RemovedUsings, .. Wpf.WpfProject.RemovedUsings(project)], StringComparer.Ordinal).Select(name => new UsingInfo(name))
            : [];
        var usings = implicitUsings.Concat(project.Usings).Distinct().ToList();
        if (usings.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder("// <auto-generated/>\n");
        foreach (var item in usings)
        {
            builder.Append("global using ");
            if (item.IsStatic)
            {
                builder.Append("static ");
            }
            else if (item.Alias is { } alias)
            {
                builder.Append(alias).Append(" = ");
            }

            builder.Append("global::").Append(item.Name).Append(";\n");
        }

        return builder.ToString();
    }

    /// <summary>Builds the Roslyn compilation for a request without emitting it.</summary>
    /// <param name="request">What to compile.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    public CSharpCompilation CreateCompilation(CompilationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parseOptions = CreateParseOptions(request.Optimize);
        var trees = request.Documents
            .Select(d => CSharpSyntaxTree.ParseText(SourceText.From(d.Text, Encoding.UTF8), parseOptions, d.FilePath, cancellationToken))
            .ToList();

        if (request.ImplicitUsings)
        {
            trees.Add(CSharpSyntaxTree.ParseText(SourceText.From(CreateGlobalUsingsSource(), Encoding.UTF8), parseOptions, $"{request.AssemblyName}.GlobalUsings.g.cs", cancellationToken));
        }

        var options = CreateCompilationOptions(request.Kind, request.Nullable, request.Optimize, request.AllowUnsafe);
        return CSharpCompilation.Create(
            request.AssemblyName,
            trees,
            [.. _references, .. request.AdditionalReferences],
            options);
    }

    /// <summary>Compiles a request into an assembly in memory.</summary>
    /// <param name="request">What to compile.</param>
    /// <param name="cancellationToken">Cancels compilation.</param>
    /// <returns>The diagnostics, and the assembly when compilation succeeded.</returns>
    public CompilationResult Compile(CompilationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var compilation = CreateCompilation(request, cancellationToken);
        return Emit(compilation, request.Kind == AssemblyKind.ConsoleApplication, stopwatch, null, resources: null, cancellationToken);
    }

    /// <summary>Emits a compilation into memory, applying the in-process safety checks to programs.</summary>
    /// <param name="compilation">The compilation to emit.</param>
    /// <param name="checkInProcessSafety">Whether to reject calls that would close TabStudio (see <see cref="InProcessSafetyAnalyzer"/>).</param>
    /// <param name="stopwatch">A running stopwatch for the duration, or <see langword="null"/> to start one now.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="resources">The files to compile into the assembly as resources (see <see cref="ManifestResources"/>), if any.</param>
    /// <param name="cancellationToken">Cancels emitting.</param>
    internal static CompilationResult Emit(
        RoslynCompilation compilation,
        bool checkInProcessSafety,
        Stopwatch? stopwatch,
        string? projectName,
        IEnumerable<ResourceDescription>? resources,
        CancellationToken cancellationToken) =>
        Emit(compilation, checkInProcessSafety, stopwatch, projectName, resources, mapDiagnostics: null, cancellationToken);

    /// <summary>
    /// Emits a compilation into memory, converting its diagnostics with a mapper of its own, such as one that places
    /// errors in generated Razor code in their Razor files and adds the Razor compiler's errors.
    /// </summary>
    /// <param name="compilation">The compilation to emit.</param>
    /// <param name="checkInProcessSafety">Whether to reject calls that would close TabStudio (see <see cref="InProcessSafetyAnalyzer"/>).</param>
    /// <param name="stopwatch">A running stopwatch for the duration, or <see langword="null"/> to start one now.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="resources">The files to compile into the assembly as resources (see <see cref="ManifestResources"/>), if any.</param>
    /// <param name="mapDiagnostics">Converts the emit's diagnostics; <see cref="DiagnosticMapper"/> when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels emitting.</param>
    internal static CompilationResult Emit(
        RoslynCompilation compilation,
        bool checkInProcessSafety,
        Stopwatch? stopwatch,
        string? projectName,
        IEnumerable<ResourceDescription>? resources,
        Func<IEnumerable<Diagnostic>, IReadOnlyList<BuildDiagnostic>>? mapDiagnostics,
        CancellationToken cancellationToken)
    {
        stopwatch ??= Stopwatch.StartNew();
        var name = compilation.AssemblyName ?? "Program";

        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = compilation.Emit(image, symbols, manifestResources: resources, options: EmitOptions, cancellationToken: cancellationToken);

        var diagnostics = (mapDiagnostics?.Invoke(emit.Diagnostics) ?? DiagnosticMapper.Map(emit.Diagnostics, projectName ?? name)).ToList();
        if (checkInProcessSafety)
        {
            diagnostics.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, projectName ?? name, cancellationToken));
        }

        var succeeded = emit.Success && diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error);
        var assembly = succeeded ? new CompiledAssembly(name, image.ToArray(), symbols.ToArray())
        {
            HotReloadCompilation = compilation.Options.OptimizationLevel == OptimizationLevel.Debug ? compilation : null,
        } : null;
        stopwatch.Stop();

        return new CompilationResult(name, diagnostics, assembly, stopwatch.Elapsed);
    }
}
