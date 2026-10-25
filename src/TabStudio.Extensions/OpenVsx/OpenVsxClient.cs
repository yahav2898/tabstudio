using System.Globalization;
using System.Net;
using System.Text;
using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.OpenVsx;

/// <summary>
/// An Open VSX registry's public API (<c>/api</c>): search, extension details with every version, files such as the
/// icon and README, and downloads that are checked against the registry's hash and signature.
/// </summary>
/// <remarks>
/// The registry is open-vsx.org unless another is given; companies can run their own. Answers are cached, so a
/// registry that cannot be reached still shows the last results, marked as offline; files of a version, which never
/// change, are downloaded once. The client is cheap to create; share the <see cref="HttpClient"/>.
/// </remarks>
public sealed partial class OpenVsxClient
{
    /// <summary>The public registry, open-vsx.org.</summary>
    public static readonly Uri DefaultRegistry = new("https://open-vsx.org/");

    private readonly HttpClient _http;
    private readonly ResponseCache _cache;

    /// <summary>Creates a client.</summary>
    /// <param name="http">The HTTP client to send requests with.</param>
    /// <param name="registry">The registry's address; open-vsx.org when <see langword="null"/>.</param>
    /// <param name="cacheFolder">Where to keep answers for offline use, or <see langword="null"/> to keep them in memory only.</param>
    public OpenVsxClient(HttpClient http, Uri? registry = null, string? cacheFolder = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        Registry = NormalizeRegistry(registry ?? DefaultRegistry);
        _cache = new ResponseCache(cacheFolder);
    }

    /// <summary>The registry's address, ending with a slash.</summary>
    public Uri Registry { get; }

    /// <summary>Turns what a user typed as a registry address into one the client can use.</summary>
    /// <param name="address">An absolute http or https address, such as <c>https://open-vsx.org</c>.</param>
    /// <exception cref="ArgumentException">The address is not an absolute http or https address.</exception>
    public static Uri NormalizeRegistry(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("A registry address must be an absolute http or https address.", nameof(address));
        }

        var text = address.GetLeftPart(UriPartial.Path);
        if (text.EndsWith("/api/", StringComparison.OrdinalIgnoreCase) || text.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..text.LastIndexOf("/api", StringComparison.OrdinalIgnoreCase)];
        }

        return new Uri(text.EndsWith('/') ? text : text + "/");
    }

    /// <summary>Searches the registry.</summary>
    /// <param name="query">What to search for, and how to order the results.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <exception cref="OpenVsxException">The registry refused the search, or cannot be reached and nothing is cached.</exception>
    public async Task<OpenVsxSearchResult> SearchAsync(OpenVsxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sort = query.Sort switch
        {
            OpenVsxSort.Downloads => "downloadCount",
            OpenVsxSort.Rating => "rating",
            OpenVsxSort.Published => "timestamp",
            _ => "relevance",
        };
        var path = new StringBuilder("api/-/search?query=")
            .Append(Uri.EscapeDataString(query.Text.Trim()))
            .Append(CultureInfo.InvariantCulture, $"&size={Math.Clamp(query.Size, 1, 100)}&offset={Math.Max(0, query.Offset)}")
            .Append("&sortBy=").Append(sort).Append("&sortOrder=desc&includeAllVersions=false");
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            path.Append("&category=").Append(Uri.EscapeDataString(query.Category));
        }

        var (body, offline) = await GetAsync(new Uri(Registry, path.ToString()), revalidate: true, cancellationToken).ConfigureAwait(false);
        return OpenVsxJson.ReadSearch(body, offline);
    }

    /// <summary>Reads an extension's latest version, or a given version, with its files and list of versions.</summary>
    /// <param name="id">The extension.</param>
    /// <param name="version">A version, or <see langword="null"/> for the latest.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="OpenVsxException">The registry has no such extension, or cannot be reached and nothing is cached.</exception>
    public async Task<OpenVsxExtension> GetExtensionAsync(ExtensionIdentifier id, string? version = null, CancellationToken cancellationToken = default)
    {
        var path = $"api/{Uri.EscapeDataString(id.Publisher)}/{Uri.EscapeDataString(id.Name)}";
        if (!string.IsNullOrWhiteSpace(version))
        {
            path += "/" + Uri.EscapeDataString(version);
        }

        var (body, _) = await GetAsync(new Uri(Registry, path), revalidate: true, cancellationToken).ConfigureAwait(false);
        return OpenVsxJson.ReadExtension(body);
    }

    /// <summary>
    /// Reads the latest version of several extensions, to check for updates. Extensions the registry does not have
    /// are left out.
    /// </summary>
    /// <param name="ids">The extensions.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <exception cref="OpenVsxException">The registry cannot be reached.</exception>
    public async Task<IReadOnlyList<OpenVsxExtension>> GetLatestAsync(IEnumerable<ExtensionIdentifier> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var results = new List<OpenVsxExtension>();
        using var throttle = new SemaphoreSlim(4);
        OpenVsxException? offline = null;
        var tasks = ids.Distinct().Select(async id =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var extension = await GetExtensionAsync(id, null, cancellationToken).ConfigureAwait(false);
                lock (results)
                {
                    results.Add(extension);
                }
            }
            catch (OpenVsxException ex) when (ex.Failure != OpenVsxFailure.Offline)
            {
                // Not on this registry, such as an extension installed from a file.
            }
            catch (OpenVsxException ex)
            {
                offline = ex;
            }
            finally
            {
                throttle.Release();
            }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (results.Count == 0 && offline is not null)
        {
            throw offline;
        }

        return results;
    }

    /// <summary>
    /// Reads a text file of an extension version, such as its README, from the cache when it was read before.
    /// </summary>
    /// <param name="address">The file's address, from <see cref="OpenVsxFiles"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="OpenVsxException">The file cannot be read and is not cached.</exception>
    public async Task<string> GetTextAsync(Uri address, CancellationToken cancellationToken = default)
    {
        var bytes = await GetFileAsync(address, cancellationToken).ConfigureAwait(false);
        var text = Encoding.UTF8.GetString(bytes);
        return text.StartsWith('\uFEFF') ? text[1..] : text;
    }

    /// <summary>Reads a file of an extension version, such as its icon, from the cache when it was read before.</summary>
    /// <param name="address">The file's address, from <see cref="OpenVsxFiles"/> or a search result.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="OpenVsxException">The file cannot be read and is not cached.</exception>
    public async Task<byte[]> GetFileAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var (body, _) = await GetAsync(address, revalidate: false, cancellationToken).ConfigureAwait(false);
        return body;
    }

    /// <summary>
    /// Sends a GET request and returns the body: from the cache without asking when <paramref name="revalidate"/> is
    /// <see langword="false"/> and the answer is cached, otherwise from the registry, revalidating a cached answer with
    /// its ETag. When the registry cannot be reached, a cached answer is returned and marked offline.
    /// </summary>
    private async Task<(byte[] Body, bool Offline)> GetAsync(Uri address, bool revalidate, CancellationToken cancellationToken)
    {
        var cached = _cache.Find(address);
        if (cached is not null && !revalidate)
        {
            return (cached.Body, false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (cached?.ETag is { } etag)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
            {
                return (cached.Body, false);
            }

            if ((int)response.StatusCode >= 500)
            {
                return OfflineAnswer(cached, $"The registry at {Registry.Host} is not working right now ({(int)response.StatusCode}).", null);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new OpenVsxException(OpenVsxFailure.NotFound, $"The registry at {Registry.Host} has no such extension or file.");
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new OpenVsxException(OpenVsxFailure.Rejected, ErrorMessage(body) ?? $"The registry refused the request ({(int)response.StatusCode} {response.ReasonPhrase}).");
            }

            _cache.Store(address, body, response.Headers.ETag?.ToString());
            return (body, false);
        }
        catch (HttpRequestException ex)
        {
            return OfflineAnswer(cached, $"The registry at {Registry.Host} cannot be reached. Check the connection.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return OfflineAnswer(cached, $"The registry at {Registry.Host} did not answer in time.", ex);
        }
        catch (IOException ex)
        {
            return OfflineAnswer(cached, $"The connection to the registry at {Registry.Host} was lost.", ex);
        }
    }

    private static (byte[] Body, bool Offline) OfflineAnswer(CachedResponse? cached, string message, Exception? cause) =>
        cached is not null ? (cached.Body, true) : throw new OpenVsxException(OpenVsxFailure.Offline, message, cause);

    /// <summary>The message of the registry's <c>{ "error": "..." }</c> answer, if the body is one.</summary>
    private static string? ErrorMessage(byte[] body)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == System.Text.Json.JsonValueKind.String
                    ? error.GetString()
                    : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
