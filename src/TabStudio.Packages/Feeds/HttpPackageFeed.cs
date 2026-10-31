using System.Net;
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
/// <para>A missing package is a 404, which is not an error. Anything else that goes wrong throws <see cref="PackageSourceException"/>.</para>
/// </remarks>
internal sealed class HttpPackageFeed : IPackageFeed
{
    private const string PackageBaseAddressType = "PackageBaseAddress/3.0.0";

    private readonly HttpClient _http;
    private readonly Lock _gate = new();
    private Task<Uri>? _baseAddress;

    /// <summary>Creates a feed for a V3 source.</summary>
    /// <param name="source">The source; its location is the service index URL.</param>
    /// <param name="http">The client to send requests with.</param>
    public HttpPackageFeed(PackageSource source, HttpClient http)
    {
        Source = source;
        _http = http;
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
    /// Sends a GET request and hands the body to <paramref name="read"/>.
    /// </summary>
    /// <returns>What <paramref name="read"/> returned, or <see langword="null"/> for a 404.</returns>
    private async Task<T?> GetAsync<T>(Uri address, Func<Stream, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new PackageSourceException(
                    Source,
                    $"{Source.Name} answered {(int)response.StatusCode} ({response.ReasonPhrase}) for {address}.",
                    isUnreachable: false);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await read(body, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
        catch (IOException ex) when (ex is HttpIOException || ex.InnerException is SocketException)
        {
            throw Unreachable(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PackageSourceException(Source, $"{Source.Name} ({Source.Location}) did not answer in time.", isUnreachable: true, ex);
        }
        catch (JsonException ex)
        {
            throw new PackageSourceException(Source, $"{Source.Name} returned JSON that is not valid for {address}.", isUnreachable: false, ex);
        }
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
