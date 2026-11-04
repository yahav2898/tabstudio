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

    /// <summary>
    /// Whether the package assemblies, such as the test framework's, are loaded once and shared with every other
    /// session that shares this option and the same packages, instead of once per session.
    /// </summary>
    /// <remarks>
    /// Sessions cannot be unloaded, so a process that loads many, as Live Unit Testing does after every edit, saves
    /// memory this way. The packages' static state then lives on from one session to the next.
    /// </remarks>
    public bool ShareRuntimeAssemblies { get; init; }

    /// <summary>
    /// The test project's output folder, such as <c>bin/Debug/net8.0</c> under the project, or <see langword="null"/>
    /// to load the assemblies from memory. With a folder, the session writes the compiled assemblies there and loads
    /// them from their files, and the folder is the current directory and <see cref="AppContext.BaseDirectory"/>
    /// while tests are found and run, as under <c>dotnet test</c>: tests that read files next to their assembly, or
    /// relative to it, find them.
    /// </summary>
    /// <remarks>
    /// When the files there were loaded by an earlier session and have changed since, a sibling folder at the same
    /// depth is used (<c>net8.0-2</c>), since a loaded file cannot be replaced while TabStudio runs.
    /// </remarks>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Whether the tests' own timeouts (NUnit's and MSTest's <c>[Timeout]</c>, xUnit's <c>Timeout =</c>) are ignored
    /// too, as under a debugger, where a test paused at a breakpoint must not be abandoned as timed out.
    /// </summary>
    public bool IgnoreTestTimeouts { get; init; }
}
