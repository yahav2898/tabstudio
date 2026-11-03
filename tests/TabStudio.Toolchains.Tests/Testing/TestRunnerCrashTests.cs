using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Testing;

namespace TabStudio.Toolchains.Tests.Testing;

/// <summary>
/// Test Explorer runs a test runner again when it crashed before reporting any test. swift-algorithms showed 0 of 219
/// tests in CI when SwiftPM crashed while it planned the build; these lines are that run's output.
/// </summary>
public sealed class TestRunnerCrashTests
{
    private static readonly string[] SwiftPmCrash =
    [
        "Building for debugging...",
        "[Computing dependencies]",
        "[Pre-planning 1 / 184]",
        "swift-runtime: cannot open status file",
        "swift-runtime: failed to suspend thread 41249 retrying...",
        string.Empty,
        "*** Signal 11: Backtracing from 0x7fe9ad53296a... done ***",
        string.Empty,
        "*** Program crashed: Bad pointer dereference at 0xffff801652b0e720 ***",
        string.Empty,
        "Thread 2 \"DispatchWorker\" crashed:",
        "0      0x00007fe9ad53296a _dispatch_event_loop_drain + 1130 in libdispatch.so",
    ];

    [Fact]
    public void SwiftPM_crashing_while_it_plans_the_build_is_a_crash()
    {
        Assert.True(TestRunnerCrash.Crashed(new ProcessExit(139, 11, WasStopped: false), SwiftPmCrash));
        Assert.True(TestRunnerCrash.Crashed(new ProcessExit(139, null, WasStopped: false), ["Segmentation fault (core dumped)"]));
        Assert.True(TestRunnerCrash.Crashed(new ProcessExit(1, null, WasStopped: false), SwiftPmCrash));
    }

    [Fact]
    public void Failed_tests_a_build_error_and_a_stopped_run_are_not_crashes()
    {
        Assert.False(TestRunnerCrash.Crashed(new ProcessExit(1, null, WasStopped: false), ["Test Suite 'All tests' failed.", "Executed 219 tests, with 2 failures"]));
        Assert.False(TestRunnerCrash.Crashed(new ProcessExit(1, null, WasStopped: false), ["error: cannot find 'x' in scope"]));
        Assert.False(TestRunnerCrash.Crashed(new ProcessExit(137, 9, WasStopped: true), SwiftPmCrash));
        Assert.False(TestRunnerCrash.Crashed(new ProcessExit(0, null, WasStopped: false), []));
        Assert.False(TestRunnerCrash.Crashed(null, []));
    }
}
