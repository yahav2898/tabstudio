using Microsoft.CodeAnalysis.Text;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Results;

/// <summary>Identifies a test across test projects: test ids are only unique within their project.</summary>
/// <param name="ProjectFilePath">The test project.</param>
/// <param name="TestId">The test's <see cref="TestCase.Id"/>.</param>
public readonly record struct TestKey(string ProjectFilePath, string TestId);

/// <summary>How the tests that run a line fare, as Live Unit Testing marks the line.</summary>
public enum LineCoverageState
{
    /// <summary>Only passing tests run the line: a green check.</summary>
    Covered,

    /// <summary>At least one failing test runs the line: a red cross.</summary>
    Failing,

    /// <summary>No test runs the line: a blue dash.</summary>
    NotCovered,
}

/// <summary>A test that runs a line, with its latest live result.</summary>
/// <param name="Key">The test.</param>
/// <param name="Test">The test as the newest build of its project found it.</param>
/// <param name="Outcome">How its latest live run ended, or <see langword="null"/> before it finished one.</param>
/// <param name="Duration">How long its latest live run took.</param>
/// <param name="IsPending">Whether it is waiting to run again because code it depends on changed.</param>
public sealed record CoveringTest(TestKey Key, TestCase Test, TestOutcome? Outcome, TimeSpan Duration, bool IsPending)
{
    /// <summary>Whether its latest result is a failure or an error.</summary>
    public bool IsFailing => Outcome is TestOutcome.Failed or TestOutcome.Errored;
}

/// <summary>The marker of one line: whether tests run it, how they fare, and whether that is up to date.</summary>
/// <param name="Line">One-based line, in the text the coverage was computed for.</param>
/// <param name="State">How the tests that run it fare.</param>
/// <param name="IsStale">
/// Whether the marker is out of date: the line's code changed, or a test that runs it waits to run again, or not
/// every test has run yet. Stale markers are drawn hollow until the tests catch up.
/// </param>
/// <param name="Tests">The tests that run the line, failing first, then by name.</param>
public sealed record LineCoverage(int Line, LineCoverageState State, bool IsStale, IReadOnlyList<CoveringTest> Tests);

/// <summary>The coverage of one source file, line by line.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="Text">The text of the file the line numbers refer to: the text of its newest coverage build.</param>
/// <param name="Lines">The lines with code, in order.</param>
public sealed record FileCoverage(string FilePath, SourceText Text, IReadOnlyList<LineCoverage> Lines);
