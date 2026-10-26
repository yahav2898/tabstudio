using System.Runtime.InteropServices;

namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>A process started by <see cref="UnixSpawner"/>: its streams, its exit status and its process tree.</summary>
internal sealed class UnixProcessHandle : IProcessHandle
{
    private readonly int _terminalFd;
    private readonly Task<(int ExitCode, int? Signal)> _exit;
    private int _inputClosed;

    public UnixProcessHandle(int pid, int terminalFd, Stream input, Stream output, Stream? error)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pid);
        Id = pid;
        _terminalFd = terminalFd;
        Input = input;
        Output = output;
        Error = error;
        UnixNative.TraceProcess("handle", pid, Id);

        // waitpid blocks, so it gets a thread of its own rather than one from the pool.
        _exit = Task.Factory.StartNew(Wait, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <inheritdoc />
    public int Id { get; }

    /// <inheritdoc />
    public bool IsTerminal => _terminalFd >= 0;

    /// <inheritdoc />
    public Stream Input { get; }

    /// <inheritdoc />
    public Stream Output { get; }

    /// <inheritdoc />
    public Stream? Error { get; }

    /// <inheritdoc />
    public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit;

    /// <inheritdoc />
    public void Resize(TerminalSize size)
    {
        if (_terminalFd >= 0)
        {
            UnixSpawner.SetWindowSize(_terminalFd, size);
        }
    }

    /// <inheritdoc />
    public void CloseInput()
    {
        if (Interlocked.Exchange(ref _inputClosed, 1) == 0)
        {
            Input.Dispose();
        }
    }

    /// <inheritdoc />
    public void KillTree() => ProcessTree.Kill(Id, ownGroup: true);

    /// <inheritdoc />
    public void Dispose()
    {
        CloseInput();
        Output.Dispose();
        Error?.Dispose();
    }

    private (int ExitCode, int? Signal) Wait()
    {
        var pid = Id;
        UnixNative.TraceProcess("wait", pid);
        while (true)
        {
            var result = UnixNative.waitpid(pid, out var status, 0);
            if (result == pid)
            {
                // WIFEXITED: the low 7 bits are 0, and the exit code is in the next byte; otherwise they are the signal.
                var signal = status & 0x7f;
                return signal == 0 ? ((status >> 8) & 0xff, null) : (128 + signal, signal);
            }

            var error = Marshal.GetLastPInvokeError();
            if (result < 0 && error == UnixConstants.ErrorInterrupted)
            {
                continue;
            }

            // ECHILD: something else reaped it (it no longer exists either way).
            Console.Error.WriteLine(FormattableString.Invariant($"TabStudio process wait: waitpid({pid}) returned {result}, errno {error}; exit status unavailable."));
            return (-1, null);
        }
    }
}
