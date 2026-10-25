using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Tests.Infrastructure;

/// <summary>
/// A Windows Forms app running on its own Forms thread, as in the app preview, with a recording presenter. Tests act
/// on it through <see cref="Invoke(Action)"/>, which runs on the Forms thread, and simulate taps and keys the way
/// the presenter delivers them.
/// </summary>
internal sealed class FormsTestRun : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly TaskCompletionSource<FormsExit> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private FormsTestRun()
    {
        Runtime = new FormsRuntime(Presenter);
        Runtime.Exited += (_, exit) => _exit.TrySetResult(exit);
    }

    /// <summary>The presenter, with each window's latest frame.</summary>
    public RecordingPresenter Presenter { get; } = new();

    /// <summary>The runtime.</summary>
    public FormsRuntime Runtime { get; }

    /// <summary>How the run ended, once it has.</summary>
    public Task<FormsExit> Exit => _exit.Task;

    /// <summary>Starts an app whose Main runs a form, and waits for the form's first frame.</summary>
    /// <param name="createForm">Creates the main form, on the Forms thread.</param>
    public static FormsTestRun Start(Func<Form> createForm)
    {
        var run = new FormsTestRun();
        run.Runtime.Start(() =>
        {
            Application.Run(createForm());
            return 0;
        });
        Assert.True(run.Presenter.WaitFor(frames => frames.Count > 0, Timeout), "The form was never shown.");
        return run;
    }

    /// <summary>Starts an app with its own Main.</summary>
    /// <param name="main">The app's Main.</param>
    public static FormsTestRun StartMain(Func<int> main)
    {
        var run = new FormsTestRun();
        run.Runtime.Start(main);
        return run;
    }

    /// <summary>Runs work on the Forms thread and waits for it, then for the windows to be drawn.</summary>
    public void Invoke(Action action)
    {
        Exception? error = null;
        Assert.True(Runtime.Send(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                error = exception;
            }
        }, Timeout), "The Forms thread did not run the work in time.");
        if (error is not null)
        {
            throw new InvalidOperationException("The work failed on the Forms thread.", error);
        }

        Settle();
    }

    /// <summary>Runs work on the Forms thread and returns its result.</summary>
    public T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Invoke(new Action(() => result = func()));
        return result;
    }

    /// <summary>Queues work on the Forms thread without waiting, for work that opens a modal loop.</summary>
    public void Post(Action action) => Runtime.Post(action);

    /// <summary>Waits until the Forms thread has handled everything queued so far.</summary>
    public void Settle() => Assert.True(Runtime.Send(() => { }, Timeout) || Runtime.IsStopping, "The Forms thread is busy.");

    /// <summary>The window of a form.</summary>
    public FormWindow WindowOf(Form form) => Invoke(() => form.WindowInternal) ?? throw new InvalidOperationException("The form is not shown.");

    /// <summary>A tap on a control, at its center or at a point of its client area.</summary>
    public void Tap(Control control, Point? at = null) => Invoke(() =>
    {
        var window = control.Window ?? throw new InvalidOperationException($"{control.Name} is not shown.");
        var point = ToWindow(control, at ?? new Point(control.ClientSize.Width / 2, control.ClientSize.Height / 2));
        window.PointerPressed(point, MouseButtons.Left, 1, touch: false);
        window.PointerReleased(point, MouseButtons.Left, touch: false);
    });

    /// <summary>A tap at a point of a window's client area.</summary>
    public void TapWindow(Form form, Point point) => Invoke(() =>
    {
        var window = form.WindowInternal ?? throw new InvalidOperationException("The form is not shown.");
        window.PointerPressed(point, MouseButtons.Left, 1, touch: false);
        window.PointerReleased(point, MouseButtons.Left, touch: false);
    });

    /// <summary>A key pressed on the active window.</summary>
    public void Key(Keys keyData) => Invoke(() =>
    {
        var window = Runtime.ActiveWindow ?? throw new InvalidOperationException("No window is active.");
        window.KeyDown(keyData);
        window.KeyUp(keyData);
    });

    /// <summary>Text typed into the focused native text box.</summary>
    public void Type(string text) => Invoke(() =>
    {
        var window = Runtime.ActiveWindow ?? throw new InvalidOperationException("No window is active.");
        var focused = window.FocusedControl ?? throw new InvalidOperationException("Nothing has focus.");
        var frame = window.Render().Editor ?? throw new InvalidOperationException("The focused control does not edit text.");
        var accepted = window.TextInput(text);
        if (accepted is null)
        {
            return;
        }

        var start = frame.SelectionStart;
        var newText = frame.Text.Remove(start, frame.SelectionLength).Insert(start, accepted);
        window.EditorTextChanged((int)focused.Handle, newText, start + accepted.Length, 0, frame.TextRevision);
    });

    /// <summary>The latest frame of a form's window.</summary>
    public WindowFrame FrameOf(Form form)
    {
        var id = Invoke(() => form.WindowInternal?.Id ?? -1);
        Settle();
        return Presenter.Frames.Single(frame => frame.Id == id);
    }

    /// <summary>A point of a control's client area, in its window's client coordinates.</summary>
    public static Point ToWindow(Control control, Point local)
    {
        var origin = control.ClientOriginInWindow();
        return new Point(origin.X + local.X, origin.Y + local.Y);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Runtime.Stop();
        Exit.Wait(Timeout);
        Runtime.Dispose();
    }
}
