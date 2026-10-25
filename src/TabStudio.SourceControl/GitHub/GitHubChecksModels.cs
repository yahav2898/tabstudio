namespace TabStudio.SourceControl.GitHub;

/// <summary>Where a check run, workflow run, job or step is in its life.</summary>
public enum GitHubRunStatus
{
    /// <summary>Waiting for a runner.</summary>
    Queued,

    /// <summary>Running now.</summary>
    InProgress,

    /// <summary>Finished; see its conclusion.</summary>
    Completed,

    /// <summary>Waiting for an approval or a protection rule, such as a deployment environment's reviewers.</summary>
    Waiting,

    /// <summary>Requested, not yet queued.</summary>
    Requested,

    /// <summary>Pending, behind another run of the same concurrency group.</summary>
    Pending,

    /// <summary>A state TabStudio does not know yet.</summary>
    Unknown,
}

/// <summary>How a completed check run, workflow run, job or step ended.</summary>
public enum GitHubConclusion
{
    /// <summary>It passed.</summary>
    Success,

    /// <summary>It failed.</summary>
    Failure,

    /// <summary>It finished without passing or failing; counted as passing.</summary>
    Neutral,

    /// <summary>Someone or something cancelled it.</summary>
    Cancelled,

    /// <summary>It did not run, for example because its condition was false.</summary>
    Skipped,

    /// <summary>It ran out of time.</summary>
    TimedOut,

    /// <summary>It needs someone to act on GitHub before it can finish.</summary>
    ActionRequired,

    /// <summary>GitHub marked it out of date.</summary>
    Stale,

    /// <summary>The workflow could not start, for example because its file is invalid.</summary>
    StartupFailure,

    /// <summary>A conclusion TabStudio does not know yet.</summary>
    Unknown,
}

/// <summary>How serious a check run annotation is.</summary>
public enum GitHubAnnotationLevel
{
    /// <summary>Information.</summary>
    Notice,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>An error.</summary>
    Failure,
}

/// <summary>The state of a commit status from an external CI service, or of all of a commit's statuses together.</summary>
public enum GitHubStatusState
{
    /// <summary>Still running, or not reported yet.</summary>
    Pending,

    /// <summary>Passed.</summary>
    Success,

    /// <summary>Failed.</summary>
    Failure,

    /// <summary>The service could not run it.</summary>
    Error,
}

/// <summary>What the statuses and conclusions GitHub reports mean.</summary>
public static class GitHubRunStates
{
    /// <summary>Whether a conclusion means the work failed: failure, timed out, could not start, or needs action.</summary>
    /// <param name="conclusion">The conclusion, or <see langword="null"/> while not completed.</param>
    public static bool IsFailure(GitHubConclusion? conclusion) =>
        conclusion is GitHubConclusion.Failure or GitHubConclusion.TimedOut or GitHubConclusion.StartupFailure or GitHubConclusion.ActionRequired;

    /// <summary>Whether a status means the work has not finished: queued, running, waiting, requested or pending.</summary>
    /// <param name="status">The status.</param>
    public static bool IsActive(GitHubRunStatus status) => status != GitHubRunStatus.Completed && status != GitHubRunStatus.Unknown;

    /// <summary>Reads a status as the API writes it, such as <c>in_progress</c>.</summary>
    /// <param name="value">The API's value.</param>
    public static GitHubRunStatus ParseStatus(string? value) => value switch
    {
        "queued" => GitHubRunStatus.Queued,
        "in_progress" => GitHubRunStatus.InProgress,
        "completed" => GitHubRunStatus.Completed,
        "waiting" => GitHubRunStatus.Waiting,
        "requested" => GitHubRunStatus.Requested,
        "pending" => GitHubRunStatus.Pending,
        _ => GitHubRunStatus.Unknown,
    };

    /// <summary>Reads a conclusion as the API writes it, such as <c>timed_out</c>.</summary>
    /// <param name="value">The API's value, or <see langword="null"/> while not completed.</param>
    public static GitHubConclusion? ParseConclusion(string? value) => value switch
    {
        null or "" => null,
        "success" => GitHubConclusion.Success,
        "failure" => GitHubConclusion.Failure,
        "neutral" => GitHubConclusion.Neutral,
        "cancelled" => GitHubConclusion.Cancelled,
        "skipped" => GitHubConclusion.Skipped,
        "timed_out" => GitHubConclusion.TimedOut,
        "action_required" => GitHubConclusion.ActionRequired,
        "stale" => GitHubConclusion.Stale,
        "startup_failure" => GitHubConclusion.StartupFailure,
        _ => GitHubConclusion.Unknown,
    };

    /// <summary>Reads a commit status state, such as <c>error</c>.</summary>
    /// <param name="value">The API's value.</param>
    public static GitHubStatusState ParseStatusState(string? value) => value switch
    {
        "success" => GitHubStatusState.Success,
        "failure" => GitHubStatusState.Failure,
        "error" => GitHubStatusState.Error,
        _ => GitHubStatusState.Pending,
    };

    /// <summary>Reads an annotation level, such as <c>failure</c>.</summary>
    /// <param name="value">The API's value.</param>
    public static GitHubAnnotationLevel ParseAnnotationLevel(string? value) => value switch
    {
        "failure" => GitHubAnnotationLevel.Failure,
        "warning" => GitHubAnnotationLevel.Warning,
        _ => GitHubAnnotationLevel.Notice,
    };

    /// <summary>How long something ran, when it has started.</summary>
    /// <param name="startedAt">When it started.</param>
    /// <param name="completedAt">When it finished, or <see langword="null"/> while it runs.</param>
    /// <param name="now">The time now, for work that is still running.</param>
    public static TimeSpan? Duration(DateTimeOffset? startedAt, DateTimeOffset? completedAt, DateTimeOffset now)
    {
        if (startedAt is not { } start)
        {
            return null;
        }

        var span = (completedAt ?? now) - start;
        return span > TimeSpan.Zero ? span : TimeSpan.Zero;
    }
}

/// <summary>
/// A check run on a commit: one job of a GitHub Actions workflow, or a check of another app, such as a code
/// scanner. For GitHub Actions, the check run's id is the job's id.
/// </summary>
/// <param name="Id">The check run's id.</param>
/// <param name="Name">Its name, such as "Build and test".</param>
/// <param name="Status">Where it is.</param>
/// <param name="Conclusion">How it ended, or <see langword="null"/> while not completed.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="CompletedAt">When it finished.</param>
/// <param name="WebUri">Its page on GitHub.</param>
/// <param name="DetailsUri">The app's page for it: the job's page for GitHub Actions.</param>
/// <param name="Title">The title of its output, if the app wrote one.</param>
/// <param name="Summary">The summary of its output, in Markdown, if the app wrote one.</param>
/// <param name="AnnotationCount">How many annotations it has.</param>
/// <param name="App">The slug of the app that runs it, such as <c>github-actions</c>.</param>
public sealed record GitHubCheckRun(
    long Id,
    string Name,
    GitHubRunStatus Status,
    GitHubConclusion? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    Uri? WebUri,
    Uri? DetailsUri,
    string? Title,
    string? Summary,
    int AnnotationCount,
    string? App)
{
    /// <summary>The slug of GitHub Actions' app.</summary>
    public const string GitHubActionsApp = "github-actions";

    /// <summary>Whether it finished.</summary>
    public bool IsCompleted => Status == GitHubRunStatus.Completed;

    /// <summary>Whether it finished and failed.</summary>
    public bool IsFailed => IsCompleted && GitHubRunStates.IsFailure(Conclusion);

    /// <summary>Whether it is a job of a GitHub Actions workflow, whose log TabStudio can show.</summary>
    public bool IsGitHubActionsJob => string.Equals(App, GitHubActionsApp, StringComparison.Ordinal);

    /// <summary>The workflow run the job belongs to, for GitHub Actions: read from its page's address.</summary>
    public long? WorkflowRunId => GitHubActionsLinks.TryParseRunId(DetailsUri ?? WebUri);
}

/// <summary>A message a check run attached to a line of a file, such as a compiler error.</summary>
/// <param name="Path">The file, relative to the repository's root, with <c>/</c> separators.</param>
/// <param name="StartLine">The 1-based first line.</param>
/// <param name="EndLine">The 1-based last line.</param>
/// <param name="StartColumn">The 1-based first column, when known.</param>
/// <param name="EndColumn">The 1-based last column, when known.</param>
/// <param name="Level">How serious it is.</param>
/// <param name="Title">A short title, if any.</param>
/// <param name="Message">The message.</param>
/// <param name="RawDetails">More details, such as the tool's original output line, if any.</param>
public sealed record GitHubCheckAnnotation(
    string Path,
    int StartLine,
    int EndLine,
    int? StartColumn,
    int? EndColumn,
    GitHubAnnotationLevel Level,
    string? Title,
    string Message,
    string? RawDetails)
{
    /// <summary>The compiler message read from the annotation: its code, such as CS0103, and its text.</summary>
    public CompilerMessage Compiler => CompilerMessage.FromAnnotation(Title, Message, RawDetails);
}

/// <summary>A status an external CI service, or any tool using the statuses API, set on a commit.</summary>
/// <param name="Context">Which check it is, such as <c>ci/circleci: build</c>.</param>
/// <param name="State">Its state.</param>
/// <param name="Description">A short description, if any.</param>
/// <param name="TargetUri">The service's page for it, if any.</param>
/// <param name="UpdatedAt">When it was last set.</param>
public sealed record GitHubCommitStatus(string Context, GitHubStatusState State, string? Description, Uri? TargetUri, DateTimeOffset? UpdatedAt);

/// <summary>A commit's statuses together.</summary>
/// <param name="State">The combined state: failure if any failed, pending if any is pending, otherwise success.</param>
/// <param name="Statuses">The latest status of each context.</param>
public sealed record GitHubCombinedStatus(GitHubStatusState State, IReadOnlyList<GitHubCommitStatus> Statuses)
{
    /// <summary>Whether any tool reported a status. GitHub calls a commit without statuses "pending".</summary>
    public bool HasStatuses => Statuses.Count > 0;
}

/// <summary>GitHub's request limit, as the last response reported it.</summary>
/// <param name="Limit">Requests allowed in the hour: 5,000 signed in, 60 signed out.</param>
/// <param name="Remaining">Requests left until <paramref name="Reset"/>.</param>
/// <param name="Reset">When the limit starts again.</param>
public sealed record GitHubRateLimit(int Limit, int Remaining, DateTimeOffset Reset)
{
    /// <summary>Whether no requests are left.</summary>
    public bool IsExhausted => Remaining <= 0;
}
