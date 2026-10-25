using System.Globalization;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>The state of a commit's CI, all its checks together.</summary>
public enum CiState
{
    /// <summary>No check has reported on the commit.</summary>
    None,

    /// <summary>Checks are queued, none has started.</summary>
    Pending,

    /// <summary>Checks are running.</summary>
    Running,

    /// <summary>Every check finished without failing.</summary>
    Passed,

    /// <summary>Every check finished and at least one failed.</summary>
    Failed,

    /// <summary>Every check finished, none failed, and at least one was cancelled.</summary>
    Cancelled,
}

/// <summary>Where a check comes from.</summary>
public enum CiCheckKind
{
    /// <summary>A GitHub Actions job, whose log TabStudio shows.</summary>
    Job,

    /// <summary>A check run of another GitHub app, such as a code scanner.</summary>
    CheckRun,

    /// <summary>A commit status from an external CI service.</summary>
    Status,
}

/// <summary>One check of a commit: a check run or a commit status, in the same terms.</summary>
/// <param name="Name">Its name, or the status's context.</param>
/// <param name="Kind">Where it comes from.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Conclusion">How it ended, or <see langword="null"/> while not finished.</param>
/// <param name="StartedAt">When it started, if known.</param>
/// <param name="CompletedAt">When it finished, if known.</param>
/// <param name="WebUri">Its page, if it has one.</param>
/// <param name="Description">A short description from the tool, if any.</param>
/// <param name="CheckRun">The check run, for checks that are check runs.</param>
public sealed record CiCheck(
    string Name,
    CiCheckKind Kind,
    GitHubRunStatus Status,
    GitHubConclusion? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    Uri? WebUri,
    string? Description,
    GitHubCheckRun? CheckRun)
{
    /// <summary>Whether it finished.</summary>
    public bool IsDone => Status == GitHubRunStatus.Completed;

    /// <summary>Whether it finished and failed.</summary>
    public bool IsFailed => IsDone && GitHubRunStates.IsFailure(Conclusion);

    /// <summary>Whether it was cancelled.</summary>
    public bool IsCancelled => IsDone && Conclusion == GitHubConclusion.Cancelled;

    /// <summary>Whether it is running or finished, rather than waiting to start.</summary>
    public bool HasStarted => IsDone || Status == GitHubRunStatus.InProgress;

    /// <summary>Reads a check run.</summary>
    /// <param name="run">The check run.</param>
    public static CiCheck From(GitHubCheckRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new CiCheck(run.Name, run.IsGitHubActionsJob ? CiCheckKind.Job : CiCheckKind.CheckRun, run.Status, run.Conclusion, run.StartedAt, run.CompletedAt, run.DetailsUri ?? run.WebUri, run.Title, run);
    }

    /// <summary>Reads a commit status: pending means running, error means failed.</summary>
    /// <param name="status">The status.</param>
    public static CiCheck From(GitHubCommitStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var (state, conclusion) = status.State switch
        {
            GitHubStatusState.Success => (GitHubRunStatus.Completed, (GitHubConclusion?)GitHubConclusion.Success),
            GitHubStatusState.Failure or GitHubStatusState.Error => (GitHubRunStatus.Completed, GitHubConclusion.Failure),
            _ => (GitHubRunStatus.InProgress, null),
        };
        return new CiCheck(status.Context, CiCheckKind.Status, state, conclusion, null, state == GitHubRunStatus.Completed ? status.UpdatedAt : null, status.TargetUri, status.Description, null);
    }
}

/// <summary>A commit's CI: its state and the checks behind it, failed ones first.</summary>
/// <param name="Sha">The commit.</param>
/// <param name="State">All checks together.</param>
/// <param name="Checks">The checks.</param>
public sealed record CiSummary(string Sha, CiState State, IReadOnlyList<CiCheck> Checks)
{
    /// <summary>How many checks there are.</summary>
    public int Total => Checks.Count;

    /// <summary>How many checks finished.</summary>
    public int Done => Checks.Count(c => c.IsDone);

    /// <summary>How many checks failed.</summary>
    public int FailedCount => Checks.Count(c => c.IsFailed);

    /// <summary>The first seven characters of the commit's hash.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>"2 of 5 checks done · 1 failed", "All 5 checks passed", or that none has reported.</summary>
    public string Description
    {
        get
        {
            var culture = CultureInfo.CurrentCulture;
            var total = Total.ToString(culture);
            return State switch
            {
                CiState.None => "No checks have reported on this commit yet.",
                CiState.Pending => Total == 1 ? "1 check queued" : $"{total} checks queued",
                CiState.Running => FailedCount > 0
                    ? $"{Done.ToString(culture)} of {total} checks done · {FailedCount.ToString(culture)} failed"
                    : $"{Done.ToString(culture)} of {total} checks done",
                CiState.Passed => Total == 1 ? "The check passed" : $"All {total} checks passed",
                CiState.Failed => $"{FailedCount.ToString(culture)} of {total} checks failed",
                _ => $"Cancelled · {Done.ToString(culture)} of {total} checks done",
            };
        }
    }

    /// <summary>A commit no check has reported on.</summary>
    /// <param name="sha">The commit.</param>
    public static CiSummary Empty(string sha) => new(sha, CiState.None, []);

    /// <summary>
    /// Puts a commit's check runs and statuses together: running while any has not finished (pending while none
    /// has started), then failed if any failed, cancelled if any was cancelled, and otherwise passed.
    /// </summary>
    /// <param name="sha">The commit.</param>
    /// <param name="checkRuns">Its check runs.</param>
    /// <param name="statuses">Its statuses from external CI services.</param>
    public static CiSummary From(string sha, IReadOnlyList<GitHubCheckRun> checkRuns, GitHubCombinedStatus? statuses)
    {
        ArgumentNullException.ThrowIfNull(sha);
        ArgumentNullException.ThrowIfNull(checkRuns);
        var checks = checkRuns
            .Where(c => c.Conclusion != GitHubConclusion.Stale)
            .Select(CiCheck.From)
            .Concat((statuses?.Statuses ?? []).Select(CiCheck.From))
            .OrderBy(Rank)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var state = checks.Count == 0 ? CiState.None
            : checks.Any(c => !c.IsDone) ? (checks.Any(c => c.HasStarted) ? CiState.Running : CiState.Pending)
            : checks.Any(c => c.IsFailed) ? CiState.Failed
            : checks.Any(c => c.IsCancelled) ? CiState.Cancelled
            : CiState.Passed;
        return new CiSummary(sha, state, checks);
    }

    /// <summary>Failed checks first, then running, waiting, and finished ones.</summary>
    private static int Rank(CiCheck check) =>
        check.IsFailed ? 0
        : check.Status == GitHubRunStatus.InProgress ? 1
        : !check.IsDone ? 2
        : check.IsCancelled ? 3
        : 4;
}
