using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace TabStudio.Toolchains.Downloads;

/// <summary>A file to download.</summary>
public sealed record DownloadRequest
{
    /// <summary>Where to download it from.</summary>
    public required Uri Address { get; init; }

    /// <summary>Where to put it once it is complete and verified.</summary>
    public required string Destination { get; init; }

    /// <summary>What it is, for messages, such as <c>python3 3.14.8</c>.</summary>
    public string? Name { get; init; }

    /// <summary>Its exact size in bytes, when known; a file of another size fails verification.</summary>
    public long? Size { get; init; }

    /// <summary>Its SHA-256 in hexadecimal, when known; a file with another hash fails verification.</summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// A further check of the complete file, such as a package's own checksums; it returns why the file is not
    /// valid, or <see langword="null"/> when it is.
    /// </summary>
    public Func<string, string?>? Verify { get; init; }

    /// <summary>The name for messages.</summary>
    internal string DisplayName => Name ?? Path.GetFileName(Destination);
}

/// <summary>How much of a download has arrived.</summary>
/// <param name="Received">The bytes on the device so far, including any from an earlier, interrupted attempt.</param>
/// <param name="Total">The file's size, once known.</param>
public readonly record struct DownloadProgress(long Received, long? Total);

/// <summary>
/// Downloads files so that a weak mobile connection does not waste what already arrived: an interrupted download
/// continues where it stopped, now or the next time it is started, and only a complete file that passes every
/// check is moved into place.
/// </summary>
/// <remarks>
/// <para>
/// Bytes go to <c>&lt;destination&gt;.part</c>. When that file exists, the request asks for the rest with a
/// <c>Range</c> header, and an <c>If-Range</c> with the server's validator from the first attempt, so a file that
/// changed on the server starts over instead of being spliced. A stalled connection (no data for
/// <see cref="StallTimeout"/>) and a broken one are retried, continuing from the last byte.
/// </para>
/// <para>
/// Every failure is a <see cref="DownloadException"/> that says what happened in words: offline, not found, a
/// server error, a file that does not match its hash, or a full storage.
/// </para>
/// </remarks>
public sealed class Downloader
{
    private const int BufferSize = 81920;
    private readonly HttpClient _http;

    /// <summary>Creates a downloader.</summary>
    /// <param name="http">The client to send requests with. Its own timeout should be infinite; see <see cref="StallTimeout"/>.</param>
    public Downloader(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>How many times a broken connection is retried before the download fails.</summary>
    public int Retries { get; init; } = 4;

    /// <summary>How long to wait before the first retry; each further retry waits twice as long.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How long a connection may go without sending data before it counts as broken.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Downloads a file, continuing an earlier attempt, and verifies it.</summary>
    /// <param name="request">What to download.</param>
    /// <param name="progress">Receives the bytes received so far.</param>
    /// <param name="cancellationToken">Cancels the download; what arrived is kept for the next attempt.</param>
    /// <exception cref="DownloadException">The download failed.</exception>
    public async Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Destination))!);

        if (File.Exists(request.Destination))
        {
            if (await CheckAsync(request, request.Destination, cancellationToken).ConfigureAwait(false) is null)
            {
                var length = new FileInfo(request.Destination).Length;
                progress?.Report(new DownloadProgress(length, length));
                return;
            }

            File.Delete(request.Destination);
        }

        var partial = request.Destination + ".part";
        var validatorFile = partial + ".validator";
        var delay = RetryDelay;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await TransferAsync(request, partial, validatorFile, progress, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken) && attempt < Retries)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay *= 2;
            }
            catch (Exception ex) when (ex is not DownloadException && !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                // Out of retries, or a failure retrying cannot fix. A cancellation by the caller propagates as it is.
                throw Describe(request, ex);
            }
        }

        if (await CheckAsync(request, partial, cancellationToken).ConfigureAwait(false) is { } problem)
        {
            File.Delete(partial);
            File.Delete(validatorFile);
            throw new DownloadException(DownloadFailure.IntegrityFailed, $"{request.DisplayName} did not download correctly: {problem} It was deleted; try again.");
        }

        File.Move(partial, request.Destination, overwrite: true);
        File.Delete(validatorFile);
    }

    /// <summary>Whether a failure is worth retrying, continuing from the bytes that arrived.</summary>
    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: { } status } => (int)status >= 500,
        IOException io => !IsDiskFull(io),
        _ => false,
    };

    private static bool IsDiskFull(IOException exception) =>
        // ENOSPC on Linux and Android; ERROR_DISK_FULL and ERROR_HANDLE_DISK_FULL on Windows.
        OperatingSystem.IsWindows() ? (exception.HResult & 0xFFFF) is 112 or 39 : (exception.HResult & 0xFFFF) == 28;

    private static DownloadException Describe(DownloadRequest request, Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone } =>
            new(DownloadFailure.NotFound, $"{request.DisplayName} is no longer on the server ({request.Address.Host}).", exception),
        HttpRequestException { StatusCode: { } status } =>
            new(DownloadFailure.ServerError, $"The server answered {(int)status} {status} for {request.DisplayName}. Try again later.", exception),
        HttpRequestException http when http.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError =>
            new(DownloadFailure.NoNetwork, $"Could not reach {request.Address.Host}: this tablet seems to be offline. Connect to the internet and try again.", exception),
        HttpRequestException or OperationCanceledException =>
            new(DownloadFailure.Interrupted, $"The connection to {request.Address.Host} kept breaking while downloading {request.DisplayName}. What arrived is kept; try again to continue.", exception),
        IOException io when IsDiskFull(io) =>
            new(DownloadFailure.NoSpace, $"The tablet's storage is full; {request.DisplayName} could not be saved. Free up space and try again.", exception),
        _ => new(DownloadFailure.Interrupted, $"Downloading {request.DisplayName} failed: {exception.Message}", exception),
    };

    private async Task TransferAsync(DownloadRequest request, string partial, string validatorFile, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (request.Size is { } size && existing > size)
        {
            File.Delete(partial);
            existing = 0;
        }

        if (request.Size is { } complete && existing == complete && complete > 0)
        {
            progress?.Report(new DownloadProgress(existing, complete));
            return;
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, request.Address);
        var validator = existing > 0 && File.Exists(validatorFile) ? await File.ReadAllTextAsync(validatorFile, cancellationToken).ConfigureAwait(false) : null;
        if (existing > 0)
        {
            message.Headers.Range = new RangeHeaderValue(existing, null);
            if (validator is { Length: > 0 })
            {
                message.Headers.TryAddWithoutValidation("If-Range", validator);
            }
        }

        using var response = await SendWithinStallLimitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // What is on the device does not fit what the server has any more: start over.
            File.Delete(partial);
            throw new HttpRequestException("The server refused to continue the download.", null, null);
        }

        response.EnsureSuccessStatusCode();
        var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!append)
        {
            existing = 0;
        }

        var total = response.Content.Headers.ContentLength is { } length ? existing + length : request.Size;
        var newValidator = response.Headers.ETag?.ToString() ?? response.Content.Headers.LastModified?.ToString("R");
        if (newValidator is not null)
        {
            await File.WriteAllTextAsync(validatorFile, newValidator, cancellationToken).ConfigureAwait(false);
        }

        await using var file = new FileStream(partial, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffer = new byte[BufferSize];
        var received = existing;
        progress?.Report(new DownloadProgress(received, total));
        while (true)
        {
            stall.CancelAfter(StallTimeout);
            var read = await body.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            progress?.Report(new DownloadProgress(received, total));
        }

        if (total is { } expected && received < expected)
        {
            throw new IOException($"The connection closed after {received} of {expected} bytes.");
        }
    }

    /// <summary>Checks a file against the request's size, hash and verifier.</summary>
    /// <returns>Why it does not match, or <see langword="null"/>.</returns>
    private static async Task<string?> CheckAsync(DownloadRequest request, string path, CancellationToken cancellationToken)
    {
        var length = new FileInfo(path).Length;
        if (request.Size is { } size && length != size)
        {
            return $"it has {length} bytes instead of {size}.";
        }

        if (request.Sha256 is { } expected)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return "its SHA-256 hash is not the one TabStudio expects, so it may have been damaged or tampered with.";
            }
        }

        return request.Verify?.Invoke(path);
    }

    /// <summary>
    /// Sends a request, giving up when no headers come within <see cref="StallTimeout"/>: a server that took the
    /// connection and never answered (a half-open connection after a network switch) held an install until cancelled.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithinStallLimitAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using var headers = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        headers.CancelAfter(StallTimeout);
        return await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, headers.Token).ConfigureAwait(false);
    }
}
