using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Toolchains.Downloads;

namespace TabStudio.Agent.Local;

/// <summary>A pinned model revision and its expected bytes. RAM is a conservative estimate for a 4096-token context.</summary>
public sealed record LocalModel(string Id, string Name, string Url, string Sha256, long Size, long RamBytes, string License, string LicenseUrl, bool SupportsFim)
{
    /// <summary>The exact license shown before any model download.</summary>
    public string LicenseText => ModelCatalog.ReadResource($"{Id}.LICENSE.txt");
}

/// <summary>The curated GGUF models and their upstream licenses.</summary>
public static class ModelCatalog
{
    /// <summary>Models offered to the user. Inference never checks for updates or contacts Hugging Face.</summary>
    public static IReadOnlyList<LocalModel> All { get; } = JsonSerializer.Deserialize<List<LocalModel>>(ReadResource("catalog.json"))!;

    internal static string ReadResource(string name)
    {
        using var stream = typeof(ModelCatalog).Assembly.GetManifestResourceStream("LocalModels/" + name) ?? throw new InvalidOperationException("Missing model resource: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>Verified, atomic, resumable and cancellable model storage in the app's private files.</summary>
public sealed class ModelManager(string directory, HttpClient http) : IDisposable
{
    private readonly SemaphoreSlim _downloads = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => _downloads.Dispose();

    /// <summary>The local path, never derived from an untrusted URL or user filename.</summary>
    public string PathFor(LocalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Id.Length == 0 || model.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Invalid model id.", nameof(model));
        return Path.Combine(directory, model.Id + ".gguf");
    }

    /// <summary>Whether a full, previously verified download is available.</summary>
    public bool IsInstalled(LocalModel model) => File.Exists(PathFor(model)) && new FileInfo(PathFor(model)).Length == model.Size;

    /// <summary>How long the connection may send nothing before it counts as broken and is retried.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How many times a broken or stalled connection is retried, continuing from the last byte.</summary>
    public int Retries { get; init; } = 4;

    /// <summary>How long to wait before the first retry; each further retry waits twice as long.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Where the bytes of an unfinished download are kept until it completes.</summary>
    public string PartialPathFor(LocalModel model) => PathFor(model) + ".part";

    /// <summary>
    /// Downloads after the user has accepted the exact model license. The toolchain downloader writes to
    /// <see cref="PartialPathFor"/> and continues it with a <c>Range</c> request after a broken connection, a stall of
    /// <see cref="StallTimeout"/>, a cancellation or an app restart; only a file of the pinned size and SHA-256 is installed.
    /// </summary>
    /// <exception cref="DownloadException">The download failed; what arrived is kept unless it failed verification.</exception>
    public async Task DownloadAsync(LocalModel model, bool licenseAccepted, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!licenseAccepted) throw new InvalidOperationException("Read and accept this model's license before downloading.");
        if (!Uri.TryCreate(model.Url, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "huggingface.co") throw new InvalidOperationException("Models must come from Hugging Face over HTTPS.");
        await _downloads.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsInstalled(model)) return;
            var downloader = new Downloader(http) { StallTimeout = StallTimeout, Retries = Retries, RetryDelay = RetryDelay };
            var request = new DownloadRequest { Address = url, Destination = PathFor(model), Name = model.Name, Size = model.Size, Sha256 = model.Sha256 };
            await downloader.DownloadAsync(request, progress is null ? null : new BytesProgress(progress), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _downloads.Release();
        }
    }

    /// <summary>Revalidates a stored model before loading, including after external file corruption.</summary>
    public async Task VerifyAsync(LocalModel model, CancellationToken cancellationToken = default)
    {
        if (!IsInstalled(model)) throw new FileNotFoundException("Download the selected model in Local AI settings first.");
        await using var file = File.OpenRead(PathFor(model));
        var hash = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Convert.ToHexString(hash), model.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Stored model verification failed. Delete it and download it again.");
    }

    /// <summary>Deletes a model after its inference engine has been unloaded by the caller.</summary>
    public async Task DeleteAsync(LocalModel model, CancellationToken cancellationToken = default)
    {
        await _downloads.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Delete(PathFor(model));
            File.Delete(PartialPathFor(model));
            File.Delete(PartialPathFor(model) + ".validator");
        }
        finally { _downloads.Release(); }
    }
}

/// <summary>Reports received bytes as they arrive, without posting to a synchronization context.</summary>
file sealed class BytesProgress(IProgress<long> inner) : IProgress<DownloadProgress>
{
    public void Report(DownloadProgress value) => inner.Report(value.Received);
}
