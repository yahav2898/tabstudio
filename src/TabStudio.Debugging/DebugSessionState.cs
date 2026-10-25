namespace TabStudio.Debugging;

/// <summary>Where a debug session is in its life.</summary>
public enum DebugSessionState
{
    /// <summary>Created; the program has not started.</summary>
    NotStarted,

    /// <summary>The program is running.</summary>
    Running,

    /// <summary>The program is paused, and its locals and call stack can be inspected.</summary>
    Paused,

    /// <summary>The program finished, crashed or was stopped.</summary>
    Exited,
}
