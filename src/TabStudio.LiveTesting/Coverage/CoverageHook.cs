using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Coverage;

/// <summary>
/// The thread hook a live test session is loaded with. A session keeps its options for its whole life, while each
/// run collects into a new <see cref="CoverageCollector"/>: this forwards to the collector of the run in progress.
/// </summary>
public sealed class CoverageHook : ITestThreadHook
{
    private CoverageCollector? _collector;

    /// <summary>The collector of the run in progress, or <see langword="null"/> between runs.</summary>
    public CoverageCollector? Collector
    {
        get => Volatile.Read(ref _collector);
        set => Volatile.Write(ref _collector, value);
    }

    /// <inheritdoc />
    public IDisposable? EnterTest(TestCase test) => Collector?.EnterTest(test);

    /// <inheritdoc />
    public IDisposable? EnterClassStage(IReadOnlyList<TestCase> tests) => Collector?.EnterClassStage(tests);
}
