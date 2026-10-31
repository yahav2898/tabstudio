using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabStudio.Containers.Images;
using TabStudio.Toolchains.Downloads;

namespace TabStudio.Containers.Registry;

/// <summary>The manifest a pull uses, after choosing a platform from a multi-platform image.</summary>
/// <param name="Reference">The image as asked for.</param>
/// <param name="Digest">The platform manifest's digest.</param>
/// <param name="Manifest">The platform manifest.</param>
/// <param name="ManifestBytes">The manifest exactly as the registry sent it, which its digest covers.</param>
/// <param name="Platform">The platform chosen.</param>
/// <param name="IndexDigest">The multi-platform index's digest, when the image has one.</param>
public sealed record ResolvedManifest(ImageReference Reference, string Digest, ImageManifest Manifest, byte[] ManifestBytes, OciPlatform? Platform, string? IndexDigest);

/// <summary>A registry problem the person can act on, such as a wrong name or a missing sign-in.</summary>
public sealed class RegistryException(string message, HttpStatusCode? status = null, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>The registry's HTTP status, if it answered.</summary>
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// A client for the OCI distribution API (Docker Hub, GHCR, Quay and any v2 registry): token and basic
/// authentication, manifests and multi-platform indexes, and blobs downloaded with progress, resumed after a
/// dropped connection and verified against their digests.
/// </summary>
public sealed class RegistryClient
{
    private readonly HttpClient _http;
    private readonly Downloader _downloader;
    private readonly IRegistryCredentialStore _credentials;
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.Ordinal);

    /// <summary>Creates a client.</summary>
    /// <param name="http">The HTTP client, which follows redirects (blob storage is usually elsewhere).</param>
    /// <param name="credentials">Saved sign-ins.</param>
    public RegistryClient(HttpClient http, IRegistryCredentialStore credentials)
    {
        _http = http;
        _credentials = credentials;
        _downloader = new Downloader(new HttpClient(new AuthenticatingHandler(this, new HttpClientHandler { AllowAutoRedirect = true })) { Timeout = Timeout.InfiniteTimeSpan });
    }

    /// <summary>Gets the manifest for <paramref name="reference"/>, choosing the best platform it offers.</summary>
    /// <param name="reference">The image.</param>
    /// <param name="preference">Acceptable platforms, best first.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="RegistryException">The image or a suitable platform does not exist, or access was refused.</exception>
    public async Task<ResolvedManifest> ResolveAsync(ImageReference reference, IReadOnlyList<OciPlatform> preference, CancellationToken cancellationToken = default)
    {
        var (bytes, mediaType, digest) = await GetManifestAsync(reference, reference.ManifestReference, cancellationToken).ConfigureAwait(false);
        if (MediaTypes.IsIndex(mediaType) || IsIndexDocument(bytes))
        {
            var index = OciJson.Read<ImageIndex>(bytes);
            var candidates = index.Manifests.Where(m => m.Platform is { } p && p.Architecture != "unknown"
                && m.Annotations?.ContainsKey("vnd.docker.reference.type") != true).ToList();
            foreach (var wanted in preference)
            {
                var match = candidates.FirstOrDefault(m => wanted.Matches(m.Platform!));
                if (match is null)
                {
                    continue;
                }

                var (manifestBytes, _, manifestDigest) = await GetManifestAsync(reference, match.Digest, cancellationToken).ConfigureAwait(false);
                return new(reference, manifestDigest, OciJson.Read<ImageManifest>(manifestBytes), manifestBytes, match.Platform, digest);
            }

            var offered = string.Join(", ", candidates.Select(m => m.Platform!.ToString()).Distinct(StringComparer.Ordinal));
            throw new RegistryException($"{reference} has no image for this device ({string.Join(", ", preference)}); it offers {(offered.Length == 0 ? "none" : offered)}.");
        }

        return new(reference, digest, OciJson.Read<ImageManifest>(bytes), bytes, null, null);
    }

    /// <summary>Gets a manifest or index by tag or digest, checking its digest.</summary>
    public async Task<(byte[] Bytes, string? MediaType, string Digest)> GetManifestAsync(ImageReference image, string reference, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Address(image, $"manifests/{reference}"));
        request.Headers.Accept.ParseAdd(MediaTypes.ManifestAccept);
        request.Headers.Accept.ParseAdd(MediaTypes.OciManifest);
        using var response = await SendAsync(request, image, cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var digest = Digest(bytes);
        if (ImageReference.IsDigest(reference) && !string.Equals(reference, digest, StringComparison.Ordinal))
        {
            throw new RegistryException($"The manifest {reference} of {image} did not match its digest ({digest}); nothing was stored.");
        }

        return (bytes, response.Content.Headers.ContentType?.MediaType, digest);
    }

    /// <summary>Gets a small blob, such as an image configuration, into memory and checks its digest.</summary>
    public async Task<byte[]> GetBlobAsync(ImageReference image, Descriptor descriptor, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Address(image, $"blobs/{descriptor.Digest}"));
        using var response = await SendAsync(request, image, cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Digest(bytes), descriptor.Digest, StringComparison.Ordinal))
        {
            throw new RegistryException($"A blob of {image} did not match its digest {descriptor.Digest}; nothing was stored.");
        }

        return bytes;
    }

    /// <summary>
    /// Downloads a blob to <paramref name="destination"/>, resuming a partial download, and verifies its size and
    /// SHA-256 before it is moved into place.
    /// </summary>
    public Task DownloadBlobAsync(ImageReference image, Descriptor descriptor, string destination, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        if (!ImageReference.IsDigest(descriptor.Digest))
        {
            throw new RegistryException($"{image} names a blob with an unsupported digest ({descriptor.Digest}).");
        }

        return _downloader.DownloadAsync(new DownloadRequest
        {
            Address = Address(image, $"blobs/{descriptor.Digest}"),
            Destination = destination,
            Name = $"a layer of {image.Familiar}",
            Size = descriptor.Size > 0 ? descriptor.Size : null,
            Sha256 = descriptor.Digest["sha256:".Length..],
        }, progress, cancellationToken);
    }

    /// <summary>Checks a sign-in against a registry, as <c>docker login</c> does.</summary>
    /// <exception cref="RegistryException">The registry refused it or could not be reached.</exception>
    public async Task LoginAsync(string registry, RegistryCredential credential, CancellationToken cancellationToken = default)
    {
        var host = registry is ImageReference.DockerHub or "index.docker.io" ? "registry-1.docker.io" : registry;
        var image = new ImageReference(registry, "library/hello-world", "latest", null);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{(image.IsInsecure ? "http" : "https")}://{host}/v2/"));
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized && Challenge.Parse(response.Headers.WwwAuthenticate) is { } challenge)
        {
            if (challenge.Scheme == "Basic")
            {
                using var basic = new HttpRequestMessage(HttpMethod.Get, request.RequestUri);
                basic.Headers.Authorization = new("Basic", Basic(credential));
                using var answer = await _http.SendAsync(basic, cancellationToken).ConfigureAwait(false);
                if (!answer.IsSuccessStatusCode) throw new RegistryException($"{registry} did not accept this user name and password.", answer.StatusCode);
                return;
            }

            await FetchTokenAsync(challenge, credential, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new RegistryException($"{registry} answered {(int)response.StatusCode} {response.StatusCode}.", response.StatusCode);
        }
    }

    /// <summary>The <c>sha256:…</c> digest of some bytes.</summary>
    public static string Digest(ReadOnlySpan<byte> bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsIndexDocument(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("manifests", out var manifests) && manifests.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Uri Address(ImageReference image, string path) => new($"{(image.IsInsecure ? "http" : "https")}://{image.ApiHost}/v2/{image.Repository}/{path}");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, ImageReference image, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await SendAuthenticatedAsync(request, _http, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new RegistryException($"Could not reach {image.ApiHost}: {ex.Message.TrimEnd('.')}. Check that the tablet is online.", null, ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        var detail = await ErrorDetailAsync(response, cancellationToken).ConfigureAwait(false);
        response.Dispose();
        throw status switch
        {
            HttpStatusCode.NotFound => new RegistryException($"{image} was not found on {image.Registry}{detail}.", status),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new RegistryException(
                $"{image.Registry} refused access to {image.Name}{detail}. If it is private, sign in to {image.Registry} in Containers → Registries.", status),
            HttpStatusCode.TooManyRequests => new RegistryException($"{image.Registry} limits pulls right now (too many requests). Sign in to raise the limit, or try again later.", status),
            _ => new RegistryException($"{image.Registry} answered {(int)status} {status} for {image}{detail}.", status),
        };
    }

    private static async Task<string> ErrorDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                && errors[0].TryGetProperty("message", out var message) && message.GetString() is { Length: > 0 } value)
            {
                return $" ({value})";
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException)
        {
        }

        return "";
    }

    /// <summary>Sends a GET, answering a 401 challenge with a token (or basic sign-in) and sending it once more.</summary>
    private async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpRequestMessage request, HttpMessageInvoker invoker, CancellationToken cancellationToken)
    {
        var key = TokenKey(request.RequestUri!);
        if (_tokens.TryGetValue(key, out var cached))
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(cached);
        }

        var response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || Challenge.Parse(response.Headers.WwwAuthenticate) is not { } challenge)
        {
            return response;
        }

        var credential = await _credentials.GetAsync(SessionRegistryCredentialStore.Normalize(RegistryName(request.RequestUri!)), cancellationToken).ConfigureAwait(false);
        string authorization;
        if (challenge.Scheme == "Basic")
        {
            if (credential is null)
            {
                return response;
            }

            authorization = "Basic " + Basic(credential);
        }
        else
        {
            challenge = challenge with { Scope = challenge.Scope ?? ScopeFor(request.RequestUri!) };
            authorization = "Bearer " + await FetchTokenAsync(challenge, credential, cancellationToken).ConfigureAwait(false);
        }

        response.Dispose();
        _tokens[key] = authorization;
        using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers.Where(h => h.Key != "Authorization"))
        {
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        retry.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        return await invoker.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> FetchTokenAsync(Challenge challenge, RegistryCredential? credential, CancellationToken cancellationToken)
    {
        if (challenge.Realm is null || !Uri.TryCreate(challenge.Realm, UriKind.Absolute, out var realm) || realm.Scheme is not ("https" or "http"))
        {
            throw new RegistryException("The registry asked for a token without saying where to get it.");
        }

        if (realm.Scheme == "http" && !realm.IsLoopback)
        {
            throw new RegistryException("The registry's token service must use HTTPS except on loopback.");
        }

        var query = new StringBuilder();
        if (challenge.Service is { } service) query.Append("service=").Append(Uri.EscapeDataString(service));
        if (challenge.Scope is { } scope)
        {
            foreach (var part in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                query.Append(query.Length > 0 ? "&" : "").Append("scope=").Append(Uri.EscapeDataString(part));
            }
        }

        var address = new UriBuilder(realm) { Query = string.IsNullOrEmpty(realm.Query) ? query.ToString() : realm.Query.TrimStart('?') + "&" + query }.Uri;
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (credential is not null)
        {
            request.Headers.Authorization = new("Basic", Basic(credential));
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new RegistryException(credential is null
                ? $"{realm.Host} did not give an anonymous token ({(int)response.StatusCode}); the image may be private."
                : $"{realm.Host} did not accept the saved sign-in ({(int)response.StatusCode}).", response.StatusCode);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        var root = document.RootElement;
        return (root.TryGetProperty("token", out var token) ? token.GetString() : null)
            ?? (root.TryGetProperty("access_token", out var access) ? access.GetString() : null)
            ?? throw new RegistryException($"{realm.Host} answered without a token.");
    }

    private static string Basic(RegistryCredential credential) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Secret}"));

    private static string RegistryName(Uri address) => address.Host == "registry-1.docker.io" ? ImageReference.DockerHub : address.IsDefaultPort ? address.Host : address.Authority;

    /// <summary>Tokens are scoped to a repository: one key per registry and repository.</summary>
    private static string TokenKey(Uri address) => address.Authority + "|" + RepositoryOf(address);

    private static string ScopeFor(Uri address) => $"repository:{RepositoryOf(address)}:pull";

    private static string RepositoryOf(Uri address)
    {
        var path = address.AbsolutePath;
        var start = path.IndexOf("/v2/", StringComparison.Ordinal);
        if (start < 0) return "";
        path = path[(start + 4)..];
        foreach (var marker in new[] { "/manifests/", "/blobs/", "/tags/" })
        {
            var at = path.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0) return path[..at];
        }

        return path.TrimEnd('/');
    }

    /// <summary>A <c>WWW-Authenticate</c> challenge.</summary>
    internal sealed record Challenge(string Scheme, string? Realm, string? Service, string? Scope)
    {
        public static Challenge? Parse(HttpHeaderValueCollection<AuthenticationHeaderValue> headers)
        {
            foreach (var header in headers)
            {
                if (header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
                {
                    return new("Basic", null, null, null);
                }

                if (header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
                {
                    var values = ParseParameters(header.Parameter ?? "");
                    return new("Bearer", values.GetValueOrDefault("realm"), values.GetValueOrDefault("service"), values.GetValueOrDefault("scope"));
                }
            }

            return null;
        }

        internal static Dictionary<string, string> ParseParameters(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && (text[i] == ',' || char.IsWhiteSpace(text[i]))) i++;
                var equals = text.IndexOf('=', i);
                if (equals < 0) break;
                var name = text[i..equals].Trim();
                i = equals + 1;
                string value;
                if (i < text.Length && text[i] == '"')
                {
                    var builder = new StringBuilder();
                    for (i++; i < text.Length && text[i] != '"'; i++)
                    {
                        if (text[i] == '\\' && i + 1 < text.Length) i++;
                        builder.Append(text[i]);
                    }

                    i++;
                    value = builder.ToString();
                }
                else
                {
                    var end = text.IndexOf(',', i);
                    value = (end < 0 ? text[i..] : text[i..end]).Trim();
                    i = end < 0 ? text.Length : end;
                }

                result[name] = value;
            }

            return result;
        }
    }

    /// <summary>Adds registry authentication to the downloader's requests, which go through the same challenge.</summary>
    private sealed class AuthenticatingHandler(RegistryClient client, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            client.SendAuthenticatedAsync(request, new Forwarder(base.SendAsync), cancellationToken);
    }

    private sealed class Forwarder(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageInvoker(new NullHandler())
    {
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class NullHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

