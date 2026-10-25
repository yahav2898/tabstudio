using TabStudio.Core.Diagnostics;

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

/// <summary>Options for <see cref="RenameFeature.GetRenameEditsAsync"/>.</summary>
/// <param name="RenameOverloads">Whether to rename the other overloads of a method too.</param>
/// <param name="RenameInStrings">Whether to rename the name where it appears in string literals.</param>
/// <param name="RenameInComments">Whether to rename the name where it appears in comments.</param>
public sealed record RenameOptions(bool RenameOverloads = false, bool RenameInStrings = false, bool RenameInComments = false)
{
    /// <summary>Renames the symbol and its references only.</summary>
    public static RenameOptions Default { get; } = new();
}

/// <summary>The outcome of a rename.</summary>
/// <param name="Success">Whether the rename can be applied.</param>
/// <param name="Error">Why it cannot, as a message for the user, or <see langword="null"/>.</param>
/// <param name="Edits">The edits for every affected file.</param>
/// <param name="Conflicts">
/// Errors the rename would introduce, such as a name that is already taken, with positions in the renamed text.
/// The rename can still be applied; the editor should show them and let the user decide.
/// </param>
public sealed record RenameResult(bool Success, string? Error, IReadOnlyList<DocumentEdit> Edits, IReadOnlyList<BuildDiagnostic> Conflicts)
{
    /// <summary>A rename that cannot be applied.</summary>
    /// <param name="error">Why, as a message for the user.</param>
    public static RenameResult Failed(string error) => new(false, error, [], []);
}
