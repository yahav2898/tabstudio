using TabStudio.Compilation.Running;

namespace TabStudio.Debugging;

/// <summary>Describes how a debugged program ended.</summary>
public sealed class DebugExitedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="result">How the program ended.</param>
    public DebugExitedEventArgs(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>How the program ended: completed with an exit code, crashed, or stopped.</summary>
    public RunResult Result { get; }
}
