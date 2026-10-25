using System.Reflection;
using Avalonia.Threading;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// One run of a Windows Forms app inside TabStudio: the app's own UI thread runs its Main, and its windows are shown on
/// a desktop the app preview hosts. Closing the main form ends the run, as on Windows; an exception the app does not
/// handle ends the app, never TabStudio.
/// </summary>
internal sealed class FormsHost : IDisposable
{
    /// <summary>How long Main waits for the desktop to be laid out before it starts anyway, with a default desktop size.</summary>
    private static readonly TimeSpan LayoutWait = TimeSpan.FromMilliseconds(500);

    private readonly FormsRuntime _runtime;
    private readonly StorageBridge _storage = new();
    private readonly TaskCompletionSource<FormsExit> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<int> _main;
    private DispatcherTimer? _startTimer;
    private bool _started;
    private bool _disposed;

    private FormsHost(Func<int> main, Assembly? entryAssembly, string? startupPath)
    {
        _main = main;
        View = new FormsDesktop { Storage = _storage };
        _runtime = new FormsRuntime(View) { EntryAssembly = entryAssembly };
        if (startupPath is not null)
        {
            _runtime.StartupPath = startupPath;
        }

        View.Runtime = _runtime;
        _runtime.Exited += (_, exit) => _completion.TrySetResult(exit);
    }

    /// <summary>The desktop the app's windows are shown on.</summary>
    public FormsDesktop View { get; }

    /// <summary>Completes when the app's Main returns or throws, or the run is stopped.</summary>
    public Task<FormsExit> Completion => _completion.Task;

    /// <summary>
    /// Starts an app. Its Main runs once the desktop knows its size, so the first form opens centered in the pane.
    /// UI thread only.
    /// </summary>
    /// <param name="main">Runs the app's Main and returns its exit code.</param>
    /// <param name="entryAssembly">The app's assembly, for Application.ProductName and its version.</param>
    /// <param name="startupPath">The folder Application.StartupPath reports, such as the project's folder.</param>
    public static FormsHost Start(Func<int> main, Assembly? entryAssembly = null, string? startupPath = null)
    {
        ArgumentNullException.ThrowIfNull(main);
        Dispatcher.UIThread.VerifyAccess();
        var host = new FormsHost(main, entryAssembly, startupPath);
        host.View.SizeKnown += (_, _) => host.StartMain();
        host._startTimer = new DispatcherTimer(LayoutWait, DispatcherPriority.Background, (_, _) => host.StartMain());
        return host;
    }

    /// <summary>
    /// A function that runs an entry point as Windows does: with the command-line arguments when it takes them, and
    /// its exit code when it returns one.
    /// </summary>
    /// <param name="entryPoint">The app's Main.</param>
    /// <param name="arguments">The command-line arguments.</param>
    public static Func<int> RunEntryPoint(MethodInfo entryPoint, IReadOnlyList<string>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        return () =>
        {
            // The runtime unwraps the TargetInvocationException an exception in Main comes in.
            var parameters = entryPoint.GetParameters().Length == 0 ? null : new object[] { (arguments ?? []).ToArray() };
            return entryPoint.Invoke(null, parameters) switch
            {
                int code => code,
                Task<int> task => task.GetAwaiter().GetResult(),
                Task task => Await(task),
                _ => 0,
            };
        };

        static int Await(Task task)
        {
            task.GetAwaiter().GetResult();
            return 0;
        }
    }

    /// <summary>Ends the run, as Shift+F5 does: the app's message loops return and its windows close. Safe on any thread.</summary>
    public void Stop() => _runtime.Stop();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _startTimer?.Stop();
        _runtime.Dispose();
        _storage.Dispose();
        View.Storage = null;
        View.Runtime = null;
    }

    private void StartMain()
    {
        _startTimer?.Stop();
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        _runtime.Start(_main);
    }
}
