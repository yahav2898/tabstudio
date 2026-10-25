namespace TabStudio.TestRunner.Execution;

/// <summary>How one test's code ended, before timing and output are added.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Message">The message to show, if any.</param>
/// <param name="Exception">The exception whose stack trace explains the outcome, if any.</param>
internal sealed record TestStepOutcome(TestOutcome Outcome, string? Message = null, Exception? Exception = null)
{
    /// <summary>A test that passed.</summary>
    public static TestStepOutcome Passed { get; } = new(TestOutcome.Passed);
}

/// <summary>
/// Collects what went wrong while a test, its setup and its cleanup ran. The first problem decides the outcome;
/// problems in cleanup that follow are added to the message, as the frameworks do.
/// </summary>
internal sealed class FailureCollector
{
    private readonly List<string> _additional = [];
    private TestStepOutcome? _first;

    /// <summary>Whether anything has gone wrong yet.</summary>
    public bool HasFailed => _first is not null;

    /// <summary>Records an exception.</summary>
    /// <param name="exception">The exception as caught; reflection wrappers are removed.</param>
    /// <param name="stage">Where it happened, such as <c>SetUp</c>, or <see langword="null"/> for the test itself.</param>
    public void Add(Exception exception, string? stage = null)
    {
        var unwrapped = OutcomeClassifier.Unwrap(exception);
        var (outcome, message) = OutcomeClassifier.Classify(unwrapped);
        if (stage is not null && outcome != TestOutcome.Passed)
        {
            message = $"{stage} failed: {message}";
        }

        Add(new TestStepOutcome(outcome, message, unwrapped));
    }

    /// <summary>Records an outcome that is not an exception, such as a result that differs from the expected one.</summary>
    /// <param name="outcome">The outcome.</param>
    public void Add(TestStepOutcome outcome)
    {
        if (_first is null)
        {
            _first = outcome;
        }
        else if (outcome.Message is { Length: > 0 } message)
        {
            _additional.Add(message);
        }
    }

    /// <summary>The combined outcome: the first problem with any later ones appended, or a pass.</summary>
    public TestStepOutcome ToOutcome()
    {
        if (_first is null)
        {
            return TestStepOutcome.Passed;
        }

        if (_additional.Count == 0)
        {
            return _first;
        }

        var lines = new List<string?> { _first.Message };
        lines.AddRange(_additional);
        return _first with { Message = string.Join('\n', lines) };
    }
}
