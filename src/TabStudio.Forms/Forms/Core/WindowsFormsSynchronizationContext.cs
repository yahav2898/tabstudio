using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// The synchronization context of a Windows Forms UI thread: <c>await</c> in an event handler continues on the UI
/// thread, and <see cref="Post"/> queues work for it.
/// </summary>
public sealed class WindowsFormsSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly FormsRuntime _runtime;

    /// <summary>Creates the context of the current UI thread.</summary>
    /// <exception cref="InvalidOperationException">The thread is not a running app's UI thread.</exception>
    public WindowsFormsSynchronizationContext()
        : this(FormsRuntime.Current ?? throw new InvalidOperationException("There is no Windows Forms UI thread here: run the app with F5 in TabStudio."))
    {
    }

    internal WindowsFormsSynchronizationContext(FormsRuntime runtime) => _runtime = runtime;

    /// <summary>Whether the context is installed on the UI thread automatically. It always is.</summary>
    public static bool AutoInstall { get; set; } = true;

    /// <summary>Removes the context from the current thread.</summary>
    public static void Uninstall() => Uninstall(true);

    /// <summary>Removes the context from the current thread.</summary>
    /// <param name="turnOffAutoInstall">Whether to stop installing it automatically.</param>
    public static void Uninstall(bool turnOffAutoInstall)
    {
        if (Current is WindowsFormsSynchronizationContext)
        {
            SetSynchronizationContext(null);
        }

        if (turnOffAutoInstall)
        {
            AutoInstall = false;
        }
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _runtime.Post(() => d(state));
    }

    /// <inheritdoc />
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (_runtime.IsOnThread)
        {
            d(state);
            return;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runtime.Post(() =>
        {
            try
            {
                d(state);
            }
            finally
            {
                done.TrySetResult();
            }
        });
        done.Task.Wait();
    }

    /// <inheritdoc />
    public override SynchronizationContext CreateCopy() => new WindowsFormsSynchronizationContext(_runtime);

    /// <inheritdoc />
    public void Dispose()
    {
        // The context holds nothing to release.
    }
}
