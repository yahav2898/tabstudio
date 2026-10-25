namespace TabStudio.TestRunner;

/// <summary>Data for <see cref="TestSession.TestFinished"/>.</summary>
public sealed class TestFinishedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="result">The result of the test that finished.</param>
    public TestFinishedEventArgs(TestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>The result of the test that finished.</summary>
    public TestResult Result { get; }
}
