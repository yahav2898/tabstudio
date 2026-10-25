namespace TabStudio.Toolchains.Processes;

/// <summary>
/// A running program, started by a toolchain runtime: its streams, how it ends, and stopping it with every
/// process it started.
/// </summary>
/// <remarks>
/// <para>
/// With pipes, <see cref="Input"/>, <see cref="Output"/> and <see cref="Error"/> are the program's standard input,
/// output and error. In a pseudo terminal (<see cref="IsTerminal"/>), <see cref="Input"/> is what is typed into the
/// terminal, <see cref="Output"/> is everything the terminal shows, standard error included, and
/// <see cref="Error"/> is <see langword="null"/>.
/// </para>
/// <para>
/// Reads from <see cref="Output"/> and <see cref="Error"/> block until the program writes, and return 0 once it
/// has exited and its output is read; read them on background threads, or with the asynchronous methods.
/// </para>
/// </remarks>
public sealed class ToolchainProcess : IDisposable
{
    private readonly IProcessHandle _handle;
    private int _stopRequested;

    internal ToolchainProcess(IProcessHandle handle, ProcessStartSpec spec)
    {
        _handle = handle;
        Spec = spec;
        Completion = WatchAsync();
    }

    /// <summary>What was started.</summary>
    public ProcessStartSpec Spec { get; }

    /// <summary>The operating system's id of the process.</summary>
    public int Id => _handle.Id;

    /// <summary>Whether the program runs in a pseudo terminal.</summary>
    public bool IsTerminal => _handle.IsTerminal;

    /// <summary>The program's standard input, or what is typed into its terminal.</summary>
    public Stream Input => _handle.Input;

    /// <summary>The program's standard output, or everything its terminal shows.</summary>
    public Stream Output => _handle.Output;

    /// <summary>The program's standard error, or <see langword="null"/> in a terminal, where it goes to <see cref="Output"/>.</summary>
    public Stream? Error => _handle.Error;

    /// <summary>Completes when the program has exited.</summary>
    public Task<ProcessExit> Completion { get; }

    /// <summary>Whether the program has exited.</summary>
    public bool HasExited => Completion.IsCompleted;

    /// <summary>Tells the program the terminal's new size. Does nothing for a program that runs with pipes.</summary>
    /// <param name="size">The new size.</param>
    public void Resize(TerminalSize size)
    {
        if (IsTerminal && !HasExited)
        {
            _handle.Resize(size.Clamped);
        }
    }

    /// <summary>Closes the program's standard input, which it reads as the end of its input.</summary>
    public void CloseInput() => _handle.CloseInput();

    /// <summary>Stops the program and every process it started. <see cref="Completion"/> then reports it as stopped.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 0 && !HasExited)
        {
            _handle.KillTree();
        }
    }

    /// <summary>Waits for the program to exit, stopping it when <paramref name="cancellationToken"/> is cancelled.</summary>
    /// <param name="cancellationToken">Stops the program.</param>
    /// <returns>How it ended.</returns>
    public async Task<ProcessExit> WaitAsync(CancellationToken cancellationToken = default)
    {
        using (cancellationToken.Register(Stop))
        {
            return await Completion.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _handle.Dispose();
    }

    private async Task<ProcessExit> WatchAsync()
    {
        var (exitCode, signal) = await _handle.WaitForExitAsync().ConfigureAwait(false);
        var stopped = Volatile.Read(ref _stopRequested) == 1;
        return new ProcessExit(signal is { } s ? 128 + s : exitCode, signal, stopped);
    }
}

/// <summary>What a <see cref="ToolchainProcess"/> needs from the operating system, one implementation per kind of process.</summary>
internal interface IProcessHandle : IDisposable
{
    /// <summary>The process id.</summary>
    int Id { get; }

    /// <summary>Whether the process has a pseudo terminal.</summary>
    bool IsTerminal { get; }

    /// <summary>Standard input or terminal input.</summary>
    Stream Input { get; }

    /// <summary>Standard output or terminal output.</summary>
    Stream Output { get; }

    /// <summary>Standard error, or <see langword="null"/> in a terminal.</summary>
    Stream? Error { get; }

    /// <summary>Completes with the exit code, or the signal that ended the process.</summary>
    Task<(int ExitCode, int? Signal)> WaitForExitAsync();

    /// <summary>Changes the terminal's size.</summary>
    void Resize(TerminalSize size);

    /// <summary>Closes standard input.</summary>
    void CloseInput();

    /// <summary>Kills the process and its descendants.</summary>
    void KillTree();
}
