namespace TabStudio.TestRunner.Execution;

/// <summary>A class-level setup or cleanup that threw.</summary>
/// <param name="Stage">What failed, such as <c>OneTimeSetUp</c> or <c>Class fixture 'DatabaseFixture'</c>.</param>
/// <param name="Exception">What it threw.</param>
internal sealed record StageFailure(string Stage, Exception Exception);

/// <summary>
/// Runs the tests of one test class in the way its framework defines: what is created once per class, what is
/// created per test, and in which order setups and cleanups run.
/// </summary>
/// <remarks>
/// The runner calls <see cref="SetUpClass"/> once, <see cref="RunTest"/> for each selected test of the class, and
/// <see cref="TearDownClass"/> once, even when the setup failed. Each call runs on a fresh
/// <see cref="TestThread"/>, and calls never overlap. None of the methods throw: problems are returned.
/// </remarks>
internal abstract class ClassLifecycle
{
    /// <summary>Prepares the class: fixtures, one-time setups and class initializers.</summary>
    /// <returns>What failed, or <see langword="null"/> when the class is ready.</returns>
    public abstract StageFailure? SetUpClass();

    /// <summary>Runs one test with its per-test setup and cleanup, and reports how it ended.</summary>
    /// <param name="test">The test.</param>
    /// <param name="sink">The test's output, for <c>ITestOutputHelper</c>.</param>
    public abstract TestStepOutcome RunTest(RunnableTest test, TestOutputSink sink);

    /// <summary>Cleans up the class.</summary>
    /// <returns>The first cleanup that failed, or <see langword="null"/>.</returns>
    public abstract StageFailure? TearDownClass();
}
