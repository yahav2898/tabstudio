using System.Text.Json.Serialization;

namespace TabStudio.SourceControl.GitHub;

// The shapes of GitHub's checks, statuses and Actions APIs, as far as TabStudio reads and writes them. Property
// names are converted to snake_case by the serializer context at the bottom of this file.

internal sealed record CheckRunsDto(int TotalCount, IReadOnlyList<CheckRunDto> CheckRuns);

internal sealed record CheckRunDto(
    long Id,
    string Name,
    string? Status,
    string? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? HtmlUrl,
    string? DetailsUrl,
    CheckRunOutputDto? Output,
    CheckRunAppDto? App);

internal sealed record CheckRunOutputDto(string? Title, string? Summary, int? AnnotationsCount);

internal sealed record CheckRunAppDto(string? Slug);

internal sealed record AnnotationDto(
    string? Path,
    int StartLine,
    int? EndLine,
    int? StartColumn,
    int? EndColumn,
    string? AnnotationLevel,
    string? Title,
    string? Message,
    string? RawDetails);

internal sealed record CombinedStatusDto(string? State, IReadOnlyList<CommitStatusDto>? Statuses);

internal sealed record CommitStatusDto(string? Context, string? State, string? Description, string? TargetUrl, DateTimeOffset? UpdatedAt);

internal sealed record WorkflowsDto(int TotalCount, IReadOnlyList<WorkflowDto> Workflows);

internal sealed record WorkflowDto(long Id, string? Name, string Path, string? State, string? HtmlUrl);

internal sealed record WorkflowRunsDto(int TotalCount, IReadOnlyList<WorkflowRunDto> WorkflowRuns);

internal sealed record WorkflowRunDto(
    long Id,
    long WorkflowId,
    string? Name,
    string? DisplayTitle,
    int RunNumber,
    int? RunAttempt,
    string? Event,
    string? Status,
    string? Conclusion,
    string? HeadBranch,
    string? HeadSha,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RunStartedAt,
    DateTimeOffset UpdatedAt,
    string HtmlUrl,
    OwnerDto? Actor,
    OwnerDto? TriggeringActor,
    HeadCommitDto? HeadCommit);

internal sealed record HeadCommitDto(string? Message);

internal sealed record JobsDto(int TotalCount, IReadOnlyList<JobDto> Jobs);

internal sealed record JobDto(
    long Id,
    long RunId,
    string Name,
    string? Status,
    string? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? HtmlUrl,
    IReadOnlyList<StepDto>? Steps);

internal sealed record StepDto(int Number, string Name, string? Status, string? Conclusion, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);

/// <summary>A workflow_dispatch request. Input names are kept as written in the workflow file.</summary>
internal sealed record DispatchRequest(
    string Ref,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Inputs);

internal sealed record ContentDto(string? Type, string? Encoding, string? Content, long Size);

/// <summary>Source-generated JSON for the checks, statuses and Actions APIs (trimming-safe on Android).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CheckRunsDto))]
[JsonSerializable(typeof(IReadOnlyList<AnnotationDto>))]
[JsonSerializable(typeof(CombinedStatusDto))]
[JsonSerializable(typeof(WorkflowsDto))]
[JsonSerializable(typeof(WorkflowRunsDto))]
[JsonSerializable(typeof(WorkflowRunDto))]
[JsonSerializable(typeof(JobsDto))]
[JsonSerializable(typeof(DispatchRequest))]
[JsonSerializable(typeof(ContentDto))]
internal sealed partial class GitHubActionsJsonContext : JsonSerializerContext
{
}
