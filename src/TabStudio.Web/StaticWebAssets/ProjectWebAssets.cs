using TabStudio.Compilation.Razor;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.Web.StaticWebAssets;

/// <summary>What one project contributes to a site: its static web assets and the problems found collecting them.</summary>
/// <param name="Assets">The assets, by route.</param>
/// <param name="ScopedCssBundleRoute">The route of the project's scoped CSS bundle, or <see langword="null"/> when it has none.</param>
/// <param name="Errors">Problems in its scoped CSS.</param>
public sealed record ProjectWebAssetsResult(IReadOnlyList<StaticWebAsset> Assets, string? ScopedCssBundleRoute, IReadOnlyList<BuildDiagnostic> Errors);

/// <summary>
/// Collects the static web assets of one project as the SDK discovers them: everything in <c>wwwroot</c>, the
/// JavaScript modules next to components and pages (<c>Counter.razor.js</c>), and the bundle of its scoped CSS,
/// which is written to a folder of TabStudio's.
/// </summary>
public static class ProjectWebAssets
{
    /// <summary>The folder of a project's web root.</summary>
    public const string WebRootFolder = "wwwroot";

    private static readonly string[] CollocatedModuleExtensions = [".razor.js", ".cshtml.js"];

    /// <summary>
    /// The base path a referenced project's assets are served under: the project's <c>StaticWebAssetBasePath</c>, the
    /// site's root for the WebAssembly client of a Blazor Web App, and <c>_content/&lt;PackageId&gt;</c> for a Razor
    /// class library.
    /// </summary>
    /// <param name="library">The referenced project.</param>
    public static string LibraryBasePath(ProjectInfo library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (library.GetProperty("StaticWebAssetBasePath") is { Length: > 0 } explicitPath)
        {
            return StaticWebAsset.NormalizeRoute(explicitPath);
        }

        return string.Equals(library.Sdk, "Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "_content/" + (library.GetProperty("PackageId") ?? library.GetProperty("AssemblyName") ?? library.Name);
    }

    /// <summary>Collects a project's assets.</summary>
    /// <param name="project">The project.</param>
    /// <param name="basePath">The base path its assets are served under: empty for the app, <see cref="LibraryBasePath"/> for a library.</param>
    /// <param name="isApp">Whether it is the app the site belongs to, whose bundle is <c>.styles.css</c> and imports the libraries'.</param>
    /// <param name="bundleImports">The routes of the libraries' bundles, for an app.</param>
    /// <param name="outputDirectory">Where the scoped CSS bundle is written.</param>
    /// <param name="otherProjectDirectories">Folders of other projects, which are never included.</param>
    public static ProjectWebAssetsResult Collect(
        ProjectInfo project,
        string basePath,
        bool isApp,
        IReadOnlyList<string> bundleImports,
        string outputDirectory,
        IEnumerable<string>? otherProjectDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(bundleImports);
        ArgumentNullException.ThrowIfNull(outputDirectory);

        var others = otherProjectDirectories?.ToList() ?? [];
        var assets = new List<StaticWebAsset>();
        var webRoot = Path.Combine(project.DirectoryPath, WebRootFolder);
        foreach (var file in EnumerateVisibleFiles(webRoot))
        {
            assets.Add(StaticWebAsset.Create(StaticWebAsset.Combine(basePath, Path.GetRelativePath(webRoot, file)), file));
        }

        if (RazorProjects.UsesRazor(project))
        {
            foreach (var module in ProjectSourceFiles.Enumerate(project, others, ".js")
                .Where(f => CollocatedModuleExtensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                .Where(f => !f.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            {
                assets.Add(StaticWebAsset.Create(StaticWebAsset.Combine(basePath, Path.GetRelativePath(project.DirectoryPath, module)), module));
            }
        }

        var scoped = ScopedCssBundle.FindFiles(project, others);
        IReadOnlyList<string> imports = isApp ? bundleImports : [];
        if (scoped.Count == 0 && imports.Count == 0)
        {
            return new ProjectWebAssetsResult(assets, null, []);
        }

        var bundle = ScopedCssBundle.Create(project, scoped, basePath, imports);
        var name = isApp ? ScopedCssBundle.AppBundleName(project) : ScopedCssBundle.LibraryBundleName(project);
        var path = Path.Combine(outputDirectory, name);
        Directory.CreateDirectory(outputDirectory);
        WriteIfChanged(path, bundle.Css);
        var route = StaticWebAsset.Combine(basePath, name);
        assets.Add(StaticWebAsset.Create(route, path));
        return new ProjectWebAssetsResult(assets, route, bundle.Errors);
    }

    /// <summary>The files under a folder, without hidden files and folders, sorted.</summary>
    internal static List<string> EnumerateVisibleFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        return
        [
            .. Directory.EnumerateFiles(folder, "*", options)
                .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar).Any(s => s.StartsWith('.')))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Writes a file only when its content changed, so its date tells the server whether to send it again.</summary>
    internal static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
        {
            return;
        }

        File.WriteAllText(path, text);
    }
}
