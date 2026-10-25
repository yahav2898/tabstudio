using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using TabStudio.Compilation;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using ProjectInfo = TabStudio.Core.Workspaces.ProjectInfo;
using TabStudio.Packages;
using TabStudio.Web.RazorApps;
using TabStudio.Web.StaticWebAssets;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Wpf.Browser;

/// <summary>A built WPF app, with what its browser host runs it with, to describe as a <see cref="WebAssemblyApp"/>.</summary>
public sealed record WpfBrowserRequest
{
    /// <summary>The WPF project.</summary>
    public required ProjectInfo Project { get; init; }

    /// <summary>Its assembly, built with the second pass of OpenSilver's XAML compiler.</summary>
    public required CompiledAssembly App { get; init; }

    /// <summary>The assemblies of the projects it references.</summary>
    public IReadOnlyList<CompiledAssembly> Dependencies { get; init; } = [];

    /// <summary>The browser host's restore (<see cref="OpenSilverPackages.BrowserHost"/>): OpenSilver, Blazor's host and the app's own packages.</summary>
    public required RestoreResult Host { get; init; }

    /// <summary>.NET for WebAssembly.</summary>
    public required BrowserRuntime Runtime { get; init; }

    /// <summary>.NET's reference assemblies, which the host compiles against.</summary>
    public required IReadOnlyList<MetadataReference> FrameworkReferences { get; init; }
}

/// <summary>
/// Describes WPF apps for the <see cref="WebAssemblyAppHost"/>, as OpenSilver's <c>.Browser</c> project builds an
/// OpenSilver app for the browser in Debug: the browser host (see <see cref="BrowserHostSource"/>), compiled here,
/// every assembly as IL, OpenSilver's scripts and styles in <c>libs/</c>, Blazor's <c>blazor.webassembly.js</c> and
/// TabStudio's page, which frames the app's window with its title bar.
/// </summary>
public static class WpfBrowserApps
{
    /// <summary>The page the app starts from.</summary>
    public const string StartPage = "index.html";

    /// <summary>Where the page sends what the app writes to the console, as JSON: <c>[{ "level": "log", "text": "…" }]</c>.</summary>
    public const string ConsoleRoute = "_tabstudio/console";

    /// <summary>Where the page sends the window's title when it changes.</summary>
    public const string TitleRoute = "_tabstudio/title";

    /// <summary>Where the page says the window's close button was pressed.</summary>
    public const string CloseRoute = "_tabstudio/close";

    private static readonly ConcurrentDictionary<string, (byte[] Image, byte[] Symbols)> Hosts = new(StringComparer.Ordinal);

    /// <summary>Describes a built app.</summary>
    /// <param name="request">The app and what it runs with.</param>
    /// <returns>The app, or <see langword="null"/> and the reason when it could not be put together.</returns>
    public static (WebAssemblyApp? App, IReadOnlyList<BuildDiagnostic> Diagnostics) Describe(WpfBrowserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = request.Project;
        var hostName = $"{project.Name}.Browser";
        var references = new List<MetadataReference>(request.FrameworkReferences);
        references.AddRange(request.Host.CompileAssemblies.Select(path => MetadataReference.CreateFromFile(path)));
        references.Add(MetadataReference.CreateFromImage(request.App.Image));
        references.AddRange(request.Dependencies.Select(d => MetadataReference.CreateFromImage(d.Image)));

        var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable, optimizationLevel: OptimizationLevel.Debug);
        var empty = CSharpCompilation.Create(hostName, [], references, options);
        var startup = WpfStartupInfo.Read(project, empty, request.App.Name);
        var source = BrowserHostSource.Create(project.Name, request.App.Name, startup);

        var key = Hash(source, request.Host.CompileAssemblies, request.FrameworkReferences.Count);
        if (!Hosts.TryGetValue(key, out var host))
        {
            var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), $"{hostName}.g.cs", Encoding.UTF8);
            using var image = new MemoryStream();
            using var symbols = new MemoryStream();
            var emitted = empty.AddSyntaxTrees(tree).Emit(image, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            if (!emitted.Success)
            {
                var errors = string.Join(" ", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(3).Select(d => d.ToString()));
                return (null, [Problem(WpfDiagnosticIds.XamlCompilerFailed, $"TabStudio's browser host for {project.Name} did not compile: {errors}", project.Name)]);
            }

            host = (image.ToArray(), symbols.ToArray());
            Hosts[key] = host;
        }

        var diagnostics = new List<BuildDiagnostic>();
        var content = Content(request, diagnostics);
        if (content is null)
        {
            return (null, diagnostics);
        }

        var app = new WebAssemblyApp
        {
            MainAssemblyName = hostName,
            Assemblies =
            [
                WebAssemblyAssembly.FromImage(hostName, host.Image, host.Symbols),
                WebAssemblyAssembly.FromImage(request.App.Name, request.App.Image, request.App.Symbols),
                .. request.Dependencies.Select(d => WebAssemblyAssembly.FromImage(d.Name, d.Image, d.Symbols)),
                .. request.Host.RuntimeAssemblies.Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(WebAssemblyAssembly.FromFile),
            ],
            Runtime = request.Runtime,
            Content = content,
            StartPage = StartPage,
            InvariantGlobalization = string.Equals(project.GetProperty("InvariantGlobalization")?.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            BootExtensions = ["blazor"],
            RuntimeProperties = BlazorWebAssemblyApps.RuntimeProperties(OpenSilverPackages.BrowserHost(project)),
        };
        return (app, diagnostics);
    }

    /// <summary>
    /// The page and the scripts: TabStudio's page, OpenSilver's scripts and styles under <c>libs/</c>, as OpenSilver's
    /// build copies them to <c>wwwroot/libs</c>, and Blazor's script; <see langword="null"/> when a package lacks one.
    /// </summary>
    private static List<StaticWebAsset>? Content(WpfBrowserRequest request, List<BuildDiagnostic> diagnostics)
    {
        var project = request.Project;
        var web = Path.Combine(WpfProject.OutputDirectory(project), "web");
        Directory.CreateDirectory(Path.Combine(web, "_tabstudio"));
        var assets = new List<StaticWebAsset>
        {
            StaticWebAsset.Create(StartPage, Write(Path.Combine(web, StartPage), Resource("index.html").Replace("{{AppName}}", WebUtility.HtmlEncode(project.Name), StringComparison.Ordinal))),
            StaticWebAsset.Create("_tabstudio/wpf.js", Write(Path.Combine(web, "_tabstudio", "wpf.js"), Resource("wpf.js"))),
            StaticWebAsset.Create("_tabstudio/wpf.css", Write(Path.Combine(web, "_tabstudio", "wpf.css"), Resource("wpf.css"))),
        };

        var webAssembly = request.Host.Packages.FirstOrDefault(p => string.Equals(p.Id, OpenSilverPackages.WebAssembly, StringComparison.OrdinalIgnoreCase));
        var scripts = webAssembly is null ? null : Path.Combine(webAssembly.DirectoryPath, "content");
        if (scripts is null || !Directory.Exists(scripts))
        {
            diagnostics.Add(Problem(WpfDiagnosticIds.OpenSilverMissing, $"OpenSilver's scripts (the {OpenSilverPackages.WebAssembly} package) were not restored for {project.Name}.", project.Name));
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(scripts, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            assets.Add(StaticWebAsset.Create($"libs/{Path.GetFileName(file)}", file));
        }

        var blazor = request.Host.Packages.FirstOrDefault(p => string.Equals(p.Id, OpenSilverPackages.BlazorWebAssembly, StringComparison.OrdinalIgnoreCase));
        var script = blazor is null || !Directory.Exists(Path.Combine(blazor.DirectoryPath, "build"))
            ? null
            : Directory.EnumerateFiles(Path.Combine(blazor.DirectoryPath, "build"), "blazor.webassembly.js", SearchOption.AllDirectories).Order(StringComparer.Ordinal).LastOrDefault();
        if (script is null)
        {
            diagnostics.Add(Problem(WpfDiagnosticIds.OpenSilverMissing, $"Blazor's WebAssembly script (the {OpenSilverPackages.BlazorWebAssembly} package) was not restored for {project.Name}.", project.Name));
            return null;
        }

        assets.Add(StaticWebAsset.Create("_framework/blazor.webassembly.js", script));
        return assets;
    }

    private static string Write(string path, string text)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != text)
        {
            File.WriteAllText(path, text);
        }

        return path;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(WpfBrowserApps).Assembly.GetManifestResourceStream($"Wpf/Browser/{name}")
            ?? throw new InvalidOperationException($"TabStudio.Wpf has no {name} resource.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Hash(string source, IReadOnlyList<string> references, int frameworkCount)
    {
        var text = new StringBuilder(source).Append('\n').Append(frameworkCount);
        foreach (var reference in references.Order(StringComparer.Ordinal))
        {
            text.Append('\n').Append(reference);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static BuildDiagnostic Problem(string id, string message, string projectName) =>
        new(id, DiagnosticSeverityLevel.Error, message, null, projectName);
}
