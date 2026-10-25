using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// Verifies Alpine's signed files: a repository index against the distribution's keys, and a package against
/// the index entry that lists it.
/// </summary>
/// <remarks>
/// <para>
/// Both are concatenated gzip streams. An index is a signature stream followed by the index stream, and the
/// signature (<c>.SIGN.RSA.&lt;key&gt;.rsa.pub</c>, RSA with SHA-1, or <c>.SIGN.RSA256.</c> with SHA-256) covers the
/// compressed bytes of the index stream. A package is a signature stream, a control stream and a data stream:
/// the index's <c>C:</c> field is the SHA-1 of the compressed control stream, and the control stream's
/// <c>.PKGINFO</c> has the SHA-256 of the compressed data stream as <c>datahash</c>.
/// </para>
/// <para>
/// So a package verifies when its control stream matches the signed index and its data stream matches the
/// control stream, the chain <c>apk</c> itself checks. The streams' boundaries are not stored anywhere; they
/// are found by trying the offsets where a gzip header starts, and only the true boundary matches a hash or a
/// signature.
/// </para>
/// </remarks>
public static class ApkSignature
{
    private const string SignaturePrefix = ".SIGN.";
    private const int SearchWindow = 1 << 20;

    /// <summary>Reads the trusted keys from a root file system's <c>etc/apk/keys</c> folder.</summary>
    /// <param name="folder">The folder of <c>*.rsa.pub</c> files.</param>
    /// <returns>The keys by file name, as signatures name them.</returns>
    public static Dictionary<string, RSA> LoadKeys(string folder)
    {
        var keys = new Dictionary<string, RSA>(StringComparer.Ordinal);
        if (!Directory.Exists(folder))
        {
            return keys;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.pub"))
        {
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(File.ReadAllText(file));
                keys[Path.GetFileName(file)] = rsa;
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                rsa.Dispose();
            }
        }

        return keys;
    }

    /// <summary>Verifies a repository index's signature and returns the index.</summary>
    /// <param name="archive">The contents of <c>APKINDEX.tar.gz</c>.</param>
    /// <param name="keys">The trusted keys, by file name.</param>
    /// <param name="repository">The repository the index belongs to, recorded on its packages.</param>
    /// <exception cref="InvalidDataException">The index is not signed by a trusted key, or is damaged.</exception>
    [SuppressMessage("Security", "CA5350", Justification = "Alpine signs its indexes with RSA and SHA-1; reading them means verifying that.")]
    public static List<ApkPackage> ReadIndex(byte[] archive, IReadOnlyDictionary<string, RSA> keys, string? repository = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(keys);
        string? signatureName = null;
        byte[]? signature = null;
        string? text = null;
        try
        {
            using var gzip = new GZipStream(new MemoryStream(archive, writable: false), CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.Name.StartsWith(SignaturePrefix, StringComparison.Ordinal) && signature is null)
                {
                    signatureName = entry.Name;
                    signature = ReadAll(entry.DataStream);
                }
                else if (entry.Name == "APKINDEX")
                {
                    text = Encoding.UTF8.GetString(ReadAll(entry.DataStream));
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or FormatException)
        {
            throw new InvalidDataException($"The package index is damaged: {ex.Message}", ex);
        }

        if (signature is null || signatureName is null)
        {
            throw new InvalidDataException("The package index is not signed.");
        }

        if (text is null)
        {
            throw new InvalidDataException("The package index has no APKINDEX.");
        }

        var (algorithm, keyName) = ParseSignatureName(signatureName);
        if (!keys.TryGetValue(keyName, out var key))
        {
            throw new InvalidDataException($"The package index is signed with a key TabStudio does not trust ({keyName}).");
        }

        var signed = false;
        foreach (var start in GzipStarts(archive, 1, Math.Min(archive.Length, SearchWindow)))
        {
            if (key.VerifyData(archive.AsSpan(start), signature, algorithm, RSASignaturePadding.Pkcs1))
            {
                signed = true;
                break;
            }
        }

        if (!signed)
        {
            throw new InvalidDataException("The package index's signature does not match its contents.");
        }

        return ApkDatabaseText.Read(text, repository);
    }

    /// <summary>Verifies a downloaded package against the index entry that lists it.</summary>
    /// <param name="path">The package file.</param>
    /// <param name="expected">Its entry in a verified index.</param>
    /// <returns>Why the package does not match, or <see langword="null"/> when it does.</returns>
    [SuppressMessage("Security", "CA5350", Justification = "An Alpine index identifies a package's control section by its SHA-1.")]
    public static string? VerifyPackage(string path, ApkPackage expected)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.Checksum is not { Length: > 2 } checksum)
        {
            return "its index entry has no checksum.";
        }

        HashAlgorithmName algorithm;
        byte[] digest;
        try
        {
            (algorithm, digest) = checksum[..2] switch
            {
                "Q1" => (HashAlgorithmName.SHA1, Convert.FromBase64String(checksum[2..])),
                "Q2" => (HashAlgorithmName.SHA256, Convert.FromBase64String(checksum[2..])),
                _ => throw new FormatException(),
            };
        }
        catch (FormatException)
        {
            return $"its index checksum '{checksum}' is not in a format TabStudio knows.";
        }

        using var file = File.OpenRead(path);
        var prefix = new byte[(int)Math.Min(file.Length, SearchWindow)];
        file.ReadExactly(prefix);
        var starts = GzipStarts(prefix, 1, prefix.Length).ToList();

        // The control stream starts right after the small signature stream, and ends where the data stream starts.
        foreach (var controlStart in starts)
        {
            using var hash = IncrementalHash.CreateHash(algorithm);
            var hashed = controlStart;
            foreach (var dataStart in starts.Where(s => s > controlStart))
            {
                hash.AppendData(prefix, hashed, dataStart - hashed);
                hashed = dataStart;
                if (!hash.GetCurrentHash().AsSpan().SequenceEqual(digest))
                {
                    continue;
                }

                var dataHash = ReadDataHash(prefix.AsSpan(controlStart, dataStart - controlStart));
                if (dataHash is null)
                {
                    return "its control section has no data hash.";
                }

                file.Position = dataStart;
                var actual = Convert.ToHexStringLower(SHA256.HashData(file));
                return string.Equals(actual, dataHash, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "its files do not match the hash in its control section.";
            }
        }

        return "it does not match the checksum in the signed package index.";
    }

    /// <summary>Reads <c>datahash</c> from a compressed control stream's <c>.PKGINFO</c>.</summary>
    private static string? ReadDataHash(ReadOnlySpan<byte> control)
    {
        try
        {
            using var gzip = new GZipStream(new MemoryStream(control.ToArray(), writable: false), CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.Name != ".PKGINFO")
                {
                    continue;
                }

                using var text = new StreamReader(entry.DataStream ?? Stream.Null);
                while (text.ReadLine() is { } line)
                {
                    if (line.StartsWith("datahash = ", StringComparison.Ordinal))
                    {
                        return line["datahash = ".Length..].Trim();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or FormatException)
        {
            // Not a control stream after all.
        }

        return null;
    }

    /// <summary>The offsets in a range where a gzip member may start: <c>1f 8b 08</c> with no reserved flags.</summary>
    private static IEnumerable<int> GzipStarts(byte[] data, int from, int to)
    {
        for (var i = from; i + 3 < to; i++)
        {
            if (data[i] == 0x1f && data[i + 1] == 0x8b && data[i + 2] == 0x08 && (data[i + 3] & 0xe0) == 0)
            {
                yield return i;
            }
        }
    }

    private static (HashAlgorithmName Algorithm, string KeyName) ParseSignatureName(string name)
    {
        // .SIGN.RSA.<key>, .SIGN.RSA256.<key> or .SIGN.RSA512.<key>.
        var rest = name[SignaturePrefix.Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);
        var kind = dot < 0 ? rest : rest[..dot];
        var key = dot < 0 ? string.Empty : rest[(dot + 1)..];
        var algorithm = kind switch
        {
            "RSA256" => HashAlgorithmName.SHA256,
            "RSA512" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA1,
        };
        return (algorithm, key);
    }

    private static byte[] ReadAll(Stream? stream)
    {
        if (stream is null)
        {
            return [];
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
