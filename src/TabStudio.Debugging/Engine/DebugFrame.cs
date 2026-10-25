namespace TabStudio.Debugging.Engine;

/// <summary>
/// One active call of an instrumented function: the debugger's view of a stack frame.
/// </summary>
/// <remarks>
/// The instrumented code keeps its frame in a local and passes it to every hook. Frames link to their caller, and
/// the innermost frame of each logical flow is tracked in an <see cref="AsyncLocal{T}"/>, so the call stack follows
/// <c>await</c> continuations onto other threads.
/// </remarks>
internal sealed class DebugFrame
{
    private volatile int _statementId;
    private volatile bool _exited;

    public DebugFrame(int methodId, DebugFrame? parent)
    {
        MethodId = methodId;
        Reparent(parent);
    }

    /// <summary>The function, in the debug info map.</summary>
    public int MethodId { get; }

    /// <summary>The caller's frame, or <see langword="null"/> for the program's entry point.</summary>
    public DebugFrame? Parent { get; private set; }

    /// <summary>How many frames deep this one is; the entry point is 1.</summary>
    public int Depth { get; private set; }

    /// <summary>The statement this frame is running, or 0 before the first one.</summary>
    public int StatementId
    {
        get => _statementId;
        set => _statementId = value;
    }

    /// <summary>Whether the function has returned or thrown.</summary>
    public bool Exited
    {
        get => _exited;
        set => _exited = value;
    }

    /// <summary>Attaches the frame to a new caller, when an iterator resumes inside a different call.</summary>
    public void Reparent(DebugFrame? parent)
    {
        Parent = parent;
        Depth = (parent?.Depth ?? 0) + 1;
    }
}
