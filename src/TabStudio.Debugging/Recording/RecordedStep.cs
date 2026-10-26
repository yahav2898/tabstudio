using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Recording;

/// <summary>One immutable point in a recording, with shallow values and a logical call stack.</summary>
/// <param name="Sequence">Monotonically increasing sequence number (not an index).</param>
/// <param name="ThreadId">Managed thread that reached this point.</param>
/// <param name="Frames">Frames, innermost first.</param>
/// <param name="WrittenPath">Variable or field just written; null for a sequence point.</param>
/// <param name="IsBreakpoint">Whether a breakpoint condition actually matched at this point.</param>
public sealed record RecordedStep(long Sequence, int ThreadId, IReadOnlyList<RecordedFrame> Frames, string? WrittenPath, bool IsBreakpoint)
{
    /// <summary>The current frame's statement.</summary>
    public DebugSourceSpan? Location => Frames.Count > 0 ? Frames[0].StackFrame.Location : null;

    internal DebugPausedEventArgs ToPause(int frameIndex = 0) => new(
        PauseReason.Step, Frames.Select(f => f.StackFrame).ToArray(),
        Frames.ElementAtOrDefault(frameIndex)?.Locals ?? [], null, null, ThreadId, Sequence)
    { IsHistorical = true, RecordedStep = Sequence };
}

/// <summary>A recorded invocation, including locals captured for that invocation.</summary>
/// <param name="Id">Identity of the invocation; recursion has distinct ids.</param>
/// <param name="MethodId">Method in the build's symbol map.</param>
/// <param name="StatementId">Statement in the build's symbol map.</param>
/// <param name="StackFrame">Display and original source location.</param>
/// <param name="Locals">Immutable shallow values, including primitive instance fields.</param>
public sealed record RecordedFrame(long Id, int MethodId, int StatementId, DebugStackFrame StackFrame, IReadOnlyList<DebugValue> Locals);
