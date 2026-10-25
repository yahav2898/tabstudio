using System.Globalization;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

public sealed partial class GitHubSync
{
    /// <summary>
    /// Switches the folder to a pull request's branch, so its code can be built, run and tested here; when the
    /// folder is on that branch already, brings it up to date.
    /// </summary>
    /// <param name="pullRequest">The pull request, which must come from a branch of the folder's repository.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the switch.</param>
    /// <exception cref="SyncException">
    /// The pull request comes from a fork, its branch was deleted, or the folder has uncommitted changes or conflicts.
    /// </exception>
    public async Task<PullResult> CheckOutPullRequestAsync(PullRequestInfo pullRequest, IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        var number = pullRequest.Number.ToString(CultureInfo.InvariantCulture);
        if (pullRequest.IsFromFork(Link.Owner, Link.Repository))
        {
            var source = pullRequest.Head.RepositoryFullName is { } fork ? $"the fork {fork}" : "a fork that was deleted";
            throw new SyncException($"Pull request #{number} comes from {source}. Only pull requests from branches of {Link.FullName} can be checked out into this folder.");
        }

        var branch = pullRequest.Head.Name;
        if (Metadata.Conflicts.Count > 0 || ScanChanges(cancellationToken).Count > 0)
        {
            throw new SyncException(
                SyncFailure.UncommittedChanges,
                $"This folder has changes that are not committed. Commit or undo them in Git Changes, then check out pull request #{number}, so nothing you wrote is lost.");
        }

        try
        {
            return string.Equals(Link.Branch, branch, StringComparison.Ordinal)
                ? await PullAsync(progress, cancellationToken).ConfigureAwait(false)
                : await SwitchBranchAsync(branch, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubApiException ex) when (ex.IsNotFound)
        {
            throw new SyncException($"The branch {branch} is no longer on GitHub, so pull request #{number} cannot be checked out.", ex);
        }
    }
}
