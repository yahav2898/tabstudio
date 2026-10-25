namespace TabStudio.TestRunner;

/// <summary>The outcome of running one test.</summary>
public sealed record TestResult
{
    /// <summary>The test that ran.</summary>
    public required TestCase Test { get; init; }

    /// <summary>How the test ended.</summary>
    public required TestOutcome Outcome { get; init; }

    /// <summary>How long the test took, including its per-test setup and cleanup.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The assertion message, the exception type and message, or why the test was skipped.
    /// <see langword="null"/> for a test that passed without a message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>The full name of the exception that ended the test, when it threw one.</summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// The stack trace, trimmed to the frames that matter: framework assertion internals at the top and the
    /// runner's own frames at the bottom are left out.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>The frames of <see cref="StackTrace"/>, for clickable stack traces.</summary>
    public IReadOnlyList<TestStackFrame> StackFrames { get; init; } = [];

    /// <summary>The innermost user-code frame with a source location: where the editor should point at the failure.</summary>
    public TestSourceLocation? FailureLocation { get; init; }

    /// <summary>
    /// Everything the test wrote: console output, standard error and xUnit's <c>ITestOutputHelper</c>, in order.
    /// </summary>
    public string Output { get; init; } = string.Empty;
}
