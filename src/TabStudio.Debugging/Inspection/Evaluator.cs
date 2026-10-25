namespace TabStudio.Debugging.Inspection;

/// <summary>
/// Runs code of the paused program, such as property getters, <see cref="object.ToString"/> overrides and
/// collection enumerators, on a pool thread with a time limit.
/// </summary>
/// <remarks>
/// A getter may loop forever or wait for a lock held by a paused thread; the time limit keeps the IDE responsive
/// when it does, and the evaluation is abandoned. Instrumented code that runs during an evaluation never stops at
/// a breakpoint: the hooks check <see cref="IsEvaluating"/>.
/// </remarks>
internal sealed class Evaluator
{
    [ThreadStatic]
    private static int _depth;

    private readonly TimeSpan _timeout;

    /// <summary>Creates an evaluator.</summary>
    /// <param name="timeout">How long one evaluation may take.</param>
    public Evaluator(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _timeout = timeout;
    }

    /// <summary>Whether the current thread is running an evaluation.</summary>
    public static bool IsEvaluating => _depth > 0;

    /// <summary>Runs a function within the time limit.</summary>
    /// <param name="function">The code to run.</param>
    /// <param name="value">What it returned, when it finished in time.</param>
    /// <param name="failure">Why there is no value: the exception message, or that it timed out.</param>
    /// <returns><see langword="true"/> when the function returned in time.</returns>
    public bool TryRun<T>(Func<T> function, out T? value, out string? failure)
    {
        if (IsEvaluating)
        {
            // Already on an evaluation thread, which has its own time limit.
            return Invoke(function, out value, out failure);
        }

        T? result = default;
        string? error = null;
        var succeeded = false;
        var task = Task.Run(() =>
        {
            _depth++;
            try
            {
                succeeded = Invoke(function, out result, out error);
            }
            finally
            {
                _depth--;
            }
        });

        if (!task.Wait(_timeout))
        {
            value = default;
            failure = "The evaluation timed out.";
            return false;
        }

        value = result;
        failure = error;
        return succeeded;
    }

#pragma warning disable CA1031 // Code of the program can throw anything; the debugger reports it instead.
    private static bool Invoke<T>(Func<T> function, out T? value, out string? failure)
    {
        try
        {
            value = function();
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            value = default;
            failure = (ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex).Message;
            return false;
        }
    }
#pragma warning restore CA1031
}
