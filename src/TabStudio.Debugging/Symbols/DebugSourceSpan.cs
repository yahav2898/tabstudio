namespace TabStudio.Debugging.Symbols;

/// <summary>
/// A range of the user's original source text, before instrumentation. Used to highlight the current statement
/// and to place breakpoint glyphs.
/// </summary>
/// <param name="FilePath">Full path of the source file, as the compilation knows it.</param>
/// <param name="Line">One-based line where the range starts.</param>
/// <param name="Column">One-based column where the range starts.</param>
/// <param name="EndLine">One-based line where the range ends.</param>
/// <param name="EndColumn">One-based column just past the end of the range.</param>
/// <param name="Start">Zero-based character offset where the range starts.</param>
/// <param name="Length">Length of the range in characters.</param>
public sealed record DebugSourceSpan(string FilePath, int Line, int Column, int EndLine, int EndColumn, int Start, int Length);
