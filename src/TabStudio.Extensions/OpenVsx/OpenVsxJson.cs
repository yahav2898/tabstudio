using System.Globalization;
using System.Text.Json;
using TabStudio.Extensions.Json;
using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.OpenVsx;

/// <summary>Reads the registry's JSON (the <c>SearchResult</c> and <c>Extension</c> objects of its API) into models.</summary>
internal static class OpenVsxJson
{
    /// <summary>Reads a search result.</summary>
    /// <exception cref="OpenVsxException">The answer is an error or not a search result.</exception>
    public static OpenVsxSearchResult ReadSearch(byte[] body, bool offline)
    {
        using var document = Parse(body);
        var root = document.RootElement;
        var extensions = root.GetArray("extensions")
            .Select(ReadSummary)
            .OfType<OpenVsxSummary>()
            .ToList();
        return new OpenVsxSearchResult(
            (int)(root.GetInt64("offset") ?? 0),
            (int)(root.GetInt64("totalSize") ?? extensions.Count),
            extensions,
            offline);
    }

    /// <summary>Reads an extension version's metadata.</summary>
    /// <exception cref="OpenVsxException">The answer is an error or not an extension.</exception>
    public static OpenVsxExtension ReadExtension(byte[] body)
    {
        using var document = Parse(body);
        var root = document.RootElement;
        if (Identifier(root) is not { } id || root.GetString("version") is not { Length: > 0 } version)
        {
            throw new OpenVsxException(OpenVsxFailure.InvalidResponse, "The registry's answer does not describe an extension.");
        }

        var files = root.GetObject("files");
        var publisher = root.GetObject("publishedBy");
        return new OpenVsxExtension
        {
            Id = id,
            Version = version,
            DisplayName = NonEmpty(root.GetString("displayName")) ?? id.Name,
            PublisherDisplayName = NonEmpty(root.GetString("namespaceDisplayName")) ?? id.Publisher,
            Description = root.GetString("description") ?? string.Empty,
            Files = new OpenVsxFiles(
                Address(files?.GetString("download")),
                Address(files?.GetString("sha256")),
                Address(files?.GetString("signature")),
                Address(files?.GetString("publicKey")),
                Address(files?.GetString("icon")),
                Address(files?.GetString("readme")),
                Address(files?.GetString("changelog")),
                Address(files?.GetString("license")),
                Address(files?.GetString("manifest"))),
            AverageRating = root.GetDouble("averageRating"),
            ReviewCount = root.GetInt64("reviewCount") ?? 0,
            DownloadCount = root.GetInt64("downloadCount") ?? 0,
            Published = Date(root.GetString("timestamp")),
            IsVerified = root.GetBoolean("verified") ?? false,
            IsDeprecated = root.GetBoolean("deprecated") ?? false,
            IsPreRelease = root.GetBoolean("preRelease") ?? false,
            IsPreview = root.GetBoolean("preview") ?? false,
            EngineVersion = root.GetObject("engines")?.GetString("vscode"),
            Categories = root.GetStrings("categories"),
            Tags = root.GetStrings("tags"),
            License = NonEmpty(root.GetString("license")),
            Homepage = Address(root.GetString("homepage")),
            Repository = Address(root.GetString("repository")),
            TargetPlatform = NonEmpty(root.GetString("targetPlatform")) ?? "universal",
            PublishedBy = NonEmpty(publisher?.GetString("fullName")) ?? NonEmpty(publisher?.GetString("loginName")),
            Versions = Versions(root),
            Dependencies = References(root, "dependencies"),
            BundledExtensions = References(root, "bundledExtensions"),
            Replacement = NonEmpty(root.GetObject("replacement")?.GetString("displayName")),
        };
    }

    /// <summary>Reads one entry of a search result, or <see langword="null"/> when it lacks its identity.</summary>
    public static OpenVsxSummary? ReadSummary(JsonElement entry)
    {
        if (Identifier(entry) is not { } id || entry.GetString("version") is not { Length: > 0 } version)
        {
            return null;
        }

        return new OpenVsxSummary(
            id,
            version,
            NonEmpty(entry.GetString("displayName")) ?? id.Name,
            entry.GetString("description") ?? string.Empty,
            Address(entry.GetObject("files")?.GetString("icon")),
            entry.GetDouble("averageRating"),
            entry.GetInt64("reviewCount") ?? 0,
            entry.GetInt64("downloadCount") ?? 0,
            Date(entry.GetString("timestamp")),
            entry.GetBoolean("verified") ?? false,
            entry.GetBoolean("deprecated") ?? false);
    }

    /// <summary>Parses an answer and throws for the registry's <c>{ "error": "..." }</c>.</summary>
    private static JsonDocument Parse(byte[] body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body, Jsonc.Options);
        }
        catch (JsonException ex)
        {
            throw new OpenVsxException(OpenVsxFailure.InvalidResponse, "The registry sent an answer TabStudio could not read.", ex);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new OpenVsxException(OpenVsxFailure.InvalidResponse, "The registry sent an answer TabStudio could not read.");
        }

        if (document.RootElement.GetString("error") is { Length: > 0 } error)
        {
            document.Dispose();
            throw new OpenVsxException(OpenVsxFailure.Rejected, error);
        }

        return document;
    }

    private static ExtensionIdentifier? Identifier(JsonElement element) =>
        element.GetString("namespace") is { Length: > 0 } ns && element.GetString("name") is { Length: > 0 } name
        && ExtensionIdentifier.TryParse($"{ns}.{name}", out var id)
            ? id
            : null;

    /// <summary>The versions in <c>allVersions</c>, without the aliases <c>latest</c> and <c>pre-release</c>.</summary>
    private static IReadOnlyList<string> Versions(JsonElement root) =>
        root.GetObject("allVersions") is { } versions
            ? [.. versions.EnumerateObject().Select(v => v.Name).Where(v => v.Length > 0 && char.IsAsciiDigit(v[0]))]
            : [];

    private static IReadOnlyList<string> References(JsonElement root, string name) =>
        [.. root.GetArray(name)
            .Select(r => r.GetString("namespace") is { } ns && r.GetString("extension") is { } extension ? $"{ns}.{extension}" : null)
            .OfType<string>()];

    private static Uri? Address(string? text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;

    private static DateTimeOffset? Date(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
