using System.Security.Cryptography;
using System.Text.Json;

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

/// <summary>Verified, atomic and cancellable model storage in the app's private files.</summary>
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

    /// <summary>Downloads after the user has accepted the exact model license; incomplete files are removed.</summary>
    public async Task DownloadAsync(LocalModel model, bool licenseAccepted, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!licenseAccepted) throw new InvalidOperationException("Read and accept this model's license before downloading.");
        if (!Uri.TryCreate(model.Url, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "huggingface.co") throw new InvalidOperationException("Models must come from Hugging Face over HTTPS.");
        await _downloads.WaitAsync(cancellationToken).ConfigureAwait(false);
        var target = PathFor(model);
        var temporary = target + ".partial";
        try
        {
            if (IsInstalled(model)) return;
            Directory.CreateDirectory(directory);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            var buffer = new byte[128 * 1024];
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true))
            {
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    size += read;
                    if (size > model.Size) throw new InvalidDataException("The model exceeds its pinned download size.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report(size);
                }
            }

            if (size != model.Size || !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), model.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The model's size or SHA-256 does not match. The download was discarded.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
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
        try { File.Delete(PathFor(model)); }
        finally { _downloads.Release(); }
    }
}
