using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Web.StaticWebAssets;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Web.RazorApps;

/// <summary>What an ASP.NET Core app serves besides its own <c>wwwroot</c>, and where TabStudio writes its manifests.</summary>
public sealed record ServerAppAssetsRequest
{
    /// <summary>The web app.</summary>
    public required ProjectInfo App { get; init; }

    /// <summary>
    /// The application name ASP.NET Core sees, the entry assembly's name; <c>MapStaticAssets</c> finds the endpoints
    /// manifest by it. The project's assembly name when <see langword="null"/>.
    /// </summary>
    public string? ApplicationName { get; init; }

    /// <summary>The projects the app references, directly or not: Razor class libraries and the WebAssembly client.</summary>
    public IReadOnlyList<ProjectInfo> References { get; init; } = [];

    /// <summary>The app's package assemblies, whose packages may bring web assets (<c>_content/&lt;PackageId&gt;</c>).</summary>
    public IReadOnlyList<string> PackageAssemblies { get; init; } = [];

    /// <summary>The folder of Blazor's scripts package (<c>Microsoft.AspNetCore.App.Internal.Assets</c>), for an app with Razor components.</summary>
    public string? BlazorScriptsPackage { get; init; }

    /// <summary>The bundle of the app's WebAssembly client, for a Blazor Web App with WebAssembly or Auto components.</summary>
    public WebAssemblyBundle? Client { get; init; }

    /// <summary>A folder of TabStudio's for what it builds: the scoped CSS bundles, the manifests and the client's built files.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Where the endpoints manifest goes: the folder <c>MapStaticAssets</c> reads it from, <see cref="AppContext.BaseDirectory"/>,
    /// as the app runs in TabStudio's process.
    /// </summary>
    public string ManifestDirectory { get; init; } = AppContext.BaseDirectory;
}

/// <summary>
/// The static web assets of an ASP.NET Core app that TabStudio runs, as the SDK provides them in development: the
/// app's <c>wwwroot</c>, the scoped CSS bundle (<c>&lt;App&gt;.styles.css</c>), Blazor's <c>_framework/blazor.web.js</c>,
/// the assets of referenced Razor class libraries and packages under <c>_content/…</c>, and a WebAssembly client's
/// runtime under <c>_framework/…</c>. They reach the app through the two manifests ASP.NET Core reads: the runtime
/// manifest, passed with <c>--staticWebAssets</c>, and the endpoints manifest <c>MapStaticAssets</c> maps.
/// </summary>
/// <remarks>Dispose it when the app stops: the endpoints manifest is removed from TabStudio's folder.</remarks>
public sealed class ServerAppAssets : IDisposable
{
    private readonly ServerAppAssetsRequest _request;

    private ServerAppAssets(ServerAppAssetsRequest request, Collected collected)
    {
        _request = request;
        Assets = collected.Assets;
        Diagnostics = collected.Diagnostics;
        RuntimeManifestPath = collected.RuntimeManifest;
        EndpointsManifestPath = collected.EndpointsManifest;
    }

    /// <summary>Every asset, by route; the first of a route is the one served.</summary>
    public IReadOnlyList<StaticWebAsset> Assets { get; private set; }

    /// <summary>Problems, such as an <c>@import</c> in scoped CSS.</summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; private set; }

    /// <summary>The runtime manifest, which makes the assets part of the app's web root.</summary>
    public string RuntimeManifestPath { get; }

    /// <summary>The endpoints manifest, from which <c>MapStaticAssets</c> creates an endpoint per asset.</summary>
    public string EndpointsManifestPath { get; }

    /// <summary>The command-line arguments that give the app its runtime manifest.</summary>
    public IReadOnlyList<string> Arguments => ["--staticWebAssets", RuntimeManifestPath];

    /// <summary>Collects an app's assets and writes its manifests.</summary>
    /// <param name="request">The app and what it references.</param>
    public static ServerAppAssets Prepare(ServerAppAssetsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ServerAppAssets(request, Collect(request));
    }

    /// <summary>
    /// Collects the assets again while the app runs, such as after a scoped CSS file was saved: the bundles are
    /// rewritten, and ASP.NET Core's development support serves their new content on the next request.
    /// </summary>
    /// <returns>The problems found, as <see cref="Diagnostics"/> now has them.</returns>
    public IReadOnlyList<BuildDiagnostic> Refresh()
    {
        var collected = Collect(_request);
        Assets = collected.Assets;
        Diagnostics = collected.Diagnostics;
        return Diagnostics;
    }

    /// <summary>Removes the endpoints manifest from TabStudio's folder.</summary>
    public void Dispose()
    {
        try
        {
            File.Delete(EndpointsManifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Overwritten by the app's next run anyway.
        }
    }

    private static Collected Collect(ServerAppAssetsRequest request)
    {
        var app = request.App;
        var others = request.References.Select(r => r.DirectoryPath).ToList();
        var assets = new List<StaticWebAsset>();
        var diagnostics = new List<BuildDiagnostic>();
        var patternRoots = new List<string> { Path.Combine(app.DirectoryPath, ProjectWebAssets.WebRootFolder) };

        // The referenced projects first, so the app's bundle can import theirs.
        var imports = new List<string>();
        var referenced = new List<StaticWebAsset>();
        foreach (var library in request.References)
        {
            var basePath = ProjectWebAssets.LibraryBasePath(library);
            var result = ProjectWebAssets.Collect(
                library,
                basePath,
                isApp: false,
                bundleImports: [],
                outputDirectory: Path.Combine(request.OutputDirectory, "scopedcss", library.Name),
                otherProjectDirectories: request.References.Where(r => r != library).Select(r => r.DirectoryPath).Append(app.DirectoryPath));
            referenced.AddRange(result.Assets);
            diagnostics.AddRange(result.Errors);
            if (result.ScopedCssBundleRoute is { } bundle)
            {
                imports.Add(bundle);
            }

            if (basePath.Length == 0)
            {
                patternRoots.Add(Path.Combine(library.DirectoryPath, ProjectWebAssets.WebRootFolder));
            }
        }

        var own = ProjectWebAssets.Collect(app, string.Empty, isApp: true, imports, Path.Combine(request.OutputDirectory, "scopedcss", app.Name), others);
        assets.AddRange(own.Assets);
        diagnostics.AddRange(own.Errors);
        assets.AddRange(referenced);

        if (request.Client is { } client)
        {
            assets.AddRange(client.WriteTo(Path.Combine(request.OutputDirectory, "client")));
        }

        assets.AddRange(PackageWebAssets.Collect(PackageWebAssets.FindPackageDirectories(request.PackageAssemblies)));
        if (request.BlazorScriptsPackage is { } scripts)
        {
            assets.AddRange(BlazorScripts(scripts));
        }

        var name = request.ApplicationName ?? app.GetProperty("AssemblyName") ?? app.Name;
        var runtimeManifest = Path.Combine(request.OutputDirectory, $"{name}.staticwebassets.runtime.json");
        var endpointsManifest = Path.Combine(request.ManifestDirectory, $"{name}.staticwebassets.endpoints.json");
        StaticAssetManifests.WriteRuntimeManifest(assets, patternRoots, runtimeManifest);
        StaticAssetManifests.WriteEndpointsManifest(assets, endpointsManifest);
        return new Collected([.. assets.DistinctBy(a => a.Route, StringComparer.Ordinal)], diagnostics, runtimeManifest, endpointsManifest);
    }

    /// <summary>Blazor's scripts, which the Web SDK serves to every app with Razor components.</summary>
    private static IEnumerable<StaticWebAsset> BlazorScripts(string package)
    {
        foreach (var script in BlazorScriptNames)
        {
            var file = Path.Combine(package, "_framework", script);
            if (File.Exists(file))
            {
                yield return StaticWebAsset.Create("_framework/" + script, file);
            }
        }
    }

    private static readonly string[] BlazorScriptNames = ["blazor.web.js", "blazor.server.js"];

    private sealed record Collected(IReadOnlyList<StaticWebAsset> Assets, IReadOnlyList<BuildDiagnostic> Diagnostics, string RuntimeManifest, string EndpointsManifest);
}
