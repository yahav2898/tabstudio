namespace TabStudio.Compilation.Features;

/// <summary>Whether a <see cref="DocumentHighlight"/> declares the symbol or uses it.</summary>
public enum HighlightKind
{
    /// <summary>A use of the symbol.</summary>
    Reference,

    /// <summary>The declaration of the symbol.</summary>
    Definition,
}
