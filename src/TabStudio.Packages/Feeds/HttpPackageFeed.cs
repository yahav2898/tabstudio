using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using NuGet.Versioning;
using TabStudio.Packages.Installation;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Feeds;

/// <summary>
/// Reads a NuGet V3 feed over HTTP, through its package base address resource (the "flat container").
/// </summary>
/// <remarks>
/// <para>
/// The feed first reads the service index to find the <c>PackageBaseAddress/3.0.0</c> resource, then makes only
/// three kinds of request, all plain static files on nuget.org's CDN: the version list
/// <c>{id}/index.json</c>, the manifest <c>{id}/{version}/{id}.nuspec</c> and the package
/// <c>{id}/{version}/{id}.{version}.nupkg</c>, with the id and version in lower case.
/// See https://learn.microsoft.com/nuget/api/package-base-address-resource.
/// </para>
/// <para>
/// A missing package is a 404, which is not an error. A failure that may pass is tried again, a few times with growing
/// delays: a connection that was reset or refused, a body cut short, a timeout, a server error (5xx), 408 or 429, and a
/// 304 Not Modified, which no request of the feed asks for, so a cache on the way gave it: that one is asked again past
/// the caches. Anything else that goes wrong, or still goes wrong after the retries, throws
/// <see cref="PackageSourceException"/>.
/// </para>
/// </remarks>
internal sealed class HttpPackageFeed : IPackageFeed
{
    private const string PackageBaseAddressType = "PackageBaseAddress/3.0.0";

    private readonly HttpClient _http;
    private readonly IReadOnlyList<TimeSpan> _retryDelays;
    private readonly Lock _gate = new();
    private Task<Uri>? _baseAddress;
    private bool _retriesExhausted;

    /// <summary>Creates a feed for a V3 source.</summary>
    /// <param name="source">The source; its location is the service index URL.</param>
    /// <param name="http">The client to send requests with.</param>
    /// <param name="retryDelays">
    /// How long to wait before each new try of a request that failed in a way that may pass (see
    /// <see cref="PackageRestoreOptions.HttpRetryDelays"/>); <see langword="null"/> for the default delays.
    /// </param>
    public HttpPackageFeed(PackageSource source, HttpClient http, IReadOnlyList<TimeSpan>? retryDelays = null)
    {
        Source = source;
        _http = http;
        _retryDelays = retryDelays ?? PackageRestoreOptions.DefaultHttpRetryDelays;
    }

    /// <inheritdoc />
    public PackageSource Source { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NuGetVersion>> GetVersionsAsync(string id, CancellationToken cancellationToken)
    {
        var address = await ResolveAsync($"{NuGetNames.Lower(id)}/index.json", cancellationToken).ConfigureAwait(false);
        return await GetAsync(address, ReadVersionsAsync, cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task<PackageManifest?> GetManifestAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
    {
        var lowerId = NuGetNames.Lower(id);
        var address = await ResolveAsync($"{lowerId}/{NuGetNames.Lower(version)}/{lowerId}.nuspec", cancellationToken).ConfigureAwait(false);
        return await GetAsync(address, ReadManifestAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DownloadAsync(string id, NuGetVersion version, string destinationPath, CancellationToken cancellationToken)
    {
        var lowerId = NuGetNames.Lower(id);
        var lowerVersion = NuGetNames.Lower(version);
        var address = await ResolveAsync($"{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg", cancellationToken).ConfigureAwait(false);

        var written = await GetAsync(
            address,
            async (stream, token) =>
            {
                await using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                await stream.CopyToAsync(file, token).ConfigureAwait(false);
                return destinationPath;
            },
            cancellationToken).ConfigureAwait(false);

        return written is not null;
    }

    /// <summary>Combines the package base address with a relative path.</summary>
    private async Task<Uri> ResolveAsync(string relativePath, CancellationToken cancellationToken)
    {
        var baseAddress = await GetBaseAddressAsync(cancellationToken).ConfigureAwait(false);
        return new Uri(baseAddress, relativePath);
    }

    /// <summary>
    /// Returns the package base address, reading the service index once. A failed read is retried by the next caller.
    /// </summary>
    private Task<Uri> GetBaseAddressAsync(CancellationToken cancellationToken)
    {
        Task<Uri> task;
        lock (_gate)
        {
            if (_baseAddress is null || _baseAddress.IsFaulted || _baseAddress.IsCanceled)
            {
                _baseAddress = ReadServiceIndexAsync();
            }

            task = _baseAddress;
        }

        return task.WaitAsync(cancellationToken);
    }

    /// <summary>How long reading a source's service index may take.</summary>
    internal static TimeSpan ServiceIndexTimeout { get; set; } = TimeSpan.FromSeconds(100);

    private async Task<Uri> ReadServiceIndexAsync()
    {
        if (!Uri.TryCreate(Source.Location, UriKind.Absolute, out var index))
        {
            throw new PackageSourceException(Source, $"The address of {Source.Name} ('{Source.Location}') is not a valid URL.", isUnreachable: false);
        }

        // Shared by every caller, so it does not take any one caller's cancellation token. HttpClient.Timeout does not
        // bound a body read once the headers have come, so a stalled connection left this task pending for good, and
        // every restore from the source waited on it until the app was restarted: it has a limit of its own.
        using var timeout = new CancellationTokenSource(ServiceIndexTimeout);
        Uri? address;
        try
        {
            address = await GetAsync(index, ReadPackageBaseAddressAsync, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
        {
            throw Unreachable(ex);
        }

        return address ?? throw new PackageSourceException(Source, $"{Source.Name} has no NuGet V3 service index at {Source.Location}.", isUnreachable: false);
    }

    /// <summary>
    /// Sends a GET request and hands the body to <paramref name="read"/>, again after a transient failure (see
    /// <see cref="IsTransient"/>), after each of the <see cref="_retryDelays"/>.
    /// </summary>
    /// <returns>What <paramref name="read"/> returned, or <see langword="null"/> for a 404.</returns>
    private async Task<T?> GetAsync<T>(Uri address, Func<Stream, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
        where T : class
    {
        var bypassCaches = false;
        for (var attempt = 0; ; attempt++)
        {
            var outcome = await GetOnceAsync(address, read, bypassCaches, cancellationToken).ConfigureAwait(false);
            if (outcome.Failure is not { } failure)
            {
                return outcome.Value;
            }

            // A source that stayed down through every retry is asked once from then on, so a restore of many packages
            // does not wait out the delays again for each of them.
            if (!outcome.IsTransient || attempt >= _retryDelays.Count || Volatile.Read(ref _retriesExhausted))
            {
                if (outcome.IsTransient)
                {
                    Volatile.Write(ref _retriesExhausted, true);
                }

                throw failure;
            }

            bypassCaches |= outcome.BypassCaches;
            var delay = outcome.RetryAfter is { } asked && asked > _retryDelays[attempt] && asked <= MaxRetryAfter ? asked : _retryDelays[attempt];
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The longest wait a server's <c>Retry-After</c> may ask for before a retry; a longer one is not waited for.</summary>
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Sends one GET request and hands the body to <paramref name="read"/>. Whatever goes wrong with the source is in
    /// the outcome, with whether it is worth trying again, rather than thrown.
    /// </summary>
    /// <param name="address">What to get.</param>
    /// <param name="read">Reads the body.</param>
    /// <param name="bypassCaches">
    /// Whether to ask the caches between here and the source (nuget.org's CDN, a proxy) for a fresh answer, after one
    /// answered with something no source would send.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    private async Task<Attempt<T>> GetOnceAsync<T>(Uri address, Func<Stream, CancellationToken, Task<T>> read, bool bypassCaches, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            if (bypassCaches)
            {
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
            }

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                var failure = new PackageSourceException(
                    Source,
                    $"{Source.Name} answered {(int)response.StatusCode} ({response.ReasonPhrase}) for {address}.",
                    isUnreachable: false);

                // 304 Not Modified answers a conditional request, and this one had no If-None-Match or If-Modified-Since:
                // a cache on the way (nuget.org's CDN) answered for a copy it does not have. Moq's restore failed with
                // NU1301 on one. Asked again past the caches, the source answers.
                if (response.StatusCode == HttpStatusCode.NotModified && IsUnconditional(request))
                {
                    return Attempt<T>.Transient(failure, bypassCaches: true);
                }

                return IsTransient(response.StatusCode)
                    ? Attempt<T>.Transient(failure, retryAfter: RetryAfterOf(response))
                    : Attempt<T>.Failed(failure);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new Attempt<T>(await read(body, cancellationToken).ConfigureAwait(false));
        }
        catch (HttpRequestException ex)
        {
            // Without a network, the name of the source does not resolve: no retry brings it back.
            return IsOffline(ex) ? Attempt<T>.Failed(Unreachable(ex)) : Attempt<T>.Transient(Unreachable(ex));
        }
        catch (IOException ex) when (ex is HttpIOException || ex.InnerException is SocketException)
        {
            // The connection was reset or the body ended before its length: a truncated answer.
            return Attempt<T>.Transient(Unreachable(ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Attempt<T>.Transient(new PackageSourceException(Source, $"{Source.Name} ({Source.Location}) did not answer in time.", isUnreachable: true, ex));
        }
        catch (JsonException ex)
        {
            // A body cut short without a length to check it against reads as JSON that ends too soon.
            return Attempt<T>.Transient(
                new PackageSourceException(Source, $"{Source.Name} returned JSON that is not valid for {address}.", isUnreachable: false, ex),
                bypassCaches: true);
        }
    }

    /// <summary>
    /// Whether an answer says the source is busy or briefly broken rather than that the request is wrong: a server
    /// error (5xx), a request timeout (408) or too many requests (429).
    /// </summary>
    /// <param name="status">The status of the answer.</param>
    internal static bool IsTransient(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

    /// <summary>Whether a request asks for a fresh copy only if it changed: only then is 304 Not Modified an answer.</summary>
    private static bool IsUnconditional(HttpRequestMessage request) =>
        request.Headers.IfNoneMatch.Count == 0 && request.Headers.IfModifiedSince is null;

    /// <summary>How long the server asked to wait before trying again, when it said.</summary>
    private static TimeSpan? RetryAfterOf(HttpResponseMessage response) =>
        response.Headers.RetryAfter is not { } retryAfter ? null
        : retryAfter.Delta is { } delta ? delta
        : retryAfter.Date is { } date ? date - DateTimeOffset.UtcNow
        : null;

    /// <summary>
    /// Whether a request failed because the device has no network: the source's name did not resolve, or there is no
    /// route to it. A connection that was refused, reset or broke off is worth trying again.
    /// </summary>
    private static bool IsOffline(HttpRequestException exception) =>
        exception.HttpRequestError == HttpRequestError.NameResolutionError
        || exception.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.NetworkDown };

    /// <summary>The outcome of one request: what was read, or what went wrong and whether it is worth trying again.</summary>
    /// <param name="Value">What was read; <see langword="null"/> for a 404 or a failure.</param>
    /// <param name="Failure">What went wrong, or <see langword="null"/>.</param>
    /// <param name="IsTransient">Whether the same request may well succeed if sent again.</param>
    /// <param name="BypassCaches">Whether to ask past the caches on the way when trying again.</param>
    /// <param name="RetryAfter">How long the server asked to wait, when it said.</param>
    private readonly record struct Attempt<T>(T? Value, PackageSourceException? Failure = null, bool IsTransient = false, bool BypassCaches = false, TimeSpan? RetryAfter = null)
        where T : class
    {
        public static Attempt<T> Failed(PackageSourceException failure) => new(null, failure);

        public static Attempt<T> Transient(PackageSourceException failure, bool bypassCaches = false, TimeSpan? retryAfter = null) =>
            new(null, failure, IsTransient: true, bypassCaches, retryAfter);
    }

    private PackageSourceException Unreachable(Exception ex) =>
        new(Source, $"Could not reach {Source.Name} ({Source.Location}): {ex.Message}", isUnreachable: true, ex);

    private static async Task<IReadOnlyList<NuGetVersion>> ReadVersionsAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var versions = new List<NuGetVersion>();
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("versions", out var list) &&
            list.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in list.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String && NuGetVersion.TryParse(element.GetString(), out var version))
                {
                    versions.Add(version);
                }
            }
        }

        return versions;
    }

    private async Task<PackageManifest> ReadManifestAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Buffer the small manifest so the XML reader does not block on the network.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        try
        {
            return NuspecReader.Read(buffer);
        }
        catch (InvalidPackageException ex)
        {
            throw new PackageSourceException(Source, $"{Source.Name} returned a package manifest that is not valid: {ex.Message}", isUnreachable: false, ex);
        }
    }

    private async Task<Uri> ReadPackageBaseAddressAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("resources", out var resources) &&
            resources.ValueKind == JsonValueKind.Array)
        {
            foreach (var resource in resources.EnumerateArray())
            {
                if (resource.ValueKind == JsonValueKind.Object &&
                    resource.TryGetProperty("@type", out var type) &&
                    type.ValueKind == JsonValueKind.String &&
                    string.Equals(type.GetString(), PackageBaseAddressType, StringComparison.Ordinal) &&
                    resource.TryGetProperty("@id", out var id) &&
                    id.ValueKind == JsonValueKind.String &&
                    Uri.TryCreate(id.GetString(), UriKind.Absolute, out var address))
                {
                    return address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
                }
            }
        }

        throw new PackageSourceException(
            Source,
            $"The service index of {Source.Name} ({Source.Location}) has no {PackageBaseAddressType} resource, which TabStudio needs to download packages.",
            isUnreachable: false);
    }
}
