using System.Text.Json;

namespace TabStudio.Core.Detection;

/// <summary>
/// Finds a repository's static web site: the <c>index.html</c> a browser opens first, at the root or in one of the
/// folders sites are published from.
/// </summary>
/// <remarks>
/// Whether that page is the repository's startup item is decided with the rest of the stack: a .NET project, or a
/// Node.js package that runs something, builds or serves its own pages (see <see cref="StackDetector"/>).
/// </remarks>
public static class StaticSiteDetector
{
    /// <summary>The file a site opens with.</summary>
    public const string IndexFileName = "index.html";

    /// <summary>
    /// The folders looked at, in order: the root, then the folders GitHub Pages and common site tools publish
    /// from. A build's output (<c>dist</c>, <c>build</c>) is not the site's source, so it is not among them.
    /// </summary>
    private static readonly string[] SiteFolders = ["", "public", "docs", "site", "www", "src"];

    /// <summary>The <c>package.json</c> scripts that run a program or build or serve pages, as Vite's <c>dev</c> does.</summary>
    private static readonly string[] PageScripts = ["start", "dev", "serve", "build"];

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Finds the site's <c>index.html</c>.</summary>
    /// <param name="rootDirectory">The repository or folder.</param>
    /// <returns>The full path of the page, or <see langword="null"/> when there is none.</returns>
    public static string? FindIndex(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            return null;
        }

        foreach (var folder in SiteFolders)
        {
            var directory = folder.Length == 0 ? rootDirectory : Path.Combine(rootDirectory, folder);
            var index = Path.Combine(directory, IndexFileName);
            if (File.Exists(index))
            {
                return Path.GetFullPath(index);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a <c>package.json</c> runs a program (its <c>main</c>) or builds or serves pages (a <c>start</c>,
    /// <c>dev</c>, <c>serve</c> or <c>build</c> script), so that Node.js, not the site's own pages, is the startup
    /// item. One that only lists tools, such as a formatter, does not.
    /// </summary>
    /// <param name="packageJson">The <c>package.json</c> file.</param>
    public static bool IsRunByNode(string packageJson)
    {
        ArgumentNullException.ThrowIfNull(packageJson);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageJson), JsonOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("main", out var main) && main.ValueKind == JsonValueKind.String)
            {
                return true;
            }

            return root.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object
                && PageScripts.Any(script => scripts.TryGetProperty(script, out _));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A package.json that cannot be read cannot be run either.
            return false;
        }
    }
}
