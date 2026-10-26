using System.Collections.Concurrent;
using System.Text.Json;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>A single selected physical board and its verified-upload operation.</summary>
/// <param name="DeviceIdentity">Attachment-specific identity. It must be distinct within a batch.</param>
/// <param name="Upload">Opens and owns only this board's permissioned connection, runs a validated plan and verifies it.</param>
/// <param name="ProvidesVerification">False for transfer-only loaders; such jobs are refused before a batch starts.</param>
public sealed record ProductionFlashJob(string DeviceIdentity, Func<CancellationToken, Task<EmbeddedFlashResult>> Upload, bool ProvidesVerification = true);

/// <summary>A per-board production result, suitable for a JSON-lines audit log.</summary>
/// <param name="DeviceIdentity">The selected board.</param>
/// <param name="CompletedAt">Completion time in UTC.</param>
/// <param name="Verified">True only if the upload and its verification returned successfully.</param>
/// <param name="FirmwareSha256">Digest of the verified input, or null on failure.</param>
/// <param name="Error">Failure/cancellation reason, or null on success.</param>
public sealed record ProductionFlashResult(string DeviceIdentity, DateTimeOffset CompletedAt, bool Verified, string? FirmwareSha256, string? Error);

/// <summary>Programs distinct boards with bounded concurrency, per-board verification and a flushed persistent result log.</summary>
public static class ProductionFlasher
{
    /// <summary>Runs a finite batch. Cancellation stops running jobs and records every board that did not complete.</summary>
    public static async Task<IReadOnlyList<ProductionFlashResult>> RunAsync(IReadOnlyList<ProductionFlashJob> jobs,
        string logPath, int parallelism = 2, IProgress<ProductionFlashResult>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        if (parallelism is < 1 or > 8 || jobs.Count > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(parallelism), "Use 1..8 simultaneous boards and at most 1024 boards per batch.");
        }
        if (jobs.Any(x => string.IsNullOrWhiteSpace(x.DeviceIdentity) || x.Upload is null) || jobs.Select(x => x.DeviceIdentity).Distinct(StringComparer.Ordinal).Count() != jobs.Count)
        {
            throw new ArgumentException("Every job needs a distinct physical device identity and upload operation.", nameof(jobs));
        }
        if (jobs.Any(job => !job.ProvidesVerification))
        {
            throw new NotSupportedException("Production flashing requires target verification; transfer-only loaders are unavailable in batches.");
        }
        var path = Path.GetFullPath(logPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        using var slots = new SemaphoreSlim(parallelism);
        using var logLock = new SemaphoreSlim(1);
        var results = new ConcurrentDictionary<string, ProductionFlashResult>(StringComparer.Ordinal);
        var tasks = jobs.Select(async job =>
        {
            var acquired = false;
            ProductionFlashResult result;
            try
            {
                await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;
                cancellationToken.ThrowIfCancellationRequested();
                var upload = await job.Upload(cancellationToken).ConfigureAwait(false);
                result = new(job.DeviceIdentity, DateTimeOffset.UtcNow, upload.Verified, upload.FirmwareSha256,
                    upload.Verified ? null : "Transport completed, but target verification/readback is unavailable.");
            }
            catch (OperationCanceledException)
            {
                result = new(job.DeviceIdentity, DateTimeOffset.UtcNow, false, null, "Cancelled; verification not completed.");
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or ToolchainRuntimeException)
            {
                result = new(job.DeviceIdentity, DateTimeOffset.UtcNow, false, null, exception.Message);
            }
            finally
            {
                if (acquired)
                {
                    slots.Release();
                }
            }
            results[job.DeviceIdentity] = result;
            await logLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(result)).ConfigureAwait(false);
                await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                logLock.Release();
            }
            progress?.Report(result);
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return jobs.Select(x => results[x.DeviceIdentity]).ToArray();
    }
}
