using System.Collections.Concurrent;
using System.Globalization;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>A run as it happens: tests queued, started and finished, the counts, and the summary at the end.</summary>
public sealed partial class TestExplorerViewModel
{
    private readonly ConcurrentQueue<(string ProjectFilePath, TestCase? Started, TestResult? Finished)> _progress = new();
    private int _progressScheduled;

    /// <summary>Reports that the build before a run failed, so no test runs.</summary>
    public void ReportBuildFailed() => _buildFailed = true;

    /// <summary>Reports that tests of a project are about to run.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="tests">The tests that will run.</param>
    public void ReportQueued(string projectFilePath, IReadOnlyList<TestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        var queued = tests.Select(t => Find(projectFilePath, t.Id)).OfType<TestEntry>().ToList();
        foreach (var entry in queued)
        {
            entry.State = TestState.Queued;
        }

        if (IsRunning)
        {
            _runTotal += tests.Count;
            IsProgressIndeterminate = false;
            UpdateRunProgress();
        }

        RefreshNodes(queued);
    }

    /// <summary>Reports that a test started.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="test">The test.</param>
    public void ReportStarted(string projectFilePath, TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        if (Find(projectFilePath, test.Id) is { State: TestState.Queued } entry)
        {
            entry.State = TestState.Running;
            RefreshNodes([entry]);
        }
    }

    /// <summary>Reports that a test finished. Reporting the same result twice changes nothing.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="result">The result.</param>
    public void ReportFinished(string projectFilePath, TestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ApplyResults(projectFilePath, [result]);
    }

    /// <summary>Reports that the run of a project's tests ended, with every result.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="summary">The totals and results.</param>
    public void ReportFinished(string projectFilePath, TestRunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        // What the runner reported as it went comes first, in its order.
        ApplyQueuedProgress();
        ApplyResults(projectFilePath, summary.Results);
        _runSummaries.Add(summary);
    }

    /// <summary>
    /// Queues that a test started, from the runner's own thread. Starts and results are shown in batches on the UI
    /// thread, behind input and drawing, each row refreshed once a batch: a run of thousands of quick tests reported
    /// one by one refreshed every row above each test, and the count of every test, thousands of times a second.
    /// </summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="test">The test.</param>
    public void QueueStarted(string projectFilePath, TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        _progress.Enqueue((projectFilePath, test, null));
        ScheduleProgress();
    }

    /// <summary>Queues that a test finished, from the runner's own thread; see <see cref="QueueStarted"/>.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="result">The result.</param>
    public void QueueFinished(string projectFilePath, TestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _progress.Enqueue((projectFilePath, null, result));
        ScheduleProgress();
    }

    /// <summary>Shows every start and result queued so far, at once. Runs on the UI thread.</summary>
    public void ApplyQueuedProgress()
    {
        Interlocked.Exchange(ref _progressScheduled, 0);
        var changed = new HashSet<TestEntry>();
        while (_progress.TryDequeue(out var item))
        {
            if (item.Started is { } test)
            {
                if (Find(item.ProjectFilePath, test.Id) is { State: TestState.Queued } started)
                {
                    started.State = TestState.Running;
                    changed.Add(started);
                }
            }
            else if (item.Finished is { } result && Find(item.ProjectFilePath, result.Test.Id) is { } finished && finished.Apply(result))
            {
                if (IsRunning)
                {
                    _runCompleted++;
                }

                changed.Add(finished);
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        if (IsRunning)
        {
            UpdateRunProgress();
        }

        RefreshNodes(changed);
    }

    private void ScheduleProgress()
    {
        if (Interlocked.Exchange(ref _progressScheduled, 1) == 0)
        {
            _ui.PostBackground(ApplyQueuedProgress);
        }
    }

    /// <summary>Applies results of a project's tests, and refreshes each row they change once.</summary>
    private void ApplyResults(string projectFilePath, IEnumerable<TestResult> results)
    {
        var changed = new List<TestEntry>();
        foreach (var result in results)
        {
            if (Find(projectFilePath, result.Test.Id) is { } entry && entry.Apply(result))
            {
                if (IsRunning)
                {
                    _runCompleted++;
                }

                changed.Add(entry);
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        if (IsRunning)
        {
            UpdateRunProgress();
        }

        RefreshNodes(changed);
    }

    private void UpdateCounts()
    {
        int passed = 0, failed = 0, skipped = 0;
        foreach (var entry in _entries)
        {
            switch (entry.State)
            {
                case TestState.Passed:
                    passed++;
                    break;
                case TestState.Failed:
                    failed++;
                    break;
                case TestState.Skipped:
                    skipped++;
                    break;
            }
        }

        TotalCount = _entries.Count;
        PassedCount = passed;
        FailedCount = failed;
        SkippedCount = skipped;
        NotRunCount = _entries.Count - passed - failed - skipped;
    }

    private void UpdateRunProgress()
    {
        Progress = _runTotal == 0 ? 0 : (double)_runCompleted / _runTotal;
        RunStatusText = $"Running {_runCompleted.ToString(CultureInfo.InvariantCulture)} of {TestDisplay.Tests(_runTotal)}…";
    }

    /// <summary>Puts tests that never got their turn back to "not run" and sums the run up.</summary>
    private void FinishRun(TestRunRequest request)
    {
        var unfinished = _entries.Where(e => e.State is TestState.Queued or TestState.Running).ToList();
        foreach (var entry in unfinished)
        {
            entry.State = TestState.NotRun;
            entry.Result = null;
        }

        RefreshNodes(unfinished);
        IsProgressIndeterminate = false;

        if (_buildFailed)
        {
            RunStatusState = TestState.Failed;
            RunStatusText = "The build failed. Fix the errors in the Error List, then run the tests again.";
            return;
        }

        var total = _runSummaries.Sum(s => s.Total);
        if (total == 0)
        {
            RunStatusState = TestState.NotRun;
            RunStatusText = _cancelRequested ? "The run was cancelled before any test ran." : $"No tests ran for {request.Description}.";
            return;
        }

        var passed = _runSummaries.Sum(s => s.Passed);
        var failed = _runSummaries.Sum(s => s.Failed + s.Errored);
        var skipped = _runSummaries.Sum(s => s.Skipped);
        var duration = TestDisplay.Duration(TimeSpan.FromTicks(_runSummaries.Sum(s => s.Duration.Ticks)));
        var counts = $"{passed.ToString(CultureInfo.InvariantCulture)} passed, {failed.ToString(CultureInfo.InvariantCulture)} failed, {skipped.ToString(CultureInfo.InvariantCulture)} skipped";
        if (_cancelRequested || _runSummaries.Exists(s => s.WasCancelled))
        {
            RunStatusState = TestState.NotRun;
            RunStatusText = $"Run cancelled after {(passed + failed + skipped).ToString(CultureInfo.InvariantCulture)} of {TestDisplay.Tests(total)}: {counts}.";
            return;
        }

        RunStatusState = failed > 0 ? TestState.Failed : passed > 0 ? TestState.Passed : TestState.Skipped;
        RunStatusText = $"{TestDisplay.Tests(total)} run in {duration}: {counts}.";
    }
}
