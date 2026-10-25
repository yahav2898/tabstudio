namespace TabStudio.TestRunner;

/// <summary>The totals and results of one test run.</summary>
public sealed record TestRunSummary
{
    /// <summary>The number of tests selected for the run.</summary>
    public required int Total { get; init; }

    /// <summary>The results, in the order the tests finished.</summary>
    public required IReadOnlyList<TestResult> Results { get; init; }

    /// <summary>How long the whole run took.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Whether the run was stopped before every selected test finished.</summary>
    public bool WasCancelled { get; init; }

    /// <summary>
    /// Problems that do not belong to a single test, such as a class or assembly cleanup that threw.
    /// </summary>
    public IReadOnlyList<string> Messages { get; init; } = [];

    /// <summary>The number of tests that passed.</summary>
    public int Passed => Count(TestOutcome.Passed);

    /// <summary>The number of tests that failed an assertion.</summary>
    public int Failed => Count(TestOutcome.Failed);

    /// <summary>The number of tests that were skipped, ignored or inconclusive.</summary>
    public int Skipped => Count(TestOutcome.Skipped);

    /// <summary>The number of tests that threw an unexpected exception or timed out.</summary>
    public int Errored => Count(TestOutcome.Errored);

    /// <summary>The number of selected tests that did not finish, because the run was stopped.</summary>
    public int NotRun => Total - Passed - Failed - Skipped - Errored;

    /// <summary>Whether every test that ran passed or was skipped.</summary>
    public bool Succeeded => Failed == 0 && Errored == 0 && !WasCancelled;

    private int Count(TestOutcome outcome)
    {
        var count = 0;
        foreach (var result in Results)
        {
            if (result.Outcome == outcome)
            {
                count++;
            }
        }

        return count;
    }
}
