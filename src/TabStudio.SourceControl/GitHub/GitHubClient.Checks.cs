using System.Globalization;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// A commit's CI results: its check runs (GitHub Actions jobs and other apps' checks), their annotations, and the
/// statuses external CI services set. These reads are conditional, so polling them is cheap.
/// </summary>
public sealed partial class GitHubClient
{
    private const int MaximumAnnotationPages = 5;

    /// <summary>Lists the latest check run of each check on a commit.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The commit's hash, or a branch or tag.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<GitHubCheckRun>> GetCheckRunsAsync(string owner, string repository, string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reference);
        var path = $"{Repo(owner, repository)}/commits/{EscapePath(reference)}/check-runs?per_page=100";
        var dto = await GetCachedAsync(path, GitHubActionsJsonContext.Default.CheckRunsDto, cancellationToken).ConfigureAwait(false);
        return [.. dto.CheckRuns.Select(ToCheckRun)];
    }

    /// <summary>Lists the annotations of a check run, such as the compiler errors of a build.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="checkRunId">The check run.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<GitHubCheckAnnotation>> GetCheckRunAnnotationsAsync(string owner, string repository, long checkRunId, CancellationToken cancellationToken = default)
    {
        var annotations = new List<GitHubCheckAnnotation>();
        for (var page = 1; page <= MaximumAnnotationPages; page++)
        {
            var path = $"{Repo(owner, repository)}/check-runs/{checkRunId.ToString(CultureInfo.InvariantCulture)}/annotations?per_page=100&page={page.ToString(CultureInfo.InvariantCulture)}";
            var batch = await GetCachedAsync(path, GitHubActionsJsonContext.Default.IReadOnlyListAnnotationDto, cancellationToken).ConfigureAwait(false);
            annotations.AddRange(batch.Select(ToAnnotation));
            if (batch.Count < 100)
            {
                break;
            }
        }

        return annotations;
    }

    /// <summary>Reads the statuses external CI services set on a commit, combined.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The commit's hash, or a branch or tag.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubCombinedStatus> GetCombinedStatusAsync(string owner, string repository, string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reference);
        var path = $"{Repo(owner, repository)}/commits/{EscapePath(reference)}/status?per_page=100";
        var dto = await GetCachedAsync(path, GitHubActionsJsonContext.Default.CombinedStatusDto, cancellationToken).ConfigureAwait(false);
        var statuses = (dto.Statuses ?? [])
            .Where(s => !string.IsNullOrEmpty(s.Context))
            .Select(s => new GitHubCommitStatus(s.Context!, GitHubRunStates.ParseStatusState(s.State), NullIfEmpty(s.Description), ToUri(s.TargetUrl), s.UpdatedAt))
            .ToList();
        return new GitHubCombinedStatus(GitHubRunStates.ParseStatusState(dto.State), statuses);
    }

    private static GitHubCheckRun ToCheckRun(CheckRunDto dto) => new(
        dto.Id,
        dto.Name,
        GitHubRunStates.ParseStatus(dto.Status),
        GitHubRunStates.ParseConclusion(dto.Conclusion),
        dto.StartedAt,
        dto.CompletedAt,
        ToUri(dto.HtmlUrl),
        ToUri(dto.DetailsUrl),
        NullIfEmpty(dto.Output?.Title),
        NullIfEmpty(dto.Output?.Summary),
        dto.Output?.AnnotationsCount ?? 0,
        dto.App?.Slug);

    private static GitHubCheckAnnotation ToAnnotation(AnnotationDto dto) => new(
        dto.Path ?? string.Empty,
        Math.Max(1, dto.StartLine),
        Math.Max(1, dto.EndLine ?? dto.StartLine),
        dto.StartColumn,
        dto.EndColumn,
        GitHubRunStates.ParseAnnotationLevel(dto.AnnotationLevel),
        NullIfEmpty(dto.Title),
        dto.Message ?? string.Empty,
        NullIfEmpty(dto.RawDetails));

    private static Uri? ToUri(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
