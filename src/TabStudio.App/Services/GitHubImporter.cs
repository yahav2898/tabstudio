using TabStudio.SourceControl;
using TabStudio.SourceControl.Git;
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
/// Downloads a repository — from GitHub, or from any Git host over Git's own protocol — into the projects folder and
/// links the folder to it, so the Git Changes window can commit, pull and switch branches there. No Git installation is
/// needed, which is what lets this work on any tablet. Private GitHub repositories work when the user is signed in to
/// GitHub; private repositories elsewhere take a sign-in written into their address. A download that breaks goes on
/// where it stopped the next time the same repository is downloaded.
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

        LastNotes = [];
        var source = "github:" + repository;
        var target = ResumableFolder(source) ?? _paths.GetAvailableProjectDirectory(repository.Name);
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
                    GitHubSync.MarkIncomplete(target, source);
                    var sync = await GitHubSync.CloneAsync(_account.CreateClient(), repository.Owner, repository.Name, reference, target, syncProgress, cancellationToken);
                    LastNotes = sync.CloneNotes;
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
        catch (GitHubApiException ex) when (ex.IsNotFound || ex.IsUnauthorized || ex.IsRejected)
        {
            TryDelete(target);
            throw new ImportException(Describe(repository, ex), ex);
        }
        catch (Exception ex) when (ex is GitHubApiException or SyncException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // What arrived stays in the folder: downloading the repository again goes on from there.
            throw new ImportException(Describe(repository, ex) + Resumable(target), ex);
        }
    }

    /// <summary>
    /// Downloads a repository from any Git host — GitLab, Bitbucket, Azure DevOps, Codeberg, a server of one's own, or
    /// GitHub — as a Git clone over Git's own protocol, into a new folder linked to it.
    /// </summary>
    /// <param name="address">The repository's address.</param>
    /// <param name="reference">A branch, tag or commit, or <see langword="null"/> for the default branch.</param>
    /// <param name="credentials">The sign-in written into the address, if any.</param>
    /// <param name="progress">Receives download progress.</param>
    /// <param name="cancellationToken">Cancels the download; the incomplete folder is removed.</param>
    /// <returns>The folder the repository was downloaded into.</returns>
    /// <exception cref="ImportException">The repository could not be downloaded.</exception>
    public async Task<string> ImportAsync(GitRemoteAddress address, string? reference, GitCredentials? credentials, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        LastNotes = [];
        var source = reference is null ? address.ToString() : $"{address}@{reference}";
        var target = ResumableFolder(source) ?? _paths.GetAvailableProjectDirectory(address.Name);
        try
        {
            GitHubSync.MarkIncomplete(target, source);
            var sync = await GitHubSync.CloneAsync(_account.CreateClient(), address, credentials, reference, target, progress is null ? null : new ProgressAdapter(progress), cancellationToken);
            LastNotes = sync.CloneNotes;
            return target;
        }
        catch (OperationCanceledException)
        {
            TryDelete(target);
            throw;
        }
        catch (GitTransportException ex) when (ex.IsNotFound || ex.IsUnauthorized)
        {
            TryDelete(target);
            throw new ImportException(ex.IsUnauthorized && credentials is null
                ? $"{address.Host} wants a sign-in for {address.FullName}. Write it into the address, as https://user:token@{address.Host}/{address.FullName}.git."
                : SyncErrors.Describe(ex), ex);
        }
        catch (SyncException ex) when (Directory.Exists(target) && !Directory.Exists(Path.Combine(target, ".git")))
        {
            TryDelete(target);
            throw new ImportException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is GitTransportException or SyncException or MissingObjectException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new ImportException(SyncErrors.Describe(ex) + Resumable(target), ex);
        }
    }

    /// <summary>
    /// The folder of an earlier download of the same source that did not finish, to go on in; the unfinished downloads
    /// of other repositories are deleted, as before 0.21.9 every unfinished download was.
    /// </summary>
    private string? ResumableFolder(string source)
    {
        string? found = null;
        try
        {
            if (!Directory.Exists(_paths.Projects))
            {
                return null;
            }

            foreach (var folder in Directory.EnumerateDirectories(_paths.Projects).Where(GitHubSync.IsIncompleteClone).ToList())
            {
                if (found is null && GitHubSync.IncompleteSource(folder) == source)
                {
                    found = folder;
                }
                else
                {
                    TryDelete(folder);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The download itself goes on; a leftover is only storage.
        }

        return found;
    }

    private static string Resumable(string target) =>
        GitHubSync.IsIncompleteClone(target) ? " What arrived is kept: download the repository again to go on from there." : string.Empty;

    /// <summary>
    /// What the person should know about the last download, such as a submodule on another host or a Git LFS file that
    /// could not be downloaded; empty when everything arrived.
    /// </summary>
    public IReadOnlyList<string> LastNotes { get; private set; } = [];

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
