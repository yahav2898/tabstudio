namespace TabStudio.Live;

/// <summary>What the host's program is doing, as every participant's status bar and Run button show it.</summary>
public enum LiveRunPhase
{
    /// <summary>Nothing runs.</summary>
    Idle,

    /// <summary>The host builds.</summary>
    Building,

    /// <summary>A program runs without the debugger.</summary>
    Running,

    /// <summary>A program runs under the debugger.</summary>
    Debugging,

    /// <summary>The debugged program is paused.</summary>
    Paused,
}
