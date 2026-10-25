namespace TabStudio.TestRunner;

/// <summary>
/// Code that runs on the threads of a test run, around each test and around each class's one-time setup and
/// cleanup. Live Unit Testing uses it to attribute the code a test runs to that test.
/// </summary>
/// <remarks>
/// <para>
/// Every test, setup and cleanup runs on a thread of its own that starts with an empty
/// <see cref="ExecutionContext"/>, so a value the hook puts in an <see cref="AsyncLocal{T}"/> belongs to that test
/// alone, and flows into every task and continuation the test starts.
/// </para>
/// <para>
/// The methods are called on runner threads, concurrently when classes run in parallel, and must not throw. The
/// object a method returns is disposed on the same thread once the test, setup or cleanup returns; a test that is
/// abandoned after its timeout never returns, so its scope is never disposed.
/// </para>
/// </remarks>
public interface ITestThreadHook
{
    /// <summary>Called on a test's thread just before the test's setup runs.</summary>
    /// <param name="test">The test about to run.</param>
    /// <returns>What to dispose after the test's cleanup, or <see langword="null"/>.</returns>
    IDisposable? EnterTest(TestCase test);

    /// <summary>Called on the thread of a class's one-time setup or cleanup, such as xUnit's class fixtures.</summary>
    /// <param name="tests">The tests of the class that the run includes.</param>
    /// <returns>What to dispose after the setup or cleanup, or <see langword="null"/>.</returns>
    IDisposable? EnterClassStage(IReadOnlyList<TestCase> tests);
}
