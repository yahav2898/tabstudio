using System.Text.Json.Serialization;

namespace TabStudio.SourceControl.GitHub;

// The shapes of GitHub's pull request, review and merge endpoints, as far as TabStudio reads and writes them.
// Property names are converted to snake_case by the serializer context at the bottom of this file; values left
// null are not sent, so an optional field such as a review's event stays out of the request.

internal sealed record PullUserDto(string Login);

internal sealed record PullRepositoryDto(string FullName);

internal sealed record PullEndDto(string Ref, string Sha, PullRepositoryDto? Repo);

/// <summary>A pull request. Lists leave out the details from <c>Mergeable</c> on, which then keep their defaults.</summary>
internal sealed record PullDto(
    int Number,
    string NodeId,
    string Title,
    string? Body,
    string State,
    bool Draft,
    PullUserDto? User,
    PullEndDto Head,
    PullEndDto Base,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MergedAt,
    string HtmlUrl,
    IReadOnlyList<PullUserDto>? RequestedReviewers,
    bool? Mergeable,
    string? MergeableState,
    int Additions,
    int Deletions,
    int ChangedFiles,
    int Commits,
    string? MergeCommitSha,
    PullUserDto? MergedBy);

internal sealed record PullFileDto(string Filename, string Status, int Additions, int Deletions, string? Patch, string? PreviousFilename, string? Sha);

internal sealed record ReviewCommentDto(
    long Id,
    string NodeId,
    string Path,
    int? Line,
    string? Side,
    int? StartLine,
    string? StartSide,
    int? OriginalLine,
    int? OriginalStartLine,
    long? InReplyToId,
    PullUserDto? User,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? DiffHunk,
    string? CommitId,
    long? PullRequestReviewId,
    string? HtmlUrl);

internal sealed record ReviewDto(long Id, string NodeId, PullUserDto? User, string State, string? Body, DateTimeOffset? SubmittedAt, string? CommitId, string? HtmlUrl);

internal sealed record ReviewCommentRequest(string Path, int Line, string Side, string Body, int? StartLine, string? StartSide);

internal sealed record CreateReviewRequest(string? CommitId, string? Body, string? Event, IReadOnlyList<ReviewCommentRequest>? Comments);

internal sealed record SubmitReviewRequest(string? Body, string Event);

internal sealed record CommentBodyRequest(string Body);

internal sealed record IssueCommentDto(long Id, PullUserDto? User, string Body, DateTimeOffset CreatedAt, string? HtmlUrl);

internal sealed record MergeRequest(string MergeMethod, string? CommitTitle, string? CommitMessage, string? Sha);

internal sealed record MergeResponseDto(string? Sha, bool Merged, string? Message);

internal sealed record RequestReviewersRequest(IReadOnlyList<string> Reviewers);

internal sealed record MergeSettingsDto(
    bool? AllowMergeCommit,
    bool? AllowSquashMerge,
    bool? AllowRebaseMerge,
    bool? DeleteBranchOnMerge,
    string? MergeCommitTitle,
    string? MergeCommitMessage,
    string? SquashMergeCommitTitle,
    string? SquashMergeCommitMessage);

internal sealed record CommitShaDto(string Sha);

internal sealed record MergeBaseDto(CommitShaDto? MergeBaseCommit);

internal sealed record ContentDto(string Type, string? Encoding, string? Content, string Sha, long Size);

internal sealed record SearchIssueDto(int Number, int Comments);

internal sealed record SearchIssuesDto(int TotalCount, IReadOnlyList<SearchIssueDto> Items);

/// <summary>Source-generated JSON for GitHub's pull request API, so nothing depends on reflection (trimming-safe on Android).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<PullDto>))]
[JsonSerializable(typeof(PullDto))]
[JsonSerializable(typeof(IReadOnlyList<PullFileDto>))]
[JsonSerializable(typeof(IReadOnlyList<ReviewCommentDto>))]
[JsonSerializable(typeof(ReviewCommentDto))]
[JsonSerializable(typeof(IReadOnlyList<ReviewDto>))]
[JsonSerializable(typeof(ReviewDto))]
[JsonSerializable(typeof(CreateReviewRequest))]
[JsonSerializable(typeof(SubmitReviewRequest))]
[JsonSerializable(typeof(CommentBodyRequest))]
[JsonSerializable(typeof(IReadOnlyList<IssueCommentDto>))]
[JsonSerializable(typeof(IssueCommentDto))]
[JsonSerializable(typeof(MergeRequest))]
[JsonSerializable(typeof(MergeResponseDto))]
[JsonSerializable(typeof(RequestReviewersRequest))]
[JsonSerializable(typeof(MergeSettingsDto))]
[JsonSerializable(typeof(MergeBaseDto))]
[JsonSerializable(typeof(ContentDto))]
[JsonSerializable(typeof(SearchIssuesDto))]
internal sealed partial class PullRequestJsonContext : JsonSerializerContext
{
}
