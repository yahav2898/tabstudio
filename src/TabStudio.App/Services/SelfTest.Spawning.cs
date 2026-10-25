using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's process part: many short programs started while the garbage collector runs all the time, as
/// toolchains, language servers and the terminal start them while the IDE works.
/// </summary>
public static partial class SelfTest
{
    /// <summary>How many programs the process part starts.</summary>
    public const int SpawnedPrograms = 200;

    /// <summary>How many threads start them at once.</summary>
    private const int SpawningThreads = 4;

    /// <summary>
    /// Starts <see cref="SpawnedPrograms"/> runs of <c>true</c>, from several threads at once, while another thread
    /// allocates garbage so that collections keep stopping the app. A collection that stopped a thread while it
    /// forked used to abort the app on Android ("Cannot transition thread ... from RUNNING with DONE_BLOCKING");
    /// every program must start and exit with 0.
    /// </summary>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether every program ran, with a one-line summary.</returns>
    internal static async Task<SelfTestResult> StartProgramsWhileCollectingAsync(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var program = OperatingSystem.IsAndroid() ? "/system/bin/true" : "/bin/true";
        if (OperatingSystem.IsWindows() || !File.Exists(program))
        {
            return new SelfTestResult(true, "Processes: no true program to start here", clock.Elapsed);
        }

        var runtime = new HostRuntime();
        var minor = GC.CollectionCount(0);
        var major = GC.CollectionCount(GC.MaxGeneration);
        using var stop = new CancellationTokenSource();
        var churn = Task.Factory.StartNew(() => Churn(stop.Token), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var failures = new ConcurrentQueue<string>();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, SpawningThreads).Select(_ => Task.Run(
                async () =>
                {
                    for (var i = 0; i < SpawnedPrograms / SpawningThreads; i++)
                    {
                        using var process = runtime.Start(new ProcessStartSpec { Program = program });
                        var exit = await process.WaitAsync(cancellationToken);
                        if (!exit.Succeeded)
                        {
                            failures.Enqueue(exit.Description);
                        }
                    }
                },
                cancellationToken)));
        }
        finally
        {
            await stop.CancelAsync();
            await churn;
        }

        minor = GC.CollectionCount(0) - minor;
        major = GC.CollectionCount(GC.MaxGeneration) - major;
        var message = failures.Count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"Processes: started {SpawnedPrograms} programs in {clock.Elapsed.TotalSeconds:0.0} s while {minor} minor and {major} major collections ran")
            : $"Processes: {failures.Count} of {SpawnedPrograms} programs failed: {string.Join(", ", failures.Distinct().Take(3))}";
        return new SelfTestResult(failures.Count == 0, message, clock.Elapsed);
    }

    /// <summary>
    /// Allocates short-lived arrays until stopped, so the nursery fills and minor collections keep running. They are
    /// small objects (Mono puts arrays of more than 8000 bytes straight into its large object space, which fills no
    /// nursery).
    /// </summary>
    private static void Churn(CancellationToken cancellationToken)
    {
        var recent = new byte[64][];
        for (var i = 0; !cancellationToken.IsCancellationRequested; i++)
        {
            recent[i & 63] = new byte[1024];
        }

        GC.KeepAlive(recent);
    }
}
