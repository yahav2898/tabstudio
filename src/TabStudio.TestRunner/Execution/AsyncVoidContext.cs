using System.Runtime.ExceptionServices;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Waits for an <c>async void</c> method, as xUnit's <c>AsyncTestSyncContext</c> does: the method's builder tells the
/// current synchronization context when it starts and completes, and posts its exception to it.
/// </summary>
internal sealed class AsyncVoidContext : SynchronizationContext
{
    private readonly object _gate = new();
    private int _pending;
    private ExceptionDispatchInfo? _failure;

    /// <summary>Runs a call under a new context and waits for every operation it starts.</summary>
    /// <param name="call">The call.</param>
    public static void Run(Action call)
    {
        var context = new AsyncVoidContext();
        var previous = Current;
        SetSynchronizationContext(context);
        try
        {
            call();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }

        lock (context._gate)
        {
            while (context._pending > 0)
            {
                _ = Monitor.Wait(context._gate);
            }
        }

        context._failure?.Throw();
    }

    /// <inheritdoc />
    public override void OperationStarted()
    {
        lock (_gate)
        {
            _pending++;
        }
    }

    /// <inheritdoc />
    public override void OperationCompleted()
    {
        lock (_gate)
        {
            if (--_pending == 0)
            {
                Monitor.PulseAll(_gate);
            }
        }
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        // Counted, so a continuation posted as the method completes runs before the wait ends.
        OperationStarted();
        _ = ThreadPool.UnsafeQueueUserWorkItem(
            _ =>
            {
                try
                {
                    Send(d, state);
                }
                finally
                {
                    OperationCompleted();
                }
            },
            null);
    }

    /// <inheritdoc />
    public override void Send(SendOrPostCallback d, object? state)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            d(state);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _failure ??= ExceptionDispatchInfo.Capture(ex);
            }
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <inheritdoc />
    public override SynchronizationContext CreateCopy() => this;
}
