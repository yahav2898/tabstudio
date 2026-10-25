namespace TabStudio.Debugging;

/// <summary>
/// Thrown inside a debugged program, on its main thread, after the user stopped debugging, so the program unwinds
/// instead of running on in the background.
/// </summary>
/// <remarks>.NET cannot abort a thread; this is the debugger's way to end a program that is still running.</remarks>
public sealed class DebugSessionStoppedException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public DebugSessionStoppedException()
        : base("The debugging session was stopped.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public DebugSessionStoppedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public DebugSessionStoppedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
