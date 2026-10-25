namespace TabStudio.App.Services;

/// <summary>
/// Gets work back onto the UI thread. Services and view models depend on this instead of on Avalonia's
/// dispatcher directly, so they can be tested without a running UI.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Whether the caller is on the UI thread.</summary>
    bool CheckAccess();

    /// <summary>Queues an action on the UI thread and returns immediately.</summary>
    /// <param name="action">The action to run.</param>
    void Post(Action action);

    /// <summary>Runs an action on the UI thread and waits for it.</summary>
    /// <param name="action">The action to run.</param>
    Task InvokeAsync(Action action);
}
