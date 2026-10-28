using System.Diagnostics;
using System.Windows.Forms;
using TabStudio.Forms.Runtime;

namespace TabStudio.VisualBasic.Forms.Tests.Infrastructure;

/// <summary>
/// A Windows Forms app running on its own Forms thread, as in the app preview, with a recording presenter. Tests act
/// on it through <see cref="Invoke(Action)"/>, which runs on the Forms thread.
/// </summary>
internal sealed class FormsTestRun : IDisposable
{
    /// <summary>How long a test waits for the app.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
        var run = StartMain(() =>
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

    /// <summary>The controls in a control, at every depth.</summary>
    public static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Runs work on the Forms thread and waits for it.</summary>
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

    /// <summary>Queues work on the Forms thread without waiting, for work that opens a modal loop or ends the app.</summary>
    public void Post(Action action) => Runtime.Post(action);

    /// <summary>Waits until the Forms thread has handled everything queued so far.</summary>
    public void Settle()
    {
        var clock = Stopwatch.StartNew();
        if (Runtime.Send(() => { }, Timeout) || Runtime.IsStopping || Exit.IsCompleted)
        {
            return;
        }

        var remaining = Timeout - clock.Elapsed;
        Assert.True(remaining > TimeSpan.Zero && Exit.Wait(remaining), "The Forms thread is busy.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Runtime.Stop();
        Exit.Wait(Timeout);
        Runtime.Dispose();
    }
}
