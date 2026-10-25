using System.Globalization;
using System.Net;
using System.Text.Json;

namespace TabStudio.Packages.Search;

/// <summary>A package found by <see cref="PackageSearch"/>.</summary>
/// <param name="Id">The package id, as the author wrote it.</param>
/// <param name="Version">The latest version, within the search's prerelease setting.</param>
/// <param name="Description">The description, or an empty string.</param>
/// <param name="Authors">The authors.</param>
/// <param name="TotalDownloads">Downloads of every version.</param>
/// <param name="IsVerified">Whether the id prefix is reserved by its owner on the feed (the blue check on nuget.org).</param>
/// <param name="IconUrl">The package icon, if it has one.</param>
/// <param name="ProjectUrl">The project's web page, if it has one.</param>
/// <param name="Versions">Every listed version within the search's prerelease setting, newest first.</param>
public sealed record PackageSearchResult(
    string Id,
    string Version,
    string Description,
    IReadOnlyList<string> Authors,
    long TotalDownloads,
    bool IsVerified,
    Uri? IconUrl,
    Uri? ProjectUrl,
    IReadOnlyList<string> Versions);

/// <summary>A page of search results.</summary>
/// <param name="TotalHits">How many packages match in all.</param>
/// <param name="Results">This page's packages, best match first.</param>
public sealed record PackageSearchPage(int TotalHits, IReadOnlyList<PackageSearchResult> Results);

/// <summary>Searching a feed failed. The message is written for the user.</summary>
public sealed class PackageSearchException : Exception
{
    /// <summary>Creates the exception.</summary>
    public PackageSearchException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public PackageSearchException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public PackageSearchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Searches a NuGet V3 feed, nuget.org by default, through its search service: what the Browse tab of Visual
/// Studio's NuGet Package Manager shows.
/// </summary>
public sealed class PackageSearch
{
    private readonly HttpClient _http;
    private readonly PackageSource _source;
    private readonly Lock _gate = new();
    private Task<Uri>? _searchAddress;

    /// <summary>Creates a search over a feed.</summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="source">The feed; nuget.org when <see langword="null"/>. It must be a V3 feed with a search service.</param>
    public PackageSearch(HttpClient http, PackageSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _source = source ?? PackageSource.NuGetOrg;
    }

    /// <summary>Searches for packages.</summary>
    /// <param name="query">What to look for: words, or a filter such as <c>owner:</c>, <c>tags:</c> or <c>packageid:</c>.</param>
    /// <param name="includePrerelease">Whether prerelease versions count.</param>
    /// <param name="skip">How many results to skip, for paging.</param>
    /// <param name="take">How many results to return, at most 100.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<PackageSearchPage> SearchAsync(string query, bool includePrerelease = false, int skip = 0, int take = 25, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var address = await GetSearchAddressAsync(cancellationToken).ConfigureAwait(false);
        var url = new Uri(
            $"{address}?q={Uri.EscapeDataString(query.Trim())}&skip={Math.Max(skip, 0).ToString(CultureInfo.InvariantCulture)}" +
            $"&take={Math.Clamp(take, 1, 100).ToString(CultureInfo.InvariantCulture)}&prerelease={(includePrerelease ? "true" : "false")}&semVerLevel=2.0.0");

        using var document = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var totalHits = root.TryGetProperty("totalHits", out var hits) && hits.TryGetInt32(out var count) ? count : 0;
        var results = new List<PackageSearchResult>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            results.AddRange(data.EnumerateArray().Select(ReadResult).OfType<PackageSearchResult>());
        }

        return new PackageSearchPage(totalHits, results);
    }

    /// <summary>Finds one package by its exact id.</summary>
    /// <param name="id">The package id; case does not matter.</param>
    /// <param name="includePrerelease">Whether prerelease versions are listed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The package with its versions, or <see langword="null"/> when the feed does not have it.</returns>
    public async Task<PackageSearchResult?> FindAsync(string id, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var page = await SearchAsync($"packageid:{id}", includePrerelease, 0, 1, cancellationToken).ConfigureAwait(false);
        return page.Results.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the service index once; a failed read is retried next time.</summary>
    private Task<Uri> GetSearchAddressAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_searchAddress is null || _searchAddress.IsFaulted || _searchAddress.IsCanceled)
            {
                _searchAddress = ReadSearchAddressAsync(cancellationToken);
            }

            return _searchAddress;
        }
    }

    private async Task<Uri> ReadSearchAddressAsync(CancellationToken cancellationToken)
    {
        using var index = await GetJsonAsync(new Uri(_source.Location), cancellationToken).ConfigureAwait(false);
        return FindSearchService(index.RootElement)
            ?? throw new PackageSearchException($"{_source.Name} has no search service, so its packages cannot be browsed.");
    }

    /// <summary>The address of the newest search service in a service index.</summary>
    private static Uri? FindSearchService(JsonElement index)
    {
        if (!index.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return resources.EnumerateArray()
            .Select(r => (Type: r.TryGetProperty("@type", out var t) ? t.GetString() : null, Id: r.TryGetProperty("@id", out var i) ? i.GetString() : null))
            .Where(r => r.Type?.StartsWith("SearchQueryService", StringComparison.Ordinal) == true && Uri.TryCreate(r.Id, UriKind.Absolute, out _))
            .OrderByDescending(r => r.Type, StringComparer.Ordinal)
            .Select(r => new Uri(r.Id!))
            .FirstOrDefault();
    }

    private static PackageSearchResult? ReadResult(JsonElement item)
    {
        var id = ReadString(item, "id");
        var version = ReadString(item, "version");
        if (id is null || version is null)
        {
            return null;
        }

        var versions = item.TryGetProperty("versions", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(v => ReadString(v, "version")).OfType<string>().Reverse().ToList()
            : [version];

        return new PackageSearchResult(
            id,
            version,
            ReadString(item, "description") ?? string.Empty,
            ReadAuthors(item),
            item.TryGetProperty("totalDownloads", out var downloads) && downloads.TryGetInt64(out var total) ? total : 0,
            item.TryGetProperty("verified", out var verified) && verified.ValueKind == JsonValueKind.True,
            ReadUrl(item, "iconUrl"),
            ReadUrl(item, "projectUrl"),
            versions);
    }

    private static IReadOnlyList<string> ReadAuthors(JsonElement item)
    {
        if (!item.TryGetProperty("authors", out var authors))
        {
            return [];
        }

        return authors.ValueKind switch
        {
            JsonValueKind.Array => [.. authors.EnumerateArray().Select(a => a.GetString()).OfType<string>()],
            JsonValueKind.String => [.. authors.GetString()!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)],
            _ => [],
        };
    }

    private static string? ReadString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Uri? ReadUrl(JsonElement item, string name) =>
        Uri.TryCreate(ReadString(item, name), UriKind.Absolute, out var uri) ? uri : null;

    private async Task<JsonDocument> GetJsonAsync(Uri url, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PackageSearchException($"Could not reach {_source.Name}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PackageSearchException($"{_source.Name} did not answer in time.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new PackageSearchException(response.StatusCode == HttpStatusCode.NotFound
                    ? $"{_source.Name} has no search service at {url.GetLeftPart(UriPartial.Path)}."
                    : $"{_source.Name} answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new PackageSearchException($"{_source.Name} sent an answer TabStudio could not read.", ex);
            }
        }
    }
}
