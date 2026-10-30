using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services;

/// <summary>Progress of a download, for the start screen.</summary>
/// <param name="BytesReceived">Bytes downloaded so far, or steps done for a download counted in files.</param>
/// <param name="TotalBytes">The total, when it is known.</param>
/// <param name="Stage">What is happening now, such as "Downloading 12 files".</param>
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
/// Downloads a GitHub repository into the projects folder and links the folder to it, so the Git Changes
/// window can commit, pull and switch branches there. No Git installation is needed, which is what lets this
/// work on any tablet. Private repositories work when the user is signed in to GitHub.
/// </summary>
public sealed class GitHubImporter
{
    private readonly GitHubAccount _account;
    private readonly AppPaths _paths;

    /// <summary>Creates the importer.</summary>
    /// <param name="account">The GitHub account to download as.</param>
    /// <param name="paths">Where projects are created.</param>
    public GitHubImporter(GitHubAccount account, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(paths);
        _account = account;
        _paths = paths;
    }

    /// <summary>Downloads a repository into a new folder linked to it.</summary>
    /// <param name="repository">The repository, and optionally a branch, tag or commit.</param>
    /// <param name="progress">Receives download progress.</param>
    /// <param name="cancellationToken">Cancels the download; the incomplete folder is removed.</param>
    /// <returns>The folder the repository was downloaded into.</returns>
    /// <exception cref="ImportException">The repository could not be downloaded.</exception>
    public async Task<string> ImportAsync(GitHubRepository repository, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var target = _paths.GetAvailableProjectDirectory(repository.Name);
        var syncProgress = progress is null ? null : new ProgressAdapter(progress);
        try
        {
            // A /tree/ address goes on with a folder after its branch (/tree/main/src/libraries), and a branch may
            // have slashes (release/9.0): the longest start of it that names a branch, tag or commit is used.
            var reference = repository.Reference;
            while (true)
            {
                try
                {
                    await GitHubSync.CloneAsync(_account.CreateClient(), repository.Owner, repository.Name, reference, target, syncProgress, cancellationToken);
                    return target;
                }
                catch (GitHubApiException ex) when ((ex.IsNotFound || ex.IsRejected) && reference?.LastIndexOf('/') is > 0 and var cut)
                {
                    TryDelete(target);
                    reference = reference[..cut];
                }
            }
        }
        catch (OperationCanceledException)
        {
            TryDelete(target);
            throw;
        }
        catch (Exception ex) when (ex is GitHubApiException or SyncException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TryDelete(target);
            throw new ImportException(Describe(repository, ex), ex);
        }
    }

    private string Describe(GitHubRepository repository, Exception exception) => exception switch
    {
        GitHubApiException { IsNotFound: true } when _account.IsSignedIn => repository.Reference is null
            ? $"GitHub has no repository '{repository.Owner}/{repository.Name}' that your account can see."
            : $"'{repository.Owner}/{repository.Name}' has no branch, tag or commit '{repository.Reference}' that your account can see.",
        GitHubApiException { IsNotFound: true } => repository.Reference is null
            ? $"GitHub has no public repository '{repository.Owner}/{repository.Name}'. Sign in to GitHub to open a private one."
            : $"'{repository.Owner}/{repository.Name}' has no branch, tag or commit '{repository.Reference}', or the repository is private. Sign in to GitHub to open a private one.",
        GitHubApiException { IsRejected: true } when repository.Reference is not null =>
            $"'{repository.Owner}/{repository.Name}' has no branch, tag or commit '{repository.Reference}'.",
        _ => SyncErrors.Describe(exception),
    };

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover folder is harmless: the next import picks another name.
        }
    }

    /// <summary>Passes the clone's progress on as the start screen's, as it happens.</summary>
    private sealed class ProgressAdapter(IProgress<ImportProgress> progress) : IProgress<SyncProgress>
    {
        public void Report(SyncProgress value) =>
            progress.Report(new ImportProgress(value.Completed, value.Total > 0 ? value.Total : (long?)null, value.Message));
    }
}
