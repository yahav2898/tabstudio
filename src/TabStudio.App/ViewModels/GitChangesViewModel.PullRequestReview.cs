using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels;

/// <summary>
/// What pull request review needs from the Git Changes window: the linked repository and branch, opening the
/// branch's pull request in the IDE, and checking out a pull request's branch the way switching branches works.
/// </summary>
public sealed partial class GitChangesViewModel
{
    /// <summary>Raised with the number of the branch's pull request when the user asks to see it in the IDE.</summary>
    public event EventHandler<int>? PullRequestViewRequested;

    /// <summary>The repository and branch of the linked folder, or <see langword="null"/> when it is not linked.</summary>
    public GitHubLink? RepositoryLink => _sync?.Link;

    /// <summary>Finds the open pull request of the folder's branch, as after a push, so the window and the editors can show it.</summary>
    public Task FindBranchPullRequestAsync() => FindPullRequestAsync();

    /// <summary>
    /// Switches the folder to a pull request's branch, saving open documents first and bringing them and the
    /// workspace up to date afterwards. Refuses, with the reason, when there are uncommitted changes.
    /// </summary>
    /// <param name="pullRequest">The pull request.</param>
    /// <returns><see langword="null"/> when it worked; otherwise what went wrong, in plain words.</returns>
    public async Task<string?> CheckOutPullRequestAsync(PullRequestInfo pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        if (!await SaveBeforeChangingFilesAsync())
        {
            return ErrorText;
        }

        var number = pullRequest.Number.ToString(CultureInfo.InvariantCulture);
        var succeeded = await RunAsync(async (sync, progress, token) =>
        {
            var before = sync.Metadata.Files.Keys.ToList();
            var documents = await _host.CaptureDocumentsAsync();
            var result = await sync.CheckOutPullRequestAsync(pullRequest, progress, token);
            PullRequest = null;
            CanCreatePullRequest = false;
            IncomingCommits = 0;
            var conflicts = await _host.ReloadDocumentsAsync(FullPaths(before.Concat(result.Updated)), documents);
            await _host.ReloadWorkspaceAsync();
            return $"Checked out pull request #{number} on {pullRequest.Head.Name}." + TypedEditsNote(conflicts);
        });

        if (!succeeded)
        {
            return ErrorText ?? $"Pull request #{number} could not be checked out.";
        }

        await FindPullRequestAsync();
        return null;
    }

    /// <summary>Opens a page in the browser, such as a pull request on GitHub.</summary>
    /// <param name="uri">The page.</param>
    internal Task OpenInBrowserAsync(Uri uri) => _browser.OpenUriAsync(uri);

    /// <summary>Opens the branch's pull request in the IDE, from the link under the commit button.</summary>
    [RelayCommand]
    private void ViewPullRequest()
    {
        if (PullRequest is { } pull)
        {
            PullRequestViewRequested?.Invoke(this, pull.Number);
        }
    }
}
