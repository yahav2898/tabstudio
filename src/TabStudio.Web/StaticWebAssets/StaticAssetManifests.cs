using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace TabStudio.Web.StaticWebAssets;

/// <summary>
/// Writes the two files an ASP.NET Core app reads its static web assets from in development, as the SDK writes them
/// next to the app: the runtime manifest (<c>&lt;App&gt;.staticwebassets.runtime.json</c>), which maps every route to a
/// file in a content root and makes them part of the app's web root, and the endpoints manifest
/// (<c>&lt;App&gt;.staticwebassets.endpoints.json</c>), from which <c>MapStaticAssets</c> creates an endpoint per asset
/// with its media type, length and ETag.
/// </summary>
/// <remarks>
/// The endpoints manifest is a build manifest, so ASP.NET Core's development support is on: a file that changes
/// while the app runs is served with its new content, and a file added later is served from the web root.
/// </remarks>
public static class StaticAssetManifests
{
    private static readonly ConcurrentDictionary<string, FileFacts> Facts = new(StringComparer.Ordinal);

    /// <summary>Writes the runtime manifest.</summary>
    /// <param name="assets">The assets; the first asset of a route wins, as the app's own files come first.</param>
    /// <param name="patternRoots">Folders whose every file is served, such as the app's <c>wwwroot</c>, including files added later.</param>
    /// <param name="path">The manifest file.</param>
    public static void WriteRuntimeManifest(IReadOnlyList<StaticWebAsset> assets, IReadOnlyList<string> patternRoots, string path)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(patternRoots);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var roots = new List<string>();
        var rootIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        int RootOf(string folder)
        {
            var normalized = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!rootIndexes.TryGetValue(normalized, out var index))
            {
                index = roots.Count;
                roots.Add(normalized);
                rootIndexes[normalized] = index;
            }

            return index;
        }

        var tree = new Node();
        foreach (var asset in Distinct(assets))
        {
            var node = tree;
            foreach (var segment in asset.Route.Split('/'))
            {
                node = node.Child(segment);
            }

            node.ContentRoot = RootOf(Path.GetDirectoryName(asset.FilePath) ?? string.Empty);
            node.SubPath = Path.GetFileName(asset.FilePath);
        }

        var patterns = patternRoots.Where(Directory.Exists).Select(RootOf).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartObject();
        json.WriteStartArray("ContentRoots");
        foreach (var root in roots)
        {
            json.WriteStringValue(root);
        }

        json.WriteEndArray();
        json.WritePropertyName("Root");
        WriteNode(json, tree, patterns);
        json.WriteEndObject();
    }

    /// <summary>Writes the endpoints manifest: an endpoint per route, served without caching, as in development.</summary>
    /// <param name="assets">The assets; the first asset of a route wins.</param>
    /// <param name="path">The manifest file.</param>
    public static void WriteEndpointsManifest(IReadOnlyList<StaticWebAsset> assets, string path)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrEmpty(path);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartObject();
        json.WriteNumber("Version", 1);
        json.WriteString("ManifestType", "Build");
        json.WriteStartArray("Endpoints");
        foreach (var asset in Distinct(assets))
        {
            if (!File.Exists(asset.FilePath))
            {
                continue;
            }

            var facts = Describe(asset.FilePath);
            json.WriteStartObject();
            json.WriteString("Route", asset.Route);
            json.WriteString("AssetFile", asset.Route);
            json.WriteStartArray("Selectors");
            json.WriteEndArray();
            json.WriteStartArray("ResponseHeaders");
            Header(json, "Cache-Control", "no-cache");
            Header(json, "Content-Length", facts.Length.ToString(CultureInfo.InvariantCulture));
            Header(json, "Content-Type", ContentTypes.For(asset.Route));
            Header(json, "ETag", $"\"{facts.Hash}\"");
            Header(json, "Last-Modified", facts.LastModified.ToString("R", CultureInfo.InvariantCulture));
            json.WriteEndArray();
            json.WriteStartArray("EndpointProperties");
            json.WriteStartObject();
            json.WriteString("Name", "integrity");
            json.WriteString("Value", "sha256-" + facts.Hash);
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    /// <summary>
    /// The length, date and SHA-256 hash (base64) of a file. Hashes are remembered while the file keeps its length and
    /// date, so the framework's assemblies are hashed once.
    /// </summary>
    /// <param name="filePath">The file.</param>
    public static FileFacts Describe(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var info = new FileInfo(filePath);
        var modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
        if (Facts.TryGetValue(filePath, out var known) && known.Length == info.Length && known.LastModified == modified)
        {
            return known;
        }

        using var stream = info.OpenRead();
        var facts = new FileFacts(info.Length, modified, Convert.ToBase64String(SHA256.HashData(stream)));
        Facts[filePath] = facts;
        return facts;
    }

    /// <summary>The assets with the first of each route, in order.</summary>
    private static IEnumerable<StaticWebAsset> Distinct(IReadOnlyList<StaticWebAsset> assets) =>
        assets.DistinctBy(a => a.Route, StringComparer.Ordinal);

    private static void Header(Utf8JsonWriter json, string name, string value)
    {
        json.WriteStartObject();
        json.WriteString("Name", name);
        json.WriteString("Value", value);
        json.WriteEndObject();
    }

    private static void WriteNode(Utf8JsonWriter json, Node node, IReadOnlyList<int>? patterns = null)
    {
        json.WriteStartObject();
        if (node.Children.Count == 0)
        {
            json.WriteNull("Children");
        }
        else
        {
            json.WriteStartObject("Children");
            foreach (var (name, child) in node.Children)
            {
                json.WritePropertyName(name);
                WriteNode(json, child);
            }

            json.WriteEndObject();
        }

        if (node.SubPath is { } subPath)
        {
            json.WriteStartObject("Asset");
            json.WriteNumber("ContentRootIndex", node.ContentRoot);
            json.WriteString("SubPath", subPath);
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("Asset");
        }

        if (patterns is { Count: > 0 })
        {
            json.WriteStartArray("Patterns");
            foreach (var root in patterns)
            {
                json.WriteStartObject();
                json.WriteNumber("ContentRootIndex", root);
                json.WriteString("Pattern", "**");
                json.WriteNumber("Depth", 0);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }
        else
        {
            json.WriteNull("Patterns");
        }

        json.WriteEndObject();
    }

    /// <summary>A folder or file of the runtime manifest's tree.</summary>
    private sealed class Node
    {
        public SortedDictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);

        public int ContentRoot { get; set; }

        public string? SubPath { get; set; }

        public Node Child(string name)
        {
            if (!Children.TryGetValue(name, out var child))
            {
                child = new Node();
                Children[name] = child;
            }

            return child;
        }
    }
}

/// <summary>What a static file's headers say about it.</summary>
/// <param name="Length">Its length in bytes.</param>
/// <param name="LastModified">When it was last written.</param>
/// <param name="Hash">The base64 SHA-256 hash of its content.</param>
public sealed record FileFacts(long Length, DateTimeOffset LastModified, string Hash);
