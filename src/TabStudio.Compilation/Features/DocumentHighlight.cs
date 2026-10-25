namespace TabStudio.Compilation.Features;

/// <summary>Whether a <see cref="DocumentHighlight"/> declares the symbol or uses it.</summary>
public enum HighlightKind
{
    /// <summary>A use of the symbol.</summary>
    Reference,

    /// <summary>The declaration of the symbol.</summary>
    Definition,
}

/// <summary>An occurrence of the symbol under the caret, highlighted in the editor.</summary>
/// <param name="Start">Zero-based character offset.</param>
/// <param name="Length">Length in characters.</param>
/// <param name="Kind">Whether it declares or uses the symbol.</param>
public readonly record struct DocumentHighlight(int Start, int Length, HighlightKind Kind);
