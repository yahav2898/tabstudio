namespace TabStudio.Compilation.Features;

/// <summary>One result of Find All References.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="Start">Zero-based character offset of the reference.</param>
/// <param name="Length">Length of the reference in characters.</param>
/// <param name="Line">One-based line, as shown to the user.</param>
/// <param name="Column">One-based column, as shown to the user.</param>
/// <param name="LineText">The text of the whole line, for the results list.</param>
/// <param name="IsDefinition">Whether this is where the symbol is declared rather than used.</param>
public sealed record ReferenceItem(string FilePath, int Start, int Length, int Line, int Column, string LineText, bool IsDefinition);
