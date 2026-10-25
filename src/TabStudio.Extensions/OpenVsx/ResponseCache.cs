using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Extensions.OpenVsx;

/// <summary>A cached answer: its body, the ETag to revalidate it with, and when it was stored.</summary>
/// <param name="Body">The response body.</param>
/// <param name="ETag">The ETag the registry sent, or <see langword="null"/>.</param>
/// <param name="StoredAt">When it was stored.</param>
internal sealed record CachedResponse(byte[] Body, string? ETag, DateTimeOffset StoredAt);

/// <summary>
/// The registry's answers kept in memory and, when a folder is given, on disk, so the Extensions window works
/// offline with what it showed last, and icons and READMEs of a version are downloaded once.
/// </summary>
internal sealed class ResponseCache
{
    private const int MaximumMemoryEntries = 400;
    private const int MaximumMemoryBodyBytes = 512 * 1024;
    private const long MaximumDiskBytes = 64L * 1024 * 1024;

    private readonly string? _folder;
    private readonly ConcurrentDictionary<string, CachedResponse> _memory = new(StringComparer.Ordinal);

    /// <summary>Creates the cache, trimming the disk part to its size limit.</summary>
    /// <param name="folder">Where to keep answers on disk, or <see langword="null"/> to keep them in memory only.</param>
    public ResponseCache(string? folder)
    {
        _folder = folder;
        if (folder is not null)
        {
            try
            {
                Directory.CreateDirectory(folder);
                Trim(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _folder = null;
            }
        }
    }

    /// <summary>The cached answer for an address, or <see langword="null"/>.</summary>
    public CachedResponse? Find(Uri address)
    {
        var key = Key(address);
        if (_memory.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_folder is null)
        {
            return null;
        }

        try
        {
            var body = Path.Combine(_folder, key + ".body");
            if (!File.Exists(body))
            {
                return null;
            }

            var etagFile = Path.Combine(_folder, key + ".etag");
            var etag = File.Exists(etagFile) ? File.ReadAllText(etagFile) : null;
            cached = new CachedResponse(File.ReadAllBytes(body), string.IsNullOrEmpty(etag) ? null : etag, File.GetLastWriteTimeUtc(body));
            Remember(key, cached);
            return cached;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Keeps an answer.</summary>
    public void Store(Uri address, byte[] body, string? etag)
    {
        var key = Key(address);
        var cached = new CachedResponse(body, etag, DateTimeOffset.UtcNow);
        Remember(key, cached);
        if (_folder is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_folder, key + ".body");
            File.WriteAllBytes(path + ".tmp", body);
            File.Move(path + ".tmp", path, overwrite: true);
            var etagFile = Path.Combine(_folder, key + ".etag");
            if (etag is null)
            {
                File.Delete(etagFile);
            }
            else
            {
                File.WriteAllText(etagFile, etag);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A full disk only costs the offline copy.
        }
    }

    private void Remember(string key, CachedResponse cached)
    {
        if (cached.Body.Length > MaximumMemoryBodyBytes)
        {
            return;
        }

        if (_memory.Count >= MaximumMemoryEntries)
        {
            _memory.Clear();
        }

        _memory[key] = cached;
    }

    private static string Key(Uri address) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(address.AbsoluteUri)));

    /// <summary>Deletes the least recently written answers while the folder is over its size limit.</summary>
    private static void Trim(string folder)
    {
        var files = new DirectoryInfo(folder).GetFiles().OrderBy(f => f.LastWriteTimeUtc).ToList();
        var total = files.Sum(f => f.Length);
        foreach (var file in files)
        {
            if (total <= MaximumDiskBytes)
            {
                break;
            }

            total -= file.Length;
            file.Delete();
        }
    }
}
