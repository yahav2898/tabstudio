using System.Globalization;
using System.Net.Http.Json;
using System.Text;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// GitHub Actions: a repository's workflows, their runs, the jobs and steps of a run, and re-running, cancelling
/// and starting runs. Reads are conditional, so polling them is cheap.
/// </summary>
/// <remarks>
/// Re-running, cancelling and starting runs need a token with the <c>repo</c> scope (classic) or the
/// "Actions: read and write" permission (fine-grained).
/// </remarks>
public sealed partial class GitHubClient
{
    /// <summary>Lists a repository's workflows.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<GitHubWorkflow>> GetWorkflowsAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        var dto = await GetCachedAsync($"{Repo(owner, repository)}/actions/workflows?per_page=100", GitHubActionsJsonContext.Default.WorkflowsDto, cancellationToken).ConfigureAwait(false);
        return [.. dto.Workflows.Select(w => new GitHubWorkflow(w.Id, string.IsNullOrWhiteSpace(w.Name) ? w.Path : w.Name, w.Path, ParseWorkflowState(w.State), ToUri(w.HtmlUrl)))];
    }

    /// <summary>Lists workflow runs, newest first.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="query">Which runs; all of them when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubWorkflowRunPage> GetWorkflowRunsAsync(string owner, string repository, GitHubRunQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new GitHubRunQuery();
        var path = new StringBuilder(Repo(owner, repository));
        path.Append(query.WorkflowId is { } workflow ? $"/actions/workflows/{workflow.ToString(CultureInfo.InvariantCulture)}/runs" : "/actions/runs");
        path.Append(CultureInfo.InvariantCulture, $"?per_page={Math.Clamp(query.PerPage, 1, 100)}");
        AppendFilter(path, "branch", query.Branch);
        AppendFilter(path, "event", query.Event);
        AppendFilter(path, "status", query.Status);
        AppendFilter(path, "head_sha", query.HeadSha);
        var dto = await GetCachedAsync(path.ToString(), GitHubActionsJsonContext.Default.WorkflowRunsDto, cancellationToken).ConfigureAwait(false);
        return new GitHubWorkflowRunPage(dto.TotalCount, [.. dto.WorkflowRuns.Select(ToWorkflowRun)]);
    }

    /// <summary>Reads a workflow run.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubWorkflowRun> GetWorkflowRunAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default)
    {
        var dto = await GetCachedAsync(RunPath(owner, repository, runId), GitHubActionsJsonContext.Default.WorkflowRunDto, cancellationToken).ConfigureAwait(false);
        return ToWorkflowRun(dto);
    }

    /// <summary>Lists the jobs of a run's latest attempt, with their steps.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<GitHubJob>> GetJobsAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default)
    {
        var jobs = new List<GitHubJob>();
        for (var page = 1; ; page++)
        {
            var path = $"{RunPath(owner, repository, runId)}/jobs?per_page=100&page={page.ToString(CultureInfo.InvariantCulture)}";
            var dto = await GetCachedAsync(path, GitHubActionsJsonContext.Default.JobsDto, cancellationToken).ConfigureAwait(false);
            jobs.AddRange(dto.Jobs.Select(ToJob));
            if (dto.Jobs.Count < 100 || jobs.Count >= dto.TotalCount)
            {
                return jobs;
            }
        }
    }

    /// <summary>Runs every job of a completed run again, as a new attempt.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task RerunWorkflowAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default) =>
        SendWithoutAnswerAsync(HttpMethod.Post, $"{RunPath(owner, repository, runId)}/rerun", null, cancellationToken);

    /// <summary>Runs the failed jobs of a completed run again, and the jobs that depend on them.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task RerunFailedJobsAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default) =>
        SendWithoutAnswerAsync(HttpMethod.Post, $"{RunPath(owner, repository, runId)}/rerun-failed-jobs", null, cancellationToken);

    /// <summary>Cancels a run that has not finished.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task CancelWorkflowRunAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default) =>
        SendWithoutAnswerAsync(HttpMethod.Post, $"{RunPath(owner, repository, runId)}/cancel", null, cancellationToken);

    /// <summary>Starts a workflow that has a <c>workflow_dispatch</c> trigger.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="workflowId">The workflow.</param>
    /// <param name="reference">The branch or tag whose version of the workflow runs.</param>
    /// <param name="inputs">The values of its inputs, by input name; inputs left out take their defaults.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task DispatchWorkflowAsync(string owner, string repository, long workflowId, string reference, IReadOnlyDictionary<string, string>? inputs = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reference);
        var request = new DispatchRequest(reference, inputs is { Count: > 0 } ? inputs : null);
        var path = $"{Repo(owner, repository)}/actions/workflows/{workflowId.ToString(CultureInfo.InvariantCulture)}/dispatches";
        return SendWithoutAnswerAsync(HttpMethod.Post, path, JsonContent.Create(request, GitHubActionsJsonContext.Default.DispatchRequest), cancellationToken);
    }

    private static string RunPath(string owner, string repository, long runId) =>
        $"{Repo(owner, repository)}/actions/runs/{runId.ToString(CultureInfo.InvariantCulture)}";

    private static void AppendFilter(StringBuilder path, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            path.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }
    }

    private static GitHubWorkflowState ParseWorkflowState(string? state) => state switch
    {
        "active" => GitHubWorkflowState.Active,
        "disabled_manually" => GitHubWorkflowState.DisabledManually,
        "disabled_inactivity" => GitHubWorkflowState.DisabledInactivity,
        "disabled_fork" => GitHubWorkflowState.DisabledFork,
        "deleted" => GitHubWorkflowState.Deleted,
        _ => GitHubWorkflowState.Unknown,
    };

    private static GitHubWorkflowRun ToWorkflowRun(WorkflowRunDto dto)
    {
        var commitMessage = dto.HeadCommit?.Message;
        var title = !string.IsNullOrWhiteSpace(dto.DisplayTitle) ? dto.DisplayTitle
            : !string.IsNullOrWhiteSpace(commitMessage) ? commitMessage.Split('\n', 2)[0].TrimEnd('\r')
            : dto.Name ?? string.Empty;
        return new GitHubWorkflowRun(
            dto.Id,
            dto.WorkflowId,
            dto.Name ?? string.Empty,
            title,
            dto.RunNumber,
            Math.Max(1, dto.RunAttempt ?? 1),
            dto.Event ?? string.Empty,
            GitHubRunStates.ParseStatus(dto.Status),
            GitHubRunStates.ParseConclusion(dto.Conclusion),
            NullIfEmpty(dto.HeadBranch),
            dto.HeadSha ?? string.Empty,
            dto.CreatedAt,
            dto.RunStartedAt,
            dto.UpdatedAt,
            new Uri(dto.HtmlUrl),
            dto.TriggeringActor?.Login ?? dto.Actor?.Login,
            commitMessage);
    }

    private static GitHubJob ToJob(JobDto dto) => new(
        dto.Id,
        dto.RunId,
        dto.Name,
        GitHubRunStates.ParseStatus(dto.Status),
        GitHubRunStates.ParseConclusion(dto.Conclusion),
        dto.StartedAt,
        dto.CompletedAt,
        ToUri(dto.HtmlUrl),
        [.. (dto.Steps ?? []).OrderBy(s => s.Number).Select(s => new GitHubJobStep(
            s.Number,
            s.Name,
            GitHubRunStates.ParseStatus(s.Status),
            GitHubRunStates.ParseConclusion(s.Conclusion),
            s.StartedAt,
            s.CompletedAt))]);
}
