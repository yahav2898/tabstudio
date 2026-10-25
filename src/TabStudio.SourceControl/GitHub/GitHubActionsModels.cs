using System.Globalization;

namespace TabStudio.SourceControl.GitHub;

/// <summary>Whether a workflow runs.</summary>
public enum GitHubWorkflowState
{
    /// <summary>It runs when its triggers fire.</summary>
    Active,

    /// <summary>Someone turned it off.</summary>
    DisabledManually,

    /// <summary>GitHub turned it off because the repository had no activity.</summary>
    DisabledInactivity,

    /// <summary>It is in a fork, where workflows start turned off.</summary>
    DisabledFork,

    /// <summary>Its file was deleted.</summary>
    Deleted,

    /// <summary>A state TabStudio does not know yet.</summary>
    Unknown,
}

/// <summary>A GitHub Actions workflow: one file in <c>.github/workflows</c>.</summary>
/// <param name="Id">The workflow's id.</param>
/// <param name="Name">Its name, from the file's <c>name:</c>, or the file's path.</param>
/// <param name="Path">The file, such as <c>.github/workflows/ci.yml</c>.</param>
/// <param name="State">Whether it runs.</param>
/// <param name="WebUri">Its page on GitHub, if known.</param>
public sealed record GitHubWorkflow(long Id, string Name, string Path, GitHubWorkflowState State, Uri? WebUri)
{
    /// <summary>Whether it runs when its triggers fire, and can be run by hand.</summary>
    public bool IsActive => State == GitHubWorkflowState.Active;
}

/// <summary>One run of a workflow.</summary>
/// <param name="Id">The run's id.</param>
/// <param name="WorkflowId">The workflow it ran.</param>
/// <param name="WorkflowName">The workflow's name.</param>
/// <param name="Title">What GitHub shows for it: usually the commit's or pull request's title.</param>
/// <param name="RunNumber">Its number among the workflow's runs, such as 57 for "#57".</param>
/// <param name="Attempt">Which attempt it is: 1, or more after a re-run.</param>
/// <param name="Event">What started it, such as <c>push</c>, <c>pull_request</c> or <c>workflow_dispatch</c>.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Conclusion">How it ended, or <see langword="null"/> while not completed.</param>
/// <param name="HeadBranch">The branch it ran on, if any.</param>
/// <param name="HeadSha">The commit it ran on.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="StartedAt">When its latest attempt started.</param>
/// <param name="UpdatedAt">When it last changed; the end time once completed.</param>
/// <param name="WebUri">Its page on GitHub.</param>
/// <param name="Actor">Who started it.</param>
/// <param name="CommitMessage">The message of the commit it ran on, if GitHub sent it.</param>
public sealed record GitHubWorkflowRun(
    long Id,
    long WorkflowId,
    string WorkflowName,
    string Title,
    int RunNumber,
    int Attempt,
    string Event,
    GitHubRunStatus Status,
    GitHubConclusion? Conclusion,
    string? HeadBranch,
    string HeadSha,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset UpdatedAt,
    Uri WebUri,
    string? Actor,
    string? CommitMessage)
{
    /// <summary>Whether it finished.</summary>
    public bool IsCompleted => Status == GitHubRunStatus.Completed;

    /// <summary>Whether it finished and failed.</summary>
    public bool IsFailed => IsCompleted && GitHubRunStates.IsFailure(Conclusion);

    /// <summary>How long it ran, or has been running.</summary>
    /// <param name="now">The time now, for a run that has not finished.</param>
    public TimeSpan? Duration(DateTimeOffset now) => GitHubRunStates.Duration(StartedAt ?? CreatedAt, IsCompleted ? UpdatedAt : null, now);
}

/// <summary>A page of workflow runs, newest first.</summary>
/// <param name="TotalCount">How many runs match, over all pages.</param>
/// <param name="Runs">The runs on this page.</param>
public sealed record GitHubWorkflowRunPage(int TotalCount, IReadOnlyList<GitHubWorkflowRun> Runs);

/// <summary>Which workflow runs to list.</summary>
/// <param name="Branch">Only runs on this branch.</param>
/// <param name="Event">Only runs started by this event, such as <c>push</c>.</param>
/// <param name="Status">Only runs with this status or conclusion, such as <c>in_progress</c> or <c>failure</c>.</param>
/// <param name="HeadSha">Only runs of this commit.</param>
/// <param name="WorkflowId">Only runs of this workflow.</param>
/// <param name="PerPage">How many runs to list, at most 100.</param>
public sealed record GitHubRunQuery(
    string? Branch = null,
    string? Event = null,
    string? Status = null,
    string? HeadSha = null,
    long? WorkflowId = null,
    int PerPage = 30);

/// <summary>One job of a workflow run, with its steps.</summary>
/// <param name="Id">The job's id, which is also its check run's id.</param>
/// <param name="RunId">The run it belongs to.</param>
/// <param name="Name">Its name, such as "Build and test" or "test (ubuntu-latest, 9.0)".</param>
/// <param name="Status">Where it is.</param>
/// <param name="Conclusion">How it ended, or <see langword="null"/> while not completed.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="CompletedAt">When it finished.</param>
/// <param name="WebUri">Its page on GitHub.</param>
/// <param name="Steps">Its steps, in order.</param>
public sealed record GitHubJob(
    long Id,
    long RunId,
    string Name,
    GitHubRunStatus Status,
    GitHubConclusion? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    Uri? WebUri,
    IReadOnlyList<GitHubJobStep> Steps)
{
    /// <summary>Whether it finished.</summary>
    public bool IsCompleted => Status == GitHubRunStatus.Completed;

    /// <summary>Whether it finished and failed.</summary>
    public bool IsFailed => IsCompleted && GitHubRunStates.IsFailure(Conclusion);
}

/// <summary>One step of a job.</summary>
/// <param name="Number">Its 1-based number in the job.</param>
/// <param name="Name">Its name, such as "Run actions/checkout@v4" or "Build".</param>
/// <param name="Status">Where it is.</param>
/// <param name="Conclusion">How it ended, or <see langword="null"/> while not completed.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="CompletedAt">When it finished.</param>
public sealed record GitHubJobStep(int Number, string Name, GitHubRunStatus Status, GitHubConclusion? Conclusion, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt)
{
    /// <summary>Whether it finished and failed.</summary>
    public bool IsFailed => Status == GitHubRunStatus.Completed && GitHubRunStates.IsFailure(Conclusion);
}

/// <summary>How much of a job's log to keep, so a huge log cannot exhaust a tablet's memory.</summary>
/// <param name="HeadBytes">Bytes kept from the start of the log.</param>
/// <param name="TailBytes">Bytes kept from the end, where a failure usually is.</param>
public sealed record GitHubJobLogLimits(long HeadBytes, long TailBytes)
{
    /// <summary>What a log shows first: 512 KB from the start and 1.5 MB from the end.</summary>
    public static GitHubJobLogLimits Preview { get; } = new(512 * 1024, 1536 * 1024);

    /// <summary>"Show the full log": up to 24 MB, which the editor still scrolls smoothly.</summary>
    public static GitHubJobLogLimits Full { get; } = new(4L * 1024 * 1024, 20L * 1024 * 1024);
}

/// <summary>A job's log, or its start and end when it is larger than the limits.</summary>
/// <param name="Head">The log, or its start when part of it was left out. Lines are whole.</param>
/// <param name="Tail">The end of the log when part of it was left out; otherwise empty.</param>
/// <param name="TotalBytes">The size of the whole log.</param>
/// <param name="OmittedBytes">How many bytes between <paramref name="Head"/> and <paramref name="Tail"/> were left out.</param>
public sealed record GitHubJobLog(string Head, string Tail, long TotalBytes, long OmittedBytes)
{
    /// <summary>Whether the whole log is in <see cref="Head"/>.</summary>
    public bool IsComplete => OmittedBytes == 0;
}

/// <summary>Reads GitHub Actions' addresses.</summary>
public static class GitHubActionsLinks
{
    /// <summary>
    /// Reads the run id from a run's or job's page, such as
    /// <c>https://github.com/octocat/hello/actions/runs/123/job/456</c>.
    /// </summary>
    /// <param name="uri">The page.</param>
    /// <returns>The run id, or <see langword="null"/> when the address is not a run's.</returns>
    public static long? TryParseRunId(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 2 < segments.Length; i++)
        {
            if (segments[i] == "actions" && segments[i + 1] == "runs"
                && long.TryParse(segments[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }

        return null;
    }
}
