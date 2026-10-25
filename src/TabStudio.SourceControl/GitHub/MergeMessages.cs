using System.Globalization;

namespace TabStudio.SourceControl.GitHub;

/// <summary>The title and message of the commit a merge creates.</summary>
/// <param name="Title">The first line.</param>
/// <param name="Message">The rest, possibly empty.</param>
public readonly record struct MergeCommitText(string Title, string Message);

/// <summary>
/// The commit title and message GitHub puts in its merge box, following the repository's settings for merge and
/// squash commits, so the user starts from what they would see on GitHub.
/// </summary>
public static class MergeMessages
{
    /// <summary>The default title and message for merging a pull request.</summary>
    /// <param name="method">How it is merged. A rebase creates no commit of its own, so both are empty.</param>
    /// <param name="pullRequest">The pull request.</param>
    /// <param name="commits">Its commits, oldest first.</param>
    /// <param name="settings">The repository's merge settings.</param>
    public static MergeCommitText Default(MergeMethod method, PullRequestInfo pullRequest, IReadOnlyList<GitCommit> commits, RepositoryMergeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(commits);
        ArgumentNullException.ThrowIfNull(settings);

        var numbered = string.Create(CultureInfo.InvariantCulture, $"{pullRequest.Title} (#{pullRequest.Number})");
        var body = pullRequest.Body?.Trim() ?? string.Empty;
        return method switch
        {
            MergeMethod.Merge => new MergeCommitText(
                settings.MergeCommitTitle == "PR_TITLE"
                    ? numbered
                    : string.Create(CultureInfo.InvariantCulture, $"Merge pull request #{pullRequest.Number} from {pullRequest.Head.Owner ?? pullRequest.Author}/{pullRequest.Head.Name}"),
                settings.MergeCommitMessage switch
                {
                    "PR_BODY" => body,
                    "BLANK" => string.Empty,
                    _ => pullRequest.Title,
                }),
            MergeMethod.Squash => new MergeCommitText(
                settings.SquashCommitTitle == "COMMIT_OR_PR_TITLE" && commits.Count == 1
                    ? string.Create(CultureInfo.InvariantCulture, $"{commits[0].Summary} (#{pullRequest.Number})")
                    : numbered,
                settings.SquashCommitMessage switch
                {
                    "PR_BODY" => body,
                    "BLANK" => string.Empty,
                    _ => CommitMessages(commits),
                }),
            _ => new MergeCommitText(string.Empty, string.Empty),
        };
    }

    /// <summary>
    /// What GitHub writes for a squashed commit's message: for one commit the rest of its message, for several each
    /// commit's message as a bullet.
    /// </summary>
    private static string CommitMessages(IReadOnlyList<GitCommit> commits) => commits.Count switch
    {
        0 => string.Empty,
        1 => commits[0].Message.ReplaceLineEndings("\n").Split('\n', 2) is [_, var rest] ? rest.Trim() : string.Empty,
        _ => string.Join("\n\n", commits.Select(c => "* " + c.Message.ReplaceLineEndings("\n").Trim())),
    };
}
