using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Testing;

/// <summary>
/// Tells a test runner that crashed from one that ran the tests and reported failures, so Test Explorer can run a
/// crashed runner once more before it says the tests did not run.
/// </summary>
/// <remarks>
/// SwiftPM's <c>swift test</c> sometimes crashes in libdispatch while it plans the build, before any test has run
/// (<c>*** Program crashed: Bad pointer dereference</c> in <c>_dispatch_event_loop_drain</c>): swift-algorithms then
/// showed 0 tests passed of 219 in CI, and the next run passed all of them.
/// </remarks>
public static class TestRunnerCrash
{
    /// <summary>The signals a crash ends a process with: SIGILL, SIGABRT, SIGBUS and SIGSEGV.</summary>
    private static readonly int[] CrashSignals = [4, 6, 7, 11];

    /// <summary>
    /// Whether a test runner crashed: it was ended by SIGSEGV, SIGBUS, SIGILL or SIGABRT, its shell reported one
    /// (exit code 128 plus the signal), or its output has a crash report such as Swift's backtracer prints.
    /// A runner that was stopped, or that exited with the code of failed tests, did not crash.
    /// </summary>
    /// <param name="exit">How the runner ended, or <see langword="null"/> when it did not start.</param>
    /// <param name="output">The runner's output.</param>
    public static bool Crashed(ProcessExit? exit, IReadOnlyList<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (exit is null || exit.WasStopped || exit.Succeeded)
        {
            return false;
        }

        if (exit.Signal is { } signal)
        {
            return CrashSignals.Contains(signal);
        }

        return (exit.ExitCode > 128 && CrashSignals.Contains(exit.ExitCode - 128))
            || output.Any(line => line.Contains("*** Program crashed:", StringComparison.Ordinal)
                || line.StartsWith("*** Signal ", StringComparison.Ordinal)
                || line.Contains("Segmentation fault", StringComparison.Ordinal));
    }
}
