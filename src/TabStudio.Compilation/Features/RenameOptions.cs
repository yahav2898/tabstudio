namespace TabStudio.Compilation.Features;

/// <summary>Options for <see cref="RenameFeature.GetRenameEditsAsync"/>.</summary>
/// <param name="RenameOverloads">Whether to rename the other overloads of a method too.</param>
/// <param name="RenameInStrings">Whether to rename the name where it appears in string literals.</param>
/// <param name="RenameInComments">Whether to rename the name where it appears in comments.</param>
public sealed record RenameOptions(bool RenameOverloads = false, bool RenameInStrings = false, bool RenameInComments = false)
{
    /// <summary>Renames the symbol and its references only.</summary>
    public static RenameOptions Default { get; } = new();
}
