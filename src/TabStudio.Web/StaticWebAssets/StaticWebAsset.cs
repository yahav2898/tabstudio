namespace TabStudio.Web.StaticWebAssets;

/// <summary>
/// A file a web app serves as it is, such as <c>wwwroot/app.css</c>, Blazor's <c>_framework/blazor.web.js</c> from its
/// package, or the bundle of a project's scoped CSS.
/// </summary>
/// <param name="Route">Where the app serves it, relative to the site and with <c>/</c> separators, such as <c>css/app.css</c>.</param>
/// <param name="FilePath">The file on disk.</param>
public sealed record StaticWebAsset(string Route, string FilePath)
{
    /// <summary>Creates an asset, normalizing the route: no leading <c>/</c>, and <c>/</c> between folders.</summary>
    /// <param name="route">Where the app serves it.</param>
    /// <param name="filePath">The file on disk.</param>
    public static StaticWebAsset Create(string route, string filePath)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(filePath);
        return new StaticWebAsset(NormalizeRoute(route), filePath);
    }

    /// <summary>A route without a leading <c>/</c>, with <c>/</c> between folders and no empty or <c>.</c> segments.</summary>
    /// <param name="route">A route or a relative path.</param>
    public static string NormalizeRoute(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var segments = route.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(s => s != ".");
        return string.Join('/', segments);
    }

    /// <summary>Joins a base path, such as <c>_content/Library</c>, and a relative path into a route.</summary>
    /// <param name="basePath">The base path; empty for the site's root.</param>
    /// <param name="relativePath">The path under it.</param>
    public static string Combine(string basePath, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePath);
        var root = NormalizeRoute(basePath);
        var path = NormalizeRoute(relativePath);
        return root.Length == 0 ? path : path.Length == 0 ? root : root + "/" + path;
    }
}
