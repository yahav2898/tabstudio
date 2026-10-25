using System.Globalization;
using System.Net;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// Pull requests: listing and reading them, their files, commits and conversation, file contents at either end,
/// requesting reviewers, and merging.
/// </summary>
public sealed partial class GitHubClient
{
    /// <summary>The login GitHub shows for accounts that were deleted.</summary>
    public const string DeletedUser = "ghost";

    private const int PageSize = 100;

    /// <summary>Lists pull requests, the most recently updated first.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="state">Open, closed or all pull requests.</param>
    /// <param name="headBranch">Only pull requests from this branch of the repository, or <see langword="null"/>.</param>
    /// <param name="baseBranch">Only pull requests into this branch, or <see langword="null"/>.</param>
    /// <param name="count">How many to list, at most 100.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(
        string owner,
        string repository,
        PullRequestStateFilter state = PullRequestStateFilter.Open,
        string? headBranch = null,
        string? baseBranch = null,
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"{Repo(owner, repository)}/pulls?state={StateParameter(state)}&sort=updated&direction=desc&per_page={Math.Clamp(count, 1, PageSize)}");
        if (headBranch is not null)
        {
            path += $"&head={Uri.EscapeDataString($"{owner}:{headBranch}")}";
        }

        if (baseBranch is not null)
        {
            path += $"&base={Uri.EscapeDataString(baseBranch)}";
        }

        var list = await SendAsync(HttpMethod.Get, path, null, PullRequestJsonContext.Default.IReadOnlyListPullDto, cancellationToken).ConfigureAwait(false);
        return [.. list.Select(ToPullRequestInfo)];
    }

    /// <summary>Reads a pull request with what GitHub works out about it, such as whether it can be merged.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<PullRequestDetails> GetPullRequestAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, Pull(owner, repository, number), null, PullRequestJsonContext.Default.PullDto, cancellationToken).ConfigureAwait(false);
        return new PullRequestDetails(
            ToPullRequestInfo(dto),
            dto.Mergeable,
            ParseMergeable(dto.MergeableState, dto.Draft),
            dto.Additions,
            dto.Deletions,
            dto.ChangedFiles,
            dto.Commits,
            dto.MergeCommitSha,
            dto.MergedBy?.Login);
    }

    /// <summary>Lists the files a pull request changes, up to GitHub's limit of 3000.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFilesAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var files = await GetPagesAsync($"{Pull(owner, repository, number)}/files", PullRequestJsonContext.Default.IReadOnlyListPullFileDto, maxPages: 30, cancellationToken).ConfigureAwait(false);
        return [.. files.Select(f => new PullRequestFile(f.Filename, f.PreviousFilename, ParseFileStatus(f.Status), f.Additions, f.Deletions, f.Patch, f.Sha))];
    }

    /// <summary>Lists a pull request's commits, oldest first, up to GitHub's limit of 250.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<GitCommit>> GetPullRequestCommitsAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var commits = await GetPagesAsync($"{Pull(owner, repository, number)}/commits", GitHubJsonContext.Default.IReadOnlyListRepositoryCommitDto, maxPages: 3, cancellationToken).ConfigureAwait(false);
        return [.. commits.Select(ToCommit)];
    }

    /// <summary>
    /// Counts the comments in the conversation of the most recently updated pull requests, which the pull request
    /// list does not include. Uses GitHub's search, which has a lower rate limit than the rest of the API.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="state">Open, closed or all pull requests.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of comments, by pull request number.</returns>
    public async Task<IReadOnlyDictionary<int, int>> GetPullRequestCommentCountsAsync(string owner, string repository, PullRequestStateFilter state, CancellationToken cancellationToken = default)
    {
        var query = $"repo:{owner}/{repository} is:pr" + state switch
        {
            PullRequestStateFilter.Open => " is:open",
            PullRequestStateFilter.Closed => " is:closed",
            _ => string.Empty,
        };
        var path = string.Create(CultureInfo.InvariantCulture, $"search/issues?q={Uri.EscapeDataString(query)}&sort=updated&order=desc&per_page={PageSize}");
        var result = await SendAsync(HttpMethod.Get, path, null, PullRequestJsonContext.Default.SearchIssuesDto, cancellationToken).ConfigureAwait(false);
        return result.Items.ToDictionary(i => i.Number, i => i.Comments);
    }

    /// <summary>Reads how a repository lets pull requests be merged.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The settings; GitHub's defaults for those the account may not see.</returns>
    public async Task<RepositoryMergeSettings> GetMergeSettingsAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, Repo(owner, repository), null, PullRequestJsonContext.Default.MergeSettingsDto, cancellationToken).ConfigureAwait(false);
        var defaults = RepositoryMergeSettings.Default;
        return new RepositoryMergeSettings(
            dto.AllowMergeCommit ?? defaults.AllowMergeCommit,
            dto.AllowSquashMerge ?? defaults.AllowSquashMerge,
            dto.AllowRebaseMerge ?? defaults.AllowRebaseMerge,
            dto.DeleteBranchOnMerge ?? defaults.DeleteBranchOnMerge,
            dto.MergeCommitTitle ?? defaults.MergeCommitTitle,
            dto.MergeCommitMessage ?? defaults.MergeCommitMessage,
            dto.SquashMergeCommitTitle ?? defaults.SquashCommitTitle,
            dto.SquashMergeCommitMessage ?? defaults.SquashCommitMessage);
    }

    /// <summary>Merges a pull request.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="method">How to merge.</param>
    /// <param name="commitTitle">The merge or squashed commit's title, or <see langword="null"/> for GitHub's.</param>
    /// <param name="commitMessage">The merge or squashed commit's message, or <see langword="null"/> for GitHub's.</param>
    /// <param name="expectedHeadSha">
    /// The head commit the user reviewed; GitHub refuses the merge if the branch moved since. <see langword="null"/>
    /// merges whatever the head is.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="GitHubApiException">
    /// GitHub refused: 405 when the pull request cannot be merged (conflicts, a draft, blocked by rules, or a method
    /// the repository does not allow), 409 when the head moved, 403 without permission.
    /// </exception>
    public async Task<PullRequestMergeResult> MergePullRequestAsync(
        string owner,
        string repository,
        int number,
        MergeMethod method,
        string? commitTitle,
        string? commitMessage,
        string? expectedHeadSha,
        CancellationToken cancellationToken = default)
    {
        var request = new MergeRequest(MergeMethodParameter(method), method == MergeMethod.Rebase ? null : commitTitle, method == MergeMethod.Rebase ? null : commitMessage, expectedHeadSha);
        var dto = await SendAsync(HttpMethod.Put, $"{Pull(owner, repository, number)}/merge", Json(request, PullRequestJsonContext.Default.MergeRequest), PullRequestJsonContext.Default.MergeResponseDto, cancellationToken).ConfigureAwait(false);
        if (!dto.Merged || dto.Sha is null)
        {
            throw new GitHubApiException(HttpStatusCode.MethodNotAllowed, dto.Message ?? "GitHub did not merge the pull request.");
        }

        return new PullRequestMergeResult(dto.Sha, dto.Message ?? "Pull request merged.");
    }

    /// <summary>Deletes a branch, as GitHub's "Delete branch" button does after a merge.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task DeleteBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) =>
        SendWithoutAnswerAsync(HttpMethod.Delete, $"{Repo(owner, repository)}/git/refs/heads/{EscapePath(branch)}", null, cancellationToken);

    /// <summary>Asks people to review a pull request.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="reviewers">Their logins.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The pull request, with its requested reviewers.</returns>
    public async Task<PullRequestInfo> RequestReviewersAsync(string owner, string repository, int number, IReadOnlyList<string> reviewers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reviewers);
        var dto = await SendAsync(HttpMethod.Post, $"{Pull(owner, repository, number)}/requested_reviewers", Json(new RequestReviewersRequest(reviewers), PullRequestJsonContext.Default.RequestReviewersRequest), PullRequestJsonContext.Default.PullDto, cancellationToken).ConfigureAwait(false);
        return ToPullRequestInfo(dto);
    }

    /// <summary>
    /// Finds the commit two commits last had in common: the version of the base a pull request's diff starts
    /// from, as GitHub's Files changed shows it.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="baseSha">The base commit.</param>
    /// <param name="headSha">The head commit.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<string> GetMergeBaseAsync(string owner, string repository, string baseSha, string headSha, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/compare/{EscapePath(baseSha)}...{EscapePath(headSha)}?per_page=1", null, PullRequestJsonContext.Default.MergeBaseDto, cancellationToken).ConfigureAwait(false);
        return dto.MergeBaseCommit?.Sha ?? throw new GitHubApiException("GitHub did not say where the branches meet.");
    }

    /// <summary>Downloads a file as it is in a commit, branch or tag.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="path">The file's path, with <c>/</c> separators.</param>
    /// <param name="reference">The commit, branch or tag.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The content, or <see langword="null"/> when the file is not there.</returns>
    public async Task<byte[]?> GetFileContentAsync(string owner, string repository, string path, string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ContentDto content;
        try
        {
            content = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(reference)}", null, PullRequestJsonContext.Default.ContentDto, cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubApiException ex) when (ex.IsNotFound)
        {
            return null;
        }

        if (content.Type != "file")
        {
            return null;
        }

        // Files over a megabyte come without their content; their blob has it.
        return content.Encoding == "base64" && content.Content is { } text
            ? Convert.FromBase64String(text.Replace("\n", string.Empty, StringComparison.Ordinal))
            : await GetBlobAsync(owner, repository, content.Sha, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists the comments in a pull request's conversation, oldest first.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var comments = await GetPagesAsync($"{Issue(owner, repository, number)}/comments", PullRequestJsonContext.Default.IReadOnlyListIssueCommentDto, maxPages: 10, cancellationToken).ConfigureAwait(false);
        return [.. comments.Select(ToIssueComment)];
    }

    /// <summary>Adds a comment to a pull request's conversation.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="body">The text, in Markdown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IssueComment> CreateIssueCommentAsync(string owner, string repository, int number, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var dto = await SendAsync(HttpMethod.Post, $"{Issue(owner, repository, number)}/comments", Json(new CommentBodyRequest(body), PullRequestJsonContext.Default.CommentBodyRequest), PullRequestJsonContext.Default.IssueCommentDto, cancellationToken).ConfigureAwait(false);
        return ToIssueComment(dto);
    }

    /// <summary>Sends a request whose answer TabStudio does not need, such as a deletion.</summary>
    private async Task SendWithoutAnswerAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, content);
        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a list GitHub sends in pages, until a page is not full or the limit is reached.</summary>
    private async Task<List<T>> GetPagesAsync<T>(string path, JsonTypeInfo<IReadOnlyList<T>> type, int maxPages, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        for (var page = 1; page <= maxPages; page++)
        {
            var batch = await SendAsync(HttpMethod.Get, string.Create(CultureInfo.InvariantCulture, $"{path}{separator}per_page={PageSize}&page={page}"), null, type, cancellationToken).ConfigureAwait(false);
            items.AddRange(batch);
            if (batch.Count < PageSize)
            {
                break;
            }
        }

        return items;
    }

    private static string Pull(string owner, string repository, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{Repo(owner, repository)}/pulls/{number}");

    private static string Issue(string owner, string repository, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{Repo(owner, repository)}/issues/{number}");

    private static string StateParameter(PullRequestStateFilter state) => state switch
    {
        PullRequestStateFilter.Closed => "closed",
        PullRequestStateFilter.All => "all",
        _ => "open",
    };

    private static string MergeMethodParameter(MergeMethod method) => method switch
    {
        MergeMethod.Squash => "squash",
        MergeMethod.Rebase => "rebase",
        _ => "merge",
    };

    private static PullRequestInfo ToPullRequestInfo(PullDto dto) => new(
        dto.Number,
        dto.Title,
        string.IsNullOrWhiteSpace(dto.Body) ? null : dto.Body,
        dto.MergedAt is not null ? PullRequestState.Merged : dto.State == "closed" ? PullRequestState.Closed : PullRequestState.Open,
        dto.Draft,
        dto.User?.Login ?? DeletedUser,
        new PullRequestBranch(dto.Head.Ref, dto.Head.Sha, dto.Head.Repo?.FullName),
        new PullRequestBranch(dto.Base.Ref, dto.Base.Sha, dto.Base.Repo?.FullName),
        dto.CreatedAt,
        dto.UpdatedAt,
        dto.MergedAt,
        new Uri(dto.HtmlUrl),
        [.. (dto.RequestedReviewers ?? []).Select(r => r.Login)],
        dto.NodeId);

    private static MergeableState ParseMergeable(string? state, bool draft) => state switch
    {
        "clean" => MergeableState.Clean,
        "dirty" => MergeableState.Conflicts,
        "blocked" => MergeableState.Blocked,
        "behind" => MergeableState.Behind,
        "unstable" => MergeableState.Unstable,
        "has_hooks" => MergeableState.HasHooks,
        "draft" => MergeableState.Draft,
        _ => draft ? MergeableState.Draft : MergeableState.Unknown,
    };

    private static PullRequestFileStatus ParseFileStatus(string status) => status switch
    {
        "added" => PullRequestFileStatus.Added,
        "removed" => PullRequestFileStatus.Removed,
        "renamed" => PullRequestFileStatus.Renamed,
        "copied" => PullRequestFileStatus.Copied,
        "changed" => PullRequestFileStatus.Changed,
        "unchanged" => PullRequestFileStatus.Unchanged,
        _ => PullRequestFileStatus.Modified,
    };

    private static IssueComment ToIssueComment(IssueCommentDto dto) =>
        new(dto.Id, dto.User?.Login ?? DeletedUser, dto.Body, dto.CreatedAt, dto.HtmlUrl is null ? null : new Uri(dto.HtmlUrl));
}
