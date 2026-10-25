namespace TabStudio.TestRunner;

/// <summary>Data for <see cref="TestSession.RunFinished"/>.</summary>
public sealed class TestRunFinishedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="summary">The totals and results of the run.</param>
    public TestRunFinishedEventArgs(TestRunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary;
    }

    /// <summary>The totals and results of the run.</summary>
    public TestRunSummary Summary { get; }
}
