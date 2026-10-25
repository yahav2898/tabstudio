using System.Text.Json.Serialization;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Swarm;

/// <summary>A breakpoint, as the tablet sends it to every device that debugs the app.</summary>
/// <param name="FilePath">The source file, by its path on the tablet, which the debug build was compiled from.</param>
/// <param name="Line">The one-based line.</param>
/// <param name="Condition">When it stops, or <see langword="null"/> for every time.</param>
public sealed record SwarmBreakpoint(string FilePath, int Line, string? Condition = null);

/// <summary>One frame of a paused device's call stack.</summary>
/// <param name="MethodName">The function, such as <c>MainViewModel.Count()</c>.</param>
/// <param name="Location">The statement it runs, in the tablet's source files.</param>
public sealed record SwarmFrame(string MethodName, DebugSourceSpan? Location);

/// <summary>A value of a paused device's program, as the Locals and Watch windows show it.</summary>
/// <param name="Name">A variable name, <c>this</c>, a member name or an index such as <c>[0]</c>.</param>
/// <param name="Value">The value as the debugger formats it.</param>
/// <param name="Type">The type as C# writes it.</param>
/// <param name="Reference">A number to ask the device for its members or items with; 0 when it has none.</param>
/// <param name="Path">An expression that finds it again, such as <c>this.Items[0]</c>.</param>
public sealed record SwarmVariable(string Name, string Value, string Type, int Reference, string Path)
{
    /// <summary>Whether it has members or items to show.</summary>
    [JsonIgnore]
    public bool HasChildren => Reference > 0;
}

/// <summary>How a paused device continues.</summary>
public enum SwarmResumeMode
{
    /// <summary>Until a breakpoint, an exception or the end (F5).</summary>
    Continue,

    /// <summary>Into the calls of the current statement (F11).</summary>
    StepInto,

    /// <summary>Over the current statement (F10).</summary>
    StepOver,

    /// <summary>Out of the current function (Shift+F11).</summary>
    StepOut,
}
