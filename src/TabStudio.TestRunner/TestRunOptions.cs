namespace TabStudio.TestRunner;

/// <summary>How a <see cref="TestSession"/> runs tests.</summary>
public sealed record TestRunOptions
{
    /// <summary>The default options: one test at a time, a one-minute timeout and a 4 MB stack.</summary>
    public static TestRunOptions Default { get; } = new();

    /// <summary>
    /// How long a test, a setup or a cleanup may run when it sets no timeout of its own, or
    /// <see langword="null"/> to wait forever. A test that runs longer is reported as
    /// <see cref="TestOutcome.Errored"/> and abandoned.
    /// </summary>
    /// <remarks>
    /// .NET cannot abort a thread, so an abandoned test keeps running in the background until it returns.
    /// The default of one minute keeps a hung test from blocking the run forever.
    /// </remarks>
    public TimeSpan? DefaultTimeout { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How many test classes may run at the same time. The default of 1 runs one test at a time, which keeps the
    /// IDE responsive on a tablet and makes console output unambiguous.
    /// </summary>
    /// <remarks>
    /// Tests of one class always run one after another. xUnit classes in the same <c>[Collection]</c> also never
    /// run in parallel with each other.
    /// </remarks>
    public int MaxParallelClasses { get; init; } = 1;

    /// <summary>The stack size, in bytes, of the thread each test runs on.</summary>
    public int StackSize { get; init; } = 4 * 1024 * 1024;

    /// <summary>
    /// The most characters of output kept per test. Anything beyond is dropped, so a test that writes in a loop
    /// cannot exhaust the device's memory.
    /// </summary>
    public int MaxOutputLength { get; init; } = 256 * 1024;

    /// <summary>
    /// Code to run on the test threads around each test and each class's setup and cleanup, or
    /// <see langword="null"/> for none. Live Unit Testing collects coverage this way.
    /// </summary>
    public ITestThreadHook? ThreadHook { get; init; }
}
