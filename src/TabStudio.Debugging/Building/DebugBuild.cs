using TabStudio.Compilation.Building;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Building;

/// <summary>An instrumented build, ready to run under the debugger.</summary>
/// <param name="Result">The assemblies and the diagnostics of the user's original code, as for a normal build.</param>
/// <param name="DebugInfo">Where every instrumented statement and method is in the original source.</param>
public sealed record DebugBuild(BuildResult Result, DebugInfoMap DebugInfo)
{
    /// <summary>Whether the build produced a program to debug.</summary>
    public bool Success => Result.Success;
}
