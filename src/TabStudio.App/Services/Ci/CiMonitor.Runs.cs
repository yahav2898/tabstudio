using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>A workflow run that finished, and where.</summary>
/// <param name="Owner">The repository's owner.</param>
/// <param name="Repository">The repository's name.</param>
/// <param name="Run">The run, as it finished.</param>
/// <param name="StartedHere">Whether it was started or re-run from TabStudio, rather than found on the workspace's branch.</param>
public sealed record CiRunFinished(string Owner, string Repository, GitHubWorkflowRun Run, bool StartedHere);

/// <summary>
/// Runs that finish: those on the workspace's branch, and those started or re-run from TabStudio in any
/// repository. Runs that had already finished when the monitor first looked are not reported.
/// </summary>
public sealed partial class CiMonitor
{
    private const int RememberedRuns = 200;
    private static readonly TimeSpan DispatchSearchTime = TimeSpan.FromMinutes(5);

    private readonly Dictionary<long, bool> _branchRunsCompleted = [];
    private readonly Dictionary<long, WatchedRun> _watched = [];
    private readonly List<PendingDispatch> _dispatches = [];
    private DateTimeOffset _baselineAt;
    private bool _hasBaseline;

    /// <summary>Raised when a run on the target's branch, or one started from TabStudio, finishes.</summary>
    public event EventHandler<CiRunFinished>? RunFinished;

    /// <summary>Whether runs started from TabStudio are still in progress, or still to be found after a dispatch.</summary>
    public bool HasRunsInProgress => _watched.Count > 0 || _dispatches.Count > 0;

    /// <summary>Follows a run started or re-run from TabStudio until it finishes.</summary>
    /// <param name="owner">The repository's owner.</param>
    /// <param name="repository">The repository's name.</param>
    /// <param name="run">The run as it was before it was re-run; a later attempt finishing reports it.</param>
    /// <param name="isRerun">Whether the run was re-run, so only a newer attempt counts.</param>
    public void Watch(string owner, string repository, GitHubWorkflowRun run, bool isRerun)
    {
        ArgumentNullException.ThrowIfNull(run);
        _watched[run.Id] = new WatchedRun(owner, repository, run.Id, isRerun ? run.Attempt + 1 : run.Attempt);
        RequestRefresh();
    }

    /// <summary>Follows the run a workflow dispatch starts, once GitHub has created it.</summary>
    /// <param name="owner">The repository's owner.</param>
    /// <param name="repository">The repository's name.</param>
    /// <param name="workflowId">The workflow.</param>
    /// <param name="reference">The branch or tag it runs on.</param>
    public void WatchDispatch(string owner, string repository, long workflowId, string reference)
    {
        _dispatches.Add(new PendingDispatch(owner, repository, workflowId, reference, _time.GetUtcNow()));
        RequestRefresh();
    }

    private void ResetRunBaseline()
    {
        _branchRunsCompleted.Clear();
        _hasBaseline = false;
    }

    /// <summary>Finds the runs dispatches started, and reads every followed run.</summary>
    private async Task<List<GitHubWorkflowRun>> ReadWatchedRunsAsync(GitHubClient client, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        foreach (var dispatch in _dispatches.ToList())
        {
            if (now - dispatch.Since > DispatchSearchTime)
            {
                _dispatches.Remove(dispatch);
                continue;
            }

            var query = new GitHubRunQuery(Branch: dispatch.Reference, Event: "workflow_dispatch", WorkflowId: dispatch.WorkflowId, PerPage: 5);
            var page = await client.GetWorkflowRunsAsync(dispatch.Owner, dispatch.Repository, query, cancellationToken);
            var started = page.Runs
                .Where(r => r.CreatedAt >= dispatch.Since - TimeSpan.FromMinutes(1) && !_watched.ContainsKey(r.Id))
                .MinBy(r => r.CreatedAt);
            if (started is not null)
            {
                _dispatches.Remove(dispatch);
                _watched[started.Id] = new WatchedRun(dispatch.Owner, dispatch.Repository, started.Id, 1);
            }
        }

        var runs = new List<GitHubWorkflowRun>();
        foreach (var watched in _watched.Values.ToList())
        {
            try
            {
                runs.Add(await client.GetWorkflowRunAsync(watched.Owner, watched.Repository, watched.RunId, cancellationToken));
            }
            catch (GitHubApiException ex) when (ex.IsNotFound)
            {
                // Deleted, or no longer visible: nothing will finish.
                _watched.Remove(watched.RunId);
            }
        }

        return runs;
    }

    /// <summary>Reports the runs that finished since the last poll, each once.</summary>
    private void ReportFinishedRuns(CiTarget? target, IReadOnlyList<GitHubWorkflowRun> branchRuns, List<GitHubWorkflowRun> watchedRuns)
    {
        var finished = new List<CiRunFinished>();
        foreach (var run in watchedRuns)
        {
            if (_watched.TryGetValue(run.Id, out var watched) && run.IsCompleted && run.Attempt >= watched.MinimumAttempt)
            {
                _watched.Remove(run.Id);
                finished.Add(new CiRunFinished(watched.Owner, watched.Repository, run, StartedHere: true));
            }
        }

        if (target is not null)
        {
            ReportFinishedBranchRuns(target, branchRuns, finished);
        }

        if (_branchRunsCompleted.Count > RememberedRuns)
        {
            var current = branchRuns.Select(r => r.Id).ToHashSet();
            foreach (var id in _branchRunsCompleted.Keys.Where(id => !current.Contains(id)).ToList())
            {
                _branchRunsCompleted.Remove(id);
            }
        }

        foreach (var run in finished)
        {
            RunFinished?.Invoke(this, run);
        }
    }

    /// <summary>
    /// Adds the runs on the branch that finished since the last poll: those seen running before, and new ones that
    /// finished after the monitor first looked.
    /// </summary>
    private void ReportFinishedBranchRuns(CiTarget target, IReadOnlyList<GitHubWorkflowRun> branchRuns, List<CiRunFinished> finished)
    {
        foreach (var run in branchRuns)
        {
            var known = _branchRunsCompleted.TryGetValue(run.Id, out var wasCompleted);
            _branchRunsCompleted[run.Id] = run.IsCompleted;
            if (!run.IsCompleted || _watched.ContainsKey(run.Id) || finished.Exists(f => f.Run.Id == run.Id && f.Repository == target.Repository))
            {
                continue;
            }

            var justFinished = known ? !wasCompleted : _hasBaseline && run.UpdatedAt >= _baselineAt;
            if (justFinished)
            {
                finished.Add(new CiRunFinished(target.Owner, target.Repository, run, StartedHere: false));
            }
        }

        if (!_hasBaseline)
        {
            _hasBaseline = true;
            _baselineAt = _time.GetUtcNow();
        }
    }

    /// <summary>A run followed until an attempt at least as new as <paramref name="MinimumAttempt"/> finishes.</summary>
    private sealed record WatchedRun(string Owner, string Repository, long RunId, int MinimumAttempt);

    /// <summary>A dispatch whose run GitHub has not shown yet.</summary>
    private sealed record PendingDispatch(string Owner, string Repository, long WorkflowId, string Reference, DateTimeOffset Since);
}
