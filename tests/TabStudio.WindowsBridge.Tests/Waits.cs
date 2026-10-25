namespace TabStudio.WindowsBridge.Tests;

/// <summary>Waits for something that happens on another thread or another process.</summary>
internal static class Waits
{
    /// <summary>The longest a test waits for a build, a program or a frame.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    /// <summary>Waits until a condition holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="what">What is waited for, for the failure's message.</param>
    /// <param name="state">Describes the state at the time of a failure, for its message.</param>
    public static async Task UntilAsync(Func<bool> condition, string what, Func<string>? state = null)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Waited {Patience.TotalSeconds:0} seconds for {what}.{(state is null ? string.Empty : $" {state()}")}");
            }

            await Task.Delay(25);
        }
    }

    /// <summary>Waits for an event's first value that matches.</summary>
    /// <param name="subscribe">Subscribes a handler.</param>
    /// <param name="unsubscribe">Unsubscribes it.</param>
    /// <param name="match">Which values count.</param>
    /// <param name="what">What is waited for.</param>
    public static async Task<T> ForAsync<T>(Action<EventHandler<T>> subscribe, Action<EventHandler<T>> unsubscribe, Func<T, bool> match, string what)
    {
        var found = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, T value)
        {
            if (match(value))
            {
                found.TrySetResult(value);
            }
        }

        subscribe(Handler);
        try
        {
            return await found.Task.WaitAsync(Patience);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Waited {Patience.TotalSeconds:0} seconds for {what}.");
        }
        finally
        {
            unsubscribe(Handler);
        }
    }
}
