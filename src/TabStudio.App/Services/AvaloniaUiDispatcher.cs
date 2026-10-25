using Avalonia.Threading;

namespace TabStudio.App.Services;

/// <summary>The <see cref="IUiDispatcher"/> backed by Avalonia's UI thread dispatcher.</summary>
/// <remarks>
/// Created on the UI thread, as the app creates it, it keeps that thread's dispatcher. Work that background threads
/// post later, such as a language server's last log line or a program's last output after its workspace closed,
/// then goes to that dispatcher, even once it has shut down, rather than making one of its own on their thread: the
/// unit tests' isolated applications shut the dispatcher down after every test.
/// </remarks>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher? _dispatcher = Dispatcher.UIThread.CheckAccess() ? Dispatcher.UIThread : null;

    private Dispatcher UiThread => _dispatcher ?? Dispatcher.UIThread;

    /// <inheritdoc />
    public bool CheckAccess() => UiThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action) => UiThread.Post(action);

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        var dispatcher = UiThread;
        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).GetTask();
    }
}
