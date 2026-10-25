using TabStudio.App.Platform;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>The repository, branch and commit whose CI is watched.</summary>
/// <param name="Owner">The repository's owner.</param>
/// <param name="Repository">The repository's name.</param>
/// <param name="Branch">The branch the workspace follows, or <see langword="null"/> when it holds a tag or commit.</param>
/// <param name="Sha">The commit the workspace's files are at.</param>
public sealed record CiTarget(string Owner, string Repository, string? Branch, string Sha)
{
    /// <summary>"owner/repository".</summary>
    public string FullName => $"{Owner}/{Repository}";
}

/// <summary>An annotation and the check run that made it.</summary>
/// <param name="Check">The check run.</param>
/// <param name="Annotation">The annotation.</param>
public sealed record CiAnnotation(GitHubCheckRun Check, GitHubCheckAnnotation Annotation);

/// <summary>
/// Watches the CI of the workspace's commit: aggregates its check runs and statuses into one state, reads the
/// failed checks' annotations, follows the runs on its branch and the runs started from TabStudio, and reports
/// runs that finish. Testable without a UI: <see cref="RefreshAsync"/> polls once.
/// </summary>
/// <remarks>
/// <para>
/// Polling follows <see cref="CiPollSchedule"/>: every 12 seconds while something runs, every 90 otherwise, less
/// often signed out, and not until the limit resets once GitHub's request limit is reached. Every read is
/// conditional, so a poll where nothing changed costs no requests from the limit.
/// </para>
/// <para>
/// In the background the monitor rests, unless <see cref="KeepsPollingInBackground"/> is set and a run someone
/// waits for is in progress: then it polls once a minute, so its notification arrives.
/// </para>
/// <para>Create, use and dispose it on one thread (the UI thread in the IDE); <see cref="Changed"/> is raised there.</para>
/// </remarks>
public sealed partial class CiMonitor : IDisposable
{
    private readonly Func<GitHubClient> _client;
    private readonly IAppActivity _activity;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<long, IReadOnlyList<GitHubCheckAnnotation>> _annotationsByCheck = [];
    private CancellationTokenSource? _wake;
    private DateTimeOffset _targetChangedAt = DateTimeOffset.MinValue;
    private bool _lastPollFailed;
    private bool _started;
    private bool _disposed;

    /// <summary>Creates the monitor. Call <see cref="Start"/> to poll on a schedule.</summary>
    /// <param name="client">Returns the GitHub client to use: the same one each time, so its conditional reads work.</param>
    /// <param name="activity">Whether the app is in the background.</param>
    /// <param name="time">The clock; the system's when <see langword="null"/>.</param>
    public CiMonitor(Func<GitHubClient> client, IAppActivity activity, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(activity);
        _client = client;
        _activity = activity;
        _time = time ?? TimeProvider.System;
        _activity.Changed += OnActivityChanged;
    }

    /// <summary>Raised after a poll or a change of target, on the thread that polled.</summary>
    public event EventHandler? Changed;

    /// <summary>The commit watched, or <see langword="null"/> when the workspace is not linked to GitHub.</summary>
    public CiTarget? Target { get; private set; }

    /// <summary>The CI of <see cref="Target"/>, or <see langword="null"/> until it has been read.</summary>
    public CiSummary? Summary { get; private set; }

    /// <summary>The annotations of the target's failed checks, once any check has failed.</summary>
    public IReadOnlyList<CiAnnotation> Annotations { get; private set; } = [];

    /// <summary>The latest runs on the target's branch, newest first.</summary>
    public IReadOnlyList<GitHubWorkflowRun> BranchRuns { get; private set; } = [];

    /// <summary>Why the last poll failed, for the user; <see langword="null"/> when it worked.</summary>
    public string? ErrorText { get; private set; }

    /// <summary>When CI was last read.</summary>
    public DateTimeOffset? LastUpdated { get; private set; }

    /// <summary>Whether to keep polling in the background while runs someone waits for are in progress.</summary>
    public bool KeepsPollingInBackground { get; set; }

    /// <summary>Whether anything watched is in progress, which makes the monitor poll often.</summary>
    public bool IsActive =>
        Summary?.State is CiState.Pending or CiState.Running
        || BranchRuns.Any(r => !r.IsCompleted)
        || HasRunsInProgress
        || _time.GetUtcNow() - _targetChangedAt < CiPollSchedule.AfterNewCommit;

    /// <summary>Watches another commit, or nothing; asks GitHub right away when the target changed.</summary>
    /// <param name="target">The commit, or <see langword="null"/> to stop watching.</param>
    public void SetTarget(CiTarget? target)
    {
        if (target == Target)
        {
            return;
        }

        var previous = Target;
        Target = target;
        if (previous is null || target is null || previous.FullName != target.FullName)
        {
            _annotationsByCheck.Clear();
        }

        if (previous?.Sha != target?.Sha || previous?.FullName != target?.FullName)
        {
            Summary = null;
            Annotations = [];
            ErrorText = null;
            _targetChangedAt = _time.GetUtcNow();
        }

        if (previous?.Branch != target?.Branch || previous?.FullName != target?.FullName)
        {
            BranchRuns = [];
            ResetRunBaseline();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        RequestRefresh();
    }

    /// <summary>Starts polling on the schedule. Polls right away.</summary>
    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        _ = RunAsync();
    }

    /// <summary>Asks GitHub again as soon as possible, such as after a commit or when the window opens.</summary>
    public void RequestRefresh() => _wake?.Cancel();

    /// <summary>Polls once now: the target's checks and statuses, their annotations when something failed, and runs.</summary>
    public async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _gate.WaitAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await PollAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Disposed while polling.
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // The gate is left to the garbage collector: a poll that is still finishing must be able to release it.
        _disposed = true;
        _activity.Changed -= OnActivityChanged;
        _lifetime.Cancel();
        _wake?.Dispose();
        _wake = null;
    }

    private async Task RunAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            var awaited = HasRunsInProgress || BranchRuns.Any(r => !r.IsCompleted);
            var resting = _activity.IsInBackground && !(KeepsPollingInBackground && awaited);
            var watching = Target is not null || HasRunsInProgress;
            if (watching && !resting)
            {
                await RefreshAsync();
            }

            await WaitAsync(resting || !(Target is not null || HasRunsInProgress) ? Timeout.InfiniteTimeSpan : NextDelay());
        }
    }

    private TimeSpan NextDelay()
    {
        var client = _client();
        if (_activity.IsInBackground)
        {
            return CiPollSchedule.Background;
        }

        return CiPollSchedule.Next(IsActive, client.IsSignedIn, client.RateLimit, _lastPollFailed, _time.GetUtcNow());
    }

    /// <summary>Waits for the delay, or until <see cref="RequestRefresh"/> or disposal.</summary>
    private async Task WaitAsync(TimeSpan delay)
    {
        _wake?.Dispose();
        _wake = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, _wake.Token);
        try
        {
            await Task.Delay(delay, _time, linked.Token);
        }
        catch (OperationCanceledException)
        {
            // Woken up early, or disposed; the loop checks which.
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var target = Target;
        if (target is null && !HasRunsInProgress)
        {
            return;
        }

        var client = _client();
        try
        {
            CiSummary? summary = null;
            IReadOnlyList<CiAnnotation> annotations = [];
            IReadOnlyList<GitHubWorkflowRun> branchRuns = [];
            if (target is not null)
            {
                var checkRuns = await client.GetCheckRunsAsync(target.Owner, target.Repository, target.Sha, cancellationToken);
                var statuses = await client.GetCombinedStatusAsync(target.Owner, target.Repository, target.Sha, cancellationToken);
                summary = CiSummary.From(target.Sha, checkRuns, statuses);
                if (summary.FailedCount > 0)
                {
                    annotations = await ReadAnnotationsAsync(client, target, checkRuns, cancellationToken);
                }

                branchRuns = await ReadBranchRunsAsync(client, target, cancellationToken);
            }

            var watchedRuns = await ReadWatchedRunsAsync(client, cancellationToken);
            if (Target != target)
            {
                // The workspace moved to another commit meanwhile; the next poll reads that one.
                return;
            }

            if (target is not null)
            {
                Summary = summary;
                Annotations = annotations;
                BranchRuns = branchRuns;
                LastUpdated = _time.GetUtcNow();
            }

            ErrorText = null;
            _lastPollFailed = false;
            ReportFinishedRuns(target, branchRuns, watchedRuns);
        }
        catch (GitHubApiException ex)
        {
            if (Target != target)
            {
                return;
            }

            ErrorText = ActionsErrors.Describe(ex, client.IsSignedIn);
            _lastPollFailed = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reads the annotations of finished check runs that have any; a finished check run's annotations never change.</summary>
    private async Task<List<CiAnnotation>> ReadAnnotationsAsync(GitHubClient client, CiTarget target, IReadOnlyList<GitHubCheckRun> checkRuns, CancellationToken cancellationToken)
    {
        var annotations = new List<CiAnnotation>();
        foreach (var check in checkRuns.Where(c => c.IsCompleted && c.AnnotationCount > 0))
        {
            if (!_annotationsByCheck.TryGetValue(check.Id, out var read))
            {
                read = await client.GetCheckRunAnnotationsAsync(target.Owner, target.Repository, check.Id, cancellationToken);
                _annotationsByCheck[check.Id] = read;
            }

            annotations.AddRange(read.Select(a => new CiAnnotation(check, a)));
        }

        return annotations;
    }

    private static async Task<IReadOnlyList<GitHubWorkflowRun>> ReadBranchRunsAsync(GitHubClient client, CiTarget target, CancellationToken cancellationToken)
    {
        if (target.Branch is not { } branch)
        {
            return [];
        }

        try
        {
            var page = await client.GetWorkflowRunsAsync(target.Owner, target.Repository, new GitHubRunQuery(Branch: branch, PerPage: 20), cancellationToken);
            return page.Runs;
        }
        catch (GitHubApiException ex) when (ex.IsNotFound || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // A repository without Actions, or a token that may read checks but not Actions: the checks still show.
            return [];
        }
    }

    private void OnActivityChanged(object? sender, EventArgs e)
    {
        if (!_activity.IsInBackground)
        {
            RequestRefresh();
        }
    }
}
