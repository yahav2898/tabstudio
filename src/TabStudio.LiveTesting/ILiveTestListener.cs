using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Building;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting;

/// <summary>
/// Follows a live run as it happens, for the editor's markers, the Test Explorer and the status bar. Every method is
/// called on a background thread.
/// </summary>
public interface ILiveTestListener
{
    /// <summary>A test project is about to be built.</summary>
    /// <param name="testProject">The test project.</param>
    void BuildStarted(ProjectInfo testProject);

    /// <summary>A test project's build ended, successfully or not.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="build">The build, with its diagnostics.</param>
    void BuildFinished(ProjectInfo testProject, CoverageBuild build);

    /// <summary>The tests of a freshly built test project are known.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="tests">Every test of the project.</param>
    void TestsDiscovered(ProjectInfo testProject, IReadOnlyList<TestCase> tests);

    /// <summary>The tests the changes affect are about to run.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="tests">The tests that will run, possibly none.</param>
    void TestsQueued(ProjectInfo testProject, IReadOnlyList<TestCase> tests);

    /// <summary>A test started.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="test">The test.</param>
    void TestStarted(ProjectInfo testProject, TestCase test);

    /// <summary>A test finished; its coverage is already in the index.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="result">The result.</param>
    void TestFinished(ProjectInfo testProject, TestResult result);
}
