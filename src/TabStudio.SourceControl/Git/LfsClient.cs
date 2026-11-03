using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TabStudio.SourceControl.Git;

/// <summary>A Git LFS object: the SHA-256 and size a pointer names.</summary>
/// <param name="Oid">The content's SHA-256, in lowercase hexadecimal.</param>
/// <param name="Size">Its size in bytes.</param>
internal sealed record LfsObject(string Oid, long Size)
{
    /// <summary>The pointer a repository holds in place of the content.</summary>
    public byte[] Pointer() => Encoding.UTF8.GetBytes($"version https://git-lfs.github.com/spec/v1\noid sha256:{Oid}\nsize {Size.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");

    /// <summary>The object a file's content would be stored as.</summary>
    public static LfsObject Of(string path)
    {
        using var stream = File.OpenRead(path);
        return new LfsObject(Convert.ToHexStringLower(SHA256.HashData(stream)), stream.Length);
    }
}

/// <summary>
/// Git LFS's batch API on any server, as <c>git lfs push</c> and <c>git lfs pull</c> use it: upload with its verify
/// step, and download, each with the headers the server gives for the transfer.
/// </summary>
internal sealed class LfsClient(HttpClient http, Uri endpoint, GitCredentials? credentials)
{
    /// <summary>The LFS endpoint Git LFS uses for a repository's address: the address (ending in <c>.git</c>) and <c>/info/lfs</c>.</summary>
    public static Uri EndpointFor(Uri repository)
    {
        var text = repository.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (!text.EndsWith(".git", StringComparison.OrdinalIgnoreCase) && !text.Contains("/_git/", StringComparison.Ordinal))
        {
            text += ".git";
        }

        return new Uri(text + "/info/lfs/");
    }

    /// <summary>Uploads files the server does not have yet.</summary>
    /// <param name="files">The objects, each with the file that holds its content.</param>
    /// <param name="cancellationToken">Cancels the uploads.</param>
    /// <exception cref="GitTransportException">The server refused or could not be reached.</exception>
    public async Task UploadAsync(IReadOnlyList<(LfsObject Object, string Path)> files, CancellationToken cancellationToken)
    {
        foreach (var batch in files.Chunk(100))
        {
            var answer = await BatchAsync("upload", [.. batch.Select(f => f.Object)], cancellationToken).ConfigureAwait(false);
            foreach (var (lfs, path) in batch)
            {
                if (!answer.TryGetValue(lfs.Oid, out var item))
                {
                    continue;
                }

                if (item["error"]?["message"]?.GetValue<string>() is { } error)
                {
                    throw new GitTransportException($"The Git LFS server refused {Path.GetFileName(path)}: {error}");
                }

                // No upload action means the server has the object already.
                if (item["actions"]?["upload"] is JsonObject upload)
                {
                    using var request = Action(HttpMethod.Put, upload);
                    await using var content = File.OpenRead(path);
                    request.Content = new StreamContent(content);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    request.Content.Headers.ContentLength = lfs.Size;
                    using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
                }

                if (item["actions"]?["verify"] is JsonObject verify)
                {
                    using var request = Action(HttpMethod.Post, verify);
                    request.Content = new StringContent(new JsonObject { ["oid"] = lfs.Oid, ["size"] = lfs.Size }.ToJsonString(), Encoding.UTF8, "application/vnd.git-lfs+json");
                    using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>Downloads one object into a stream, checking its hash.</summary>
    /// <exception cref="GitTransportException">The server does not have it, refused, or sent something else.</exception>
    public async Task DownloadAsync(LfsObject lfs, Stream destination, CancellationToken cancellationToken)
    {
        var answer = await BatchAsync("download", [lfs], cancellationToken).ConfigureAwait(false);
        if (!answer.TryGetValue(lfs.Oid, out var item) || item["actions"]?["download"] is not JsonObject download)
        {
            throw new GitTransportException(item?["error"]?["message"]?.GetValue<string>() ?? "the Git LFS server did not offer it.");
        }

        using var request = Action(HttpMethod.Get, download);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            received += read;
            if (received > lfs.Size)
            {
                throw new GitTransportException("the Git LFS download sent more than the file's size.");
            }

            sha.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(Convert.ToHexStringLower(sha.GetHashAndReset()), lfs.Oid, StringComparison.Ordinal))
        {
            throw new GitTransportException("what the Git LFS server sent is not the file the pointer names.");
        }
    }

    private async Task<Dictionary<string, JsonNode>> BatchAsync(string operation, IReadOnlyList<LfsObject> objects, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["operation"] = operation,
            ["transfers"] = new JsonArray("basic"),
            ["objects"] = new JsonArray([.. objects.Select(o => (JsonNode)new JsonObject { ["oid"] = o.Oid, ["size"] = o.Size })]),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "objects/batch"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/vnd.git-lfs+json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.git-lfs+json"));
        request.Headers.UserAgent.ParseAdd("git-lfs/3.0 (TabStudio)");
        if (credentials is { } sign)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sign.UserName}:{sign.Password}")));
        }

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new GitTransportException("The Git LFS server sent an answer TabStudio could not read.", ex);
        }

        foreach (var item in (parsed as JsonObject)?["objects"]?.AsArray() ?? new JsonArray())
        {
            if (item?["oid"]?.GetValue<string>() is { } oid)
            {
                result[oid] = item;
            }
        }

        return result;
    }

    private static HttpRequestMessage Action(HttpMethod method, JsonObject action)
    {
        var href = action["href"]?.GetValue<string>() ?? throw new GitTransportException("The Git LFS server gave a transfer without an address.");
        var request = new HttpRequestMessage(method, new Uri(href));
        request.Headers.UserAgent.ParseAdd("git-lfs/3.0 (TabStudio)");

        // Only the headers the server gave: the transfer is often on another host, which must not see the sign-in.
        foreach (var (name, value) in action["header"]?.AsObject() ?? new JsonObject())
        {
            if (value is not null)
            {
                request.Headers.TryAddWithoutValidation(name, value.GetValue<string>());
            }
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GitTransportException($"Could not reach the Git LFS server: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new GitTransportException(status, $"The Git LFS server answered {(int)status} ({status}).");
        }

        return response;
    }
}
