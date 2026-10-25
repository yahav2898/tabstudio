namespace TabStudio.TestRunner;

/// <summary>How a test ended.</summary>
public enum TestOutcome
{
    /// <summary>The test ran and every assertion held.</summary>
    Passed,

    /// <summary>An assertion of the test framework failed, or an expected result or exception did not match.</summary>
    Failed,

    /// <summary>
    /// The test did not run because it is skipped or ignored, or it ended as inconclusive
    /// (<c>Assert.Ignore</c>, <c>Assert.Inconclusive</c>).
    /// </summary>
    Skipped,

    /// <summary>
    /// The test, its setup or its cleanup threw an exception that is not an assertion failure, or the test ran
    /// longer than its timeout.
    /// </summary>
    Errored,

    /// <summary>The run was stopped before the test finished.</summary>
    NotRun,
}
