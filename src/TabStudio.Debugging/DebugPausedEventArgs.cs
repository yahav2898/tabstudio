using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging;

/// <summary>Describes where and why the program paused: what the Locals and Call Stack panels show.</summary>
public sealed class DebugPausedEventArgs : EventArgs
{
    internal DebugPausedEventArgs(
        PauseReason reason,
        IReadOnlyList<DebugStackFrame> callStack,
        IReadOnlyList<DebugValue> locals,
        Breakpoint? breakpoint,
        Exception? exception,
        int threadId,
        long generation)
    {
        Reason = reason;
        CallStack = callStack;
        Locals = locals;
        Breakpoint = breakpoint;
        Exception = exception;
        ThreadId = threadId;
        Generation = generation;
    }

    /// <summary>Why it paused.</summary>
    public PauseReason Reason { get; }

    /// <summary>The statement about to run, or for an exception the statement that threw. The editor highlights it.</summary>
    public DebugSourceSpan? Location => CallStack.Count == 0 ? null : CallStack[0].Location;

    /// <summary>The call stack of the paused thread, innermost frame first.</summary>
    public IReadOnlyList<DebugStackFrame> CallStack { get; }

    /// <summary>
    /// The variables of the top frame: <c>this</c>, parameters and locals in scope and assigned. For an exception,
    /// only <c>this</c> and the parameters of the frame that threw.
    /// </summary>
    public IReadOnlyList<DebugValue> Locals { get; }

    /// <summary>The breakpoint that was hit, when <see cref="Reason"/> is <see cref="PauseReason.Breakpoint"/>.</summary>
    public Breakpoint? Breakpoint { get; }

    /// <summary>The exception, when <see cref="Reason"/> is <see cref="PauseReason.Exception"/>.</summary>
    public Exception? Exception { get; }

    /// <summary>The managed thread id of the paused thread.</summary>
    public int ThreadId { get; }

    /// <summary>Identifies this pause, so a late resume cannot release a newer one.</summary>
    internal long Generation { get; }

    /// <summary>
    /// Finds a value by a simple path, for DataTips and the Watch window: a local, then member names and indexes as
    /// <see cref="DebugValue.Expand"/> names them, such as <c>order.Customer.Name</c>, <c>items[0]</c> or
    /// <c>prices["tea"]</c>. The first part may also be a field or property of <c>this</c>.
    /// </summary>
    /// <remarks>This is not a C# evaluator: method calls and operators are not supported.</remarks>
    /// <param name="path">The path.</param>
    /// <returns>The value, or <see langword="null"/> when a part of the path does not exist.</returns>
    public DebugValue? FindValue(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Inspection.ValuePath.Find(Locals, path);
    }
}
