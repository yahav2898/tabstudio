namespace TabStudio.Live;

/// <summary>What a participant asks the host's debugger to do, as the debug toolbar does on the host.</summary>
public enum LiveDebugCommand
{
    /// <summary>Continue until a breakpoint (F5).</summary>
    Continue,

    /// <summary>Step into (F11).</summary>
    StepInto,

    /// <summary>Step over (F10).</summary>
    StepOver,

    /// <summary>Step out (Shift+F11).</summary>
    StepOut,

    /// <summary>Pause at the next statement (Break All).</summary>
    BreakAll,
}
