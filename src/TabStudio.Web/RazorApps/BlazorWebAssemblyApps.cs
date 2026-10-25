using TabStudio.Compilation;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Web.StaticWebAssets;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Web.RazorApps;

/// <summary>A build of a Blazor WebAssembly project, to describe as a <see cref="WebAssemblyApp"/>.</summary>
public sealed record BlazorWebAssemblyRequest
{
    /// <summary>The Blazor WebAssembly project (<c>Microsoft.NET.Sdk.BlazorWebAssembly</c>).</summary>
    public required ProjectInfo Project { get; init; }

    /// <summary>Its compiled assembly.</summary>
    public required CompiledAssembly Entry { get; init; }

    /// <summary>The compiled assemblies of the projects it references.</summary>
    public IReadOnlyList<CompiledAssembly> Dependencies { get; init; } = [];

    /// <summary>The projects it references, directly or not, whose web assets it serves.</summary>
    public IReadOnlyList<ProjectInfo> References { get; init; } = [];

    /// <summary>Its package assemblies, from its restore.</summary>
    public IReadOnlyList<string> PackageAssemblies { get; init; } = [];

    /// <summary>.NET for WebAssembly.</summary>
    public required BrowserRuntime Runtime { get; init; }

    /// <summary>The hosting environment the app sees, such as the launch profile's.</summary>
    public string Environment { get; init; } = "Development";

    /// <summary>
    /// Whether it is a standalone app, served with its own <c>index.html</c> and web content; otherwise it is the client
    /// of a Blazor Web App, whose server serves the pages and the client's web content.
    /// </summary>
    public bool IsStandalone { get; init; } = true;

    /// <summary>A folder of TabStudio's for what it builds, such as the scoped CSS bundle.</summary>
    public required string OutputDirectory { get; init; }
}

/// <summary>
/// Describes Blazor WebAssembly apps for the <see cref="WebAssemblyAppHost"/>, as the Blazor WebAssembly SDK builds
/// them in Debug: every assembly as IL, Blazor's <c>_framework/blazor.webassembly.js</c>, the app's <c>wwwroot</c> and
/// scoped CSS, and the runtime configuration its project asks for.
/// </summary>
public static class BlazorWebAssemblyApps
{
    /// <summary>The page a standalone Blazor WebAssembly app starts from.</summary>
    public const string StartPage = "index.html";

    /// <summary>The package with Blazor's WebAssembly script.</summary>
    public const string ComponentsWebAssemblyPackage = "Microsoft.AspNetCore.Components.WebAssembly";

    /// <summary>Describes a build.</summary>
    /// <param name="request">The build.</param>
    /// <returns>The app, and problems such as an <c>@import</c> in scoped CSS.</returns>
    public static (WebAssemblyApp App, IReadOnlyList<BuildDiagnostic> Diagnostics) Describe(BlazorWebAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = request.Project;
        var diagnostics = new List<BuildDiagnostic>();
        var content = new List<StaticWebAsset>();
        var appSettings = new List<string>();

        if (request.IsStandalone)
        {
            content.AddRange(CollectContent(request, diagnostics));
            appSettings.AddRange(AppSettingsFiles(project));
        }

        var app = new WebAssemblyApp
        {
            MainAssemblyName = request.Entry.Name,
            Assemblies =
            [
                FromCompiled(request.Entry),
                .. request.Dependencies.Select(FromCompiled),
                .. request.PackageAssemblies.Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(WebAssemblyAssembly.FromFile),
            ],
            Runtime = request.Runtime,
            Content = content,
            StartPage = request.IsStandalone ? StartPage : null,
            ApplicationEnvironment = request.Environment,
            InvariantGlobalization = IsTrue(project, "InvariantGlobalization"),
            LoadAllGlobalizationData = IsTrue(project, "BlazorWebAssemblyLoadAllGlobalizationData"),
            AppSettings = appSettings,
            BootExtensions = ["blazor"],
            RuntimeProperties = RuntimeProperties(project),
            LibraryInitializers = [.. content.Select(a => a.Route).Where(JavaScriptInitializers.IsInitializer).Distinct(StringComparer.Ordinal)],
        };

        return (app, diagnostics);
    }

    /// <summary>A compiled assembly, with its symbols.</summary>
    /// <param name="assembly">The assembly.</param>
    public static WebAssemblyAssembly FromCompiled(CompiledAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return WebAssemblyAssembly.FromImage(assembly.Name, assembly.Image, assembly.Symbols);
    }

    /// <summary>
    /// The runtime configuration a Blazor WebAssembly project sets through its properties, as the SDK turns them into
    /// <c>RuntimeHostConfigurationOption</c> items.
    /// </summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyDictionary<string, object> RuntimeProperties(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Microsoft.AspNetCore.Components.Routing.RegexConstraintSupport"] = IsTrue(project, "BlazorRoutingEnableRegexConstraint"),
            [RazorAppSwitches.DisableThrowNavigationException] = IsTrue(project, "BlazorDisableThrowNavigationException"),
        };
    }

    /// <summary>The app's web content: its own, its libraries', its packages' and Blazor's script.</summary>
    private static List<StaticWebAsset> CollectContent(BlazorWebAssemblyRequest request, List<BuildDiagnostic> diagnostics)
    {
        var project = request.Project;
        var assets = new List<StaticWebAsset>();
        var imports = new List<string>();
        var libraries = new List<StaticWebAsset>();
        foreach (var library in request.References)
        {
            var result = ProjectWebAssets.Collect(
                library,
                ProjectWebAssets.LibraryBasePath(library),
                isApp: false,
                bundleImports: [],
                outputDirectory: Path.Combine(request.OutputDirectory, "scopedcss", library.Name),
                otherProjectDirectories: request.References.Where(r => r != library).Select(r => r.DirectoryPath).Append(project.DirectoryPath));
            libraries.AddRange(result.Assets);
            diagnostics.AddRange(result.Errors);
            if (result.ScopedCssBundleRoute is { } bundle)
            {
                imports.Add(bundle);
            }
        }

        var own = ProjectWebAssets.Collect(
            project,
            string.Empty,
            isApp: true,
            imports,
            Path.Combine(request.OutputDirectory, "scopedcss", project.Name),
            request.References.Select(r => r.DirectoryPath));
        assets.AddRange(own.Assets);
        diagnostics.AddRange(own.Errors);
        assets.AddRange(libraries);

        var packages = PackageWebAssets.FindPackageDirectories(request.PackageAssemblies);
        assets.AddRange(PackageWebAssets.Collect(packages));
        if (BlazorWebAssemblyScript(packages) is { } script)
        {
            assets.Add(StaticWebAsset.Create("_framework/blazor.webassembly.js", script));
        }

        return assets;
    }

    /// <summary>Blazor's WebAssembly script, from the <c>Microsoft.AspNetCore.Components.WebAssembly</c> package.</summary>
    private static string? BlazorWebAssemblyScript(IReadOnlyList<string> packageDirectories)
    {
        foreach (var package in packageDirectories)
        {
            if (!string.Equals(PackageWebAssets.ReadPackageId(package), ComponentsWebAssemblyPackage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var build = Path.Combine(package, "build");
            if (Directory.Exists(build)
                && Directory.EnumerateFiles(build, "blazor.webassembly.js", SearchOption.AllDirectories).Order(StringComparer.Ordinal).LastOrDefault() is { } script)
            {
                return script;
            }
        }

        return null;
    }

    /// <summary>The configuration files at the top of <c>wwwroot</c>, which the app reads at startup.</summary>
    private static IEnumerable<string> AppSettingsFiles(ProjectInfo project)
    {
        var webRoot = Path.Combine(project.DirectoryPath, ProjectWebAssets.WebRootFolder);
        return Directory.Exists(webRoot)
            ? Directory.EnumerateFiles(webRoot, "appsettings*.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)
            : [];
    }

    private static bool IsTrue(ProjectInfo project, string property) =>
        string.Equals(project.GetProperty(property)?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
