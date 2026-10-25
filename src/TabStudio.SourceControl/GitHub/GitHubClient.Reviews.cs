using System.Globalization;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// Reviews of pull requests: comments on lines of the diff and their replies, and reviews, which start pending
/// (only their author sees them) and are submitted as a comment, an approval or a request for changes.
/// </summary>
public sealed partial class GitHubClient
{
    /// <summary>Lists the comments on a pull request's diff, oldest first. Pending comments are listed by <see cref="GetReviewCommentsOfReviewAsync"/>.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<ReviewComment>> GetReviewCommentsAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var comments = await GetPagesAsync($"{Pull(owner, repository, number)}/comments", PullRequestJsonContext.Default.IReadOnlyListReviewCommentDto, maxPages: 30, cancellationToken).ConfigureAwait(false);
        return [.. comments.Select(ToReviewComment)];
    }

    /// <summary>Lists the comments of one review, such as the signed-in account's pending one.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="reviewId">The review.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<ReviewComment>> GetReviewCommentsOfReviewAsync(string owner, string repository, int number, long reviewId, CancellationToken cancellationToken = default)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"{Pull(owner, repository, number)}/reviews/{reviewId}/comments");
        var comments = await GetPagesAsync(path, PullRequestJsonContext.Default.IReadOnlyListReviewCommentDto, maxPages: 10, cancellationToken).ConfigureAwait(false);
        return [.. comments.Select(ToReviewComment)];
    }

    /// <summary>Lists a pull request's reviews, oldest first, with the signed-in account's pending one if there is one.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<PullRequestReview>> GetReviewsAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var reviews = await GetPagesAsync($"{Pull(owner, repository, number)}/reviews", PullRequestJsonContext.Default.IReadOnlyListReviewDto, maxPages: 10, cancellationToken).ConfigureAwait(false);
        return [.. reviews.Select(ToReview)];
    }

    /// <summary>
    /// Creates a review. Without <paramref name="reviewEvent"/> it stays pending, holding its comments until it is
    /// submitted with <see cref="SubmitReviewAsync"/>; GitHub allows one pending review per person.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="commitId">The head commit the review is of, or <see langword="null"/> for the latest.</param>
    /// <param name="body">The summary, or <see langword="null"/>.</param>
    /// <param name="reviewEvent">How to submit it now, or <see langword="null"/> to keep it pending.</param>
    /// <param name="comments">Comments on lines of the diff.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<PullRequestReview> CreateReviewAsync(
        string owner,
        string repository,
        int number,
        string? commitId,
        string? body,
        ReviewEvent? reviewEvent,
        IReadOnlyList<NewReviewComment> comments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comments);
        var request = new CreateReviewRequest(
            commitId,
            string.IsNullOrWhiteSpace(body) ? null : body,
            reviewEvent is { } submitted ? EventParameter(submitted) : null,
            comments.Count == 0 ? null : [.. comments.Select(c => new ReviewCommentRequest(c.Path, c.Line, SideParameter(c.Side), c.Body, c.StartLine, c.StartSide is { } side ? SideParameter(side) : null))]);
        var dto = await SendAsync(HttpMethod.Post, $"{Pull(owner, repository, number)}/reviews", Json(request, PullRequestJsonContext.Default.CreateReviewRequest), PullRequestJsonContext.Default.ReviewDto, cancellationToken).ConfigureAwait(false);
        return ToReview(dto);
    }

    /// <summary>Submits a pending review.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="reviewId">The pending review.</param>
    /// <param name="reviewEvent">Comment, approve or request changes.</param>
    /// <param name="body">The summary, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<PullRequestReview> SubmitReviewAsync(string owner, string repository, int number, long reviewId, ReviewEvent reviewEvent, string? body, CancellationToken cancellationToken = default)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"{Pull(owner, repository, number)}/reviews/{reviewId}/events");
        var request = new SubmitReviewRequest(string.IsNullOrWhiteSpace(body) ? null : body, EventParameter(reviewEvent));
        var dto = await SendAsync(HttpMethod.Post, path, Json(request, PullRequestJsonContext.Default.SubmitReviewRequest), PullRequestJsonContext.Default.ReviewDto, cancellationToken).ConfigureAwait(false);
        return ToReview(dto);
    }

    /// <summary>Deletes a pending review and its comments.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="reviewId">The pending review.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task DeletePendingReviewAsync(string owner, string repository, int number, long reviewId, CancellationToken cancellationToken = default) =>
        SendIgnoringAnswerAsync(HttpMethod.Delete, string.Create(CultureInfo.InvariantCulture, $"{Pull(owner, repository, number)}/reviews/{reviewId}"), null, cancellationToken);

    /// <summary>Replies to a comment on the diff. The reply is posted at once.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="commentId">The first comment of the thread.</param>
    /// <param name="body">The text, in Markdown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<ReviewComment> ReplyToReviewCommentAsync(string owner, string repository, int number, long commentId, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var path = string.Create(CultureInfo.InvariantCulture, $"{Pull(owner, repository, number)}/comments/{commentId}/replies");
        var dto = await SendAsync(HttpMethod.Post, path, Json(new CommentBodyRequest(body), PullRequestJsonContext.Default.CommentBodyRequest), PullRequestJsonContext.Default.ReviewCommentDto, cancellationToken).ConfigureAwait(false);
        return ToReviewComment(dto);
    }

    /// <summary>Changes the text of a comment on the diff.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="commentId">The comment.</param>
    /// <param name="body">The new text, in Markdown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<ReviewComment> UpdateReviewCommentAsync(string owner, string repository, long commentId, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var dto = await SendAsync(new HttpMethod("PATCH"), ReviewCommentPath(owner, repository, commentId), Json(new CommentBodyRequest(body), PullRequestJsonContext.Default.CommentBodyRequest), PullRequestJsonContext.Default.ReviewCommentDto, cancellationToken).ConfigureAwait(false);
        return ToReviewComment(dto);
    }

    /// <summary>Deletes a comment on the diff, such as one of a pending review.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="commentId">The comment.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task DeleteReviewCommentAsync(string owner, string repository, long commentId, CancellationToken cancellationToken = default) =>
        SendIgnoringAnswerAsync(HttpMethod.Delete, ReviewCommentPath(owner, repository, commentId), null, cancellationToken);

    private static string ReviewCommentPath(string owner, string repository, long commentId) =>
        string.Create(CultureInfo.InvariantCulture, $"{Repo(owner, repository)}/pulls/comments/{commentId}");

    private static string EventParameter(ReviewEvent reviewEvent) => reviewEvent switch
    {
        ReviewEvent.Approve => "APPROVE",
        ReviewEvent.RequestChanges => "REQUEST_CHANGES",
        _ => "COMMENT",
    };

    private static string SideParameter(DiffSide side) => side == DiffSide.Left ? "LEFT" : "RIGHT";

    private static DiffSide ParseSide(string? side) => side == "LEFT" ? DiffSide.Left : DiffSide.Right;

    private static ReviewState ParseReviewState(string state) => state switch
    {
        "PENDING" => ReviewState.Pending,
        "APPROVED" => ReviewState.Approved,
        "CHANGES_REQUESTED" => ReviewState.ChangesRequested,
        "DISMISSED" => ReviewState.Dismissed,
        _ => ReviewState.Commented,
    };

    private static ReviewComment ToReviewComment(ReviewCommentDto dto) => new(
        dto.Id,
        dto.NodeId,
        dto.Path,
        dto.Line,
        ParseSide(dto.Side),
        dto.StartLine,
        dto.StartLine is null ? null : ParseSide(dto.StartSide ?? dto.Side),
        dto.OriginalLine,
        dto.OriginalStartLine,
        dto.InReplyToId,
        dto.User?.Login ?? DeletedUser,
        dto.Body,
        dto.CreatedAt,
        dto.UpdatedAt,
        dto.DiffHunk ?? string.Empty,
        dto.CommitId ?? string.Empty,
        dto.PullRequestReviewId,
        dto.HtmlUrl is null ? null : new Uri(dto.HtmlUrl));

    private static PullRequestReview ToReview(ReviewDto dto) => new(
        dto.Id,
        dto.NodeId,
        dto.User?.Login ?? DeletedUser,
        ParseReviewState(dto.State),
        string.IsNullOrWhiteSpace(dto.Body) ? null : dto.Body,
        dto.SubmittedAt,
        dto.CommitId,
        dto.HtmlUrl is null ? null : new Uri(dto.HtmlUrl));
}
