using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TabStudio.Link.Security;

/// <summary>
/// A device's TabStudio Link identity: a self-signed ECDSA P-256 certificate with its private key. It is made once
/// per device and kept, so the fingerprint other devices paired with stays the same.
/// </summary>
/// <remarks>
/// There is no certificate authority: devices trust each other's fingerprints, learned when they pair. The private
/// key never leaves the device; it is stored in TabStudio's private storage.
/// </remarks>
public sealed class LinkIdentity : IDisposable
{
    private LinkIdentity(X509Certificate2 certificate)
    {
        Certificate = certificate;
        Fingerprint = LinkFingerprint.Of(certificate);
    }

    /// <summary>The certificate, with its private key, that TLS presents.</summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>The fingerprint of the certificate's public key.</summary>
    public LinkFingerprint Fingerprint { get; }

    /// <summary>Makes a new identity with a fresh key.</summary>
    public static LinkIdentity Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=TabStudio Link", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication"),
                new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication"),
            },
            critical: false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(30));

        // A certificate loaded from PKCS #12 has a key every platform's TLS can use, Windows' included. The key stays
        // exportable, because Windows would otherwise refuse to export it again when the identity is saved.
        return new LinkIdentity(X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), password: null, X509KeyStorageFlags.Exportable));
    }

    /// <summary>Loads the identity saved in a file, or makes and saves a new one when there is none or it is unreadable.</summary>
    /// <param name="path">The file, in the app's private storage.</param>
    public static LinkIdentity LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (File.Exists(path))
        {
            try
            {
                var certificate = X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(path), password: null, X509KeyStorageFlags.Exportable);
                if (certificate.HasPrivateKey)
                {
                    return new LinkIdentity(certificate);
                }

                certificate.Dispose();
            }
            catch (CryptographicException)
            {
                // A damaged file: devices paired with the old identity pair again.
            }
        }

        var identity = Create();
        identity.Save(path);
        return identity;
    }

    /// <summary>Saves the identity, with its private key, to a file.</summary>
    /// <param name="path">The file, in the app's private storage.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, Certificate.Export(X509ContentType.Pkcs12));
        File.Move(temporary, path, overwrite: true);
    }

    /// <inheritdoc />
    public void Dispose() => Certificate.Dispose();
}
