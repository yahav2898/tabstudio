using System.Diagnostics;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// A program running on the PC for a tablet: the bridge started it, or the debugger did. It watches the program's
/// top-level windows and dialogs, so the tablet can show and pick them, and its exit.
/// </summary>
internal sealed class BridgeApp : IDisposable
{
    /// <summary>How often the program's windows are looked at.</summary>
    private static readonly TimeSpan WindowPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IBridgeDesktop _desktop;
    private readonly BridgeProcess? _process;
    private readonly TaskCompletionSource<int?> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private IReadOnlyList<DesktopWindow> _windows = [];
    private int _disposed;

    /// <summary>Starts watching a program.</summary>
    /// <param name="id">The bridge's number for it.</param>
    /// <param name="name">Its name.</param>
    /// <param name="projectPath">The project it was built from, on the tablet's paths.</param>
    /// <param name="processId">Its process.</param>
    /// <param name="process">The process, when the bridge started it; <see langword="null"/> when the debugger did.</param>
    /// <param name="isDebugging">Whether the debugger started it.</param>
    /// <param name="desktop">The PC's desktop.</param>
    public BridgeApp(int id, string name, string projectPath, int processId, BridgeProcess? process, bool isDebugging, IBridgeDesktop desktop)
    {
        Id = id;
        Name = name;
        ProjectPath = projectPath;
        ProcessId = processId;
        IsDebugging = isDebugging;
        _process = process;
        _desktop = desktop;
        _ = WatchAsync();
    }

    /// <summary>Raised on the thread pool when the program's windows changed.</summary>
    public event EventHandler? WindowsChanged;

    /// <summary>The bridge's number for the program.</summary>
    public int Id { get; }

    /// <summary>Its name.</summary>
    public string Name { get; }

    /// <summary>The project it was built from, on the tablet's paths.</summary>
    public string ProjectPath { get; }

    /// <summary>Its process.</summary>
    public int ProcessId { get; }

    /// <summary>Whether the debugger started it.</summary>
    public bool IsDebugging { get; }

    /// <summary>Completes with the exit code, when known, once the program exited.</summary>
    public Task<int?> Exited => _exited.Task;

    /// <summary>The program's visible top-level windows and dialogs, the oldest first.</summary>
    public IReadOnlyList<DesktopWindow> Windows
    {
        get
        {
            lock (_gate)
            {
                return _windows;
            }
        }
    }

    /// <summary>The program as the tablet sees it.</summary>
    public BridgeAppInfo Info => new(Id, Name, ProcessId, ProjectPath, IsDebugging);

    /// <summary>The program's windows as the tablet sees them.</summary>
    public AppWindows WindowList => new(Id, [.. Windows.Select(w => new BridgeWindowInfo(w.Id, w.Title, w.Width, w.Height, w.IsDialog, w.Scale))]);

    /// <summary>Whether a window is one of the program's.</summary>
    /// <param name="windowId">The window.</param>
    public bool Owns(long windowId) => Windows.Any(w => w.Id == windowId);

    /// <summary>Ends the program and the processes it started.</summary>
    public void Stop()
    {
        if (_process is not null)
        {
            _process.Kill();
            return;
        }

        try
        {
            using var process = Process.GetProcessById(ProcessId);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // It ended already.
        }
    }

    /// <summary>Marks the program as ended, such as when the debugger that ran it closed.</summary>
    /// <param name="exitCode">Its exit code, when known.</param>
    public void MarkExited(int? exitCode) => _exited.TrySetResult(exitCode);

    /// <summary>Stops watching; the program keeps running unless <see cref="Stop"/> ended it.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stopping.Cancel();
        _stopping.Dispose();
        _process?.Dispose();
        _exited.TrySetResult(null);
    }

    private async Task WatchAsync()
    {
        var token = _stopping.Token;
        var exit = _process is not null ? _process.Exited.ContinueWith(t => (int?)t.Result, TaskScheduler.Default) : WaitForExitAsync(ProcessId, token);
        _ = exit.ContinueWith(t => _exited.TrySetResult(t.IsCompletedSuccessfully ? t.Result : null), TaskScheduler.Default);
        try
        {
            using var timer = new PeriodicTimer(WindowPollInterval);
            do
            {
                if (_exited.Task.IsCompleted)
                {
                    SetWindows([]);
                    return;
                }

                var processes = _desktop.FindProcessTree(ProcessId);
                SetWindows(processes.Count == 0 ? [] : _desktop.FindWindows(processes));
            }
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Stopped watching.
        }
    }

    private void SetWindows(IReadOnlyList<DesktopWindow> windows)
    {
        lock (_gate)
        {
            if (_windows.SequenceEqual(windows))
            {
                return;
            }

            _windows = windows;
        }

        WindowsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<int?> WaitForExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return process.ExitCode;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
        catch (ArgumentException)
        {
            // It ended before it was watched.
            return null;
        }
    }
}
