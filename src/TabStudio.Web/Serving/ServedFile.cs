using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TabStudio.Web.StaticWebAssets;

namespace TabStudio.Web.Serving;

/// <summary>
/// A file a <see cref="LocalWebServer"/> serves: a file on disk, served as it is, or content built in memory, such as
/// a compiled assembly or a page whose placeholders were filled in.
/// </summary>
public sealed class ServedFile
{
    private readonly ReadOnlyMemory<byte> _content;
    private string? _hash;

    private ServedFile(string route, string? filePath, ReadOnlyMemory<byte> content)
    {
        Route = StaticWebAsset.NormalizeRoute(route);
        FilePath = filePath;
        _content = content;
        ContentType = ContentTypes.For(Route);
    }

    /// <summary>Where it is served, such as <c>_framework/dotnet.js</c>.</summary>
    public string Route { get; }

    /// <summary>Its media type, from its extension.</summary>
    public string ContentType { get; }

    /// <summary>The file it is read from, or <see langword="null"/> when it is held in memory.</summary>
    public string? FilePath { get; }

    /// <summary>Whether it is held in memory rather than read from a file.</summary>
    public bool IsInMemory => FilePath is null;

    /// <summary>Its content when it is held in memory; empty for a file.</summary>
    public ReadOnlyMemory<byte> Content => _content;

    /// <summary>Its length in bytes.</summary>
    public long Length => FilePath is null ? _content.Length : StaticAssetManifests.Describe(FilePath).Length;

    /// <summary>The base64 SHA-256 hash of its content, which is its ETag and its subresource integrity.</summary>
    public string Hash => _hash ??= FilePath is null
        ? Convert.ToBase64String(SHA256.HashData(_content.Span))
        : StaticAssetManifests.Describe(FilePath).Hash;

    /// <summary>The <c>sha256-…</c> integrity value of its content, as boot configurations and import maps write it.</summary>
    public string Integrity => "sha256-" + Hash;

    /// <summary>A file on disk.</summary>
    /// <param name="route">Where it is served.</param>
    /// <param name="filePath">The file.</param>
    public static ServedFile FromFile(string route, string filePath)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return new ServedFile(route, filePath, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Content built in memory.</summary>
    /// <param name="route">Where it is served.</param>
    /// <param name="content">The bytes.</param>
    public static ServedFile FromContent(string route, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(route);
        return new ServedFile(route, null, content);
    }

    /// <summary>Opens its content for reading.</summary>
    public Stream OpenRead()
    {
        if (FilePath is not null)
        {
            return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 81920, useAsync: true);
        }

        return MemoryMarshal.TryGetArray(_content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(_content.ToArray(), writable: false);
    }

    /// <summary>Writes its content to a stream.</summary>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    public async Task CopyToAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (FilePath is null)
        {
            await destination.WriteAsync(_content, cancellationToken).ConfigureAwait(false);
            return;
        }

        var source = OpenRead();
        await using (source.ConfigureAwait(false))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override string ToString() => Route;
}
