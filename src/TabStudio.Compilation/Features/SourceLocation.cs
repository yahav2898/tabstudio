namespace TabStudio.Compilation.Features;

/// <summary>A range of text in a source file, such as the definition of a symbol.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="Start">Zero-based character offset of the range.</param>
/// <param name="Length">Length of the range in characters.</param>
/// <param name="Line">One-based line of the start, as shown to the user.</param>
/// <param name="Column">One-based column of the start, as shown to the user.</param>
public sealed record SourceLocation(string FilePath, int Start, int Length, int Line, int Column);
