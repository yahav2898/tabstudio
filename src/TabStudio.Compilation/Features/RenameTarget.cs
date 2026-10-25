namespace TabStudio.Compilation.Features;

/// <summary>What can be renamed at a position, for the inline rename box.</summary>
/// <param name="CanRename">Whether the symbol at the position can be renamed.</param>
/// <param name="Reason">Why it cannot, as a message for the user, or <see langword="null"/>.</param>
/// <param name="CurrentName">The current name of the symbol, or an empty string.</param>
/// <param name="SpanStart">Start of the identifier under the caret, where the rename box goes.</param>
/// <param name="SpanLength">Length of that identifier.</param>
public sealed record RenameTarget(bool CanRename, string? Reason, string CurrentName, int SpanStart, int SpanLength)
{
    /// <summary>A target that cannot be renamed.</summary>
    /// <param name="reason">Why, as a message for the user.</param>
    public static RenameTarget Unavailable(string reason) => new(false, reason, string.Empty, 0, 0);
}
