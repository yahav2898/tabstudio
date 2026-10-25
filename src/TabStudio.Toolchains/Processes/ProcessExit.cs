namespace TabStudio.Toolchains.Processes;

/// <summary>How a process ended.</summary>
/// <param name="ExitCode">
/// The exit code; for a process ended by a signal, 128 plus the signal number, as a shell reports it.
/// </param>
/// <param name="Signal">The signal that ended the process, or <see langword="null"/> when it exited by itself.</param>
/// <param name="WasStopped">Whether TabStudio stopped it, because the user pressed Stop or the run was cancelled.</param>
public sealed record ProcessExit(int ExitCode, int? Signal, bool WasStopped)
{
    /// <summary>The signal number of SIGKILL.</summary>
    public const int KillSignal = 9;

    /// <summary>
    /// Whether something other than TabStudio killed the process: on Android, usually the system's limit on the
    /// processes apps may run in the background (Android 12 and later) or a shortage of memory.
    /// </summary>
    public bool WasKilledBySystem => Signal == KillSignal && !WasStopped;

    /// <summary>Whether the process exited by itself with code 0.</summary>
    public bool Succeeded => ExitCode == 0 && Signal is null && !WasStopped;

    /// <summary>A line that says how the process ended, such as <c>exited with code 1</c>.</summary>
    public string Description => this switch
    {
        { WasStopped: true } => "was stopped",
        { WasKilledBySystem: true } => "was stopped by Android (signal 9)",
        { Signal: { } signal } => $"was ended by signal {signal}",
        _ => $"exited with code {ExitCode}",
    };
}
