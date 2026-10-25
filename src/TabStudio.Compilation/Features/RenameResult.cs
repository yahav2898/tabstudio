using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Features;

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
