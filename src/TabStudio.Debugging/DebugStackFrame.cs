using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging;

/// <summary>One row of the Call Stack panel.</summary>
/// <param name="MethodName">The function, for example <c>Calculator.Add(int, int)</c> or <c>&lt;top-level statements&gt;</c>.</param>
/// <param name="Location">The statement the frame is executing: for the top frame where the program paused, for the
/// others the call in progress. <see langword="null"/> before the function's first statement.</param>
public sealed record DebugStackFrame(string MethodName, DebugSourceSpan? Location);
