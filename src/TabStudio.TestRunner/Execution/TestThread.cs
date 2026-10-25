namespace TabStudio.TestRunner.Execution;

/// <summary>How a piece of test code on a <see cref="TestThread"/> ended.</summary>
internal enum ThreadRunStatus
{
    /// <summary>It returned a value.</summary>
    Completed,

    /// <summary>It threw.</summary>
    Faulted,

    /// <summary>It ran longer than its timeout and was abandoned.</summary>
    TimedOut,

    /// <summary>The run was stopped while it ran, and it was abandoned.</summary>
    Cancelled,
}

/// <summary>The outcome of running code on a <see cref="TestThread"/>.</summary>
/// <typeparam name="T">The code's return type.</typeparam>
/// <param name="Status">How the code ended.</param>
/// <param name="Value">What it returned, when it completed.</param>
/// <param name="Exception">What it threw, when it faulted.</param>
internal readonly record struct ThreadRunResult<T>(ThreadRunStatus Status, T? Value, Exception? Exception);

/// <summary>
/// Runs test code on a dedicated thread, with a timeout and cancellation.
/// </summary>
/// <remarks>
/// <para>
/// Each piece of test code gets a fresh thread: one test cannot exhaust another's stack, a test that blocks
/// cannot block the runner, and a test that ignores its timeout is simply abandoned. .NET cannot abort a
/// thread, so an abandoned one runs on in the background until it returns; its output is cut off.
/// </para>
/// <para>
/// The thread starts with an empty <see cref="ExecutionContext"/>, so values the caller keeps in
/// <see cref="AsyncLocal{T}"/> do not leak into tests, and each test gets fresh framework state such as NUnit's
/// ad hoc test context.
/// </para>
/// </remarks>
internal static class TestThread
{
    /// <summary>Runs code on a new thread and waits for it, its timeout or cancellation.</summary>
    /// <typeparam name="T">The code's return type.</typeparam>
    /// <param name="work">The code to run.</param>
    /// <param name="name">The thread name, shown in debuggers and crash reports.</param>
    /// <param name="stackSize">The thread's stack size in bytes.</param>
    /// <param name="sink">Where console output written by the code goes, or <see langword="null"/> to drop it.</param>
    /// <param name="timeout">How long to wait, or <see langword="null"/> to wait until it ends.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    public static async Task<ThreadRunResult<T>> RunAsync<T>(
        Func<T> work,
        string name,
        int stackSize,
        TestOutputSink? sink,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<ThreadRunResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                ConsoleCapture.Current = sink;
                try
                {
                    completion.TrySetResult(new ThreadRunResult<T>(ThreadRunStatus.Completed, work(), null));
                }
                catch (Exception ex)
                {
                    completion.TrySetResult(new ThreadRunResult<T>(ThreadRunStatus.Faulted, default, ex));
                }
            },
            stackSize)
        {
            IsBackground = true,
            Name = name.Length > 100 ? name[..100] : name,
        };

        if (ExecutionContext.IsFlowSuppressed())
        {
            thread.Start();
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                thread.Start();
            }
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } limit && limit != Timeout.InfiniteTimeSpan)
        {
            stop.CancelAfter(limit);
        }

        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stop.Token.Register(() => stopped.TrySetResult());

        var finished = await Task.WhenAny(completion.Task, stopped.Task).ConfigureAwait(false);
        if (finished == completion.Task)
        {
            return await completion.Task.ConfigureAwait(false);
        }

        return new ThreadRunResult<T>(
            cancellationToken.IsCancellationRequested ? ThreadRunStatus.Cancelled : ThreadRunStatus.TimedOut,
            default,
            null);
    }
}
