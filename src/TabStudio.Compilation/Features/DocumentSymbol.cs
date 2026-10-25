namespace TabStudio.Compilation.Features;

/// <summary>The declared accessibility of a <see cref="DocumentSymbol"/>, for the small overlay on its icon.</summary>
public enum SymbolAccessibility
{
    /// <summary>Accessibility does not apply, as for namespaces and enum members.</summary>
    NotApplicable,

    /// <summary><c>public</c>.</summary>
    Public,

    /// <summary><c>internal</c>.</summary>
    Internal,

    /// <summary><c>protected</c>.</summary>
    Protected,

    /// <summary><c>protected internal</c>.</summary>
    ProtectedInternal,

    /// <summary><c>private protected</c>.</summary>
    PrivateProtected,

    /// <summary><c>private</c>.</summary>
    Private,
}

/// <summary>A namespace, type or member declared in a file, for the navigation bar, the breadcrumb and the outline.</summary>
/// <param name="Name">The plain name, such as <c>Greet</c>.</param>
/// <param name="DisplayName">The name as shown, with type parameters and parameter types, such as <c>Greet(string)</c>.</param>
/// <param name="Kind">The icon to show.</param>
/// <param name="Accessibility">The declared accessibility, or the default C# gives the declaration.</param>
/// <param name="Start">Zero-based offset where the whole declaration starts, including attributes.</param>
/// <param name="Length">Length of the whole declaration.</param>
/// <param name="NameStart">Zero-based offset of the name, where navigating to the symbol puts the caret.</param>
/// <param name="NameLength">Length of the name.</param>
/// <param name="Children">The types and members declared inside it, in document order.</param>
public sealed record DocumentSymbol(
    string Name,
    string DisplayName,
    SymbolGlyph Kind,
    SymbolAccessibility Accessibility,
    int Start,
    int Length,
    int NameStart,
    int NameLength,
    IReadOnlyList<DocumentSymbol> Children)
{
    /// <summary>Whether a position is inside the declaration, its last character included.</summary>
    /// <param name="position">A zero-based offset.</param>
    public bool Contains(int position) => position >= Start && position <= Start + Length;
}
