namespace TabStudio.App.ViewModels;

/// <summary>The tests of one project that a run includes.</summary>
/// <param name="ProjectFilePath">The test project.</param>
/// <param name="Names">
/// Test ids, method names, class names or namespaces that select the project's tests, as
/// <see cref="TestRunner.TestSession.FindTests"/> takes them; <see langword="null"/> for all of them except explicit ones.
/// </param>
public sealed record TestProjectSelection(string ProjectFilePath, IReadOnlyList<string>? Names);

/// <summary>Which tests a run includes.</summary>
/// <param name="Description">What runs, for the Test Explorer's status line, such as "All tests" or "MathTests".</param>
/// <param name="Projects">The test projects, each with its selected tests.</param>
public sealed record TestRunRequest(string Description, IReadOnlyList<TestProjectSelection> Projects);

/// <summary>
/// What the Test Explorer needs from the IDE: building test projects and running their tests, stopping a run,
/// and opening source files. Implemented by the workspace.
/// </summary>
public interface ITestRunHost
{
    /// <summary>Whether a run can start now: nothing is building or running, and the language service is ready.</summary>
    bool CanStartTestRun { get; }

    /// <summary>
    /// Builds the request's projects through the normal build, then runs the tests and reports their progress to the
    /// Test Explorer. A build failure ends the run before any test runs.
    /// </summary>
    /// <param name="request">The tests to run.</param>
    Task RunTestsAsync(TestRunRequest request);

    /// <summary>Stops the run in progress, if any.</summary>
    void CancelTestRun();

    /// <summary>Opens a file and moves the caret to a line and column.</summary>
    /// <param name="path">Full path of the file.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    Task NavigateToAsync(string path, int line, int column);
}
