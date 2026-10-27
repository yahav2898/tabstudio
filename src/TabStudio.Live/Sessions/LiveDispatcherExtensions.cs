namespace TabStudio.Live.Sessions;

/// <summary>Waiting for work on a session's thread.</summary>
public static class LiveDispatcherExtensions
{
    /// <summary>Runs work on the session's thread and returns its result.</summary>
    /// <param name="dispatcher">The session's thread.</param>
    /// <param name="work">The work.</param>
    public static Task<T> InvokeAsync<T>(this ILiveDispatcher dispatcher, Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(work);
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(() =>
        {
            try
            {
                result.TrySetResult(work());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                result.TrySetException(ex);
            }
        });
        return result.Task;
    }

    /// <summary>Runs asynchronous work that starts on the session's thread and returns its result.</summary>
    /// <param name="dispatcher">The session's thread.</param>
    /// <param name="work">The work.</param>
    public static async Task<T> InvokeAsync<T>(this ILiveDispatcher dispatcher, Func<Task<T>> work)
    {
        var task = await dispatcher.InvokeAsync<Task<T>>(work).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    /// <summary>Runs asynchronous work that starts on the session's thread and waits for it.</summary>
    /// <param name="dispatcher">The session's thread.</param>
    /// <param name="work">The work.</param>
    public static async Task InvokeAsync(this ILiveDispatcher dispatcher, Func<Task> work)
    {
        var task = await dispatcher.InvokeAsync<Task>(work).ConfigureAwait(false);
        await task.ConfigureAwait(false);
    }

    /// <summary>Runs work on the session's thread and waits for it.</summary>
    /// <param name="dispatcher">The session's thread.</param>
    /// <param name="work">The work.</param>
    public static Task InvokeAsync(this ILiveDispatcher dispatcher, Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return dispatcher.InvokeAsync(() =>
        {
            work();
            return true;
        });
    }
}
