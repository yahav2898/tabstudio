using TabStudio.Compilation.Running;

namespace TabStudio.Swarm.Coordinator;

/// <summary>Output of a console app on a device.</summary>
public sealed class SwarmOutputEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="device">The device.</param>
    /// <param name="text">The text, which may hold several lines.</param>
    /// <param name="isError">Whether it was written to standard error.</param>
    public SwarmOutputEventArgs(SwarmDevice device, string text, bool isError)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(text);
        Device = device;
        Text = text;
        IsError = isError;
    }

    /// <summary>The device.</summary>
    public SwarmDevice Device { get; }

    /// <summary>The text, which may hold several lines.</summary>
    public string Text { get; }

    /// <summary>Whether it was written to standard error.</summary>
    public bool IsError { get; }
}

/// <summary>How the app ended on a device.</summary>
public sealed class SwarmExitEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="device">The device.</param>
    /// <param name="runId">The run that ended.</param>
    /// <param name="outcome">How it ended.</param>
    /// <param name="exitCode">Its exit code.</param>
    /// <param name="fault">The exception it crashed with, if it did.</param>
    public SwarmExitEventArgs(SwarmDevice device, long runId, RunOutcome outcome, int exitCode, SwarmFault? fault)
    {
        ArgumentNullException.ThrowIfNull(device);
        Device = device;
        RunId = runId;
        Outcome = outcome;
        ExitCode = exitCode;
        Fault = fault;
    }

    /// <summary>The device.</summary>
    public SwarmDevice Device { get; }

    /// <summary>The run that ended.</summary>
    public long RunId { get; }

    /// <summary>How it ended.</summary>
    public RunOutcome Outcome { get; }

    /// <summary>Its exit code.</summary>
    public int ExitCode { get; }

    /// <summary>The exception it crashed with, with its stack trace, if it did.</summary>
    public SwarmFault? Fault { get; }
}

/// <summary>A person's input on a device's screen.</summary>
public sealed class SwarmInputEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="device">The device.</param>
    /// <param name="input">The input.</param>
    public SwarmInputEventArgs(SwarmDevice device, SwarmInput input)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(input);
        Device = device;
        Input = input;
    }

    /// <summary>The device.</summary>
    public SwarmDevice Device { get; }

    /// <summary>The input.</summary>
    public SwarmInput Input { get; }
}
