namespace TabStudio.TestRunner;

/// <summary>A position in a source file, for navigating from Test Explorer to the editor.</summary>
/// <param name="FilePath">The full path of the source file, as it was given to the compiler.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Column">One-based column number.</param>
public sealed record TestSourceLocation(string FilePath, int Line, int Column = 1);
