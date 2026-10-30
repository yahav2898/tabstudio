using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace TabStudio.Forms.Runtime;

/// <summary>How a Windows Forms app's run ended.</summary>
/// <param name="ExitCode">The exit code Main returned.</param>
/// <param name="Error">The exception that ended the app, or <see langword="null"/>.</param>
/// <param name="Stopped">Whether the run was stopped from outside, as Shift+F5 does.</param>
internal sealed record FormsExit(int ExitCode, Exception? Error, bool Stopped)
{
    /// <summary>Whether the app asked to run again, with <see cref="System.Windows.Forms.Application.Restart"/>.</summary>
    public bool Restart { get; init; }
}

/// <summary>
/// The UI thread of one running Windows Forms app: its own thread with a message loop, as a Windows Forms process has.
/// </summary>
/// <remarks>
/// <para>
/// The app's Main runs on this thread. <see cref="Application.Run(Form)"/> shows the main form and runs the message
/// loop until it closes; modal dialogs and message boxes run nested loops, so <c>ShowDialog</c> and
/// <c>MessageBox.Show</c> return their result as in Windows Forms, on Android too, where the platform's own UI
/// thread cannot nest loops. An app's long-running handler blocks its own window, never TabStudio.
/// </para>
/// <para>
/// Input arrives from the presenter's thread through the Post methods; after each batch of work, the windows that
/// changed are drawn into frames and handed to the presenter.
/// </para>
/// </remarks>
internal sealed partial class FormsRuntime : IDisposable
{
    private static readonly AsyncLocal<FormsRuntime?> FlowingRuntime = new();

    [ThreadStatic]
    private static FormsRuntime? _threadRuntime;

    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly object _gate = new();
    private readonly List<FormWindow> _windows = [];

    /// <summary>The filters <see cref="Application.AddMessageFilter"/> added, which see input before its controls.</summary>
    public List<IMessageFilter> MessageFilters { get; } = [];
    private readonly List<int> _closedWindows = [];
    private List<int> _presentedOrder = [];
    private Thread? _thread;
    private volatile bool _stopping;
    private volatile bool _paintRequested;
    private readonly TaskCompletionSource _exitedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextWindowId;
    private bool _disposed;

    /// <summary>Creates the runtime of one run of an app.</summary>
    /// <param name="presenter">Shows the app's windows.</param>
    public FormsRuntime(IFormsPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        Presenter = presenter;
    }

    /// <summary>Raised on the Forms thread when the app's Main returns, throws, or the run is stopped.</summary>
    public event EventHandler<FormsExit>? Exited;

    /// <summary>The runtime of the current thread, or the one that started the current asynchronous flow.</summary>
    public static FormsRuntime? Current => _threadRuntime ?? FlowingRuntime.Value;

    /// <summary>Shows the app's windows.</summary>
    public IFormsPresenter Presenter { get; }

    /// <summary>Whether the caller is on the app's UI thread.</summary>
    public bool IsOnThread => _thread is { } thread && Thread.CurrentThread == thread;

    /// <summary>Whether the run is ending.</summary>
    public bool IsStopping => _stopping;

    /// <summary>The form <see cref="Application.Run(Form)"/> runs.</summary>
    public Form? MainForm { get; set; }

    /// <summary>Whether <see cref="Application.Exit()"/> was called.</summary>
    public bool ExitRequested { get; set; }

    /// <summary>Whether the app asked to run again once it exits, with <see cref="Application.Restart"/>.</summary>
    public bool RestartRequested { get; set; }

    /// <summary>The open windows, from the bottom of the z-order to the top.</summary>
    public IReadOnlyList<FormWindow> Windows => _windows;

    /// <summary>The modifier keys held, as the last key event reported them.</summary>
    public Keys ModifierKeys { get; set; }

    /// <summary>The mouse buttons held; a finger is the left button.</summary>
    public MouseButtons MouseButtons { get; set; }

    /// <summary>The font Application.SetDefaultFont set for the app's controls, or <see langword="null"/>.</summary>
    public System.Drawing.Font? DefaultFont { get; set; }

    /// <summary>The app's assembly, for its product name and version.</summary>
    public System.Reflection.Assembly? EntryAssembly { get; set; }

    /// <summary>The folder the app runs from, which <see cref="Application.StartupPath"/> reports: its project's folder.</summary>
    public string StartupPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>The exception that ended the app, if one did.</summary>
    public Exception? Crash { get; private set; }

    /// <summary>The exception TabStudio compiles a program's Environment.Exit into, which ends the run rather than crashing it.</summary>
    private const string ProgramExitExceptionName = "TabStudio.Runtime.ProgramExitException";

    /// <summary>The Application.ThreadException handlers.</summary>
    public System.Threading.ThreadExceptionEventHandler? ThreadException { get; set; }

    /// <summary>The Application.Idle handlers.</summary>
    public EventHandler? Idle { get; set; }

    /// <summary>The Application.ApplicationExit handlers.</summary>
    public EventHandler? ApplicationExit { get; set; }

    /// <summary>Starts the app's UI thread and runs its entry point on it.</summary>
    /// <param name="main">Runs the app's Main and returns its exit code.</param>
    public void Start(Func<int> main)
    {
        ArgumentNullException.ThrowIfNull(main);
        if (_thread is not null)
        {
            throw new InvalidOperationException("The app is already running.");
        }

        _thread = new Thread(() => ThreadMain(main)) { IsBackground = true, Name = "Windows Forms UI thread" };
        _thread.Start();
    }

    /// <summary>Ends the run: every message loop returns, and the windows close. Safe on any thread.</summary>
    public void Stop()
    {
        _stopping = true;
        Signal();
    }

    /// <summary>Queues work for the UI thread. Safe on any thread.</summary>
    /// <param name="action">The work.</param>
    public void Post(Action action)
    {
        _queue.Enqueue(action);
        Signal();
    }

    /// <summary>
    /// Runs work on the UI thread and waits for it, for at most a while: the presenter asks the app about a key this
    /// way, so a handler can cancel it, without letting a busy app freeze the IDE.
    /// </summary>
    /// <param name="action">The work.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>Whether the work finished in time.</returns>
    public bool Send(Action action, TimeSpan timeout)
    {
        if (IsOnThread)
        {
            Execute(action);
            return true;
        }

        if (_stopping || _exitedSource.Task.IsCompleted)
        {
            return false;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                action();
            }
            finally
            {
                done.TrySetResult();
            }
        });
        // The app may end before it gets to the work; the wait then ends too.
        Task.WaitAny([done.Task, _exitedSource.Task], timeout);
        return done.Task.IsCompleted;
    }

    /// <summary>Runs a message loop until a condition holds or the run ends. UI thread only.</summary>
    /// <param name="done">Checked after each message.</param>
    public void RunLoop(Func<bool> done)
    {
        ArgumentNullException.ThrowIfNull(done);
        var idle = false;
        while (!_stopping && !done())
        {
            if (_queue.TryDequeue(out var work))
            {
                idle = false;
                Execute(work);
                continue;
            }

            if (!idle)
            {
                idle = true;
                RaiseIdle();
                continue;
            }

            PaintIfRequested();
            if (done() || _stopping || !_queue.IsEmpty)
            {
                continue;
            }

            lock (_gate)
            {
                if (_queue.IsEmpty && !_stopping && !_paintRequested)
                {
                    Monitor.Wait(_gate, TimeSpan.FromMilliseconds(250));
                }
            }
        }

        PaintIfRequested();
    }

    /// <summary>Handles every message waiting now, then draws, as <see cref="Application.DoEvents"/> does. UI thread only.</summary>
    public void DoEvents()
    {
        var count = _queue.Count;
        for (var i = 0; i < count && _queue.TryDequeue(out var work); i++)
        {
            Execute(work);
        }

        PaintIfRequested();
    }

    /// <summary>Asks for the windows that changed to be drawn when the app is next idle. Safe on any thread.</summary>
    public void RequestPaint()
    {
        if (!_paintRequested)
        {
            _paintRequested = true;
            if (!IsOnThread)
            {
                Signal();
            }
        }
    }

    /// <summary>Draws the windows that changed now. UI thread only.</summary>
    public void PaintNow()
    {
        _paintRequested = true;
        PaintIfRequested();
    }

    /// <summary>
    /// Handles an exception the app's code did not: Application.ThreadException gets it when the app handles that
    /// event; otherwise the app ends with it, as an unhandled exception does.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void HandleException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Environment.Exit in a handler, which TabStudio compiles as this exception (TabStudio.Runtime.ProgramExitException):
        // the app ends, as its process would, rather than crashing.
        if (exception.GetType().FullName == ProgramExitExceptionName)
        {
            Stop();
            return;
        }

        if (ThreadException is { } handler && !_stopping)
        {
            try
            {
                handler(this, new System.Threading.ThreadExceptionEventArgs(exception));
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                exception = ex;
            }
        }

        Crash ??= exception;
        Stop();
    }

    /// <summary>Throws the exception that ended the app, from the loop that was running when it happened.</summary>
    public void ThrowIfCrashed()
    {
        if (Crash is { } crash)
        {
            ExceptionDispatchInfo.Capture(crash).Throw();
        }
    }

    /// <summary>Creates a window for a form and shows it.</summary>
    /// <param name="form">The form.</param>
    /// <param name="owner">The form that owns it, if any.</param>
    /// <param name="modal">Whether it is a modal dialog.</param>
    public FormWindow OpenWindow(Form form, Form? owner, bool modal)
    {
        var window = new FormWindow(this, ++_nextWindowId, form, owner, modal);
        _windows.Add(window);
        Activate(window);
        RequestPaint();
        return window;
    }

    /// <summary>Removes a closed window.</summary>
    /// <param name="window">The window.</param>
    public void RemoveWindow(FormWindow window)
    {
        if (_windows.Remove(window))
        {
            _closedWindows.Add(window.Id);
            if (_windows.Count > 0)
            {
                var next = window.Owner?.WindowInternal is { } owner && _windows.Contains(owner) ? owner : _windows[^1];
                Activate(next);
            }

            RequestPaint();
        }
    }

    /// <summary>Brings a window to the top of the z-order and makes it the active one.</summary>
    /// <param name="window">The window.</param>
    public void Activate(FormWindow window)
    {
        var top = TopModal();
        if (top is not null && top != window && window.Owner != top.Form)
        {
            // A modal dialog keeps the focus until it closes.
            window = top;
        }

        var previous = ActiveWindow;
        if (_windows.Remove(window))
        {
            _windows.Add(window);
        }

        // Windows owned by the activated window stay above it.
        foreach (var owned in _windows.Where(w => w.Owner == window.Form).ToList())
        {
            _windows.Remove(owned);
            _windows.Add(owned);
        }

        if (previous != window)
        {
            ActiveWindow = window;
            previous?.OnDeactivated();
            window.OnActivated();
        }

        RequestPaint();
    }

    /// <summary>The active window.</summary>
    public FormWindow? ActiveWindow { get; private set; }

    /// <summary>The topmost modal dialog, which blocks the windows below it, or <see langword="null"/>.</summary>
    public FormWindow? TopModal() => _windows.LastOrDefault(w => w.IsModal);

    /// <summary>Finds an open window.</summary>
    /// <param name="id">The window's identifier.</param>
    public FormWindow? FindWindow(int id) => _windows.Find(w => w.Id == id);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    /// <summary>Wakes the UI thread's loop.</summary>
    private void Signal()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }

    private void ThreadMain(Func<int> main)
    {
        _threadRuntime = this;
        FlowingRuntime.Value = this;
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext(this));
        var exitCode = 0;
        Exception? error = null;
        try
        {
            exitCode = main();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex;
        }

        // Environment.Exit in Main ends the app with its code (see HandleException).
        if (error is not null && error.GetType().FullName == ProgramExitExceptionName && error.GetType().GetProperty("ExitCode")?.GetValue(error) is int code)
        {
            exitCode = code;
            error = null;
        }

        try
        {
            CloseAll();
            ApplicationExit?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error ??= ex;
        }

        _exitedSource.TrySetResult();
        Exited?.Invoke(this, new FormsExit(exitCode, error ?? Crash, Stopped: _stopping && Crash is null && error is null) { Restart = RestartRequested && error is null && Crash is null && !_stopping });
    }

    /// <summary>Closes every window without asking, as the end of a run does.</summary>
    private void CloseAll()
    {
        foreach (var window in _windows.ToList())
        {
            window.Form.CloseForRuntimeEnd();
        }

        _windows.Clear();
        PaintNow();
    }

    private void Execute(Action work)
    {
        try
        {
            work();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && !_stopping)
        {
            HandleException(ex);
        }
    }

    private void RaiseIdle()
    {
        if (Idle is { } idle)
        {
            Execute(() => idle(this, EventArgs.Empty));
        }

        foreach (var window in _windows.ToList())
        {
            Execute(window.OnIdle);
        }
    }

    private void PaintIfRequested()
    {
        if (!_paintRequested)
        {
            return;
        }

        _paintRequested = false;
        var frames = new List<WindowFrame>();
        foreach (var window in _windows.ToList())
        {
            if (window.IsInvalid)
            {
                try
                {
                    frames.Add(window.Render());
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && !_stopping)
                {
                    HandleException(ex);
                    return;
                }
            }
        }

        var closed = _closedWindows.ToList();
        _closedWindows.Clear();
        var order = _windows.ConvertAll(w => w.Id);
        if (frames.Count > 0 || closed.Count > 0 || !order.SequenceEqual(_presentedOrder))
        {
            _presentedOrder = order;
            Presenter.Present(frames, closed, order);
        }
    }

    /// <summary>The desktop's size, for centering and maximizing windows.</summary>
    public Size DesktopSize => Presenter.DesktopSize;

    /// <summary>The desktop changed size: the windows move to stay on it. UI thread only.</summary>
    public void OnDesktopResized()
    {
        foreach (var window in _windows.ToList())
        {
            Execute(window.Form.OnDesktopResized);
        }
    }
}
