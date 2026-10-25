using System.Text;
using System.Text.Json;

namespace TabStudio.Web.StaticWebAssets;

/// <summary>
/// The JavaScript initializers of a Blazor app: the <c>*.lib.module.js</c> files of the app and its libraries, which
/// Blazor imports and calls before and after it starts. The SDK lists them for a server app in
/// <c>&lt;App&gt;.modules.json</c>, which ASP.NET Core reads from the web root.
/// </summary>
public static class JavaScriptInitializers
{
    private const string Extension = ".lib.module.js";

    /// <summary>Whether a route is an initializer, such as <c>_content/Library/Library.lib.module.js</c>.</summary>
    /// <param name="route">The route.</param>
    public static bool IsInitializer(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return route.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Writes the list of a site's initializers, when it has any.</summary>
    /// <param name="assets">The site's assets.</param>
    /// <param name="path">The <c>&lt;App&gt;.modules.json</c> file to write.</param>
    /// <returns>The file, or <see langword="null"/> when the site has no initializers and nothing was written.</returns>
    public static string? Write(IEnumerable<StaticWebAsset> assets, string path)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrEmpty(path);
        var routes = assets.Select(a => a.Route).Where(IsInitializer).Distinct(StringComparer.Ordinal).ToList();
        if (routes.Count == 0)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartArray();
            foreach (var route in routes)
            {
                json.WriteStringValue(route);
            }

            json.WriteEndArray();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ProjectWebAssets.WriteIfChanged(path, Encoding.UTF8.GetString(buffer.ToArray()));
        return path;
    }
}
