using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TabStudio.Link.Security;

/// <summary>
/// The SHA-256 hash of a device's public key, which identifies the device for good. A tablet pins its host by
/// this fingerprint, and a host recognizes its paired devices by theirs.
/// </summary>
public sealed record LinkFingerprint
{
    /// <summary>The length of the hash in bytes.</summary>
    public const int ByteLength = 32;

    private readonly byte[] _bytes;

    private LinkFingerprint(byte[] bytes)
    {
        _bytes = bytes;
        Value = Base64Url.Encode(bytes);
    }

    /// <summary>The hash in base64url without padding, as the QR code and the paired device list carry it.</summary>
    public string Value { get; }

    /// <summary>
    /// The first eight bytes as four groups of hexadecimal digits, such as <c>7F3A 91C2 0B4E D6A1</c>, for people
    /// who compare two screens.
    /// </summary>
    public string ShortText
    {
        get
        {
            var text = new StringBuilder(19);
            for (var i = 0; i < 8; i += 2)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                text.Append(_bytes[i].ToString("X2", CultureInfo.InvariantCulture)).Append(_bytes[i + 1].ToString("X2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }
    }

    /// <summary>The fingerprint of a public key.</summary>
    /// <param name="subjectPublicKeyInfo">The key in X.509 SubjectPublicKeyInfo form.</param>
    public static LinkFingerprint FromPublicKey(ReadOnlySpan<byte> subjectPublicKeyInfo) => new(SHA256.HashData(subjectPublicKeyInfo));

    /// <summary>The fingerprint of a certificate's public key.</summary>
    /// <param name="certificate">The certificate.</param>
    public static LinkFingerprint Of(X509Certificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (certificate is X509Certificate2 full)
        {
            return FromPublicKey(full.PublicKey.ExportSubjectPublicKeyInfo());
        }

        using var copy = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        return FromPublicKey(copy.PublicKey.ExportSubjectPublicKeyInfo());
    }

    /// <summary>Reads a fingerprint from its <see cref="Value"/>.</summary>
    /// <param name="text">The base64url text.</param>
    /// <param name="fingerprint">The fingerprint, when the text is one.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out LinkFingerprint? fingerprint)
    {
        fingerprint = Base64Url.TryDecode(text) is { Length: ByteLength } bytes ? new LinkFingerprint(bytes) : null;
        return fingerprint is not null;
    }

    /// <summary>Reads a fingerprint from its <see cref="Value"/>.</summary>
    /// <param name="text">The base64url text.</param>
    /// <exception cref="FormatException">The text is not a fingerprint.</exception>
    public static LinkFingerprint Parse(string text) =>
        TryParse(text, out var fingerprint) ? fingerprint : throw new FormatException("This is not a TabStudio Link fingerprint.");

    /// <summary>The hash's bytes.</summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();

    /// <summary>Whether two fingerprints are the same.</summary>
    /// <param name="other">The other fingerprint.</param>
    public bool Equals(LinkFingerprint? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
