using System.Text.Json.Serialization;

namespace TabStudio.SourceControl.GitHub;

// The shapes of GitHub's REST API, as far as TabStudio reads and writes them. Property names are converted
// to snake_case by the serializer context at the bottom of this file.

internal sealed record UserDto(string Login, string? Name);

internal sealed record OwnerDto(string Login);

internal sealed record PermissionsDto(bool Push);

internal sealed record RepositoryDto(string Name, OwnerDto Owner, string? DefaultBranch, bool Private, string HtmlUrl, PermissionsDto? Permissions);

internal sealed record CreateRepositoryRequest(string Name, string? Description, bool Private, bool AutoInit);

internal sealed record ShaDto(string Sha);

internal sealed record BranchDto(string Name, ShaDto Commit, bool Protected);

internal sealed record RefObjectDto(string Sha, string Type);

internal sealed record RefDto(string Ref, RefObjectDto Object);

internal sealed record SignatureDto(string? Name, string? Email, DateTimeOffset? Date);

internal sealed record GitCommitDto(string Sha, ShaDto Tree, string Message, IReadOnlyList<ShaDto> Parents, SignatureDto? Author);

/// <summary>A commit as the repository commits endpoints return it: the Git data is nested.</summary>
internal sealed record RepositoryCommitDto(string Sha, RepositoryCommitDataDto Commit, IReadOnlyList<ShaDto> Parents);

internal sealed record RepositoryCommitDataDto(ShaDto Tree, string Message, SignatureDto? Author);

internal sealed record TreeEntryDto(string Path, string Mode, string Type, string Sha, long? Size);

internal sealed record TreeDto(string Sha, IReadOnlyList<TreeEntryDto> Tree, bool Truncated);

internal sealed record BlobDto(string? Content, string? Encoding, long Size);

internal sealed record CreateBlobRequest(string Content, string Encoding);

internal sealed record CreateTreeEntry(string Path, string Mode, string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Sha);

internal sealed record CreateTreeRequest(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BaseTree,
    IReadOnlyList<CreateTreeEntry> Tree);

internal sealed record CreateCommitRequest(string Message, string Tree, IReadOnlyList<string> Parents);

internal sealed record UpdateRefRequest(string Sha, bool Force);

internal sealed record CreateRefRequest(string Ref, string Sha);

internal sealed record CompareDto(string Status, int AheadBy, int BehindBy);

internal sealed record PullRequestBranchDto(string Ref);

internal sealed record PullRequestDto(int Number, string Title, string State, PullRequestBranchDto Head, PullRequestBranchDto Base, string HtmlUrl, bool Draft);

internal sealed record CreatePullRequestRequest(string Title, string? Body, string Head, string Base, bool Draft);

internal sealed record ErrorDto(string? Message);

/// <summary>Source-generated JSON for GitHub's API, so nothing depends on reflection (trimming-safe on Android).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(RepositoryDto))]
[JsonSerializable(typeof(CreateRepositoryRequest))]
[JsonSerializable(typeof(IReadOnlyList<BranchDto>))]
[JsonSerializable(typeof(RefDto))]
[JsonSerializable(typeof(GitCommitDto))]
[JsonSerializable(typeof(RepositoryCommitDto))]
[JsonSerializable(typeof(IReadOnlyList<RepositoryCommitDto>))]
[JsonSerializable(typeof(TreeDto))]
[JsonSerializable(typeof(BlobDto))]
[JsonSerializable(typeof(CreateBlobRequest))]
[JsonSerializable(typeof(ShaDto))]
[JsonSerializable(typeof(CreateTreeRequest))]
[JsonSerializable(typeof(CreateCommitRequest))]
[JsonSerializable(typeof(UpdateRefRequest))]
[JsonSerializable(typeof(CreateRefRequest))]
[JsonSerializable(typeof(CompareDto))]
[JsonSerializable(typeof(PullRequestDto))]
[JsonSerializable(typeof(IReadOnlyList<PullRequestDto>))]
[JsonSerializable(typeof(CreatePullRequestRequest))]
[JsonSerializable(typeof(ErrorDto))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext
{
}
