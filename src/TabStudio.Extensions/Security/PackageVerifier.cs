using System.IO.Compression;
using System.Security.Cryptography;

namespace TabStudio.Extensions.Security;

/// <summary>
/// Checks a downloaded package against what its registry published: the SHA-256 hash, and Open VSX's signature, an
/// Ed25519 signature of the whole .vsix in a <c>.sigzip</c> archive, checked with the registry's public key.
/// </summary>
public static class PackageVerifier
{
    private const string SignatureEntry = ".signature.sig";

    // The DER prefix of an Ed25519 SubjectPublicKeyInfo: SEQUENCE { SEQUENCE { OID 1.3.101.112 }, BIT STRING }.
    private static readonly byte[] Ed25519KeyPrefix = [0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70, 0x03, 0x21, 0x00];

    /// <summary>The SHA-256 hash of a stream's bytes, read to its end, in lowercase hexadecimal.</summary>
    /// <param name="stream">The bytes.</param>
    public static string ComputeSha256(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>
    /// Whether a hash matches the one a registry published. Open VSX publishes the hexadecimal hash, sometimes
    /// followed by the file name, as <c>sha256sum</c> writes it.
    /// </summary>
    /// <param name="sha256">The computed hash, in hexadecimal.</param>
    /// <param name="published">The text of the registry's <c>.sha256</c> file.</param>
    public static bool MatchesPublishedSha256(string sha256, string published)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(published);
        var expected = published.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return expected is not null && string.Equals(expected, sha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads an Ed25519 public key from the PEM text Open VSX publishes (<c>-----BEGIN PUBLIC KEY-----</c>).</summary>
    /// <param name="pem">The PEM text.</param>
    /// <returns>The 32-byte key.</returns>
    /// <exception cref="FormatException">The text is not an Ed25519 public key.</exception>
    public static byte[] ReadPublicKey(string pem)
    {
        ArgumentNullException.ThrowIfNull(pem);
        if (!PemEncoding.TryFind(pem, out var fields) || !pem.AsSpan()[fields.Label].SequenceEqual("PUBLIC KEY"))
        {
            throw new FormatException("The registry's public key is not in PEM format.");
        }

        var der = Convert.FromBase64String(pem[fields.Base64Data]);
        if (der.Length != Ed25519KeyPrefix.Length + Ed25519.PublicKeyLength || !der.AsSpan(0, Ed25519KeyPrefix.Length).SequenceEqual(Ed25519KeyPrefix))
        {
            throw new FormatException("The registry's public key is not an Ed25519 key.");
        }

        return der[Ed25519KeyPrefix.Length..];
    }

    /// <summary>Reads the signature from an Open VSX signature archive (<c>.sigzip</c>).</summary>
    /// <param name="signatureArchive">The archive's bytes.</param>
    /// <returns>The 64-byte signature.</returns>
    /// <exception cref="FormatException">The archive has no valid signature.</exception>
    public static byte[] ReadSignature(Stream signatureArchive)
    {
        ArgumentNullException.ThrowIfNull(signatureArchive);
        try
        {
            using var zip = new ZipArchive(signatureArchive, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry(SignatureEntry) ?? throw new FormatException("The signature archive has no signature.");
            if (entry.Length != Ed25519.SignatureLength)
            {
                throw new FormatException("The signature archive's signature has the wrong length.");
            }

            using var stream = entry.Open();
            var signature = new byte[Ed25519.SignatureLength];
            stream.ReadExactly(signature);
            return signature;
        }
        catch (InvalidDataException ex)
        {
            throw new FormatException("The signature archive is damaged.", ex);
        }
    }

    /// <summary>Verifies a package's signature.</summary>
    /// <param name="package">The package's bytes, read to the end.</param>
    /// <param name="signature">The 64-byte signature.</param>
    /// <param name="publicKey">The registry's 32-byte public key.</param>
    public static bool VerifySignature(Stream package, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey) =>
        Ed25519.Verify(publicKey, package, signature);
}
