namespace TabStudio.Debugging;

/// <summary>Why the program paused.</summary>
public enum PauseReason
{
    /// <summary>It reached a breakpoint.</summary>
    Breakpoint,

    /// <summary>A Step Into, Step Over or Step Out finished.</summary>
    Step,

    /// <summary>The user pressed Pause (Break All).</summary>
    Pause,

    /// <summary>An exception is about to leave the program unhandled.</summary>
    Exception,
}
