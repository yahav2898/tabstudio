using System.IO.Compression;
using System.Net;

namespace TabStudio.App.Services;

/// <summary>Progress of a download, for the start screen.</summary>
/// <param name="BytesReceived">Bytes downloaded so far.</param>
/// <param name="TotalBytes">The total size, when the server reports it.</param>
/// <param name="Stage">What is happening now.</param>
public sealed record ImportProgress(long BytesReceived, long? TotalBytes, string Stage);

/// <summary>Raised when a repository cannot be downloaded, with a message for the user.</summary>
public sealed class ImportException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ImportException()
    {
    }

    /// <summary>Creates the exception with a message for the user.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    public ImportException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    /// <param name="innerException">The cause.</param>
    public ImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Downloads a public GitHub repository as a zip archive and unpacks it into the projects folder.
/// No Git installation is needed, which is what lets this work on any tablet.
/// </summary>
public sealed class GitHubImporter
{
    /// <summary>Archives larger than this are refused, to protect the tablet's storage.</summary>
    public const long MaximumArchiveBytes = 1024L * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly AppPaths _paths;

    /// <summary>Creates the importer.</summary>
    /// <param name="http">The HTTP client to download with.</param>
    /// <param name="paths">Where projects are created.</param>
    public GitHubImporter(HttpClient http, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(paths);
        _http = http;
        _paths = paths;
    }

    /// <summary>Downloads a repository and unpacks it into a new folder.</summary>
    /// <param name="repository">The repository to download.</param>
    /// <param name="progress">Receives download progress.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The folder the repository was unpacked into.</returns>
    /// <exception cref="ImportException">The repository could not be downloaded or unpacked.</exception>
    public async Task<string> ImportAsync(GitHubRepository repository, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var archive = Path.Combine(_paths.Cache, $"download-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadAsync(repository, archive, progress, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ImportProgress(new FileInfo(archive).Length, null, "Unpacking"));
            var target = _paths.GetAvailableProjectDirectory(repository.Name);
            await Task.Run(() => ArchiveExtractor.ExtractStrippingRoot(archive, target), cancellationToken).ConfigureAwait(false);
            return target;
        }
        catch (InvalidDataException ex)
        {
            throw new ImportException("The download is not a valid zip archive.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ImportException($"Could not reach GitHub: {ex.Message}", ex);
        }
        finally
        {
            TryDelete(archive);
        }
    }

    private async Task DownloadAsync(GitHubRepository repository, string archive, IProgress<ImportProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(repository.ArchiveUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ImportException(repository.Reference is null
                ? $"GitHub has no public repository '{repository.Owner}/{repository.Name}'."
                : $"'{repository.Owner}/{repository.Name}' has no branch, tag or commit '{repository.Reference}'.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ImportException($"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var total = response.Content.Headers.ContentLength;
        if (total > MaximumArchiveBytes)
        {
            throw new ImportException("The repository is larger than 1 GB, which is more than TabStudio downloads.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(archive);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            if (received > MaximumArchiveBytes)
            {
                throw new ImportException("The repository is larger than 1 GB, which is more than TabStudio downloads.");
            }

            progress?.Report(new ImportProgress(received, total, "Downloading"));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover file in the cache folder is harmless.
        }
    }
}

/// <summary>Unpacks zip archives safely.</summary>
internal static class ArchiveExtractor
{
    /// <summary>
    /// Extracts an archive whose entries all live under one top-level folder (as GitHub archives do), dropping
    /// that folder. Entries that would land outside the target are rejected.
    /// </summary>
    /// <param name="archivePath">The zip file.</param>
    /// <param name="targetDirectory">The folder to extract into. Created if missing.</param>
    public static void ExtractStrippingRoot(string archivePath, string targetDirectory)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        var root = CommonRoot(zip);
        var fullTarget = Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(fullTarget);

        foreach (var entry in zip.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (root is not null)
            {
                relative = relative.Length > root.Length ? relative[root.Length..] : string.Empty;
            }

            if (relative.Length == 0)
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(fullTarget, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(fullTarget, StringComparison.Ordinal))
            {
                throw new ImportException($"The archive contains an unsafe path: '{entry.FullName}'.");
            }

            if (relative.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    /// <summary>The single top-level folder all entries share, including its trailing slash, if there is one.</summary>
    private static string? CommonRoot(ZipArchive zip)
    {
        string? root = null;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            var slash = name.IndexOf('/', StringComparison.Ordinal);
            if (slash <= 0)
            {
                return null;
            }

            var first = name[..(slash + 1)];
            if (root is null)
            {
                root = first;
            }
            else if (!string.Equals(root, first, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return root;
    }
}
