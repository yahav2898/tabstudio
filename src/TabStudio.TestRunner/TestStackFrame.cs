namespace TabStudio.TestRunner;

/// <summary>One line of a failed test's stack trace.</summary>
/// <param name="Method">The method, as the runtime prints it, for example <c>Calculator.Tests.MathTests.Adds()</c>.</param>
/// <param name="FilePath">The source file, when the runtime could map the frame to one.</param>
/// <param name="Line">The one-based line in <paramref name="FilePath"/>, when known.</param>
/// <param name="IsUserCode">Whether the frame is in one of the user's own assemblies, rather than a framework or .NET.</param>
public sealed record TestStackFrame(string Method, string? FilePath, int? Line, bool IsUserCode)
{
    /// <summary>The frame as one line of a stack trace.</summary>
    public override string ToString() =>
        FilePath is null ? $"   at {Method}" : $"   at {Method} in {FilePath}:line {Line}";
}
