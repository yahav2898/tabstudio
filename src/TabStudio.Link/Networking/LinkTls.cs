using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>
/// TLS for TabStudio Link: both sides present their self-signed certificate, and each side checks the other's
/// fingerprint itself instead of a certificate authority.
/// </summary>
/// <remarks>
/// TLS 1.3 is negotiated wherever both devices support it; TLS 1.2 (with forward-secret ECDHE suites only, which is
/// all .NET offers by default) remains allowed for Android 9 and Windows 10, whose platform TLS stops there.
/// </remarks>
internal static class LinkTls
{
    /// <summary>The name both sides use in the handshake. Certificates are pinned, so it identifies nothing.</summary>
    public const string TargetHost = "tabstudio-link";

    /// <summary>The TLS versions allowed.</summary>
    public const SslProtocols Protocols = SslProtocols.Tls12 | SslProtocols.Tls13;

    /// <summary>Runs the host's side of the TLS handshake.</summary>
    /// <param name="stream">The accepted connection.</param>
    /// <param name="identity">The host's identity.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The TLS stream and the client certificate's fingerprint.</returns>
    public static async Task<(SslStream Stream, LinkFingerprint Client)> AuthenticateAsHostAsync(Stream stream, LinkIdentity identity, CancellationToken cancellationToken)
    {
        LinkFingerprint? client = null;
        var tls = new SslStream(stream, leaveInnerStreamOpen: false);
        try
        {
            await tls.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = identity.Certificate,
                    ClientCertificateRequired = true,
                    EnabledSslProtocols = Protocols,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    {
                        // Any device may try; whether it is paired or knows the code is decided after the handshake.
                        client = certificate is null ? null : LinkFingerprint.Of(certificate);
                        return client is not null;
                    },
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tls.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        if (client is null)
        {
            await tls.DisposeAsync().ConfigureAwait(false);
            throw new AuthenticationException("The device did not present a certificate.");
        }

        return (tls, client);
    }

    /// <summary>Runs the client's side of the TLS handshake.</summary>
    /// <param name="stream">The connection.</param>
    /// <param name="identity">The client's identity.</param>
    /// <param name="pinned">The host's expected fingerprint; <see langword="null"/> when pairing by typed code.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The TLS stream and the host certificate's fingerprint.</returns>
    /// <exception cref="AuthenticationException">The host's certificate is not the pinned one, or TLS failed.</exception>
    public static async Task<(SslStream Stream, LinkFingerprint Host)> AuthenticateAsClientAsync(Stream stream, LinkIdentity identity, LinkFingerprint? pinned, CancellationToken cancellationToken)
    {
        LinkFingerprint? host = null;
        var tls = new SslStream(stream, leaveInnerStreamOpen: false);
        try
        {
            await tls.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = TargetHost,
                    ClientCertificates = new X509CertificateCollection { identity.Certificate },
                    EnabledSslProtocols = Protocols,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    {
                        host = certificate is null ? null : LinkFingerprint.Of(certificate);
                        return host is not null && (pinned is null || host == pinned);
                    },
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tls.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return (tls, host!);
    }

    /// <summary>The negotiated TLS version, for display, such as <c>TLS 1.3</c>.</summary>
    /// <param name="stream">An authenticated TLS stream.</param>
    public static string Describe(SslStream stream) => stream.SslProtocol switch
    {
        SslProtocols.Tls13 => "TLS 1.3",
        SslProtocols.Tls12 => "TLS 1.2",
        var other => other.ToString(),
    };
}
