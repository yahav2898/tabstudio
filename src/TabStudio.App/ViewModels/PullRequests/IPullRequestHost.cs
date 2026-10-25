using TabStudio.App.Services.Agent;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>What pull request review needs from the IDE around it. Implemented by the workspace.</summary>
public interface IPullRequestHost
{
    /// <summary>The GitHub account: who is signed in, and the dialog that signs in.</summary>
    GitHubAccountViewModel Account { get; }

    /// <summary>The repository and branch of the workspace's linked folder, or <see langword="null"/> when it is not linked.</summary>
    GitHubLink? Link { get; }

    /// <summary>The in-app dialog for confirmations.</summary>
    PromptViewModel Prompt { get; }

    /// <summary>The HTTP client Claude is reached with.</summary>
    HttpClient AgentHttp { get; }

    /// <summary>A client of GitHub's API that works as the signed-in account.</summary>
    GitHubClient CreateClient();

    /// <summary>Where a file of the repository is in the linked folder, which names its diff's tab and colors it.</summary>
    /// <param name="relativePath">The file's path in the repository, with <c>/</c> separators.</param>
    string ReviewFilePath(string relativePath);

    /// <summary>Opens a page in the browser, such as the pull request on GitHub.</summary>
    /// <param name="uri">The page.</param>
    Task OpenUriAsync(Uri uri);

    /// <summary>Shows a pull request's document, opening a tab for it if it has none.</summary>
    /// <param name="pullRequest">The pull request.</param>
    void ShowPullRequest(PullRequestViewModel pullRequest);

    /// <summary>Shows a file's diff with its review, replacing an earlier tab of the same file of the same pull request.</summary>
    /// <param name="comparison">The two versions of the file.</param>
    /// <param name="review">The review of the file.</param>
    void ShowFileReview(FileComparison comparison, FileReviewViewModel review);

    /// <summary>Switches the linked folder to a pull request's branch.</summary>
    /// <param name="pullRequest">The pull request.</param>
    /// <returns><see langword="null"/> when it worked; otherwise why not, in plain words.</returns>
    Task<string?> CheckOutAsync(PullRequestInfo pullRequest);

    /// <summary>Reads the agent's settings, for Claude's API key and model.</summary>
    AgentSettings LoadAgentSettings();

    /// <summary>Shows the agent's settings, where the user adds an API key.</summary>
    void ShowAgentSettings();

    /// <summary>Shows a short message in the status bar.</summary>
    /// <param name="message">The message.</param>
    void ReportStatus(string message);
}
