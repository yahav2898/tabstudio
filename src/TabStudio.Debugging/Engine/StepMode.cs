namespace TabStudio.Debugging.Engine;

/// <summary>What the program does until it next pauses.</summary>
internal enum StepMode
{
    /// <summary>Run until a breakpoint or an unhandled exception.</summary>
    Run,

    /// <summary>Break at the next statement on any thread (Break All).</summary>
    Pause,

    /// <summary>Break at the next statement, entering calls.</summary>
    Into,

    /// <summary>Break at the next statement of the same frame, or of a caller once the frame returns.</summary>
    Over,

    /// <summary>Break at the next statement of a caller once the frame returns.</summary>
    Out,
}
