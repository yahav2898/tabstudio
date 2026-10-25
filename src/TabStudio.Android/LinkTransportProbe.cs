using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TabStudio.Android;

/// <summary>
/// Checks, on the device, which secure transports TabStudio Link can use: TLS through <see cref="SslStream"/>
/// with a pinned self-signed certificate, and the primitives of an authenticated key exchange.
/// </summary>
internal static class LinkTransportProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Runs every check and returns one line per check.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync()
    {
        return
        [
            await TryAsync("tls13-ecdsa-mutual", () => TlsAsync(SslProtocols.Tls13, useRsa: false, clientCertificate: true, pinMatches: true)),
            await TryAsync("tls13-ecdsa", () => TlsAsync(SslProtocols.Tls13, useRsa: false, clientCertificate: false, pinMatches: true)),
            await TryAsync("tls13-rsa", () => TlsAsync(SslProtocols.Tls13, useRsa: true, clientCertificate: false, pinMatches: true)),
            await TryAsync("tls12+13-ecdsa", () => TlsAsync(SslProtocols.Tls12 | SslProtocols.Tls13, useRsa: false, clientCertificate: false, pinMatches: true)),
            await TryAsync("tls13-wrong-pin", () => TlsAsync(SslProtocols.Tls13, useRsa: false, clientCertificate: false, pinMatches: false)),
            await TryAsync("crypto", () => Task.FromResult(Crypto())),
        ];
    }

    private static async Task<string> TryAsync(string name, Func<Task<string>> check)
    {
        try
        {
            return $"{name}: {await check().WaitAsync(Timeout)}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var inner = ex.InnerException is { } cause ? $" <- {cause.GetType().Name}: {cause.Message}" : string.Empty;
            return $"{name}: FAILED {ex.GetType().Name}: {ex.Message}{inner}".ReplaceLineEndings(" ");
        }
    }

    private static async Task<string> TlsAsync(SslProtocols protocols, bool useRsa, bool clientCertificate, bool pinMatches)
    {
        using var serverCertificate = CreateCertificate("TabStudio Link host", useRsa);
        using var clientCert = CreateCertificate("TabStudio Link client", useRsa: false);
        var pinned = serverCertificate.GetCertHash(HashAlgorithmName.SHA256);
        if (!pinMatches)
        {
            pinned[0] ^= 0xFF;
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverSaw = "not called";
            var server = Task.Run(async () =>
            {
                using var socket = await listener.AcceptTcpClientAsync();
                using var tls = new SslStream(socket.GetStream(), leaveInnerStreamOpen: false);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = serverCertificate,
                    EnabledSslProtocols = protocols,
                    ClientCertificateRequired = clientCertificate,
                    RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    {
                        serverSaw = certificate is null ? "no client certificate" : "client certificate";
                        return !clientCertificate || certificate is not null;
                    },
                });
                var buffer = new byte[5];
                await tls.ReadExactlyAsync(buffer);
                await tls.WriteAsync(buffer);
                await tls.FlushAsync();
                return $"{tls.SslProtocol}/{tls.NegotiatedCipherSuite}";
            });

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var clientTls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            var clientCalled = false;
            await clientTls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "tabstudio-link",
                EnabledSslProtocols = protocols,
                ClientCertificates = clientCertificate ? new X509CertificateCollection { clientCert } : null,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    clientCalled = true;
                    return certificate is not null && certificate.GetCertHash(HashAlgorithmName.SHA256).AsSpan().SequenceEqual(pinned);
                },
            });
            await clientTls.WriteAsync("hello"u8.ToArray());
            await clientTls.FlushAsync();
            var echo = new byte[5];
            await clientTls.ReadExactlyAsync(echo);
            var serverResult = await server.WaitAsync(Timeout);
            return $"ok client={clientTls.SslProtocol}/{clientTls.NegotiatedCipherSuite} clientCallback={clientCalled} " +
                $"server={serverResult} serverCallback={serverSaw} echo={Encoding.ASCII.GetString(echo)}";
        }
        finally
        {
            listener.Stop();
        }
    }

    private static X509Certificate2 CreateCertificate(string name, bool useRsa)
    {
        var validFrom = DateTimeOffset.UtcNow.AddDays(-1);
        var validTo = DateTimeOffset.UtcNow.AddYears(10);
        if (useRsa)
        {
            using var rsa = RSA.Create(2048);
            using var rsaCertificate = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(validFrom, validTo);
            return X509CertificateLoader.LoadPkcs12(rsaCertificate.Export(X509ContentType.Pkcs12), null);
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256).CreateSelfSigned(validFrom, validTo);
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
    }

    private static string Crypto()
    {
        var results = new List<string> { $"AesGcm.IsSupported={AesGcm.IsSupported}" };
        using var alice = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var bob = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var bobPublic = ECDiffieHellman.Create();
        bobPublic.ImportSubjectPublicKeyInfo(bob.PublicKey.ExportSubjectPublicKeyInfo(), out _);
        try
        {
            var raw1 = alice.DeriveRawSecretAgreement(bobPublic.PublicKey);
            var raw2 = bob.DeriveRawSecretAgreement(alice.PublicKey);
            results.Add($"rawAgreement={raw1.AsSpan().SequenceEqual(raw2)}");
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or CryptographicException or NotSupportedException)
        {
            results.Add($"rawAgreement=FAILED {ex.GetType().Name}");
        }

        var hashed1 = alice.DeriveKeyFromHash(bobPublic.PublicKey, HashAlgorithmName.SHA256);
        var hashed2 = bob.DeriveKeyFromHash(alice.PublicKey, HashAlgorithmName.SHA256);
        results.Add($"hashAgreement={hashed1.AsSpan().SequenceEqual(hashed2)}");

        byte[] salt = [1, 2, 3];
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, hashed1, 32, salt, "probe"u8.ToArray());
        using var gcm = new AesGcm(key, 16);
        var nonce = new byte[12];
        var plain = "hello"u8.ToArray();
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        byte[] associated = [7];
        gcm.Encrypt(nonce, plain, cipher, tag, associated);
        var back = new byte[plain.Length];
        gcm.Decrypt(nonce, cipher, tag, back, associated);
        results.Add($"aesgcm={back.AsSpan().SequenceEqual(plain)}");

        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = signer.SignData(plain, HashAlgorithmName.SHA256);
        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(signer.ExportSubjectPublicKeyInfo(), out _);
        results.Add($"ecdsa={verifier.VerifyData(plain, signature, HashAlgorithmName.SHA256)}");
        var pkcs8 = signer.ExportPkcs8PrivateKey();
        using var reloaded = ECDsa.Create();
        reloaded.ImportPkcs8PrivateKey(pkcs8, out _);
        results.Add($"pkcs8={verifier.VerifyData(plain, reloaded.SignData(plain, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256)}");
        return "ok " + string.Join(' ', results);
    }
}
