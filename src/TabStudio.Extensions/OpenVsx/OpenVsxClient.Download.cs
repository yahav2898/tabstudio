using System.Net;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Extensions.Security;

namespace TabStudio.Extensions.OpenVsx;

/// <summary>Downloading packages, checked against the hash and the signature the registry published for them.</summary>
public sealed partial class OpenVsxClient
{
    /// <summary>
    /// Downloads an extension version's package to a file and verifies it: its SHA-256 hash against the registry's,
    /// and the registry's Ed25519 signature with its public key. Checks the registry does not offer for a version are
    /// skipped; a package that fails a check is deleted.
    /// </summary>
    /// <param name="extension">The version to download, from <see cref="GetExtensionAsync"/>.</param>
    /// <param name="destination">The file to write.</param>
    /// <param name="progress">Receives how much of the package arrived, from 0 to 1, when its size is known.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The downloaded package and what was verified.</returns>
    /// <exception cref="OpenVsxException">The download failed, or the package does not match what the registry published.</exception>
    public async Task<OpenVsxDownload> DownloadAsync(OpenVsxExtension extension, string destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        var address = extension.Files.Download
            ?? throw new OpenVsxException(OpenVsxFailure.NotFound, $"{extension.DisplayName} {extension.Version} cannot be downloaded from the registry.");

        try
        {
            var sha256 = await DownloadFileAsync(address, destination, progress, cancellationToken).ConfigureAwait(false);
            var checksum = await VerifyChecksumAsync(extension, sha256, cancellationToken).ConfigureAwait(false);
            var signature = await VerifySignatureAsync(extension, destination, cancellationToken).ConfigureAwait(false);
            return new OpenVsxDownload(destination, sha256, checksum, signature);
        }
        catch
        {
            TryDelete(destination);
            throw;
        }
    }

    /// <summary>Streams a file to disk, hashing it on the way, and returns its SHA-256 hash.</summary>
    private async Task<string> DownloadFileAsync(Uri address, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new OpenVsxException(OpenVsxFailure.NotFound, "The registry no longer has this package.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OpenVsxException(
                    (int)response.StatusCode >= 500 ? OpenVsxFailure.Offline : OpenVsxFailure.Rejected,
                    $"The registry could not send the package ({(int)response.StatusCode} {response.ReasonPhrase}).");
            }

            var total = response.Content.Headers.ContentLength;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(destination);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                if (total is > 0)
                {
                    progress?.Report(Math.Min(1, (double)received / total.Value));
                }
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch (HttpRequestException ex)
        {
            throw new OpenVsxException(OpenVsxFailure.Offline, $"The registry at {Registry.Host} cannot be reached. Check the connection.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenVsxException(OpenVsxFailure.Offline, $"The download from {Registry.Host} took too long.", ex);
        }
        catch (IOException ex) when (ex is not FileNotFoundException and not DirectoryNotFoundException)
        {
            throw new OpenVsxException(OpenVsxFailure.Offline, "The download was interrupted.", ex);
        }
    }

    /// <summary>Compares the hash with the registry's; <see langword="false"/> when the registry publishes none or it cannot be read.</summary>
    private async Task<bool> VerifyChecksumAsync(OpenVsxExtension extension, string sha256, CancellationToken cancellationToken)
    {
        if (extension.Files.Sha256 is not { } address || await TryGetFileAsync(address, cancellationToken).ConfigureAwait(false) is not { } published)
        {
            return false;
        }

        return PackageVerifier.MatchesPublishedSha256(sha256, Encoding.UTF8.GetString(published))
            ? true
            : throw new OpenVsxException(OpenVsxFailure.IntegrityFailed, $"The downloaded package of {extension.DisplayName} does not match the hash the registry published. It was not installed.");
    }

    /// <summary>Checks the registry's signature; <see langword="false"/> when it publishes none or it cannot be read.</summary>
    private async Task<bool> VerifySignatureAsync(OpenVsxExtension extension, string package, CancellationToken cancellationToken)
    {
        if (extension.Files.Signature is not { } signatureAddress || extension.Files.PublicKey is not { } keyAddress
            || await TryGetFileAsync(signatureAddress, cancellationToken).ConfigureAwait(false) is not { } archive
            || await TryGetFileAsync(keyAddress, cancellationToken).ConfigureAwait(false) is not { } pem)
        {
            return false;
        }

        bool valid;
        try
        {
            using var archiveStream = new MemoryStream(archive, writable: false);
            var signature = PackageVerifier.ReadSignature(archiveStream);
            var key = PackageVerifier.ReadPublicKey(Encoding.UTF8.GetString(pem));
            valid = await Task.Run(
                () =>
                {
                    using var stream = File.OpenRead(package);
                    return PackageVerifier.VerifySignature(stream, signature, key);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            throw new OpenVsxException(OpenVsxFailure.IntegrityFailed, $"The registry's signature of {extension.DisplayName} cannot be read. It was not installed.", ex);
        }

        return valid
            ? true
            : throw new OpenVsxException(OpenVsxFailure.IntegrityFailed, $"The signature of {extension.DisplayName} does not match the downloaded package. It was not installed.");
    }

    /// <summary>A file of a version, or <see langword="null"/> when the registry does not have it or cannot be reached.</summary>
    private async Task<byte[]?> TryGetFileAsync(Uri address, CancellationToken cancellationToken)
    {
        try
        {
            return await GetFileAsync(address, cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVsxException ex) when (ex.Failure is OpenVsxFailure.NotFound or OpenVsxFailure.Offline)
        {
            return null;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The caller's download folder is cleaned up later.
        }
    }
}
