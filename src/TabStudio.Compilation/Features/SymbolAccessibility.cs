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
