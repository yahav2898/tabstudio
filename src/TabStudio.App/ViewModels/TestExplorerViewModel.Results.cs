using System.Globalization;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>A run as it happens: tests queued, started and finished, the counts, and the summary at the end.</summary>
public sealed partial class TestExplorerViewModel
{
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
        if (Find(projectFilePath, result.Test.Id) is { } entry && entry.Apply(result))
        {
            if (IsRunning)
            {
                _runCompleted++;
                UpdateRunProgress();
            }

            RefreshNodes([entry]);
        }
    }

    /// <summary>Reports that the run of a project's tests ended, with every result.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="summary">The totals and results.</param>
    public void ReportFinished(string projectFilePath, TestRunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        foreach (var result in summary.Results)
        {
            ReportFinished(projectFilePath, result);
        }

        _runSummaries.Add(summary);
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
