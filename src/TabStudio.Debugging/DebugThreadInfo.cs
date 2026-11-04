namespace TabStudio.Debugging;

/// <summary>A thread of the paused program, for the Call Stack panel's threads.</summary>
/// <param name="Id">The managed thread id.</param>
/// <param name="Name">Its name, or <c>Main Thread</c> or <c>Thread 12</c> when it has none.</param>
/// <param name="CallStack">Its frames in the program's own code, innermost first.</param>
public sealed record DebugThreadInfo(int Id, string Name, IReadOnlyList<DebugStackFrame> CallStack);
