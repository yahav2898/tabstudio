using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.SourceControl.GitHub;

/// <summary>Where one Git LFS object can be downloaded, as the server's batch API answered.</summary>
/// <param name="Oid">The object's SHA-256, in hexadecimal.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="Address">Where to download it; <see langword="null"/> when the server gave an error instead.</param>
/// <param name="Headers">Headers the download must send, such as a signed authorization.</param>
/// <param name="Error">Why the server will not serve it, such as "Object does not exist on the server".</param>
public sealed record LfsObjectLink(string Oid, long Size, Uri? Address, IReadOnlyDictionary<string, string> Headers, string? Error);

public sealed partial class GitHubClient
{
    /// <summary>GitHub's web address, where it serves Git LFS files.</summary>
    public static readonly Uri DefaultWebAddress = new("https://github.com/");

    private readonly Uri? _webAddress;

    /// <summary>Whether Git LFS files can be downloaded from this server.</summary>
    public bool CanReadLfs => _webAddress is not null;

    /// <summary>
    /// Asks the repository's Git LFS server where its objects can be downloaded, with the batch API Git LFS itself
    /// uses: a file stored with Git LFS is only a small pointer in the repository, and its content lives there.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="objects">The objects, by SHA-256 and size, as the pointers name them.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A link, or the server's error, for every object it answered for.</returns>
    /// <exception cref="GitHubApiException">The server could not be asked or refused.</exception>
    public async Task<IReadOnlyList<LfsObjectLink>> GetLfsDownloadLinksAsync(
        string owner,
        string repository,
        IReadOnlyList<(string Oid, long Size)> objects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(objects);
        if (_webAddress is not { } web)
        {
            throw new GitHubApiException("This server does not serve Git LFS files to TabStudio.");
        }

        var body = new JsonObject
        {
            ["operation"] = "download",
            ["transfers"] = new JsonArray("basic"),
            ["objects"] = new JsonArray([.. objects.Select(o => (JsonNode)new JsonObject { ["oid"] = o.Oid, ["size"] = o.Size })]),
        };
        var address = new Uri(web, $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}.git/info/lfs/objects/batch");
        using var request = new HttpRequestMessage(HttpMethod.Post, address)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/vnd.git-lfs+json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.git-lfs+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TabStudio", "1.0"));
        if (_token is not null)
        {
            // Git's HTTP endpoints take a token as the password of basic authentication, as Git LFS sends it.
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + _token)));
        }

        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JsonNode? answer;
        try
        {
            answer = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            answer = null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = (answer as JsonObject)?["message"]?.GetValue<string>();
            throw new GitHubApiException(response.StatusCode, message is null
                ? $"GitHub's Git LFS server answered {(int)response.StatusCode} ({response.ReasonPhrase})."
                : $"GitHub's Git LFS server refused: {message}");
        }

        var links = new List<LfsObjectLink>();
        foreach (var item in (answer as JsonObject)?["objects"]?.AsArray() ?? new JsonArray())
        {
            if (item is null)
            {
                continue;
            }

            var oid = item["oid"]?.GetValue<string>() ?? string.Empty;
            var size = item["size"]?.GetValue<long>() ?? 0;
            var download = item["actions"]?["download"];
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in download?["header"]?.AsObject() ?? new JsonObject())
            {
                if (value is not null)
                {
                    headers[name] = value.GetValue<string>();
                }
            }

            var href = download?["href"]?.GetValue<string>();
            var error = item["error"]?["message"]?.GetValue<string>();
            links.Add(new LfsObjectLink(
                oid,
                size,
                href is not null && Uri.TryCreate(href, UriKind.Absolute, out var uri) ? uri : null,
                headers,
                href is null ? error ?? "The server gave no address for it." : null));
        }

        return links;
    }

    /// <summary>Downloads one Git LFS object.</summary>
    /// <param name="link">Where, as <see cref="GetLfsDownloadLinksAsync"/> answered.</param>
    /// <param name="destination">Receives the content.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <exception cref="GitHubApiException">The download failed or sent more than the object's size.</exception>
    public async Task DownloadLfsObjectAsync(LfsObjectLink link, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(destination);
        if (link.Address is not { } address)
        {
            throw new GitHubApiException(link.Error ?? "The server gave no address for this Git LFS file.");
        }

        // Only the headers the server gave: the link is often on another host, which must not see the token.
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TabStudio", "1.0"));
        foreach (var (name, value) in link.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubApiException(response.StatusCode, $"The Git LFS download answered {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await ReadBodyAsync(source, buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            received += read;
            if (received > link.Size)
            {
                throw new GitHubApiException("The Git LFS download sent more than the file's size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
