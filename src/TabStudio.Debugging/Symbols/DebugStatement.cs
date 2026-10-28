namespace TabStudio.Debugging.Symbols;

/// <summary>A variable whose value an instrumented statement hands to the debugger when it pauses there.</summary>
/// <param name="Name">The name as written in the source, or <c>this</c> (<c>Me</c> in Visual Basic).</param>
/// <param name="Type">The declared type as the source's language displays it, for example <c>List&lt;int&gt;</c> or
/// <c>List(Of Integer)</c>.</param>
public sealed record DebugLocalSlot(string Name, string Type)
{
    /// <summary>Whether the slot is a static field captured only by recording sessions.</summary>
    public bool RecordingOnly { get; init; }
}

/// <summary>
/// A statement the debugger can stop at. Every statement in the user's code has one, numbered across all the
/// assemblies of a debug build.
/// </summary>
/// <param name="Id">The statement id the instrumented code reports.</param>
/// <param name="MethodId">The id of the method, accessor, local function or lambda that contains it.</param>
/// <param name="Location">Where it is in the original source. For compound statements such as <c>if</c> and
/// <c>while</c>, only the header is covered.</param>
/// <param name="Locals">The locals and parameters in scope and definitely assigned before it runs, in the order the
/// instrumented code passes their values.</param>
public sealed record DebugStatement(int Id, int MethodId, DebugSourceSpan Location, IReadOnlyList<DebugLocalSlot> Locals)
{
    /// <summary>A simple divisor path, when this point has exactly one identifiable division.</summary>
    public string? DivisionOperand { get; init; }

    /// <summary>A simple dereferenced path, when this point has exactly one identifiable receiver.</summary>
    public string? NullReceiver { get; init; }
}

/// <summary>A method, accessor, local function or lambda that the debugger tracks on the call stack.</summary>
/// <param name="Id">The method id the instrumented code reports.</param>
/// <param name="Name">The name shown in the Call Stack, for example <c>Calculator.Add(int, int)</c>.</param>
/// <param name="Location">Where it is declared.</param>
/// <param name="Parameters">The parameters (and <c>this</c>) whose values are reported when an exception leaves the
/// method, in order.</param>
public sealed record DebugMethod(int Id, string Name, DebugSourceSpan Location, IReadOnlyList<DebugLocalSlot> Parameters)
{
    /// <summary>Original write-expression locations, separately numbered so breakpoint/coverage points stay unchanged.</summary>
    public IReadOnlyDictionary<int, DebugSourceSpan> WriteSites { get; init; } = new Dictionary<int, DebugSourceSpan>();
}
